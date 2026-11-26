# Document Management API

A RESTful API built with ASP.NET Core 10 for managing document uploads, downloads, and deletion.

Document information is stored in SQL Server, while the actual files are stored separately in the local file system.

## Features

- Upload documents with file validation.
- View all uploaded documents.
- Get document details using its ID.
- Download uploaded files.
- Delete documents and their associated files.
- Limit uploads to 10 MB.
- Generate unique filenames for file storage.
- Automated unit and integration tests.

## Tech Stack

- C# / ASP.NET Core 10
- Entity Framework Core 10
- SQL Server
- SQLite for integration tests
- xUnit
- WebApplicationFactory

## Project Structure

The project uses a simple layered architecture:

- **Controllers** – Handles API requests and responses.
- **Services** – Contains document validation and business logic.
- **Repositories** – Handles database operations.
- **Storage** – Handles physical file storage.
- **Data** – Contains the Entity Framework database context.
- **Models and DTOs** – Defines document data and API responses.

This separation keeps the code organized and makes individual components easier to test and maintain.

## Requirements

Before running the application, make sure you have:

- .NET 10 SDK
- SQL Server 2022 or a compatible SQL Server instance
- EF Core CLI tool

Install the EF Core CLI tool if needed:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.0
```

## Configuration

Update the connection string in `DocumentManagement.API/appsettings.json` to match your SQL Server setup.

Example:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=DocumentManagementDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

For production, use environment variables or a secure secrets manager instead of committing credentials.

## Database Setup

Run the following command from the solution root to apply the database migrations:

```powershell
dotnet ef database update --project DocumentManagement.API --startup-project DocumentManagement.API
```

EF Core Code First migrations are used to create and update the database schema.

## Run the Application

From the solution root:

```powershell
dotnet restore
dotnet build
dotnet run --project DocumentManagement.API
```

The application will display its local URL in the terminal.

API documentation is available when running in the Development environment.

- **Swagger UI:** `http://localhost:5173/swagger`
- **OpenAPI JSON:** `http://localhost:5173/openapi/v1.json`

Use Swagger UI to explore and test the available endpoints. Click **Try it out** to execute API requests, including document upload, retrieval, download, and deletion.

> Note: Swagger UI and the OpenAPI JSON endpoint are enabled only in the Development environment. If the application uses a different port, use the URL displayed in the terminal.

## Run the Tests

Execute all unit and integration tests:

```powershell
dotnet test
```

The tests use:

- **Unit tests** to check document service logic and validation.
- **Integration tests** to verify API behavior using WebApplicationFactory, SQLite in-memory, and temporary file storage.

The integration tests do not require a separate SQL Server database.

The current test suite contains 23 tests:
- 14 unit tests
- 9 integration tests

## API Endpoints

Base route: `/api/documents`

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/documents` | Upload a document |
| GET | `/api/documents` | Get all document metadata |
| GET | `/api/documents/{id}` | Get document details |
| GET | `/api/documents/{id}/download` | Download a document |
| DELETE | `/api/documents/{id}` | Delete a document |

### Upload Example

Send a `multipart/form-data` request with a field named `file`.

```bash
curl -X POST "https://localhost:<port>/api/documents" \
  -F "file=@sample.pdf"
```

A successful upload returns `201 Created` with the document metadata and generated ID.

## File Validation and Storage

The API accepts the following file types:

- PDF
- DOCX
- JPG / JPEG
- PNG
- TXT

Files must not exceed 10 MB.

The API checks the file extension, content type, and file contents before accepting an upload.

Files are saved in the local `uploads` directory using generated GUID-based filenames. The original filename is kept in the database as metadata.

## Error Handling

The API uses appropriate HTTP status codes:

| Status | Description |
|---|---|
| 200 | Request successful |
| 201 | Document uploaded |
| 204 | Document deleted |
| 400 | Invalid upload request |
| 404 | Document or physical file not found |
| 413 | File exceeds the upload limit |

## Assumptions and Limitations

- Authentication and authorization are not included because they are outside the assessment scope.
- Files are stored locally rather than in cloud storage.
- File validation checks basic file formats but does not include antivirus scanning.
- Database records and physical files are stored separately, so they are not covered by a single transaction. If file deletion fails after the database record is deleted, the application attempts to restore the metadata. If restoration also fails, manual recovery may be required.
- Pagination and advanced search are not included.

## AI Disclosure

**AI Tool Used:** ChatGPT (OpenAI).

**How It Was Used:**
ChatGPT was used as a development assistant to discuss implementation approaches, explain technical concepts, troubleshoot issues, suggest validation and testing scenarios, and improve project documentation.

**Code Review and Modifications:**
I reviewed the suggested implementations against the assessment requirements, inspected the API controllers, document service, repository, file storage implementation, and automated tests. I also reviewed the suggested changes, executed the application build and automated tests, and checked the resulting test output before finalizing the project.

**Areas Requiring Further Improvement:**
The area I am least confident about is failure recovery involving both database operations and filesystem operations. The implementation attempts to restore document metadata if physical file deletion fails, but the database and filesystem cannot participate in one atomic transaction. If both deletion and metadata restoration fail, manual recovery may be required. File validation also does not include antivirus scanning.
## License

Created for technical assessment purposes.
