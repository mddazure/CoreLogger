using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Extensions.Bindings;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Newtonsoft.Json;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System.IO;
using System.Text;
using System.Net.Http;
using System.Threading;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;
using System.Collections.Generic;

namespace CoreLoggerFunctionApp2
{
    public class Function2    {
                       
        [FunctionName("Function2")]
        public static void Run([TimerTrigger("0 */1 * * * *")]TimerInfo myTimer, TraceWriter log)
        {
            log.Info($"C# Timer trigger function executed at: {DateTime.Now}");

            Function2 F = new Function2();

            F.MainAsync(log).GetAwaiter().GetResult();
        }

        public async Task MainAsync(TraceWriter ll)
        {

            var docDB = new DocDBClass();
            var httpClient = new HttpClient();
            HttpResponseMessage response;
            Usages usages = new Usages();
           
            



            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var accessToken = azureServiceTokenProvider.GetAccessTokenAsync("https://management.core.windows.net/").Result;
            
            string[] subscrArray = { "0245be41-c89b-4b46-a3cc-a705c90cd1e8" };

            var canceltoken = new CancellationToken();

            var httpRequestMessage = new HttpRequestMessage();

            foreach (var subscr in subscrArray)
            {
                httpRequestMessage.Method = new HttpMethod("GET");
                httpRequestMessage.RequestUri = new Uri($"https://management.azure.com/subscriptions/{subscr}/providers/Microsoft.Compute/locations/west-europe/usages?api-version=2015-06-15");
                httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpRequestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                response = httpClient.SendAsync(httpRequestMessage, canceltoken).Result;

                


                //Console.WriteLine("Received Log {0}", response.Content.AsString());
                //Console.ReadKey();
                await docDB.DocumentDB(subscr, await response.Content.ReadAsStringAsync(), ll);


            }
        }

    }
    public class DocDBClass

    {
        private string EndpointUrl = Environment.GetEnvironmentVariable("EndpointUrl");
        private string PrimaryKey = Environment.GetEnvironmentVariable("PrimaryKey");
        private string databaseName = Environment.GetEnvironmentVariable("databaseName");
        
        private string PrimaryKeyKV;
        private string databasenameKV;

        private DocumentClient client;

        public async Task DocumentDB(string collectionName, string resp, TraceWriter llog)
        {
            try
            {

                var azureServiceTokenProvider = new AzureServiceTokenProvider();
                var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
                var EndpointUrlKV = await keyVaultClient.GetSecretAsync("https://Demo-KV001.vault.azure.net/secrets/EndpointUrlKV")
            .ConfigureAwait(false);
                var PrimaryKeyKV = await keyVaultClient.GetSecretAsync("https://Demo-KV001.vault.azure.net/secrets/PrimaryKeyKV")
            .ConfigureAwait(false);
                var databaseNameKV = await keyVaultClient.GetSecretAsync("https://Demo-KV001.vault.azure.net/secrets/databasenameKV")
            .ConfigureAwait(false);


                client = new DocumentClient(new Uri(EndpointUrlKV.Value), PrimaryKeyKV.Value);

                await client.CreateDatabaseIfNotExistsAsync(new Database { Id = databaseNameKV.Value });
                try
                {
                    await client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(databaseNameKV.Value), new DocumentCollection { Id = collectionName }, new RequestOptions { OfferThroughput = 400 });
                }
                catch (Exception e)
                {
                    llog.Info("DocumentClientException: " + e.Message.ToString());
                }

                using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(resp)))
                {
                    var doc = JsonSerializable.LoadFrom<Document>(ms);
                    doc.Id = DateTime.Now.ToLongTimeString() + " " + DateTime.Now.ToLongDateString();

                    var docDBresp = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseNameKV.Value, collectionName), doc);

                    /*while (docDBresp.IsCompleted == false)
                    { }*/
                }
            }
            catch (Exception e)
            {
                llog.Info("General Exception: " + e.Message.ToString());

            }
        }
    }

    


    


}

