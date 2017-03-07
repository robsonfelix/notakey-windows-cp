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
using Notakey.Utility;
using System.Windows.Threading;
using System.IO;
using System.Diagnostics;

namespace NotakeyBGService
{
    // TODO: read config from args
    public static class BGServiceConfiguration
    {
        public static readonly TimeSpan AsyncTimeout = TimeSpan.FromSeconds(30);
    }

    public static class ApiConfiguration
    {
        public static string AccessId = "<UNSET: pass as 2nd cli parameter>";
        public static string ApiEndpoint = "<UNSET: pass as 1st cli parameter>";
    }

    public class UnattendedLogger : Logger
    {
        protected override void SetColors(ConsoleColor fore, ConsoleColor bg)
        {
            
        }

        protected override void ResetColors()
        {
            
        }

        protected override void WriteFullLine_WhileLocked(string text, ConsoleColor fore, ConsoleColor bg)
        {
            output.WriteLine(text);
        }

        protected override string CenterTextWithLeftPad(string text)
        {
            return text;
        }
    }

    public class Application
    {
        Random random = new Random();

        SimpleApi api = new SimpleApi();
        Logger logger;

        PipeServerFactory factory;
        ManualResetEvent terminationEvent;

        List<IDisposable> disposableSubscriptions = new List<IDisposable>();

        public Application(ManualResetEvent terminationEvent, bool unattended)
        {
            Console.Out.WriteLine("Starting service. Unattended: " + Convert.ToString(unattended));

            if (!unattended)
            {
                 logger = new Logger();
            }
            else
            {
                 logger = new UnattendedLogger();
            }

            logger.WriteMessage($"Using API endpoint: {ApiConfiguration.ApiEndpoint}");
            logger.WriteMessage($"Using API access id: {ApiConfiguration.AccessId}");

            this.terminationEvent = terminationEvent;
            this.factory = new PipeServerFactory(logger);
        }

        internal void Cleanup()
        {
            logger.WriteMessage("Cleaning up...");
            while (disposableSubscriptions.Any())
            {
                var disposable = disposableSubscriptions.First();
                disposableSubscriptions.Remove(disposable);

                logger.WriteMessage("  disposing " + disposable.ToString());
                disposable.Dispose();
            }
        }

        internal void Run()
        { 
            logger.WriteHeader("Starting Notakey BG IPC service", ConsoleColor.White, ConsoleColor.DarkBlue);

            logger.WriteMessage("Trying to bind to Notakey API (" + ApiConfiguration.ApiEndpoint + "; " + ApiConfiguration.AccessId + ")");
            logger.LineWithEmphasis("Retry strategy", "ExponentialBackoff", ConsoleColor.White);

            Task.Run(() =>
            {
                // Keep attempting to bind to API in the background
                // Failure with this should NOT cause termination or block the IPC
                // pipe server from starting up, because its a valid situation where
                // e.g. network is down but the Credential Provider still needs
                // to query for status (e.g. is service running even if API down?)
                api.Bind(ApiConfiguration.ApiEndpoint, ApiConfiguration.AccessId)
                    .Timeout(TimeSpan.FromSeconds(1))
                    .RetryWithBackoffStrategy(
                        retryCount: 0,
                        retryOnError: e => { logger.ErrorLine("Bind attempt failed", e); return true; },
                        strategy: RetryWithBackoffStrategy_ObservableExtensions.ExponentialBackoff
                    )
                    .Subscribe(
                        p => logger.LineWithReverseEmphasis("SUCCESS", "Bound to: " + p.ToString(), ConsoleColor.Green),
                        error => logger.ErrorLine("Could not bind to the Notakey API", error)
                    );
            });
            SpawnServer();
        }

        void SpawnServer()
        {
            terminationEvent.WaitOne(0);
            logger.WriteMessage("Spawning main listener");

            IDisposable masterPipeListener = null;
            masterPipeListener = factory
                .GetConnectedServer()
                .SubscribeOn(NewThreadScheduler.Default)
                .ObserveOn(NewThreadScheduler.Default)

                // Spawn server before attempting to process messages (which may block)
                .Do(_ => SpawnServer())

                // On some windows versions (for example, 2012R2 server) new main named pipe is being initialized before
                // previous one is fully disposed and exception is throws that all pipes are busy. As only 1 is alowed with
                // specific name.
                .RetryWithBackoffStrategy(retryCount: 3,
                                            retryOnError: (Exception arg) =>
                {
                    if (arg is System.IO.IOException)
                    {
                        logger.WriteMessage("Retry to spawn server. ERROR: " + arg.ToString());
                        return true;
                    }
                    logger.WriteMessage("Do not retry to spawn server. ERROR: " + arg.ToString());
                    return false;
                })
                .Finally(() => disposableSubscriptions.Remove(masterPipeListener))   
                .Subscribe(
                    OnClientPipeCreated,
                    OnClientSpawningError
                );
            disposableSubscriptions.Add(masterPipeListener);
        }

