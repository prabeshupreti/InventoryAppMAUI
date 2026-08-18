namespace InventoryApp.Client.Services;

/// <summary>
/// Writes an exported report to the platform cache directory and opens the share sheet.
/// Uses only cross-platform MAUI Essentials APIs, so there is no per-platform branch here.
/// </summary>
public sealed class FileExportService(ILogger<FileExportService> logger)
{
    public async Task<bool> SaveAndShareAsync(string fileName, string content, string title)
    {
        try
        {
            var path = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(path, content);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = title,
                File = new ShareFile(path)
            });

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to export {FileName}", fileName);
            return false;
        }
    }
}
