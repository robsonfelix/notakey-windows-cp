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
using Microsoft.Win32;

namespace NotakeyBGService
{
    static class EntryPoint
    {
        static ManualResetEvent terminationEvent = new ManualResetEvent(false);
        
        private static string BaseRegistryKey = "Software\\Notakey\\WindowsCP";
        public static string LogSource = "Application";
        public static string LogApplication = "Notakey BG Service";
       
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            if (args.Length >= 2)
            {
                ApiConfiguration.ApiEndpoint = args[0];
                ApiConfiguration.AccessId = args[1];
            }

            if (!EventLog.SourceExists(LogSource))
                EventLog.CreateEventSource(LogSource, LogApplication);

            LoadRegistryConfigOverrrides();

            if(String.IsNullOrEmpty(ApiConfiguration.ApiEndpoint) || String.IsNullOrEmpty(ApiConfiguration.AccessId))
            {
                Console.WriteLine("Missing configuration for: 1. ApiEndpoint and/or 2. AccessId");
                EventLog.WriteEntry(LogApplication, "Missing configuration for ApiEndpoint and/or AccessId", EventLogEntryType.Error);

                return;
            }

            var app = new Application(terminationEvent, args.Contains("/unattended"));
            app.Run();

            terminationEvent.WaitOne();
            Console.WriteLine("Received termination event. Quitting ...");
            EventLog.WriteEntry(LogApplication, "Service is stopping", EventLogEntryType.Information);
            app.Cleanup();
        }

        static void LoadRegistryConfigOverrrides()
        {

            RegistryKey registryNode;

            try
            {
                // Open a subKey as read-only
                registryNode = Registry.LocalMachine.OpenSubKey(BaseRegistryKey);
                // If the RegistrySubKey doesn't exist -> (null)
                if (registryNode == null)
                {
                    Console.WriteLine("No registry configuration overrides provided...");
                    EventLog.WriteEntry(LogApplication, "No registry information available in key [" + BaseRegistryKey + "]", EventLogEntryType.Information, 99);
                    return;
                }

                string ServiceURL = (string)registryNode.GetValue("ServiceURL");

                if (!String.IsNullOrEmpty(ServiceURL))
                {
                    Console.WriteLine("Loaded ApiConfiguration.ApiEndpoint: " + ServiceURL + " from registry");
                    ApiConfiguration.ApiEndpoint = ServiceURL;
                }

                string ServiceID = (string)registryNode.GetValue("ServiceID");

                if (!String.IsNullOrEmpty(ServiceID))
                {
                    Console.WriteLine("Loaded ApiConfiguration.AccessId: " + ServiceID + " from registry");
                    ApiConfiguration.AccessId = ServiceID;
                }

                var ttl = registryNode.GetValue("MessageTtlSeconds");

                if (ttl != null && (int)ttl > 0)
                {
                    Console.WriteLine("Loaded ApiConfiguration.MessageTtlSeconds: " + ttl.ToString() + " from registry");
                    ApiConfiguration.MessageTtlSeconds = (int)ttl;
                }

                var mt = (string)registryNode.GetValue("MessageActionTitle");
                if (!String.IsNullOrEmpty(mt))
                {
                    Console.WriteLine("Loaded ApiConfiguration.MessageActionTitle: " + mt + " from registry");
                    ApiConfiguration.MessageActionTitle = mt;
                }

                var md = (string)registryNode.GetValue("MessageDescription");
                if (!String.IsNullOrEmpty(md))
                {
                    Console.WriteLine("Loaded ApiConfiguration.MessageDescription: " + md + " from registry");
                    ApiConfiguration.MessageDescription = md;
                }

                ttl = (int)registryNode.GetValue("AuthCreateTimeoutSecs");

                if (ttl != null && (int)ttl > 0)
                {
                    Console.WriteLine("Loaded ApiConfiguration.AuthCreateTimeoutSecs: " + ttl.ToString() + " from registry");
                    ApiConfiguration.AuthCreateTimeoutSecs = (int)ttl;
                    if(ApiConfiguration.AuthCreateTimeoutSecs > 100)
                    {
                        EventLog.WriteEntry(LogApplication, "AuthCreateTimeoutSecs value of "+ ApiConfiguration.AuthCreateTimeoutSecs + " over enforced limit of 100 seconds", EventLogEntryType.Warning, 98);
                    }
                }

                ttl = (int)registryNode.GetValue("AuthWaitTimeoutSecs");

                if (ttl != null && (int)ttl > 0)
                {
                    Console.WriteLine("Loaded ApiConfiguration.AuthWaitTimeoutSecs: " + ttl.ToString() + " from registry");
                    ApiConfiguration.AuthWaitTimeoutSecs = (int)ttl;
                    if (ApiConfiguration.AuthWaitTimeoutSecs > 100)
                    {
                        EventLog.WriteEntry(LogApplication, "AuthWaitTimeoutSecs value of " + ApiConfiguration.AuthWaitTimeoutSecs + " over enforced limit of 100 seconds", EventLogEntryType.Warning, 97);
                    }
                }


                // TODO 
                // Enforce this setting, move config from IPC client to service. 
                ttl = registryNode.GetValue("HealthTimeoutSecs");

                if (ttl != null && (int)ttl > 0)
                {
                    Console.WriteLine("Loaded ApiConfiguration.HealthTimeoutSecs: " + ttl.ToString() + " from registry");
                    ApiConfiguration.HealthTimeoutSecs = (int)ttl;
                    if (ApiConfiguration.HealthTimeoutSecs > 100)
                    {
                        EventLog.WriteEntry(LogApplication, "HealthTimeoutSecs value of " + ApiConfiguration.HealthTimeoutSecs + " over enforced limit of 100 seconds", EventLogEntryType.Warning, 96);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error loading registry: " + e.Message );
                return;
            }
            registryNode.Close();
            registryNode.Dispose();
        }
    }
}
