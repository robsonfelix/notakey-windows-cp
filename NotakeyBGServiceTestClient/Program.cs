﻿using NotakeyIPCLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotakeyBGServiceTestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Press any key to start the test in two threads...");
            Console.ReadKey();

            var a = Task.Run(() => DoStuff());
            var b = Task.Run(() => DoStuff());
            Task.WaitAll(a, b);

            Console.WriteLine("Finished. Press any key to quit ...");
            Console.ReadKey();
        }

        static void DoStuff()
        {
            try
            {
                var client = new NotakeyPipeClient();
                string result = null;
                client.Execute((StreamReader sr) => {
                    result = sr.ReadLine();
                    Debug.WriteLine("Received response");
                }, "API_HEALTH_CHECK");
                Console.WriteLine("STATUS_CHECK: {0}", result);

                client.Execute(
                    (StreamReader sr) =>
                    {
                        bool status = ("OK".Equals(sr.ReadLine()));
                        string msg = sr.ReadLine();

                        Console.WriteLine("Requested auth. Success: {0}. Message: {1}", status, msg);
                    },
                    "REQUEST_AUTH", "20208714", "nAda52Ed");

                client.Execute(
                    (StreamReader sr) =>
                    {
                        bool status = ("OK".Equals(sr.ReadLine()));
                        string msg = sr.ReadLine();

                        Console.WriteLine("Requested auth 2. Success: {0}. Message: {1}", status, msg);
                    },
                    "REQUEST_AUTH", "20208715", "nAda52Ed");

                string uuid = "7f098073-afc2-45e2-8fef-9e33bfd81690";
                client.Execute(
                    (StreamReader sr) =>
                    {
                        Console.WriteLine("Health check for {0}: {1}", uuid, sr.ReadLine());
                    }, "STATUS_FOR_REQUEST", uuid);


                client.Execute(p => { Console.WriteLine("Sent DIE"); }, "DIE");
                //Console.WriteLine("CMD3: {0}", client.Execute("CMD3"));
                //Console.WriteLine("CMD4: {0}", client.Execute("CMD4"));
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception happened: {0}", e.ToString());
            }
        }
    }
}
