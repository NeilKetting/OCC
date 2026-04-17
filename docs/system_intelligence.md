# OCC System Intelligence & Architecture Guide

This guide is intended for AI assistants and developers to quickly understand the OCC project structure and environment.

## 1. Environment Topology

### **Local Development (Neil's PC)**
- **Role**: Source of Truth for code.
- **Tools**: Visual Studio, Git.
- **Database**: Local SQL instances.
- **Access**: Full filesystem access.

### **Hosted Server (Production)**
- **IP**: `102.221.36.149` (Port `8081` for API)
- **Repo Location**: `C:\OCC-Source`
- **Publish Location**: `C:\inetpub\wwwroot\OCC_API`
- **Database**: SQL Server `OCOR\OCC_SQL`
- **Access Level**:
    - **Filesystem**: NOT directly accessible from the local PC via this agent.
    - **Logs**: Accessible via `GET http://102.221.36.149:8081/api/Logs`
    - **Deployment**: Managed by `update_main.bat` on the server.

---

## 2. API Architecture & Routing

### **Standard Patterns**
- **Base Route**: `api/[ControllerName]`
- **HTTP Methods**: ALWAYS use standard Restful methods.
    - `GET`: Retrieve data.
    - `POST`: Create new records (`api/AttendanceRecords`).
    - `PUT`: Update records (`api/AttendanceRecords/{id}`).
    - `DELETE`: Delete records (`api/AttendanceRecords/{id}`).
- **Auth**: JWT Bearer tokens. Header: `Authorization: Bearer <token>`.
    - **CRITICAL**: The JWT secret key MUST be encoded using `Encoding.UTF8`. Using `Encoding.ASCII` will result in persistent 401 Unauthorized errors even with valid payloads.

- **API Interaction Standards (Environment Switching)**:
    - **Rule**: NEVER mutate an active HttpClient's `BaseAddress` or `DefaultRequestHeaders`.
    - **Implementation**: Singleton services (e.g., `ApiAuthService`, `SignalRNotificationService`) must construct the full absolute URI for every request dynamically based on the current `ApiBaseUrl`.
    - **SignalR**: Connections should be re-initialized (Stopped, Disposed, Re-built) when switching environments to ensure the hub URL is updated.

---

## 3. Maintenance & Deployment

### **Deployment Script (`update_main.bat`)**
- **Admin Required**: MUST be run as Administrator to manage IIS services.
- **Git Sync**: Uses `git reset --hard origin/master` to ensure a clean state. Stashing is NO LONGER USED as secrets are in `.secrets.json`.
- **Dotnet**: Uses `dotnet publish` to update the IIS directory.
- **Batch Syntax**: Use `REM` for comments inside `if/else` blocks; `::` can cause crashes.
    - **CRITICAL**: The ampersand `&` is a command separator in Batch. In `echo` statements, it must be escaped (`^&`) or replaced with `and` to avoid executing the following text as a separate command.
    - **IIS AppCmd**: The `/warning:false` flag is not supported in the standard `appcmd` usage for stopping/starting sites/pools in this environment.

### **SDK & Versioning**
- Managed by `global.json`. 
- **Minimum SDK**: `9.0.100` (pinned to ensure server compatibility).

---

## 4. Feature Map
- **Employee Hub**: Attendance, Time Tracking, Leave, Overtime.
- **Projects Hub**: Tasks, Assignments, Gantt charts.
- **Bug Hub**: Performance-optimized bug reporting.
- **Auth Hub**: JWT-based login and environment switching.

## 5. Database & Migration Gotchas

### **RowVersion / Concurrency Tokens**
- **SQL Server limitation**: `rowversion` (timestamp) columns cannot be explicitly altered in many scenarios (e.g., nullability).
- **EF Core fix**: If changing nullability or resetting models, manually remove `AlterColumn` calls for `RowVersion` from migrations to prevent failure.

### **IncidentPhoto Schema**
- **Existing Columns**: `FileName`, `FilePath`, `FileSize`, and `UploadedBy`.
- **Renames**: `Base64Content` was renamed to `UploadedBy` in the `SoftDeleteBaseEntity` migration. Redundant renames in subsequent migrations will fail.
- **Snapshot Sync**: If the schema already contains columns but EF isn't aware, manually update `AppDbContextModelSnapshot.cs` to align before applying migrations.
