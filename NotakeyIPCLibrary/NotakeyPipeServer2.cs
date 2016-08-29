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
                    Console.WriteLine("=> Factory waiting on thread " + Thread.CurrentThread.ManagedThreadId);
                    bootstrapServer.WaitForConnection();

                    string clientPipe = string.Format("{0}.{1}", NotakeyPipeServer.MasterPipeName, Guid.NewGuid().ToString());
                    Debug.WriteLine("Got client on master pipe. Generated client pipe name: {0}", clientPipe);

                    // This will communicate the child pipe to the client, so it can connect
                    using (var sw = new StreamWriter(bootstrapServer, System.Text.Encoding.UTF8, 4096, true))
                    {
                        sw.WriteLine(clientPipe);
                    }

                    Debug.WriteLine("Sent back pipe name. Creating server for it.");

                    // Now wait for the client to connect
                    var client = new NotakeyPipeServer2(clientPipe);
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

        internal NotakeyPipeServer2(string pipeName)
        {
            this.pipeName = pipeName;
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
                    Console.WriteLine("Waiting on connection on thread " + Thread.CurrentThread.ManagedThreadId);
                    stream.WaitForConnection();

                    // Don't wrap these in using(), otherwise there will be a "Pipe is broken" error when
                    // the client has disconnected
                    StreamReader sr = new StreamReader(stream, Encoding.UTF8, true, 4096, true);
                    StreamWriter sw = new StreamWriter(stream, Encoding.UTF8, 4096, true);

                    Console.WriteLine("Opening pipe");

                    while (stream.IsConnected && !sr.EndOfStream)
                    {
                        var msgStr = sr.ReadLine();

                        if (msgStr.Equals("DIE"))
                        {
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

                    Console.WriteLine("Closed pipe");

                    o.OnCompleted();
                }
                
                return Disposable.Empty;
            });
        }
    }
}
