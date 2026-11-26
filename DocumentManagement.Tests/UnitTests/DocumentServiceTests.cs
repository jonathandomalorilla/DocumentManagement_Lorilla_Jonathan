using System.Text;
using DocumentManagement.API.Models;
using DocumentManagement.API.Repositories.Interfaces;
using DocumentManagement.API.Services;
using DocumentManagement.API.Storage.Interfaces;

namespace DocumentManagement.Tests.UnitTests;

public class DocumentServiceTests
{
    private static byte[] ValidPdfBytes =>
        Encoding.ASCII.GetBytes("%PDF-1.4\n");

    [Fact]
    public async Task UploadAsync_ValidFile_SavesDocument()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeFileStorageService();
        var service = new DocumentService(repository, storage);

        using var stream = new MemoryStream(ValidPdfBytes);

        var result = await service.UploadAsync(
            stream,
            "sample.pdf",
            "application/pdf",
            ValidPdfBytes.Length);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("sample.pdf", result.OriginalFileName);
        Assert.Equal(ValidPdfBytes.Length, result.FileSize);
        Assert.Single(repository.Documents);
        Assert.True(storage.SaveCalled);
    }

    [Fact]
    public async Task UploadAsync_UnsupportedExtension_ThrowsException()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeFileStorageService();
        var service = new DocumentService(repository, storage);

        using var stream = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadAsync(stream, "sample.exe", "application/octet-stream", 3));

        Assert.Empty(repository.Documents);
        Assert.False(storage.SaveCalled);
    }

    [Fact]
    public async Task UploadAsync_EmptyFile_ThrowsException()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeFileStorageService();
        var service = new DocumentService(repository, storage);

        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadAsync(stream, "sample.pdf", "application/pdf", 0));

        Assert.Empty(repository.Documents);
        Assert.False(storage.SaveCalled);
    }

    [Fact]
    public async Task UploadAsync_FileExceeds10MB_ThrowsException()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeFileStorageService();
        var service = new DocumentService(repository, storage);

        using var stream = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadAsync(
                stream,
                "sample.pdf",
                "application/pdf",
                10 * 1024 * 1024 + 1));

        Assert.Empty(repository.Documents);
        Assert.False(storage.SaveCalled);
    }

    [Fact]
    public async Task UploadAsync_MismatchedContentType_ThrowsException()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeFileStorageService();
        var service = new DocumentService(repository, storage);

        using var stream = new MemoryStream(ValidPdfBytes);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadAsync(
                stream,
                "sample.pdf",
                "image/png",
                ValidPdfBytes.Length));

        Assert.Empty(repository.Documents);
        Assert.False(storage.SaveCalled);
    }

    [Fact]
    public async Task UploadAsync_InvalidFileSignature_ThrowsException()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeFileStorageService();
        var service = new DocumentService(repository, storage);

        using var stream = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadAsync(
                stream,
                "sample.pdf",
                "application/pdf",
                3));

        Assert.Empty(repository.Documents);
        Assert.False(storage.SaveCalled);
    }

    [Fact]
    public async Task UploadAsync_ActualContentExceeds10MB_ThrowsException()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeFileStorageService();
        var service = new DocumentService(repository, storage);

        using var stream = new MemoryStream(new byte[10 * 1024 * 1024 + 1]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadAsync(
                stream,
                "sample.pdf",
                "application/pdf",
                10 * 1024 * 1024));

        Assert.Empty(repository.Documents);
        Assert.False(storage.SaveCalled);
    }

    [Fact]
    public async Task UploadAsync_DatabaseFails_DeletesStoredFile()
    {
        var repository = new FakeDocumentRepository
        {
            ShouldFailOnAdd = true
        };

        var storage = new FakeFileStorageService();
        var service = new DocumentService(repository, storage);

        using var stream = new MemoryStream(ValidPdfBytes);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadAsync(
                stream,
                "sample.pdf",
                "application/pdf",
                ValidPdfBytes.Length));

        Assert.True(storage.SaveCalled);
        Assert.True(storage.DeleteCalled);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingDocument_ReturnsDocument()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeFileStorageService();
        var service = new DocumentService(repository, storage);

        var document = new Document
        {
            Id = Guid.NewGuid(),
            OriginalFileName = "sample.pdf",
            StoredFileName = $"{Guid.NewGuid():N}.pdf",
            ContentType = "application/pdf",
            FileSize = 3,
            UploadedAt = DateTimeOffset.UtcNow
        };

        repository.Documents.Add(document);

        var result = await service.GetByIdAsync(document.Id);

        Assert.NotNull(result);
        Assert.Equal(document.Id, result.Id);
        Assert.Equal("sample.pdf", result.OriginalFileName);
    }

    [Fact]
    public async Task GetByIdAsync_NonexistentDocument_ReturnsNull()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeFileStorageService();
        var service = new DocumentService(repository, storage);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_ExistingDocument_DeletesDocumentAndFile()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeFileStorageService();
        var service = new DocumentService(repository, storage);

        var document = new Document
        {
            Id = Guid.NewGuid(),
            OriginalFileName = "sample.pdf",
            StoredFileName = $"{Guid.NewGuid():N}.pdf",
            ContentType = "application/pdf",
            FileSize = 3,
            UploadedAt = DateTimeOffset.UtcNow
        };

        repository.Documents.Add(document);

        var result = await service.DeleteAsync(document.Id);

        Assert.True(result);
        Assert.Empty(repository.Documents);
        Assert.True(storage.DeleteCalled);
    }

        [Fact]
    public async Task DeleteAsync_FileDeletionFails_RestoresDocumentMetadata()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeFileStorageService
        {
            ShouldFailOnDelete = true
        };

        var service = new DocumentService(repository, storage);

        var document = new Document
        {
            Id = Guid.NewGuid(),
            OriginalFileName = "sample.pdf",
            StoredFileName = $"{Guid.NewGuid():N}.pdf",
            ContentType = "application/pdf",
            FileSize = 3,
            UploadedAt = DateTimeOffset.UtcNow
        };

        repository.Documents.Add(document);

        await Assert.ThrowsAsync<IOException>(() =>
            service.DeleteAsync(document.Id));

        Assert.Single(repository.Documents);
        Assert.Equal(document.Id, repository.Documents[0].Id);
        Assert.True(storage.DeleteCalled);
    }

    [Fact]
    public async Task DeleteAsync_DatabaseDeletionFails_DoesNotDeleteFile()
    {
        var repository = new FakeDocumentRepository
        {
            ShouldFailOnDelete = true
        };

        var storage = new FakeFileStorageService();
        var service = new DocumentService(repository, storage);

        var document = new Document
        {
            Id = Guid.NewGuid(),
            OriginalFileName = "sample.pdf",
            StoredFileName = $"{Guid.NewGuid():N}.pdf",
            ContentType = "application/pdf",
            FileSize = 3,
            UploadedAt = DateTimeOffset.UtcNow
        };

        repository.Documents.Add(document);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteAsync(document.Id));

        Assert.Single(repository.Documents);
        Assert.False(storage.DeleteCalled);
    }
