using DocumentManagement.API.Data;
using DocumentManagement.API.Storage.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DocumentManagement.Tests.IntegrationTests;

public class DocumentManagementWebApplicationFactory
    : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;
    private readonly string _storagePath;

    public string StoragePath => _storagePath;

    public DocumentManagementWebApplicationFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _storagePath = Path.Combine(
            Path.GetTempPath(),
            "DocumentManagementTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_storagePath);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(_connection));

            services.RemoveAll<IFileStorageService>();

            services.AddSingleton<IFileStorageService>(
                new TestFileStorageService(_storagePath));
        });
    }

    public HttpClient CreateClientWithDatabase()
    {
        var client = CreateClient();

        using var scope = Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        dbContext.Database.EnsureCreated();

        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();

            if (Directory.Exists(_storagePath))
            {
                Directory.Delete(_storagePath, recursive: true);
            }
        }
    }
}

internal sealed class TestFileStorageService : IFileStorageService
{
    private readonly string _storagePath;

    public TestFileStorageService(string storagePath)
    {
        _storagePath = storagePath;
        Directory.CreateDirectory(_storagePath);
    }

    public async Task<string> SaveAsync(
        Stream fileStream,
        string fileExtension,
        CancellationToken cancellationToken = default)
    {
        var storedFileName = $"{Guid.NewGuid():N}{fileExtension}";
        var filePath = Path.Combine(_storagePath, storedFileName);

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

    public Stream? OpenRead(string storedFileName)
    {
        var filePath = Path.Combine(_storagePath, storedFileName);

        return File.Exists(filePath)
            ? File.OpenRead(filePath)
            : null;
    }

    public bool Exists(string storedFileName)
    {
        return File.Exists(Path.Combine(_storagePath, storedFileName));
    }

    public void Delete(string storedFileName)
    {
        var filePath = Path.Combine(_storagePath, storedFileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
