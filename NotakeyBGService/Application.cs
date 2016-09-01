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
        SimpleApi api = new SimpleApi();
        Logger logger = new Logger();

        PipeServerFactory factory;
        ManualResetEvent terminationEvent;
        
        public Application(ManualResetEvent terminationEvent)
        {
            this.terminationEvent = terminationEvent;
            this.factory = new PipeServerFactory(logger);
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
            logger.WriteMessage("Spawning main listener");

            factory
                .GetConnectedServer()
                .SubscribeOn(NewThreadScheduler.Default)
                .ObserveOn(NewThreadScheduler.Default)

                // Spawn server before attempting to process messages (which may block)
                .Do(_ => SpawnServer())

                .Subscribe(
                    OnClientPipeCreated,
                    OnClientSpawningError
                );
        }

        private void OnClientSpawningError(Exception error)
        {
            logger.ErrorLine("Error spawning child server: " + error.ToString());
        }

        private void OnClientPipeCreated(NotakeyPipeServer2 server)
        {
            logger.WriteMessage("Created client pipe. Connecting ...");

            // Make sure to stay on the same thread (or the pipes will fail)
            server.Connect()
                .Subscribe(OnServerMessage, OnServerError, OnCompleted);
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

                logger.WriteMessage("Received " + obj.FirstLine);

                switch (obj.FirstLine)
                {
                    case "API_HEALTH_CHECK":
                        throw new NotImplementedException();
                        break;
                    case "REQUEST_AUTH":
                        throw new NotImplementedException();
                        break;
                    case "STATUS_FOR_REQUEST":
                        throw new NotImplementedException();
                        break;
                    default:
                        throw new InvalidOperationException("Unknown message " + obj.FirstLine);
                        break;
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
