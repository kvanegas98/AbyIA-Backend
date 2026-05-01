# Variedades Aby Backend - Agent Instructions

## Architecture & Data Access
- **.NET 8 Web API** following Clean Architecture boundaries (`VariedadesAby.Api`, `VariedadesAby.Core`, `VariedadesAby.Infrastructure`).
- **No Entity Framework**: Data access uses **Dapper** with `Microsoft.Data.SqlClient`.
- **Inline SQL**: Queries are written directly as inline strings inside Repository classes (`src/VariedadesAby.Infrastructure/Repositories/`). You will often see comments referencing old Stored Procedure names (e.g. `// sp_Finanzas_CarteraClientes`), but the logic is executed inline in C#.
- **Workers**: Background jobs (e.g., `FileTransferWorker`, `ReporteDiarioWorker`) run as `IHostedService`s registered in `Program.cs`.

## Build & Run Commands
- **Build API**: `dotnet build src/VariedadesAby.Api`
- **Run API**: `dotnet run --project src/VariedadesAby.Api`
- **PDF Playground**: `TestQuestPdf` is a separate console app used to test `QuestPDF` layouts. Run it via `dotnet run --project TestQuestPdf`.

## Testing
- **No Automated Tests**: There are currently no xUnit/NUnit unit test projects. Do not attempt to run `dotnet test` or try to search for test suites.
- Code verification should be done by inspecting execution flows, adding temporary console/logger outputs, or testing the endpoint manually if requested.

## Infrastructure Quirks
- **File Transfers (FTP/Drive)**: The app handles FTP to Google Drive transfers. This requires Google credentials located at `src/VariedadesAby.Api/credentials/service-account.json` and `oauth-client.json`. The `.csproj` ensures these copy to the build output.
- **Reporting**: Uses `QuestPDF` for PDF generation and `ClosedXML` for Excel.
- **Dependencies**: The `Core` project should remain pure (no package references), while `Infrastructure` holds the third-party integrations (Dapper, Google.GenAI, MailKit, Cloudinary).