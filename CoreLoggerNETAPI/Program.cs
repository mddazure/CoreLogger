using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Compute;
using Microsoft.Azure.Management.Compute.Models;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Services.AppAuthentication;
using Newtonsoft.Json;
using System.IO;
using Microsoft.Azure.KeyVault;
using System.Threading;
using Microsoft.Rest;

namespace CoreLoggerNETAPI
{
    class Program
    {
        static void Main(string[] args)
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var accessToken = azureServiceTokenProvider.GetAccessTokenAsync("https://management.core.windows.net/").Result;

            string[] subscrArray = { "0245be41-c89b-4b46-a3cc-a705c90cd1e8" };

            TokenCredentials tokcred = new TokenCredentials(accessToken);

            var canceltoken = new CancellationToken();

            SubscriptionClient subclient = new SubscriptionClient(tokcred);
            Console.WriteLine($"subscrId: {subclient.Subscriptions.Get(subscriptionId: subscrArray[0]).SubscriptionId}\ndisplayName {subclient.Subscriptions.Get(subscriptionId: subscrArray[0]).DisplayName}");
            Console.ReadLine();





        }
    }
}
