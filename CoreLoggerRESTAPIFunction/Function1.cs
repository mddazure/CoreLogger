using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Rest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Threading;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using Newtonsoft.Json;
using System.IO;


namespace CoreLoggerRESTAPIFunction
{
    public class CustomLoginCredentials : ServiceClientCredentials
    {
        public string InitializeServiceClient()
        {
            var clientId = "aebd413d-0723-4240-91a6-3000b891430d";
            var clientSecret = "Nienke040598";
            var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";

            var authenticationContext =
                new AuthenticationContext("https://login.windows.net/" + tenantId);
            var credential = new ClientCredential(clientId, clientSecret);

            var result = authenticationContext.AcquireTokenAsync(resource: "https://management.core.windows.net/",
                clientCredential: credential);

            return result.Result.AccessToken;
        }
    }

    public static class Function1
    {
        [FunctionName("CoreLoggerRESTAPIFunction")]
        public static void Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, [DocumentDB(
                databaseName: "CoreLogDB",
                collectionName: "0245be41-c89b-4b46-a3cc-a705c90cd1e8",
                ConnectionStringSetting = "CosmosDBConnection")] out dynamic document,
                TraceWriter log)
        {
            log.Info($"C# Timer trigger function executed at: {DateTime.Now}");

            var httpClient = new HttpClient();
            HttpResponseMessage response;
            var customLoginCredentials = new CustomLoginCredentials();
            var bearerToken = customLoginCredentials.InitializeServiceClient();

            string subscr = "0245be41-c89b-4b46-a3cc-a705c90cd1e8";

            var canceltoken = new CancellationToken();

            var httpRequestMessage = new HttpRequestMessage();

            httpRequestMessage.Method = new HttpMethod("GET");
            httpRequestMessage.RequestUri = new Uri($"https://management.azure.com/subscriptions/{subscr}/providers/Microsoft.Compute/locations/west-europe/usages?api-version=2015-06-15");
            httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            httpRequestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            response = httpClient.SendAsync(httpRequestMessage, canceltoken).Result;

            string jsonContent = response.Content.AsString();

            document = JsonConvert.DeserializeObject(jsonContent); 
            
            document.Id = DateTime.Now.ToLongTimeString() + " " + DateTime.Now.ToLongDateString();

        }

    }
}
