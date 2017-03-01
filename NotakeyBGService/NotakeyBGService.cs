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
    }
}
