using Notakey.SDK;
using NotakeyIPCLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Notakey.Utility;
using System.Windows.Threading;
using System.IO;

namespace NotakeyBGService
{
    // TODO: read config from args
    public static class BGServiceConfiguration
    {
        public static readonly TimeSpan AsyncTimeout = TimeSpan.FromSeconds(30);
    }

    public static class ApiConfiguration
    {
        public static readonly string AccessId = "84c328f2-4ff2-4980-8db6-3ecabf55bff1";
        public static readonly string ApiEndpoint = "https://demo.notakey.com/api/v2/";
    }

    public class Application
    {
        Random random = new Random();

        SimpleApi api = new SimpleApi();
        Logger logger = new Logger();

        PipeServerFactory factory;
        ManualResetEvent terminationEvent;

        List<IDisposable> disposableSubscriptions = new List<IDisposable>();

        public Application(ManualResetEvent terminationEvent)
        {
            this.terminationEvent = terminationEvent;
            this.factory = new PipeServerFactory(logger);
        }

        internal void Cleanup()
        {
            logger.WriteMessage("Cleaning up...");
            while (disposableSubscriptions.Any())
            {
                var disposable = disposableSubscriptions.First();
                disposableSubscriptions.Remove(disposable);

                logger.WriteMessage("  disposing " + disposable.ToString());
                disposable.Dispose();
            }
            disposableSubscriptions = null;
        }

        internal void Run()
        {
            logger.WriteHeader("Starting Notakey BG IPC service", ConsoleColor.White, ConsoleColor.DarkBlue);

            logger.WriteMessage("Binding to Notakey API (" + ApiConfiguration.ApiEndpoint + "; " + ApiConfiguration.AccessId + ")");
            api.Bind(ApiConfiguration.ApiEndpoint, ApiConfiguration.AccessId)
               .Timeout(TimeSpan.FromSeconds(15))
               .Subscribe(
                   p => logger.WriteMessage("Bound to: " + p.ToString()),
                   error =>
                   {
                       logger.ErrorLine("Notakey API bind failure", error);
                       terminationEvent.Set();
                   },
                   SpawnServer);   
        }

        void SpawnServer()
        {
            bool status = terminationEvent.WaitOne(0);
            logger.WriteMessage("Spawning main listener");

            IDisposable masterPipeListener = null;
            masterPipeListener = factory
                .GetConnectedServer()
                .SubscribeOn(NewThreadScheduler.Default)
                .ObserveOn(NewThreadScheduler.Default)

                // Spawn server before attempting to process messages (which may block)
                .Do(_ => SpawnServer())
                .Finally(() => disposableSubscriptions.Remove(masterPipeListener))
                
                .Subscribe(
                    OnClientPipeCreated,
                    OnClientSpawningError
                );
            disposableSubscriptions.Add(masterPipeListener);
        }

        private void OnClientSpawningError(Exception error)
        {
            logger.ErrorLine("Error spawning child server: " + error.ToString());
            terminationEvent.Set();
        }

        private void OnClientPipeCreated(NotakeyPipeServer2 server)
        {
            logger.WriteMessage("Created client pipe. Connecting ...");

            // Make sure to stay on the same thread (or the pipes will fail)
            IDisposable clientSubscription = null;
            clientSubscription = server.Connect()
                .Finally(() => disposableSubscriptions.Remove(clientSubscription))
                .Subscribe(OnServerMessage, OnServerError, OnCompleted);
            disposableSubscriptions.Add(clientSubscription);
        }

        private void OnCompleted()
        {
            logger.WriteMessage("Client disconnected");
        }

        private void OnServerError(Exception e)
        {
            logger.ErrorLine("Client communication error: " + e.ToString());
        }

        private void OnServerMessage(PipeServerMessage obj)
        {
            try
            {
                // This is on a dedicated thread - don't do async operations
                ManualResetEvent mre = new ManualResetEvent(false);

                logger.LineWithEmphasis("Processing message", obj.FirstLine, ConsoleColor.Magenta);
                
                int opId = random.Next();
                bool shouldTerminate = false;
                
                switch (obj.FirstLine)
                {
                    case "DIE":
                        shouldTerminate = true;
                        mre.Set();
                        break;
                    case "API_HEALTH_CHECK":
                        logger.LineWithEmphasis("Performing health check. Operation", opId.ToString(), ConsoleColor.White);
                        // TODO: invoke /api/health and and check NOTAKEY_STATUS ?
                        obj.Writer.WriteLine("OK");
                        mre.Set();
                        break;
                    case "REQUEST_AUTH":
                        logger.LineWithEmphasis("Requesting authentication. Operation", opId.ToString(), ConsoleColor.White);

                        string username = obj.Reader.ReadLine();
                        string action= obj.Reader.ReadLine();
                        string description = obj.Reader.ReadLine();

                        var sb = new StringBuilder();
                        sb.AppendLine("Authentication request parameters:");
                        sb.Append("  username: "); sb.AppendLine(username);
                        sb.Append("  action: "); sb.AppendLine(action);
                        sb.Append("  description: "); sb.AppendLine(description);
                        logger.WriteMessage(sb.ToString());

                        var authReqTerminationEvent = new ManualResetEvent(false);
                        string authReqUuid = null;
                        Exception authReqException = null;

                        api
                            .CreateAuthRequest(username, action, description)
                            .SubscribeOn(NewThreadScheduler.Default)
                            .ObserveOn(Scheduler.Immediate)

                            .Finally(() => {
                                logger.LineWithEmphasis("Finished operation", opId.ToString(), ConsoleColor.White);
                                authReqTerminationEvent.Set();
                            })
                            
                            .Timeout(TimeSpan.FromSeconds(10))
                            
                            .Subscribe(
                                uuid => authReqUuid = uuid,
                                error => authReqException = error);
                        
                        authReqTerminationEvent.WaitOne();
                        if (authReqException == null)
                        {
                            logger.LineWithEmphasis("Created auth request", authReqUuid, ConsoleColor.White);
                            obj.Writer.WriteLine("OK");
                            obj.Writer.WriteLine(authReqUuid);
                        } else {
                            logger.ErrorLine("Failed to create auth request", authReqException);
                            obj.Writer.WriteLine("NOK");
                            obj.Writer.WriteLine(authReqException.Message);
                            obj.Writer.WriteLine(authReqException.ToString());
                        }
                        mre.Set();
                        break;
                    case "STATUS_FOR_REQUEST":
                        string reqUuid = obj.Reader.ReadLine();

                        obj.Writer.WriteLine("NOK");
                        obj.Writer.WriteLine("NOT_IMPLEMENTED");
                        mre.Set();
                        break;
                    default:
                        throw new InvalidOperationException("Unknown message " + obj.FirstLine);
                        break;
                }

                mre.WaitOne();
                try
                {
                    obj.Writer.Flush();
                }
                catch (IOException)
                {
                    // Ungraceful shutdown by client ...
                    // ...
                }

                if (shouldTerminate)
                {
                    logger.WriteHeader("Quitting (received DIE)...", ConsoleColor.White, ConsoleColor.Red);
                    terminationEvent.Set();
                }
            }
            catch (Exception e)
            {
                logger.ErrorLine("Failure while processing message from client. Disconnecting", e);
                obj.Disconnect();
            }
        }
    }
}
