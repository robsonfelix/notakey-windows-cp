using NotakeyIPCLibrary;
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
            int threadC = 1;
            Console.WriteLine("Press any key to start the test in {0} thread(s)...", threadC);
            Console.ReadKey();

            var tasks = new List<Task>();
            for (int i = 0; i < threadC; ++i)
            {
                tasks.Add(Task.Run(() => DoStuff()));
            }
            
            Task.WaitAll(tasks.ToArray());

            Console.WriteLine("Finished. Press any key to quit ...");
            Console.ReadKey();
        }

        static void DoStuff()
        {
            NotakeyPipeClient client = null;
            try
            {
                string goodUuid = null;

                client = new NotakeyPipeClient();

                return; // trigger DIE

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

                        if (status)
                        {
                            goodUuid = msg;
                        }
                        Console.WriteLine("Requested auth. Success: {0}. Message: {1}", status, msg);
                    },
                    "REQUEST_AUTH", "jkirsteins", "My OK Action", "My OK Description");

                client.Execute(
                    (StreamReader sr) =>
                    {
                        bool status = ("OK".Equals(sr.ReadLine()));
                        string msg = sr.ReadLine();

                        Console.WriteLine("Requested auth 2. Success: {0}. Message: {1}", status, msg);
                    },
                    "REQUEST_AUTH", "jkirsteins_which_does_not_exist", "My NOK Action", "My NOK Description");

                string uuid = goodUuid ?? "7f098073-afc2-45e2-8fef-9e33bfd81690";

                client.Execute(
                    (StreamReader sr) =>
                    {
                        Console.WriteLine("Status for request {0}: {1}", uuid, sr.ReadLine());
                    }, "STATUS_FOR_REQUEST", uuid);

                client.Execute(
                    (StreamReader sr) =>
                    {
                        bool status = ("OK".Equals(sr.ReadLine()));
                       
                        Console.WriteLine("Sync'ed status for request {0}: {1}", uuid, status);
                        if (!status)
                        {
                            Console.WriteLine("  message: {0}", sr.ReadLine());
                        }
                    }, "SYNC_REQUEST_STATUS", uuid);


                //Console.WriteLine("CMD3: {0}", client.Execute("CMD3"));
                //Console.WriteLine("CMD4: {0}", client.Execute("CMD4"));
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception happened: {0}", e.ToString());
            }
            finally
            {
                if (client != null)
                {
                    client.Execute(p => { Console.WriteLine("Sent DIE"); }, "DIE");
                }
            }
        }
    }
}
