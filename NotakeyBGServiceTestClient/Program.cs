using NotakeyIPCLibrary;
using System;
using System.Collections.Generic;
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
            try
            {
                var client = new NotakeyBGServerClient();
                string result = null;
                client.Execute((StreamReader sr) => {
                    result = sr.ReadLine();
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
            Console.WriteLine("Client has finished. Press any key ...");
            Console.ReadKey();
        }
    }
}
