using Notakey.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NotakeyIPCLibrary
{
    public class PipeServerMessage
    {
        public string FirstLine { get; set; }

        public StreamWriter Writer { get; internal set; }
        public StreamReader Reader { get; internal set; }

        internal NamedPipeServerStream Stream { get; set; }

        public void Disconnect()
        {
            Stream.Disconnect();
        }
    }

    public class PipeServerFactory
    {
        Logger parentLogger;
        Logger logger;

        public PipeServerFactory(Logger parentLogger)
        {
            this.parentLogger = parentLogger;
            this.logger = new Logger("Connection Factory", parentLogger);
        }

        public IObservable<NotakeyPipeServer2> GetConnectedServer()
        {
            return Observable.Defer(() => _CreateDeferred());
        }

        private IObservable<NotakeyPipeServer2> _CreateDeferred()
        {
            return Observable.Create<NotakeyPipeServer2>(o =>
            {
                using (NamedPipeServerStream bootstrapServer = new NamedPipeServerStream(NotakeyPipeServer.MasterPipeName, PipeDirection.Out, 1))
                {
                    logger.WriteMessage("Waiting for connection");
                    bootstrapServer.WaitForConnection();

                    string clientPipe = string.Format("{0}.{1}", NotakeyPipeServer.MasterPipeName, Guid.NewGuid().ToString());

                    logger.LineWithEmphasis("Connected. Generated name", clientPipe, ConsoleColor.White);

                    // This will communicate the child pipe to the client, so it can connect
                    using (var sw = new StreamWriter(bootstrapServer, System.Text.Encoding.UTF8, 4096, true))
                    {
                        sw.WriteLine(clientPipe);
                    }

                    // Now wait for the client to connect
                    var client = new NotakeyPipeServer2(clientPipe, parentLogger);
                    o.OnNext(client);
                }
                o.OnCompleted();
                return Disposable.Empty;
            }).SubscribeOn(NewThreadScheduler.Default);
        }
    }

    public class NotakeyPipeServer2
    {
        string pipeName;
        Logger logger;

        internal NotakeyPipeServer2(string pipeName, Logger parentLogger)
        {
            this.pipeName = pipeName;
            this.logger = new Logger(pipeName, parentLogger);
        }

        public IObservable<PipeServerMessage> Connect()
        {
            return Observable.Defer(() => _CreateDeferred());
        }

        private IObservable<PipeServerMessage> _CreateDeferred()
        {
            return Observable.Create<PipeServerMessage>(o =>
            {
                using (NamedPipeServerStream stream = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.WriteThrough))
                {
                    logger.WriteMessage("Waiting on connection");
                    stream.WaitForConnection();

                    // Don't wrap these in using(), otherwise there will be a "Pipe is broken" error when
                    // the client has disconnected
                    StreamReader sr = new StreamReader(stream, Encoding.UTF8, true, 4096, true);
                    StreamWriter sw = new StreamWriter(stream, Encoding.UTF8, 4096, true);

                    logger.WriteMessage("Connected. Reading message...");

                    while (stream.IsConnected && !sr.EndOfStream)
                    {
                        var msgStr = sr.ReadLine();

                        logger.LineWithEmphasis("Received", msgStr, ConsoleColor.Magenta);

                        if (msgStr.Equals("DIE"))
                        {
                            logger.WriteMessage("Disconnecting pipe as requested");
                            stream.Disconnect();
                        }
                        else
                        {
                            var msg = new PipeServerMessage
                            {
                                FirstLine = msgStr,
                                Writer = sw,
                                Reader = sr,
                                Stream = stream
                            };
                            o.OnNext(msg);
                        }
                    }

                    logger.WriteMessage("Terminating instance");

                    o.OnCompleted();
                }
                
                return Disposable.Empty;
            });
        }
    }
}
