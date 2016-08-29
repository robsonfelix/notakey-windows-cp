using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace test
{
    static class Program_Back
    {
        // P/Invoke required:
        private const UInt32 StdOutputHandle = 0xFFFFFFF5;
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(UInt32 nStdHandle);
        [DllImport("kernel32.dll")]
        private static extern void SetStdHandle(UInt32 nStdHandle, IntPtr handle);
        [DllImport("kernel32")]
        static extern bool AllocConsole();

        public static void CreateConsole()
        {
            AllocConsole();

            // stdout's handle seems to always be equal to 7
            IntPtr defaultStdout = new IntPtr(7);
            IntPtr currentStdout = GetStdHandle(StdOutputHandle);

            if (currentStdout != defaultStdout)
                // reset stdout
                SetStdHandle(StdOutputHandle, defaultStdout);

            // reopen stdout
            TextWriter writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            Console.SetOut(writer);
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main_Old(string[] args)
        {
            ManualResetEvent terminationEvent = new ManualResetEvent(false);
            Console.WriteLine("Starting Notakey CP BG service... ({0})", NotakeyIPCLibrary.NotakeyPipeServer.MasterPipeName);

            new NotakeyBGService.NotakeyBGService(terminationEvent).StartAsApp();
            terminationEvent.WaitOne();
        }
    }
}
