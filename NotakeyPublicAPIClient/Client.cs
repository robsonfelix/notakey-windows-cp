using Notakey.PublicAPI.RestModels;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Notakey.PublicAPI
{
    public class Client
    {
        private RestClient client;

        public static string EndpointURL
        {
            get
            {
                return Properties.Settings.Default.NotakeyPublicAPIEndpoint;
            }
        }

        public Client()
        {
            this.client = new RestClient(EndpointURL);
        }

        public AuthRequestStatus StatusForRequest(string uuid)
        {
            var request = new RestRequest(string.Format("auth_request/{0}", uuid));
            var response = client.Execute<AuthRequestResponse>(request);

            Console.WriteLine(response.Content);

            ThrowIfErrorResponse(response, response.Data.Error);

            return response.Data.Status;
        }

        private void ThrowIfErrorResponse(IRestResponse response, string error)
        {
            if (response.ErrorException != null)
            {
                Console.WriteLine("Error: {0}", response.ErrorMessage);
                throw response.ErrorException;
            }
            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
            {
                Console.WriteLine("Error. Status code: {0}", response.StatusCode);
                throw new InvalidOperationException(error);
            }
        }

        /// <summary>
        /// Returns UUID of request on success
        /// </summary>
        /// <param name="phoneNumber"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public string RequestAuth(string phoneNumber, string password)
        {
            var request = new RestRequest(string.Format("user/{0}/auth_request", phoneNumber), Method.POST);
            request.AddParameter("password", password);
            request.AddParameter("description", string.Format("Windows autentifikācija ({0})", System.Environment.MachineName));
            //request.AddParameter("remote_ip", this.GetPublicIpAddress());
            request.AddParameter("uuid", Guid.NewGuid().ToString());

            var response = client.Execute<AuthRequestResponse>(request);
            Console.WriteLine(response.Content);

            ThrowIfErrorResponse(response, response.Data.Error);
            

            return response.Data.Uuid;
        }

        public static bool IsAPIAvailable(out string msg)
        {
            try
            {
                var client = new RestClient(EndpointURL);
                var request = new RestRequest("status", Method.GET);
                
                var response = client.Execute<Status>(request);
                if (response.ErrorException != null)
                {
                    msg = response.ErrorException.Message;
                }
                else
                {
                    msg = response.Data.StatusText;
                }
                
                
                Debug.Assert(msg != null);
                return "OK".Equals(msg.ToUpperInvariant());
            }
            catch (Exception e)
            {
                msg = e.Message;
                return false;
            }
        }
    }
}
