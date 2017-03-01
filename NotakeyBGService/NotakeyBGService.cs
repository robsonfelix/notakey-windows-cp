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
using Notakey.Utility;

namespace NotakeyBGService
{
    class NotakeyBGService 
    {
        ManualResetEvent terminationEvent;
        SimpleApi api;
        Logger logger;

        const int ConnectTimeout = 5000;

        public NotakeyBGService(ManualResetEvent terminationEvent, Logger parentLogger)
        {
            this.terminationEvent = terminationEvent;

            api = new SimpleApi();
            logger = new Logger("Service", parentLogger);
        }

        public void StartAsApp()
        {
            Observable.Defer(() => api.Bind(ApiConfiguration.ApiEndpoint, ApiConfiguration.AccessId))
                .RetryWithBackoffStrategy(
                    retryCount: 0,
                    strategy: RetryWithBackoffStrategy_ObservableExtensions.ExponentialBackoff
                )
                .Subscribe(
                    p => { },
                    error =>
                    {
                        logger.Debug("BIND failure: " + error.ToString());
                        logger.LineWithEmphasis("Trying again in a bit using. Strategy", "Exponential Backoff", ConsoleColor.White);
                    },
                    SpawnPipeNameServerListenerThread);   
        }

        private void ListenThenCommunicateWithSpecificClient(string pipeName)
        {
            AutoResetEvent asyncWaitEvent = new AutoResetEvent(false);
            bool shouldTerminateService = false;

            var ps = new PipeSecurity();
            var sid = new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.WorldSid, null);
            var par = new PipeAccessRule(sid, PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow);
            ps.AddAccessRule(par);

            using (NamedPipeServerStream server = new NamedPipeServerStream(pipeName, 
                PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.WriteThrough, 0, 0, ps)) {
                
                server.WaitForConnection();

                using (var sr = new StreamReader(server, Encoding.UTF8, true, 4096, true))
                {
                    using (var sw = new StreamWriter(server, Encoding.UTF8, 4096, true))
                    {
                        try
                        {
                            string cmd = sr.ReadLine().ToUpperInvariant();
                            logger.WriteMessage($"Received {cmd}");

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
                                    string action = sr.ReadLine();
                                    string description = sr.ReadLine();

                                    try
                                    {
                                        var authReqChain = api.PerformFullVerification(username, action, description);
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
                                logger.Debug("Not flushing. BaseStream already null");
                            }
                            else
                            {
                                sw.Flush();
                            }
                        }
                        catch (IOException e)
                        {
                            logger.ErrorLine("IOException. Disconnecting serving pipe for this connection");
                            logger.Debug(e.ToString());
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
            logger.Debug("OnRequestingAuthError: " + arg2.ToString());
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
            logger.Debug("Health check failed: " + obj.ToString());
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
            throw new NotImplementedException();
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
}
