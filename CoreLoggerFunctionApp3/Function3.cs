using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Rest;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.Management.ResourceManager;
using Newtonsoft.Json;
using System.IO;
using Microsoft.Azure.KeyVault;

namespace CoreLoggerFunctionApp3
{
    public static class Function3
    {

         [FunctionName("Function3")]
        public static void Run([TimerTrigger("0 0/1 * * * *")]TimerInfo myTimer, TraceWriter log) //[TimerTrigger("0 0 12 * * 1-5")]
        {
            log.Info($"C# Timer trigger function executed at: {DateTime.Now}");

            var docDB = new DocDBClass();
            var httpClient = new HttpClient();

            HttpResponseMessage response;

            //use managed service identity to obtain token from AAD
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var accessToken = azureServiceTokenProvider.GetAccessTokenAsync("https://management.core.windows.net/").Result;
            TokenCredentials tokcred = new TokenCredentials(accessToken);

            string[] subscrArray = { "0245be41-c89b-4b46-a3cc-a705c90cd1e8" };
            var canceltoken = new CancellationToken();

            foreach (var subscr in subscrArray)
            {
                Usages usages = new Usages();

                //compose usage query to Microsoft.Compute resource provider
                var httpRequestMessage = new HttpRequestMessage();
                httpRequestMessage.Method = new HttpMethod("GET");
                httpRequestMessage.RequestUri = new Uri($"https://management.azure.com/subscriptions/{subscr}/providers/Microsoft.Compute/locations/west-europe/usages?api-version=2015-06-15");
                httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpRequestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                //send query, convert response content to string
                response = httpClient.SendAsync(httpRequestMessage, canceltoken).Result;
                string respstring = response.Content.AsString();

                //deserialize response json to object
                usages = JsonConvert.DeserializeObject<Usages>(respstring);

                //step through each value element in usages object
                foreach (value val in usages.vAlue)
                {
                    /*write to console, does not work in Function
                    Console.WriteLine($"limit, {val.limit}");
                    Console.WriteLine($"unit, {val.unit}");
                    Console.WriteLine($"currentValue, {val.currentValue}");
                    Console.WriteLine($"name.value, {val.nAme.value}");
                    Console.WriteLine($"name.localizedValue, {val.nAme.localizedValue}\n\n");*/

                    //actual resource usage > 80% of limit
                    if (val.currentValue == 4)//> 0.8 * val.limit)
                    {
                        // send alert email via Logic App through webhook trigger

                        //use managed service identity to obtain key vault access and retrieve Logi App URL
                        var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
                        string logAppUri = keyVaultClient.GetSecretAsync("https://Demo-KV001.vault.azure.net/secrets/CorelogLogicAppURL").Result.Value;

                        //get display name of subscription
                        SubscriptionClient subclient = new SubscriptionClient(tokcred);
                        var subscrDisplayName = subclient.Subscriptions.Get(subscriptionId: subscr).DisplayName;

                        //compose message body
                        string body = $"SubscriptionID: { subscr}\nSubscription Name:{subscrDisplayName}\nType: {val.nAme.localizedValue}\nLimit: {val.limit}\nCurrent value: {val.currentValue}\n";
                        var httpRequestMessage2 = new HttpRequestMessage();
                        httpRequestMessage2.Method = new HttpMethod("POST");
                        httpRequestMessage2.RequestUri = new Uri(logAppUri);
                        httpRequestMessage2.Content = new StringContent(body);

                        // send webhook
                        response = httpClient.SendAsync(httpRequestMessage2, canceltoken).Result;

                        //write usage data for type at risk data to CosmosDB
                        var usagedoc = JsonConvert.SerializeObject(val);
                        docDB.DocumentDB(subscrDisplayName, usagedoc);
                    }
                }
            }
        }
    }
    
    public class DocDBClass
    {
        private DocumentClient client;
        

        public void DocumentDB(string collectionName, string resp)
        {
            try
            {
                //use managed service identity to obtain key vault access
                AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();
                KeyVaultClient keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));

                //read CosmosDB creds from key vault
                string endpointUrlKV = keyVaultClient.GetSecretAsync("https://Demo-KV001.vault.azure.net/secrets/EndpointUrlKV").Result.Value;
                string primaryKeyKV = keyVaultClient.GetSecretAsync("https://Demo-KV001.vault.azure.net/secrets/PrimaryKeyKV").Result.Value;
                string databaseNameKV = keyVaultClient.GetSecretAsync("https://Demo-KV001.vault.azure.net/secrets/DbNameKV").Result.Value;


                client = new DocumentClient(new Uri(endpointUrlKV), primaryKeyKV);

                client.CreateDatabaseIfNotExistsAsync(new Database { Id = databaseNameKV }).GetAwaiter().GetResult();
                client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(databaseNameKV), new DocumentCollection { Id = collectionName }).GetAwaiter().GetResult();

                using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(resp)))
                {
                    var doc = JsonSerializable.LoadFrom<Document>(ms);
                    doc.Id = DateTime.Now.ToLongTimeString() + " " + DateTime.Now.ToLongDateString();

                    client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseNameKV, collectionName), doc).GetAwaiter().GetResult();

                }
            }
            catch (DocumentClientException de)
            {
                Console.WriteLine("DocumentClientException: {0}", de.Message);
                Console.ReadKey();
            }
        }
    }

    //template for usage object
    public class Usages
    {
        public List<value> vAlue { get; set; }
    }

    public class value
    {
        public int limit;
        public string unit;
        public int currentValue;
        public name nAme;
    }

    public class name
    {
        public string value;
        public string localizedValue;
    }
}





