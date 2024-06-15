using ContentManager_Server.DatabaseEntityCore;

namespace ContentManager_Server
{
    public interface IFileService
    {
        Task<string> AddFileAsync(string base64file);
        string GenerateId(string filePath);
        Task<bool> SaveToDatabaseAsync(string fileName, string? decription = null);
        Task<string?> GetFilePathAsync(string fileId, int extentionID);
        string? GetFilePath(FileData file, int extentionID);
        string? GetFileInStringFormat(FileData file);
        Task<string?> GetFileInStringFormatAsync(string fileId);
    }
}