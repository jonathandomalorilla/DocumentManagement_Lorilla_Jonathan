using DocumentManagement.API.Storage.Interfaces;

namespace DocumentManagement.API.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _storagePath;

    public LocalFileStorageService(IWebHostEnvironment environment)
    {
        _storagePath = Path.Combine(environment.ContentRootPath, "uploads");
    }

    public async Task<string> SaveAsync(
        Stream fileStream,
        string fileExtension,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileExtension) ||
            fileExtension.Length > 10 ||
            fileExtension[0] != '.' ||
            fileExtension[1..].Any(c => !char.IsAsciiLetterOrDigit(c)))
        {
            throw new ArgumentException("Invalid file extension.", nameof(fileExtension));
        }

        Directory.CreateDirectory(_storagePath);

        var storedFileName = $"{Guid.NewGuid():N}{fileExtension.ToLowerInvariant()}";
        var filePath = GetFilePath(storedFileName);

        try
        {
            await using var outputStream = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous);

            await fileStream.CopyToAsync(outputStream, cancellationToken);

            return storedFileName;
        }
        catch
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            throw;
        }
    }

    public Stream? OpenRead(string storedFileName)
    {
        var filePath = GetFilePath(storedFileName);

        if (!File.Exists(filePath))
        {
            return null;
        }

        return File.OpenRead(filePath);
    }

    public bool Exists(string storedFileName)
    {
        return File.Exists(GetFilePath(storedFileName));
    }

    public void Delete(string storedFileName)
    {
        var filePath = GetFilePath(storedFileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private string GetFilePath(string storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName) ||
            !Guid.TryParseExact(
                Path.GetFileNameWithoutExtension(storedFileName),
                "N",
                out _) ||
            string.IsNullOrEmpty(Path.GetExtension(storedFileName)) ||
            Path.GetExtension(storedFileName).Length > 10 ||
            Path.GetExtension(storedFileName)[1..]
                .Any(c => !char.IsAsciiLetterOrDigit(c)))
        {
            throw new ArgumentException("Invalid stored file name.", nameof(storedFileName));
        }

        return Path.Combine(_storagePath, storedFileName);
    }
}
