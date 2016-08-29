using Notakey.SDK;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reactive.Linq;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NotakeyBGService
{
    partial class NotakeyBGService 
    {
        ManualResetEvent terminationEvent;
        SimpleApi api;

        const int ConnectTimeout = 5000;

        public NotakeyBGService(ManualResetEvent terminationEvent)
        {
            this.terminationEvent = terminationEvent;

            api = new SimpleApi();       
        }

        public void StartAsApp()
        {
            api.Bind(ApiConfiguration.ApiEndpoint, ApiConfiguration.AccessId)
                .Subscribe(
                p => { },
                error =>
                {
                    Debug.WriteLine("BIND failure: " + error.ToString());
                    terminationEvent.Set();
                },
                SpawnPipeNameServerListenerThread);   
        }

        private void ListenThenCommunicateWithSpecificClient(string pipeName)
        {
            AutoResetEvent asyncWaitEvent = new AutoResetEvent(false);
            bool shouldTerminateService = false;

            using (NamedPipeServerStream server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.WriteThrough)) {
                server.WaitForConnection();

                using (var sr = new StreamReader(server, Encoding.UTF8, true, 4096, true))
                {
                    using (var sw = new StreamWriter(server, Encoding.UTF8, 4096, true))
                    {
                        try
                        {
                            string cmd = sr.ReadLine().ToUpperInvariant();

                            switch (cmd)
                            {
                                case "DIE":
                                    sw.WriteLine("OK");
                                    asyncWaitEvent.Set();
                                    shouldTerminateService = true;
                                    break;
                                case "API_HEALTH_CHECK":
                                    var chain = api.GetBoundApplication();
                                    HandleChain(chain, sw, asyncWaitEvent, OnHealthCheckSuccess, OnHealthCheckError);
                                    break;
                                case "REQUEST_AUTH":
                                    string username = sr.ReadLine();
                                    string password = sr.ReadLine();

                                    try
                                    {
                                        var authReqChain = api.RequestApproval("demo", "Windows", "Desc yo");
                                        HandleChain(authReqChain, sw, asyncWaitEvent, OnRequestedAuth, OnRequestingAuthError);
                                        
                                        //sw.WriteLine("OK");
                                        //sw.WriteLine(uuid_returned);
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
                                        asyncWaitEvent.Set();
                                        throw new NotImplementedException();
                                        //var status = c.StatusForRequest(uuid_to_check);
                                        //if (status == Notakey.PublicAPI.AuthRequestStatus.Approved)
                                        //{
                                        //    sw.WriteLine("OK");
                                        //}
                                        //else if (status == Notakey.PublicAPI.AuthRequestStatus.Pending)
                                        //{
                                        //    sw.WriteLine("WAIT");
                                        //}
                                        //else
                                        //{
                                        //    sw.WriteLine("NOK");
                                        //}
                                    }
                                    catch (Exception e)
                                    {
                                        sw.WriteLine("NOK");
                                        sw.WriteLine(e.Message);
                                    }
                                    break;
                                default:
                                    sw.WriteLine("UNRECOGNIZED({0})", cmd);
                                    asyncWaitEvent.Set();
                                    break;
                            }

                            asyncWaitEvent.WaitOne(BGServiceConfiguration.AsyncTimeout);

                            if (!server.IsConnected)
                            {
                                Debug.WriteLine("Not flushing. BaseStream already null");
                            }
                            else
                            {
                                sw.Flush();
                            }
                        }
                        catch (IOException e)
                        {
                            Console.WriteLine("IOException. Disconnecting serving pipe for this connection");
                            Debug.WriteLine(e.ToString());
                            if (server.IsConnected)
                            {
                                server.Disconnect();
                            }
                        }

                        if (shouldTerminateService)
                        {
                            this.terminationEvent.Set();
                        }
                    }
                }
            }
        }

        private void OnRequestingAuthError(StreamWriter sw, Exception arg2)
        {
            Debug.WriteLine("OnRequestingAuthError: " + arg2.ToString());
            sw.WriteLine("NOK");
            sw.WriteLine(arg2.Message);
        }

        private void OnRequestedAuth(StreamWriter sw, ApprovalRequestResponse arg2)
        {
            sw.WriteLine("NOK");
            sw.WriteLine("TODO: this should return UUID for request, not wait for completion");
        }

        private void OnHealthCheckError(StreamWriter sw, Exception obj)
        {
            Debug.WriteLine("Health check failed: " + obj.ToString());
            sw.WriteLine(obj.Message);
        }

        private void OnHealthCheckSuccess(StreamWriter sw, ApplicationInformation obj)
        {
            sw.WriteLine("Bound to " + obj.DisplayName);
        }

        private void HandleChain<T>(
            IObservable<T> chain, 
            StreamWriter sw,
            AutoResetEvent asyncWaitEvent,
            Action<StreamWriter, T> onNext, 
            Action<StreamWriter, Exception> onError,
            Action onCompleted = null)
        {
            chain
                .Finally(() => Debug.WriteLine("API chain terminating"))
                .Finally(() => asyncWaitEvent.Set())
                .Subscribe(
                result => onNext(sw, result), 
                error => onError(sw, error), 
                onCompleted ?? (() => {}));
        }

        private void ListenThenCommunicatePipeNames()
        {
            // Allow 2 instances. One that' s still being processed, and another one (spawned right before
            // first one winds down)
            using (NamedPipeServerStream pipeServer =
            new NamedPipeServerStream(NotakeyIPCLibrary.NotakeyPipeServer.MasterPipeName, PipeDirection.Out, 2))
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
            new Task(ListenThenCommunicatePipeNames).Start();
        }
    }

    public static class BGServiceConfiguration
    {
        public static readonly TimeSpan AsyncTimeout = TimeSpan.FromSeconds(30);
    }

    public static class ApiConfiguration
    {
        public static readonly string AccessId = "84c328f2-4ff2-4980-8db6-3ecabf55bff1";
        public static readonly string ApiEndpoint = "https://demo.notakey.com/api/v2/";
    }
}
