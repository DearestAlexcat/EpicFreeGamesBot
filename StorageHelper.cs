using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Google.Cloud.Storage.V1;
using Newtonsoft.Json;

namespace EpicFreeGamesBot
{
    /* 
     // Code for working with the database
     public class FirestoreSaver
     {
         private static FirestoreDb _db;
         static FirestoreSaver()
         {
             // Establishing a connection to Firestore
             _db = FirestoreDb.Create("your-project-id");  // Replace with your project ID
         }

         public static async Task SaveWatchedGamesAsync(string collectionName, string documentId, List<string> watchedGames)
         {
             var docRef = _db.Collection(collectionName).Document(documentId);
             await docRef.SetAsync(new { WatchedGames = watchedGames });
             Program.DebugLog("Data saved to Firestore", ConsoleColor.Green);
         }

         public static async Task<List<string>> GetWatchedGamesAsync(string collectionName, string documentId)
         {
             try
             {
                 var docRef = _db.Collection(collectionName).Document(documentId);
                 var snapshot = await docRef.GetSnapshotAsync();

                 if (snapshot.Exists)
                 {
                     var watchedGames = snapshot.GetValue<List<string>>("WatchedGames");
                     return watchedGames;
                 }
                 else
                 {
                     Program.DebugLog("Document not found", ConsoleColor.Red);
                     return new List<string>();
                 }
             }
             catch (Exception ex)
             {
                 Program.DebugLogException("Error loading data from Firestore: ", ex.Message);
                 return new List<string>();
             }
         }
     }
     */

    public static class GoogleCloudStorageHelper
    {
        private static readonly string bucketName = "epic-games-bot-data"; // Provide a name bucket
        private static StorageClient storageClient;

        static GoogleCloudStorageHelper()
        {
            // Path where the secret is mounted
            var credentialPath = "/secrets/gcp-key.json";

            if (File.Exists(credentialPath))
            {
                var credential = GoogleCredential.FromFile(credentialPath);
                storageClient = StorageClient.Create(credential);
            }
            else
            {
                throw new Exception("Google Cloud credential file not found!");
            }
        }

        public static async Task SaveWatchedGamesAsync(string fileName, List<string> watchedGames)
        {
            // Convert a list of games to a JSON string. We save only Title
            string jsonData = JsonConvert.SerializeObject(watchedGames);

            // Create a stream to write data to a file
            using (var memoryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonData)))
            {
                // Uploading a file to Cloud Storage
                await storageClient.UploadObjectAsync(bucketName, fileName, "application/json", memoryStream);
                Debug.Log("File uploaded to Google Cloud Storage.", ConsoleColor.Green);
            }
        }

        public static async Task<List<string>> LoadWatchedGamesAsync(string fileName)
        {
            try
            {
                // Getting an object from storage
                var obj = await storageClient.GetObjectAsync(bucketName, fileName);

                using (var memoryStream = new MemoryStream())
                {
                    // Loading an object into a stream
                    await storageClient.DownloadObjectAsync(obj, memoryStream);

                    // Convert the stream back to a JSON string
                    memoryStream.Position = 0;
                    using (var reader = new StreamReader(memoryStream))
                    {
                        string jsonData = await reader.ReadToEndAsync();
                        var watchedGames = JsonConvert.DeserializeObject<List<string>>(jsonData);

                        Debug.Log("File loaded from Google Cloud Storage.", ConsoleColor.Green);

                        if (watchedGames == null)
                            return null;

                        return watchedGames;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading file from Google Cloud Storage: ", ex.Message);
                return null;
            }
        }
    }
}
