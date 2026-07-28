# Agent Instructions & Project Notes

1. **Production Grade & Clean Code**: All code written must be production grade, fully commented, maintainable, adhering to standard C# / .NET design patterns and SOLID principles.
2. **100% Unit Test Coverage**: EVERYTHING must be unit tested. Every feature, service, controller, domain model, and view model must have comprehensive unit tests covering success paths, edge cases, error conditions, and input validation.
3. **Security Standards**: All code and API endpoints must adhere to modern security standards (OWASP Top 10):
   - Strict authentication and role-based authorization.
   - Comprehensive input validation and sanitization (SQL injection, XSS, payload tampering prevention).
   - Secure credential, token, and secret management (no hardcoded secrets).
   - Safe error handling and structured logging without sensitive data leakage.
   - Secure communication (TLS/HTTPS) and data privacy.
4. **Cloud Database Access**: OCC.API is running live in the cloud, so direct database access is unavailable in live environments. When data analysis or debugging is needed, request a copy of the database to local environment.
5. **System Architecture Overview**:
   - `OCC.Client`: Old legacy desktop app.
   - `OCC.WPF` (`OCC.WpfClient`): New desktop application for internal management.
   - `OCC.Mobile`: Tablet application used by site managers in the field to update project progress.
   - `OCC.Portal`: Client web portal where clients log in to view project progress.
   - `OCC.API`: Central ASP.NET Core backend API serving all clients.
   - `OCC.Shared`: Shared domain models, DTOs, interfaces, and utilities.
   - `OCC.Tests`: Centralized test suite for unit, integration, and feature tests.
6. **Feature-by-Feature Strategy**: Work through project features systematically, ensuring each feature meets industry standards, security best practices, and has complete unit test coverage before moving to the next.
7. **Live Server Downtime Safety**: Be 100% certain when updating API logic or troubleshooting, as live server updates require brief API downtime.
8. **Code Refactoring**: Proactively refactor code wherever needed to eliminate technical debt, improve performance, enforce SOLID principles, remove redundancy, and maintain high codebase quality.
9. **OCC.WPF Pattern & Naming Conventions**:
   - Feature folders in `src/OCC.WpfClient/Features/` must end with `Hub` (e.g., `AdminHub`, `EmployeeHub`, `ProjectHub`).
   - List and Detail views must be isolated into separate files (e.g., `*ListView.xaml` and `*DetailView.xaml`).
   - Feature dialogs must be placed in a `Dialogs/` subfolder within the feature folder (`Features/[FeatureHub]/Dialogs/`).
   - Auxiliary/shared view components must be organized in dedicated subfolders (`Shared/`, `Controls/`).
10. **Multi-Agent Task Execution**: Specialized subagents (for research, WPF refactoring, backend security hardening, unit testing) will be deployed for each feature to ensure comprehensive quality and 100% test coverage.
11. **Database Schema & Entity Standards**:
   - EF Core DbContext (`AppDbContext`) and domain models must follow industry standard database practices.
   - Enforce proper index configurations on foreign keys, lookups, timestamps, and search columns.
   - Enforce explicit decimal/currency precision (e.g. `decimal(18,2)`), string length constraints, and required fields.
   - Standardize soft deletes (`IsDeleted`), audit properties (`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`), and concurrency tokens (`RowVersion`).
12. **OCC.Tests Project Structure & Folder Conventions**:
   - Unit test files must be cleanly organized into dedicated subfolders in `src/OCC.Tests/` mirroring the feature/component structure:
     - Backend API Controllers: `src/OCC.Tests/API/Controllers/[ControllerName]Tests.cs`
     - Services & Domain Logic: `src/OCC.Tests/Services/[ServiceName]Tests.cs`
     - WPF ViewModels: `src/OCC.Tests/Features/[FeatureHub]/[ViewModelName]Tests.cs`
   - Test class names must explicitly match the target class with `Tests` appended (e.g. `EmployeesControllerTests`, `WageCalculationServiceTests`, `EmployeeDetailViewModelTests`).