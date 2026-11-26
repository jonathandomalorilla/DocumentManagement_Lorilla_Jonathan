using DocumentManagement.API.Models;

namespace DocumentManagement.API.Services.Interfaces;

public interface IDocumentService
{
    Task<Document> UploadAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        long fileSize,
        CancellationToken cancellationToken = default);

    Task<List<Document>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<Document?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Stream?> GetFileAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
