using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DocumentManagement.Tests.IntegrationTests;

public class DocumentsApiTests
{
    [Fact]
    public async Task Upload_ValidFile_ReturnsCreated()
    {
        using var factory = new DocumentManagementWebApplicationFactory();
        using var client = factory.CreateClientWithDatabase();

        using var content = CreatePdfContent();

        var response = await client.PostAsync("/api/documents", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(responseBody);

        Assert.True(json.RootElement.GetProperty("id").GetGuid() != Guid.Empty);
        Assert.Equal(
            "sample.pdf",
            json.RootElement.GetProperty("originalFileName").GetString());

        Assert.Single(Directory.GetFiles(factory.StoragePath));
    }

    [Fact]
    public async Task GetById_ExistingDocument_ReturnsOk()
    {
        using var factory = new DocumentManagementWebApplicationFactory();
        using var client = factory.CreateClientWithDatabase();

        var documentId = await UploadDocumentAsync(client);

        var response = await client.GetAsync(
            $"/api/documents/{documentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(responseBody);

        Assert.Equal(
            documentId,
            json.RootElement.GetProperty("id").GetGuid());

        Assert.Equal(
            "sample.pdf",
            json.RootElement.GetProperty("originalFileName").GetString());
    }

    [Fact]
    public async Task GetById_NonexistentDocument_ReturnsNotFound()
    {
        using var factory = new DocumentManagementWebApplicationFactory();
        using var client = factory.CreateClientWithDatabase();

        var response = await client.GetAsync(
            $"/api/documents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_FileExceeds10MB_ReturnsPayloadTooLarge()
    {
        using var factory = new DocumentManagementWebApplicationFactory();
        using var client = factory.CreateClientWithDatabase();

        var fileBytes = new byte[10 * 1024 * 1024 + 1];

        using var content = new MultipartFormDataContent();

        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue("application/pdf");

        content.Add(fileContent, "file", "large.pdf");

        var response = await client.PostAsync(
            "/api/documents",
            content);

        Assert.Equal(
            HttpStatusCode.RequestEntityTooLarge,
            response.StatusCode);

        Assert.Empty(Directory.GetFiles(factory.StoragePath));
    }

    [Fact]
    public async Task Delete_ExistingDocument_ReturnsNoContentAndRemovesFile()
    {
        using var factory = new DocumentManagementWebApplicationFactory();
        using var client = factory.CreateClientWithDatabase();

        var documentId = await UploadDocumentAsync(client);

        Assert.Single(Directory.GetFiles(factory.StoragePath));

        var response = await client.DeleteAsync(
            $"/api/documents/{documentId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Empty(Directory.GetFiles(factory.StoragePath));

        var getResponse = await client.GetAsync(
            $"/api/documents/{documentId}");

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Download_ExistingDocument_ReturnsFileContent()
    {
        using var factory = new DocumentManagementWebApplicationFactory();
        using var client = factory.CreateClientWithDatabase();

        var documentId = await UploadDocumentAsync(client);

        var response = await client.GetAsync(
            $"/api/documents/{documentId}/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var downloadedBytes = await response.Content.ReadAsByteArrayAsync();
        var expectedBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n");

        Assert.Equal(expectedBytes, downloadedBytes);
    }

    [Fact]
    public async Task Download_MissingPhysicalFile_ReturnsNotFound()
    {
        using var factory = new DocumentManagementWebApplicationFactory();
        using var client = factory.CreateClientWithDatabase();

        var documentId = await UploadDocumentAsync(client);

        var storedFiles = Directory.GetFiles(factory.StoragePath);
        Assert.Single(storedFiles);

        File.Delete(storedFiles[0]);

        var response = await client.GetAsync(
            $"/api/documents/{documentId}/download");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_MissingPhysicalFile_StillDeletesMetadata()
    {
        using var factory = new DocumentManagementWebApplicationFactory();
        using var client = factory.CreateClientWithDatabase();

        var documentId = await UploadDocumentAsync(client);

        var storedFiles = Directory.GetFiles(factory.StoragePath);
        Assert.Single(storedFiles);

        File.Delete(storedFiles[0]);

        var response = await client.DeleteAsync(
            $"/api/documents/{documentId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await client.GetAsync(
            $"/api/documents/{documentId}");

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_NonexistentDocument_ReturnsNotFound()
    {
        using var factory = new DocumentManagementWebApplicationFactory();
        using var client = factory.CreateClientWithDatabase();

        var response = await client.DeleteAsync(
            $"/api/documents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    private static async Task<Guid> UploadDocumentAsync(HttpClient client)
    {
        using var content = CreatePdfContent();

        var response = await client.PostAsync("/api/documents", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(responseBody);

        return json.RootElement.GetProperty("id").GetGuid();
    }

    private static MultipartFormDataContent CreatePdfContent()
    {
        var content = new MultipartFormDataContent();

        var fileContent = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\n"));
        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue("application/pdf");

        content.Add(fileContent, "file", "sample.pdf");

        return content;
    }
}
