using System.IO.Compression;
using System.Text;
using DocumentManagement.API.Models;
using DocumentManagement.API.Repositories.Interfaces;
using DocumentManagement.API.Services.Interfaces;
using DocumentManagement.API.Storage.Interfaces;

namespace DocumentManagement.API.Services;

public class DocumentService : IDocumentService
{
    private const long MaxFileSize = 10 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedFileTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".txt"] = "text/plain"
        };

    private readonly IDocumentRepository _repository;
    private readonly IFileStorageService _fileStorage;

    public DocumentService(
        IDocumentRepository repository,
        IFileStorageService fileStorage)
    {
        _repository = repository;
        _fileStorage = fileStorage;
    }

    public async Task<Document> UploadAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        long fileSize,
        CancellationToken cancellationToken = default)
    {
        if (fileStream is null || !fileStream.CanRead)
        {
            throw new ArgumentException("Invalid file stream.");
        }

        if (string.IsNullOrWhiteSpace(originalFileName) ||
            originalFileName != Path.GetFileName(originalFileName) ||
            originalFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Invalid file name.");
        }

        if (fileSize <= 0)
        {
            throw new ArgumentException("The uploaded file is empty.");
        }

        if (fileSize > MaxFileSize)
        {
            throw new ArgumentException("File size must not exceed 10 MB.");
        }

        var extension = Path.GetExtension(originalFileName);

        if (!AllowedFileTypes.TryGetValue(extension, out var expectedContentType))
        {
            throw new ArgumentException("File type is not supported.");
        }

        var suppliedContentType = contentType?
            .Split(';', 2)[0]
            .Trim();

        if (!string.Equals(
            suppliedContentType,
            expectedContentType,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("File content type does not match the file extension.");
        }

        using var buffer = new MemoryStream();
        var readBuffer = new byte[81920];
        long actualFileSize = 0;

        while (true)
        {
            var bytesRead = await fileStream.ReadAsync(
                readBuffer.AsMemory(),
                cancellationToken);

            if (bytesRead == 0)
            {
                break;
            }

            actualFileSize += bytesRead;

            if (actualFileSize > MaxFileSize)
            {
                throw new ArgumentException("File size must not exceed 10 MB.");
            }

            await buffer.WriteAsync(
                readBuffer.AsMemory(0, bytesRead),
                cancellationToken);
        }

        if (actualFileSize == 0)
        {
            throw new ArgumentException("The uploaded file is empty.");
        }

        if (actualFileSize != fileSize)
        {
            throw new ArgumentException("File size does not match the uploaded content.");
        }

        var fileBytes = buffer.ToArray();

        if (!IsValidFileContent(extension, fileBytes))
        {
            throw new ArgumentException("File content does not match the selected file type.");
        }

        buffer.Position = 0;

        var storedFileName = await _fileStorage.SaveAsync(
            buffer,
            extension.ToLowerInvariant(),
            cancellationToken);

        try
        {
            var document = new Document
            {
                Id = Guid.NewGuid(),
                OriginalFileName = originalFileName,
                StoredFileName = storedFileName,
                ContentType = expectedContentType,
                FileSize = actualFileSize,
                UploadedAt = DateTimeOffset.UtcNow
            };

            await _repository.AddAsync(document, cancellationToken);

            return document;
        }
        catch
        {
            _fileStorage.Delete(storedFileName);
            throw;
        }
    }

    private static bool IsValidFileContent(string extension, byte[] bytes)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => bytes.Length >= 5 &&
                      Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-",

            ".png" => bytes.Length >= 8 &&
                      bytes.AsSpan(0, 8).SequenceEqual(
                          new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),

            ".jpg" or ".jpeg" => bytes.Length >= 3 &&
                                 bytes[0] == 0xFF &&
                                 bytes[1] == 0xD8 &&
                                 bytes[2] == 0xFF,

            ".docx" => IsValidDocx(bytes),

            ".txt" => IsValidText(bytes),

            _ => false
        };
    }

    private static bool IsValidDocx(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            return archive.GetEntry("[Content_Types].xml") is not null &&
                   archive.GetEntry("word/document.xml") is not null;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static bool IsValidText(byte[] bytes)
    {
        try
        {
            var text = new UTF8Encoding(false, true).GetString(bytes);

            return !text.Contains('\0');
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    public Task<List<Document>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public Task<Document?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<Stream?> GetFileAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document = await _repository.GetByIdAsync(id, cancellationToken);

        if (document is null)
        {
            return null;
        }

        return _fileStorage.OpenRead(document.StoredFileName);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document = await _repository.GetByIdAsync(id, cancellationToken);

        if (document is null)
        {
            return false;
        }

        var deleted = await _repository.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            return false;
        }

        try
        {
            _fileStorage.Delete(document.StoredFileName);
        }
        catch (Exception fileDeletionException)
        {
            try
            {
                await _repository.AddAsync(document, CancellationToken.None);
            }
            catch (Exception restorationException)
            {
                throw new AggregateException(
                    "File deletion failed and document metadata could not be restored.",
                    fileDeletionException,
                    restorationException);
            }

            throw new IOException(
                "The document file could not be deleted. Its metadata was restored.",
                fileDeletionException);
        }

        return true;
    }
}