[Fact]
    public async Task DeleteAsync_NonexistentDocument_ReturnsFalse()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeFileStorageService();
        var service = new DocumentService(repository, storage);

        var result = await service.DeleteAsync(Guid.NewGuid());

        Assert.False(result);
        Assert.Empty(repository.Documents);
        Assert.False(storage.DeleteCalled);
    }

    private sealed class FakeDocumentRepository : IDocumentRepository
    {
        public List<Document> Documents { get; } = [];

        public bool ShouldFailOnAdd { get; init; }
        public bool ShouldFailOnDelete { get; init; }

        public Task AddAsync(
            Document document,
            CancellationToken cancellationToken = default)
        {
            if (ShouldFailOnAdd)
            {
                throw new InvalidOperationException("Simulated database failure.");
            }

            Documents.Add(document);
            return Task.CompletedTask;
        }

        public Task<List<Document>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Documents.ToList());
        }

        public Task<Document?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Documents.FirstOrDefault(d => d.Id == id));
        }

        public Task<bool> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            if (ShouldFailOnDelete)
            {
                throw new InvalidOperationException(
                    "Simulated database deletion failure.");
            }

            var document = Documents.FirstOrDefault(d => d.Id == id);

            if (document is null)
            {
                return Task.FromResult(false);
            }

            Documents.Remove(document);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeFileStorageService : IFileStorageService
    {
        public bool SaveCalled { get; private set; }
        public bool DeleteCalled { get; private set; }
        public bool ShouldFailOnDelete { get; init; }

        public Task<string> SaveAsync(
            Stream fileStream,
            string fileExtension,
            CancellationToken cancellationToken = default)
        {
            SaveCalled = true;

            return Task.FromResult($"{Guid.NewGuid():N}{fileExtension}");
        }

        public Stream? OpenRead(string storedFileName)
        {
            return null;
        }

        public bool Exists(string storedFileName)
        {
            return false;
        }

        public void Delete(string storedFileName)
        {
            DeleteCalled = true;

            if (ShouldFailOnDelete)
            {
                throw new IOException("Simulated file deletion failure.");
            }
        }
    }
}
