using Microsoft.Rest;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Services.AppAuthentication;
using Newtonsoft.Json;
using System.IO;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Management.Compute.Models;

namespace CoreLoggerRESTAPI
{
    class Program
    {
        static void Main()
        {
            Program p = new Program();
            p.MainAsync().GetAwaiter().GetResult();
        }
        private async Task MainAsync()
        {
            var docDB = new DocDBClass();
            var httpClient = new HttpClient();
            
            HttpResponseMessage response;

            //use managed service identity to obtain token from AAD
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var accessToken = azureServiceTokenProvider.GetAccessTokenAsync("https://management.core.windows.net/").Result;

            string[] subscrArray = { "0245be41-c89b-4b46-a3cc-a705c90cd1e8" };

            var canceltoken = new CancellationToken();

            

            foreach (var subscr in subscrArray)
            {
                Usages usages = new Usages();

                //compose query to Compute resource provider for usage  
                var httpRequestMessage = new HttpRequestMessage();
                httpRequestMessage.Method = new HttpMethod("GET");
                httpRequestMessage.RequestUri = new Uri($"https://management.azure.com/subscriptions/{subscr}/providers/Microsoft.Compute/locations/west-europe/usages?api-version=2015-06-15");
                httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpRequestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                //send query, convert response content to string
                response = httpClient.SendAsync(httpRequestMessage, canceltoken).Result;
                string respstring = response.Content.AsString();

                Console.WriteLine("Received Log {0}", respstring);

                //deserialize response json to object
                usages = JsonConvert.DeserializeObject<Usages>(respstring);

                //step through each value element in usages object
                foreach (value val in usages.vAlue)
                {
                    Console.WriteLine($"limit, {val.limit}");
                    Console.WriteLine($"unit, {val.unit}");
                    Console.WriteLine($"currentValue, {val.currentValue}");
                    Console.WriteLine($"name.value, {val.nAme.value}");
                    Console.WriteLine($"name.localizedValue, {val.nAme.localizedValue}\n\n");

                    //actual resource usage > 80% of limit
                    if (val.currentValue > 0.8 * val.limit)
                    {
                        // send alert email via Logic App
                        
                        //use managed service identity to obtain key vault access and retrieve Logi App URL
                        var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
                        var logAppURL= await keyVaultClient.GetSecretAsync("https://Demo-KV001.vault.azure.net/secrets/CorelogLogicAppURL")
            .ConfigureAwait(false);

                        //compose message body
                        string body =$"SubscriptionID: { subscr}\nType: {val.nAme.localizedValue}\n,Limit: {val.limit}\n,Current value: {val.currentValue}\n";
                        var httpRequestMessage2 = new HttpRequestMessage();
                        httpRequestMessage2.Method = new HttpMethod("POST");
                        httpRequestMessage2.RequestUri = new Uri(logAppURL.Value);
                        httpRequestMessage2.Content = new StringContent(body);
                        response = httpClient.SendAsync(httpRequestMessage2, canceltoken).Result;
                        Console.WriteLine($"response from LogicApp: {response}\n");
                    }
                }
                Console.ReadKey();
                //write usage to CosmosDB
                await docDB.DocumentDB(subscr, respstring);
            }
        }
    }

    public class DocDBClass
    {    
        private DocumentClient client;   
        public async Task DocumentDB(string collectionName, string resp)
        {
            try
            {
                //use managed service identity to obtain key vault access
                var azureServiceTokenProvider = new AzureServiceTokenProvider();
                var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));

                //read CosmosDB creds from key vault
                var EndpointUrlKV = await keyVaultClient.GetSecretAsync("https://Demo-KV001.vault.azure.net/secrets/EndpointUrlKV")
            .ConfigureAwait(false);
                var PrimaryKeyKV = await keyVaultClient.GetSecretAsync("https://Demo-KV001.vault.azure.net/secrets/PrimaryKeyKV")
            .ConfigureAwait(false);
                var databaseNameKV = await keyVaultClient.GetSecretAsync("https://Demo-KV001.vault.azure.net/secrets/databasenameKV")
            .ConfigureAwait(false);


                client = new DocumentClient(new Uri(EndpointUrlKV.Value), PrimaryKeyKV.Value);         

                await client.CreateDatabaseIfNotExistsAsync(new Database { Id = databaseNameKV.Value });
                await client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(databaseNameKV.Value), new DocumentCollection { Id = collectionName });

                using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(resp)))
                {
                    var doc = JsonSerializable.LoadFrom<Document>(ms);
                    doc.Id = DateTime.Now.ToLongTimeString() + " " + DateTime.Now.ToLongDateString();

                    var docDBresp = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseNameKV.Value, collectionName), doc);

                    /*while (docDBresp.IsCompleted == false)
                    { }*/
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
