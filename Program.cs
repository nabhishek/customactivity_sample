using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CleanupTask
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Start to execute custom activity.");
            try
            {
                ExcuteCustomActivityAsync().Wait();
            }
            catch (Exception ex)
            {
                ex = ex.InnerException ?? ex;

                Console.Error.WriteLine($"Custom activity execution failed: {ex.Message}");
                Console.Error.WriteLine($"StackTrace {ex.StackTrace}");
                throw;
            }

            Console.WriteLine("Execute custom activity complete.");
        }

        static async Task ExcuteCustomActivityAsync()
        {
            JArray linkedServices = await CustomActivityHelper.ParseLinkedServicesFromInputFileAsync(true);
            JObject activity = await CustomActivityHelper.ParseActivityFromInputFileAsync(true);

            string connectionString = linkedServices.GetProperty<string>(@"$[?(@.name == 'AzureStorageLinkedService')].properties.typeProperties.connectionString");
            //folder path and blob container are retrieved from Extended Properties (in the Activity.json)
            string folderName = activity.GetProperty<string>(@"$..extendedProperties.folderPath");
            string containerName = activity.GetProperty<string>(@"$..extendedProperties.container");

            // Create blob client
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Delete all files in folder
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            CloudBlobDirectory folder = container.GetDirectoryReference(folderName);
            bool result = false;
            List<string> deleteFiles = new List<string>();
            foreach (IListBlobItem blob in folder.ListBlobs(true))
            {
                if (blob.GetType() == typeof(CloudBlob) || blob.GetType().BaseType == typeof(CloudBlob))
                {
                    string deletedBlobUri = blob.StorageUri.ToString();
                    result |= ((CloudBlob)blob).DeleteIfExists();
                    deleteFiles.Add(deletedBlobUri);
                }
            }

            Console.WriteLine($"{containerName}:{folderName} delete result: {result}");

            // Put result to output of custom activity
            if (deleteFiles != null)
            {
                File.WriteAllText("outputs.json", JsonConvert.SerializeObject(new { deletedFile = deleteFiles }));
            }
        }
    }
}
