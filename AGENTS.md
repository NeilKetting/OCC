# Agent Instructions & Project Notes

## Core Development Guidelines
1. All code written needs to be production grade.
2. All code needs to be commented.
3. All code written needs to be unit tested.
4. OCC.API is also running live in the cloud so remember you do not have access to the DB. When you need to access the data, ask to copy over the DB to local PC.
5. Application Ecosystem:
   - `OCC.Client`: Old legacy app.
   - `OCC.WpfClient`: New desktop app.
   - `OCC.Mobile`: Tablet app used in the field by Site Managers.
   - `OCC.Portal`: Website for clients to log in and view project progress.
6. Make sure when troubleshooting you are sure because every time we update the API the live server has to go down.
7. Always keep code strictly organized into domain feature subfolders (e.g. HR, Projects, Operations, HSEQ, Portal, Infrastructure) across Services, Hubs, Views, and ViewModels. Avoid dumping files flatly in root feature folders.
8. All enums displayed in ComboBoxes, dropdowns, or DataGrids in WPF UI views MUST be run through `FriendlyEnumConverter` (or formatted with friendly display names and tooltips) so raw PascalCase enum names (e.g., `AdHocAdvance`) are never displayed directly to users.
9. Always clean up the solution/workspace of temporary test scripts, dump files, and transient test artifacts.
10. All solution builds MUST complete with zero warnings (0 warnings).
11. All list views in WPF Client MUST implement progressive staged loading (rendering top 100 records instantly to unblock user interaction, then hydrating full datasets seamlessly in the background).

---

## System Status & Task Tracker

### Completed Work [DONE]
- [DONE] **Wage System Overhaul**: Comprehensive rework of wage calculation engine, customizable BIBC rates/shift rules via `WageSettings`, dynamic pay frequencies (Weekly CPT, Fortnightly/Weekly JHB), ad-hoc "mamparra" advance run recoveries, and unpaid leave tagging non-duplication.
- [DONE] **Time & Attendance SignalR Delta Payload Real-Time Streaming**: Implemented real-time payload streaming (`EntityChangeDto<T>`) for Employees, Attendance Records, Wage Runs, and Wage Settings across backend API controllers and WPF ViewModels.
- [DONE] **SignalR Hub Feature Organization**: Modularized SignalR hub backend into feature-specific partial classes (`src/OCC.API/Hubs/TimeAttendanceHub.cs`).
- [DONE] **WPF Services Feature Organization**: Organized all 33 services in `src/OCC.WpfClient/Services/` into feature subfolders (`HR/`, `Projects/`, `Operations/`, `HSEQ/`, `Portal/`, `Infrastructure/`).
- [DONE] **User-Level Custom Project Name History & Deletable Auto-Suggestions**: Added per-user history tracking in `%APPDATA%\OCC.WpfClient\settings.json` for custom typed project names on Create Purchase Order screen, with real-time auto-suggestions and inline entry deletion.
- [DONE] **UI Redundant Refresh & Duplicate Save Buttons Cleaned Up**: Removed legacy refresh buttons across Dashboard, Wage Run, Attendance, Teams, Loans, and Leave views since SignalR streaming updates data automatically; removed duplicate top header Save button in WageSettingsView.
- [DONE] **Progressive Staged Loading for Attendance Views**: Implemented initial top 100 fast-render + background hydration for `AttendanceHistoryListView` and `AttendanceDashboardView`.

### Remaining Work [TODO]
- [TODO] **Progressive List View Loading Expansion**: Expand progressive staged loading (initial top 100 instant load + background hydration) across all remaining WPF list views.
- [TODO] **SignalR Delta Streaming Expansion**: Expand SignalR Delta Payload Real-Time Streaming to all remaining system modules (Projects, Procurement, HSEQ, Todo, etc.) in a future phase.
- [TODO] **Application Framework Evaluation**: Evaluate and incorporate a suitable application framework for the app in a future phase.