        private void OnClientSpawningError(Exception error)
        {
            logger.ErrorLine("Error spawning child server: " + error.ToString());
            terminationEvent.Set();
        }

        private void OnClientPipeCreated(NotakeyPipeServer2 server)
        {
            logger.WriteMessage("Created client pipe. Connecting ...");

            // Make sure to stay on the same thread (or the pipes will fail)
            IDisposable clientSubscription = null;
            clientSubscription = server.Connect()
                .Finally(() => disposableSubscriptions.Remove(clientSubscription))
                .Subscribe(OnServerMessage, OnServerError, OnCompleted);
            disposableSubscriptions.Add(clientSubscription);
        }

        private void OnCompleted()
        {
            logger.WriteMessage("Client disconnected");
        }

        private void OnServerError(Exception e)
        {
            logger.ErrorLine("Client communication error: " + e.ToString());
        }

        private void OnServerMessage(PipeServerMessage obj)
        {
            try
            {
                // This is on a dedicated thread - don't do async operations
                ManualResetEvent mre = new ManualResetEvent(false);

                logger.LineWithEmphasis("Processing message", obj.FirstLine, ConsoleColor.Magenta);
                
                int opId = random.Next();
                bool shouldTerminate = false;
                
                StringBuilder sb;
                
                switch (obj.FirstLine)
                {
                    case "DIE":
                        shouldTerminate = true;
                        mre.Set();
                        break;
                    case "API_HEALTH_CHECK":
                        logger.LineWithEmphasis("Performing health check. Operation", opId.ToString(), ConsoleColor.White);

                        TimeSpan checkTimeout = TimeSpan.FromSeconds(int.Parse(obj.Reader.ReadLine()));
                        logger.LineWithEmphasis("Health check timeout (sec)", checkTimeout.TotalSeconds.ToString(), ConsoleColor.White);
                        
                        var healthCheckEvent = new ManualResetEvent(false);

                        Dictionary<string,string> healthStatus = null;
                        Exception healthException = null;

                        api
                            .PerformHealthCheck()
                            .SubscribeOn(NewThreadScheduler.Default)
                            .ObserveOn(Scheduler.Immediate)

                            .Finally(() => {
                                logger.LineWithEmphasis("Finished operation", opId.ToString(), ConsoleColor.White);
                                healthCheckEvent.Set();
                            })
                            
                            .Timeout(checkTimeout)
                            
                            .Subscribe(
                                response => healthStatus = response,
                                error => healthException = error);

                        healthCheckEvent.WaitOne();
                        if (healthException == null)
                        {
                            var strResponse = String.Join(", ", healthStatus.Select(kvp =>
                                  String.Format("{0} {1}", kvp.Key, kvp.Value)));
                            logger.LineWithEmphasis("Received response", strResponse, ConsoleColor.White);
                            try
                            {
                                obj.Writer.WriteLine(healthStatus["STATUS"] == "LIVE" ? "OK" : "NOK");
                            }
                            catch (KeyNotFoundException)
                            {
                                obj.Writer.WriteLine("Server is reachable but not reporting overall status.");
                            }
                        } else {
                            logger.ErrorLine("Health check failed", healthException);
                            if (healthException is TimeoutException)
                            {
                                obj.Writer.WriteLine("API call timed out.");
                            }
                            else if (healthException is ApiNotBoundException)
                            {
                                obj.Writer.WriteLine(healthException.Message);
                            }
                            else
                            {
                                obj.Writer.WriteLine(healthException.Message);
                            }
                        }
                        mre.Set();
                        break;
                    case "SYNC_REQUEST_STATUS":
                        logger.LineWithEmphasis("Checking auth request status (until completion). Operation", opId.ToString(), ConsoleColor.White);

                        string checkUuid = obj.Reader.ReadLine();

                        sb = new StringBuilder();
                        sb.AppendLine("Response check parameters:");
                        sb.Append("  uuid: "); sb.AppendLine(checkUuid);
                        logger.WriteMessage(sb.ToString());

                        var authCheckTerminationEvent = new ManualResetEvent(false);

                        ApprovalRequestResponse authCheckResponse = null;
                        Exception authCheckException = null;

                        api
                            .Poll(checkUuid)
                            .SubscribeOn(NewThreadScheduler.Default)
                            .ObserveOn(Scheduler.Immediate)

                            .Finally(() => {
                                logger.LineWithEmphasis("Finished operation", opId.ToString(), ConsoleColor.White);
                                authCheckTerminationEvent.Set();
                            })
                            
                            .Timeout(TimeSpan.FromSeconds(30))
                            
                            .Subscribe(
                                response => authCheckResponse = response,
                                error => authCheckException = error);

                        authCheckTerminationEvent.WaitOne();
                        if (authCheckException == null)
                        {
                            logger.LineWithEmphasis("Received response", authCheckResponse.ApprovalGranted.ToString(), ConsoleColor.White);
                            obj.Writer.WriteLine("OK");
                            obj.Writer.WriteLine(authCheckResponse.ApprovalGranted ? "TRUE" : "FALSE");
                        } else {
                            logger.ErrorLine("Failed to create auth request", authCheckException);
                            obj.Writer.WriteLine("NOK");
                            obj.Writer.WriteLine(authCheckException.Message);
                            obj.Writer.WriteLine(authCheckException.ToString());
                        }
                        mre.Set();
                        break;
                    case "REQUEST_AUTH":
                        logger.LineWithEmphasis("Requesting authentication. Operation", opId.ToString(), ConsoleColor.White);
                        
                        string username = obj.Reader.ReadLine();
                        logger.LineWithEmphasis("Requested username", username, ConsoleColor.White);
                        
                        string action= obj.Reader.ReadLine();
                        logger.LineWithEmphasis("Requested action", action, ConsoleColor.White);
                        
                        string description = obj.Reader.ReadLine();
                        logger.LineWithEmphasis("Requested description", description, ConsoleColor.White);

                        var authReqTerminationEvent = new ManualResetEvent(false);
                        string authReqUuid = null;
                        Exception authReqException = null;

                        api
                            .CreateAuthRequest(username, action, description)
                            .SubscribeOn(NewThreadScheduler.Default)
                            .ObserveOn(Scheduler.Immediate)

                            .Finally(() => {
                                logger.LineWithEmphasis("Finished operation", opId.ToString(), ConsoleColor.White);
                                authReqTerminationEvent.Set();
                            })
                            
                            .Timeout(TimeSpan.FromSeconds(10))
                            
                            .Subscribe(
                                uuid => authReqUuid = uuid,
                                error => authReqException = error);
                        
                        authReqTerminationEvent.WaitOne();
                        if (authReqException == null)
                        {
                            logger.LineWithEmphasis("Created auth request", authReqUuid, ConsoleColor.White);
                            obj.Writer.WriteLine("OK");
                            obj.Writer.WriteLine(authReqUuid);
                        } else {
                            logger.ErrorLine("Failed to create auth request", authReqException);
                            obj.Writer.WriteLine("NOK");
                            obj.Writer.WriteLine(authReqException.Message);
                            obj.Writer.WriteLine(authReqException.ToString());
                        }
                        mre.Set();
                        break;
                    case "STATUS_FOR_REQUEST":
                        string reqUuid = obj.Reader.ReadLine();
						Debug.WriteLine($"STATUS_FOR_REQUEST {reqUuid}");

                        obj.Writer.WriteLine("NOK");
                        obj.Writer.WriteLine("NOT_IMPLEMENTED");
                        mre.Set();
                        break;
                    default:
                        throw new InvalidOperationException("Unknown message " + obj.FirstLine);
                }

                mre.WaitOne();
                try
                {
                    obj.Writer.Flush();
                }
                catch (IOException)
                {
                    // Ungraceful shutdown by client ...
                    // ...
                }

                if (shouldTerminate)
                {
                    logger.WriteHeader("Quitting (received DIE)...", ConsoleColor.White, ConsoleColor.Red);
                    terminationEvent.Set();
                }
            }
            catch (Exception e)
            {
                logger.ErrorLine("Failure while processing message from client. Disconnecting", e);
                obj.Disconnect();
            }
        }
    }
}
