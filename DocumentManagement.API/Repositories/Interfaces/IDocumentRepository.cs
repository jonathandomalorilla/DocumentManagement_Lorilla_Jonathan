using DocumentManagement.API.Models;

namespace DocumentManagement.API.Repositories.Interfaces;

public interface IDocumentRepository
{
    Task AddAsync(
        Document document,
        CancellationToken cancellationToken = default);

    Task<List<Document>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<Document?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
