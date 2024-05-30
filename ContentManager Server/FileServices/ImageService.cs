using ContentManager_Server.DatabaseEntityCore;
using static System.Net.Mime.MediaTypeNames;

namespace ContentManager_Server
{
    public class ImageService : BaseModule, IFileService
    {
        private readonly string imagesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
        private readonly DBController dbController;
        public FileData? LoadingImage { get; set; }

        public ImageService(DBController dbController) : base("ImageService")
        {
            this.dbController = dbController;

            if (!Directory.Exists(imagesDirectory))
            {
                Directory.CreateDirectory(imagesDirectory);
            }
            Logger.Instance.Log("ImageService started.", this);
        }

        public async Task AddFileAsync()
        {
            string filePath = FilePicker.ShowDialog();

            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try
                {
                    string fileExtension = Path.GetExtension(filePath).ToLower();
                    if (fileExtension == ".png" || fileExtension == ".gif")
                    {
                        string imageId = GenerateId(filePath);
                        string newFileName = imageId + fileExtension;
                        string destinationPath = Path.Combine(imagesDirectory, newFileName);

                        if (File.Exists(destinationPath))
                        {
                            Logger.Instance.Log($"Image already exists with ID: {imageId}.", this);
                            return;
                        }

                        File.Copy(filePath, destinationPath);

                        Logger.Instance.Log("Введите описание изображения (ВАЖНО!). Если не знаете, для чего это, просто нажмите клавишу Enter.", this);
                        string? imageDescription = Console.ReadLine();
                        bool saved = await SaveToDatabaseAsync(imageId, imageDescription);
                        if (saved)
                            Logger.Instance.Log($"Image {newFileName} added successfully.", this);
                        else
                        {
                            Logger.Instance.Log($"Error when saving image to database.", this);
                            File.Delete(destinationPath);
                        }
                    }
                    else
                    {
                        Logger.Instance.Log("Selected file is not a valid image.", this);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Error adding image: {ex.Message}", this);
                }
            }
            else
            {
                Logger.Instance.Log("File does not exist or file picker was cancelled.", this);
            }
        }

        public string GenerateId(string filePath)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hashBytes = sha256.ComputeHash(stream);
                    var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                    return hashString;
                }
            }
        }

        public async Task<bool> SaveToDatabaseAsync(string fileName, string? decription = null)
        {
            try
            {
                var fileData = new FileData
                {
                    TypeId = (int)TypeFile.IMAGE,
                    FileKey = fileName,
                    FileDescription = string.IsNullOrEmpty(decription) ? "Image File" : decription
                };

                bool saved = await dbController.AddEntityAsync(fileData);
                if (saved)
                {
                    Logger.Instance.Log("Image information saved to database.", this);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Error saving image info to database: {ex.Message}", this);
                return false;
            }
        }

        public string? GetFileInStringFormat(FileData? image)
        {
            if (image == null)
                return null;
            string? filePath = string.Empty;
            if (Server.ImageService != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    filePath = Server.ImageService.GetFilePath(image, i);
                    if (File.Exists(filePath))
                        break;
                }
            }

            if (!File.Exists(filePath))
            {
                Logger.Instance.Log($"File not found: {filePath}", this);
                return null;
            }

            try
            {
                byte[] imageBytes = File.ReadAllBytes(filePath);
                string base64String = Convert.ToBase64String(imageBytes);
                return base64String;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Error reading file {filePath}: {ex.Message}", this);
                return null;
            }
        }

        public async Task<string?> GetFileInStringFormatAsync(string imageId)
        {
            string? filePath = string.Empty;
            if (Server.ImageService != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    filePath = await Server.ImageService.GetFilePathAsync(imageId, i);
                    if (File.Exists(filePath))
                        break;
                }
            }

            if (!File.Exists(filePath))
            {
                Logger.Instance.Log($"File not found: {filePath}", this);
                return null;
            }

            try
            {
                byte[] imageBytes = File.ReadAllBytes(filePath);
                string base64String = Convert.ToBase64String(imageBytes);
                return base64String;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Error reading file {filePath}: {ex.Message}", this);
                return null;
            }
        }

        public async Task<string?> GetFilePathAsync(string imageId, int extensionId = 0)
        {
            try
            {
                var imageFile = await dbController.GetFileDataByKeyAsync(imageId);
                if (imageFile != null)
                {
                    return $"{Path.Combine(imagesDirectory, imageFile.FileKey)}.{imageFile.Type.Extension.Split(';')[extensionId]}";
                }
                else
                {
                    Logger.Instance.Log("Image not found in database.", this);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Error retrieving image path: {ex.Message}", this);
                return null;
            }
            return null;
        }

        public string? GetFilePath(FileData imageFile, int extensionId = 0)
        {
            try
            {
                if (imageFile != null)
                {
                    return $"{Path.Combine(imagesDirectory, imageFile.FileKey)}.{imageFile.Type.Extension.Split(';')[extensionId]}";
                }
                else
                {
                    Logger.Instance.Log("Image not found in database.", this);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Error retrieving image path: {ex.Message}", this);
                return null;
            }
            return null;

        }
    }
}