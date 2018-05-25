using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CleanupTask.Properties;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CleanupTask
{
    /// <summary>
    /// Used for parse 
    /// </summary>
    internal static class CustomActivityHelper
    {
        internal static async Task<JObject> ParseActivityFromInputFileAsync(bool resolveAkvSecret = false)
        {
            if (!File.Exists("activity.json"))
            {
                throw new FileNotFoundException("activity.json");
            }

            Console.WriteLine("Start to parse Activity from input file.");
            // Read Activity Object from input files
            JObject activity = (JObject)JsonConvert.DeserializeObject(File.ReadAllText("activity.json"));

            if (resolveAkvSecret)
            {
                Console.WriteLine("Start to resolve akv secret in Activity.");
                await ResolveAkvSecretAsync(activity);
                Console.WriteLine("Resolve akv secret in Activity Complete.");
            }

            return activity;
        }

        internal static async Task<JArray> ParseLinkedServicesFromInputFileAsync(bool resolveAkvSecret = false)
        {
            if (!File.Exists("linkedServices.json"))
            {
                throw new FileNotFoundException("linkedServices.json");
            }

            Console.WriteLine("Start to parse LinkedServices from input file.");
            // Read Activity Object from input files
            JArray linkedServices = (JArray)JsonConvert.DeserializeObject(File.ReadAllText("linkedServices.json"));

            if (resolveAkvSecret)
            {
                Console.WriteLine("Start to resolve akv secret in LinkedServices.");
                
                foreach (var linkedService in linkedServices)
                {
                    await ResolveAkvSecretAsync(linkedService);
                }

                Console.WriteLine("Resolve akv secret in LinkedServices Complete.");
            }

            return linkedServices;
        }

        /***
         * Resolve any property in JObject with below AKV structure
         * 
         * {
         *       "type": "AzureKeyVaultSecret",
         *       "secretName": "<your secret name>",
         *       "store": {
         *           "referenceName": "<your akv linked service name>",
         *           "type": "LinkedServiceReference"
         *      }
         * }
         * 
         */
        internal static async Task<JToken> ResolveAkvSecretAsync(JToken obj)
        {
            if (!File.Exists("linkedServices.json"))
            {
                throw new FileNotFoundException("linkedServices.json");
            }

            // Select all linked service with type == AzureKeyVault
            IEnumerable<JToken> akvLinkedServices = 
                ((JArray)JsonConvert.DeserializeObject(File.ReadAllText("linkedServices.json"))).SelectTokens("$[?(@.properties.type == 'AzureKeyVault')]");

            if (akvLinkedServices == null || akvLinkedServices.Count() == 0)
            {
                return null;
            }

            // Collect all base urls from akv linked services
            Dictionary<string, string> akvUrls = new Dictionary<string, string>();
            foreach (JToken t in akvLinkedServices)
            {
                akvUrls[(string)t["name"]] = (string)t.SelectToken("$.properties.typeProperties.baseUrl");
            }

            // Get all akv secrets in JObject
            // JPath is not well supported in all Newton.Json version. 
            JToken[] akvSecretToken = obj.SelectTokens("$..*").Where(_ =>
            {
                if (!_.HasValues || _ is JArray)
                {
                    return false;
                }
                
                return "AzureKeyVaultSecret".Equals(_.Value<string>("type"));
            }).ToArray();
            Console.WriteLine($"{akvSecretToken.Count()} secrets found.");

            // Use Cert to access AKV, can be replace to use key or token.
            X509Certificate2 cert = FindCertificateByThumbprint(Settings.Default.Thumbprint);
            foreach (var token in akvSecretToken)
            {
                string secretName = (string)token["secretName"];
                string akvLsName = token.GetProperty<string>("$.store.referenceName");
                string baseUrl = akvUrls[akvLsName];

                string value = await RetrieveSecretAsync(baseUrl, 
                                                         Settings.Default.ClientId,
                                                         cert,
                                                         secretName);
                token.Replace(JToken.FromObject(value));
            }

            return obj;
        }

        internal static T GetProperty<T>(this JToken obj, string jPath, bool throwExceptionIfNotFound = true)
        {
            var token = obj.SelectToken(jPath);
            if (token == null && throwExceptionIfNotFound)
            {
                throw new FormatException($"{jPath} not found.");
            }

            return token != null ? token.Value<T>() : default(T);
        }

        // Use Cert to access AKV
        private static async Task<string> RetrieveSecretAsync(string akvBaseUrl, string clientId, X509Certificate2 cert, string secretName)
        {
            var assertionCert = new ClientAssertionCertificate(clientId, cert);
            var kvClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(async (authority, resource, scope) =>
            {
                var context = new AuthenticationContext(authority, TokenCache.DefaultShared);

                var result = await context.AcquireTokenAsync(resource, assertionCert);
                return result.AccessToken;
            }));

            SecretBundle secretBundle = await kvClient.GetSecretAsync(akvBaseUrl, secretName);
            return secretBundle != null ? secretBundle.Value : null;
        }


        private static X509Certificate2 FindCertificateByThumbprint(string thumbprint)
        {
            X509Store store = null;
            try
            {
                store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection certCollection = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);

                return certCollection == null || certCollection.Count == 0
                        ? null
                        : certCollection[0];
            }
            finally
            {
                if (store != null)
                {
                    store.Close();
                }
            }
        }
    }
}
