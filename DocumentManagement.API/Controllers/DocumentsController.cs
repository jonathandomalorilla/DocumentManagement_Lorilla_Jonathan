using DocumentManagement.API.DTOs.Responses;
using DocumentManagement.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private const long MaxFileSize = 10 * 1024 * 1024;

    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "A file is required." });
        }

        if (file.Length > MaxFileSize)
        {
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                new { message = "File size must not exceed 10 MB." });
        }

        try
        {
            await using var stream = file.OpenReadStream();

            var document = await _documentService.UploadAsync(
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                cancellationToken);

            var response = ToResponse(document);

            return CreatedAtAction(
                nameof(GetById),
                new { id = document.Id },
                response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var documents = await _documentService.GetAllAsync(cancellationToken);

        var response = documents.Select(ToResponse);

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var document = await _documentService.GetByIdAsync(
            id,
            cancellationToken);

        if (document is null)
        {
            return NotFound(new { message = "Document not found." });
        }

        return Ok(ToResponse(document));
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(
        Guid id,
        CancellationToken cancellationToken)
    {
        var document = await _documentService.GetByIdAsync(
            id,
            cancellationToken);

        if (document is null)
        {
            return NotFound(new { message = "Document not found." });
        }

        var stream = await _documentService.GetFileAsync(
            id,
            cancellationToken);

        if (stream is null)
        {
            return NotFound(new { message = "Document file not found." });
        }

        return File(stream, document.ContentType, document.OriginalFileName);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _documentService.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return NotFound(new { message = "Document not found." });
        }

        return NoContent();
    }

    private static DocumentResponse ToResponse(
        DocumentManagement.API.Models.Document document)
    {
        return new DocumentResponse
        {
            Id = document.Id,
            OriginalFileName = document.OriginalFileName,
            ContentType = document.ContentType,
            FileSize = document.FileSize,
            UploadedAt = document.UploadedAt
        };
    }
}
