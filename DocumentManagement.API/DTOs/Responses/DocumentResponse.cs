namespace DocumentManagement.API.DTOs.Responses;

public class DocumentResponse
{
    public Guid Id { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DateTimeOffset UploadedAt { get; set; }
}
