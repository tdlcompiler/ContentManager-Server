using ContentManager_Server.DatabaseEntityCore;
using System.Security.Cryptography;
using static System.Net.Mime.MediaTypeNames;

namespace ContentManager_Server.FileServices
{
    public class PrefabService : BaseModule, IFileService
    {
        private readonly string prefabsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prefabs");
        private readonly DBController dbController;

        public PrefabService(DBController dbController) : base("PrefabService")
        {
            this.dbController = dbController;

            if (!Directory.Exists(prefabsDirectory))
            {
                Directory.CreateDirectory(prefabsDirectory);
            }
            Logger.Instance.Log("PrefabService started.", this);
        }

        public async Task<string> AddFileAsync(string base64prefab)
        {
            string prefabId = string.Empty;
            if (!string.IsNullOrEmpty(base64prefab))
            {
                try
                {
                    prefabId = GenerateId(base64prefab);
                    string newFileName = prefabId + ".prefab";
                    string destinationPath = Path.Combine(prefabsDirectory, newFileName);

                    if (File.Exists(destinationPath))
                    {
                        Logger.Instance.Log($"Image already exists with ID: {prefabId}.", this);
                        return prefabId;
                    }

                    byte[] fileBytes = Convert.FromBase64String(base64prefab);

                    File.WriteAllBytes(destinationPath, fileBytes);
                    bool saved = await SaveToDatabaseAsync(prefabId, null);
                    if (saved)
                        Logger.Instance.Log($"Prefab {newFileName} added successfully.", this);
                    else
                    {
                        Logger.Instance.Log($"Error when saving prefab to database.", this);
                        File.Delete(destinationPath);
                    }

                    return prefabId;
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Error adding prefab: {ex.Message}", this);
                }
            }

            return prefabId;
        }


        public string GenerateId(string base64prefab)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] fileBytes = Convert.FromBase64String(base64prefab);
                byte[] hashBytes = sha256.ComputeHash(fileBytes);
                string hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                return hashString;
            }
        }

        public string? GetFileInStringFormat(FileData file)
        {
            throw new NotImplementedException();
        }

        public Task<string?> GetFileInStringFormatAsync(string fileId)
        {
            throw new NotImplementedException();
        }

        public string? GetFilePath(FileData file, int extentionID)
        {
            throw new NotImplementedException();
        }

        public Task<string?> GetFilePathAsync(string fileId, int extentionID)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> SaveToDatabaseAsync(string fileName, string? decription = null)
        {
            try
            {
                var fileData = new FileData
                {
                    TypeId = (int)TypeFile.PREFAB,
                    FileKey = fileName,
                    FileDescription = string.IsNullOrEmpty(decription) ? "Prefab file" : decription
                };

                bool saved = await dbController.AddEntityAsync(fileData);
                if (saved)
                {
                    Logger.Instance.Log("Prefab information saved to database.", this);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Error saving prefab info to database: {ex.Message}", this);
                return false;
            }
        }
    }
}
