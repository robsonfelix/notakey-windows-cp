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

namespace NotakeyBGService
{
    public class Application
    {
        SimpleApi api = new SimpleApi();
        PipeServerFactory factory = new PipeServerFactory();

        ManualResetEvent terminationEvent;
        
        public Application(ManualResetEvent terminationEvent)
        {
            this.terminationEvent = terminationEvent;
        }

        internal void Run()
        {
            Log("Binding to Notakey API (" + ApiConfiguration.ApiEndpoint + "; " + ApiConfiguration.AccessId + ")");
            api.Bind(ApiConfiguration.ApiEndpoint, ApiConfiguration.AccessId)
               .Timeout(TimeSpan.FromSeconds(15))
               .Subscribe(
                   p => Log("Wtf"),
                   error =>
                   {
                       Log("Notakey API bind failure: " + error.ToString());
                       terminationEvent.Set();
                   },
                   SpawnServer);   
        }

        void Log(string msg)
        {
            Console.WriteLine("[{0}] => {1}", Thread.CurrentThread.ManagedThreadId, msg);
        }

        void SpawnServer()
        {
            Log("Spawning main listener");

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
            Log("Error spawning child server: " + error.ToString());
        }

        private void OnClientPipeCreated(NotakeyPipeServer2 server)
        {
            Log("Created client pipe. Connecting ...");

            // Make sure to stay on the same thread (or the pipes will fail)
            server.Connect()
                .Subscribe(OnServerMessage, OnServerError, OnCompleted);
        }

        private void OnCompleted()
        {
            Log("Client disconnected");
        }

        private void OnServerError(Exception e)
        {
            Log("Client communication error: " + e.ToString());
        }

        private void OnServerMessage(PipeServerMessage obj)
        {
            // This is on a dedicated thread - don't do async operations
            ManualResetEvent mre = new ManualResetEvent(false);

            Log("Received " + obj.FirstLine);

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
                    obj.Disconnect();
                    Console.WriteLine("Unknown message. Terminating.");
                    break;
            }
            
        }
    }
}
