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
using System.Runtime.InteropServices;

namespace NotakeyBGService
{
    static class EntryPoint
    {
        static ManualResetEvent terminationEvent = new ManualResetEvent(false);

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            var app = new Application(terminationEvent, args.Contains("/unattended"));
            app.Run();

            terminationEvent.WaitOne();
            Console.WriteLine("Received termination event. Quitting ...");
            app.Cleanup();
        }
    }
}
