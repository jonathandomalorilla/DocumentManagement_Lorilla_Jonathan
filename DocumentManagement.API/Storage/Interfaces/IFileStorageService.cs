namespace DocumentManagement.API.Storage.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveAsync(
        Stream fileStream,
        string fileExtension,
        CancellationToken cancellationToken = default);

    Stream? OpenRead(string storedFileName);

    bool Exists(string storedFileName);

    void Delete(string storedFileName);
}
