using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NotakeyBGService
{
    partial class NotakeyBGService : ServiceBase
    {
        const int ConnectTimeout = 5000;

        public NotakeyBGService()
        {
            InitializeComponent();
        }

        public void StartAsApp()
        {
            OnStart(new string[] {});
        }

        protected override void OnStart(string[] args)
        {
            SpawnPipeNameServerListenerThread();
        }

        protected override void OnStop()
        {
            // TODO: Add code here to perform any tear-down necessary to stop your service.
        }

        private void ListenThenCommunicateWithSpecificClient(string pipeName)
        {
            AutoResetEvent connectedEvent = new AutoResetEvent(false);
            using (NamedPipeServerStream server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.WriteThrough)) {
                server.WaitForConnection();

                using (var sr = new StreamReader(server, Encoding.UTF8, true, 4096, true))
                {
                    using (var sw = new StreamWriter(server, Encoding.UTF8, 4096, true))
                    {
                        string cmd = sr.ReadLine().ToUpperInvariant();
                        var c = new Notakey.PublicAPI.Client();

                        switch (cmd)
                        {
                            case "API_HEALTH_CHECK":
                                string msg;
                                bool result = Notakey.PublicAPI.Client.IsAPIAvailable(out msg);
                                Console.WriteLine("Notakey API availability: {0} {1}", result, msg);

                                sw.WriteLine(msg);
                                break;
                            case "REQUEST_AUTH":
                                string phone_number = sr.ReadLine();
                                string password = sr.ReadLine();
                                
                                try
                                {
                                    string uuid_returned = c.RequestAuth(phone_number, password);
                                    sw.WriteLine("OK");
                                    sw.WriteLine(uuid_returned);
                                }
                                catch (Exception e)
                                {
                                    sw.WriteLine("NOK");
                                    sw.WriteLine(e.Message);
                                }
                                break;
                            case "STATUS_FOR_REQUEST":
                                string uuid_to_check = sr.ReadLine();
                                
                                try
                                {
                                    var status = c.StatusForRequest(uuid_to_check);
                                    if (status == Notakey.PublicAPI.AuthRequestStatus.Approved)
                                    {
                                        sw.WriteLine("OK");
                                    }
                                    else if (status == Notakey.PublicAPI.AuthRequestStatus.Pending)
                                    {
                                        sw.WriteLine("WAIT");
                                    }
                                    else
                                    {
                                        sw.WriteLine("NOK");
                                    }
                                }
                                catch (Exception e)
                                {
                                    sw.WriteLine("NOK");
                                    sw.WriteLine(e.Message);
                                }
                                break;
                            default:
                                sw.WriteLine("UNRECOGNIZED({0})", cmd);
                                break;
                        }

                        sw.Flush();
                    }
                }
            }
        }

        private void ListenThenCommunicatePipeNames()
        {
            // Allow 2 instances. One that' s still being processed, and another one (spawned right before
            // first one winds down)
            using (NamedPipeServerStream pipeServer =
            new NamedPipeServerStream("lv.montadigital.notakey.pipenameserver", PipeDirection.Out, 2))
            {
                try
                {
                    pipeServer.WaitForConnection();

                    string clientPipe = string.Format("lv.montadigital.notakey.client.{0}", Guid.NewGuid().ToString());
                    Console.WriteLine("Generated pipe name for client: {0}", clientPipe);
                    SpawnSpecificClientListenerThread(clientPipe);

                    using (var sw = new StreamWriter(pipeServer))
                    {
                        sw.WriteLine(clientPipe);
                    }
                    SpawnPipeNameServerListenerThread(); 
                }
                catch (IOException e)
                {
                    Console.WriteLine("ERROR: {0}", e.Message);
                }
            }
        }

        private void SpawnSpecificClientListenerThread(string pipeName)
        {
            Task task = new Task(() => ListenThenCommunicateWithSpecificClient(pipeName));
            task.Start();
        }

        private void SpawnPipeNameServerListenerThread()
        {
            Task task = new Task(ListenThenCommunicatePipeNames);
            task.Start();
        }
    }
}
