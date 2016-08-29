using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NotakeyIPCLibrary;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.IO;
using System.IO.Pipes;

namespace NotakeyBGService
{
    static class Program
    {
        static ManualResetEvent terminationEvent = new ManualResetEvent(false);

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Notakey CP BG service...");

            var factory = new PipeServerFactory();
            SpawnServer(factory);

            terminationEvent.WaitOne();
        }

        private static void SpawnServer(PipeServerFactory factory)
        {
            factory
                .GetConnectedServer()
                .SubscribeOn(NewThreadScheduler.Default)
                .ObserveOn(NewThreadScheduler.Default)
                
                // Spawn server before attempting to process messages (which may block)
                .Do(_ => SpawnServer(factory))
                
                .Subscribe(
                    OnChildServer,
                    OnChildError
                );
        }

        private static void OnChildError(Exception error)
        {
            Debug.WriteLine("Error spawning child server: " + error.ToString());
        }

        private static void OnChildServer(NotakeyPipeServer2 server)
        {
            // Make sure to stay on the same thread (or the pipes will fail)
            server.Connect()
                .Subscribe(OnServerMessage, OnServerError, OnCompleted);
        }

        private static void OnCompleted()
        {
            Console.WriteLine("Child server disconnected");
        }

        private static void OnServerError(Exception e)
        {
            Console.WriteLine("Child server error: " + e.ToString());
        }

        private static void OnServerMessage(PipeServerMessage obj)
        {
            try
            {
                Console.WriteLine("Received " + obj.FirstLine + " on thread " + Thread.CurrentThread.ManagedThreadId);
                switch (obj.FirstLine)
                {
                    default:
                        obj.Disconnect();
                        Console.WriteLine("Unknown message. Terminating.");
                        break;
                }
            } catch (IOException e)
            {
                Console.WriteLine("Socket IO exception in communication with client. Did client disconnect?");
                Debug.WriteLine(e.ToString());
            }
        }


    }
}
