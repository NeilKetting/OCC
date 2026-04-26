# Project Handover: OCC Mobile Rebuild

## Current Status
We have successfully initialized the new `OCC.Mobile` project (Avalonia/Cross-platform) with a modern architecture and premium design. The foundation is solid, and the application now builds and runs on Android with basic navigation.

### Completed (Phase 1: Foundation & Build Fixes)
- [x] **MVVM Architecture**: Implemented `ViewModelBase`, `MainViewModel`, and a custom `ViewLocator`.
- [x] **Dependency Injection**: Setup `Microsoft.Extensions.DependencyInjection` in `App.axaml.cs`.
- [x] **Navigation Service**: Created `INavigationService` and its implementation for seamless view transitions.
- [x] **Android Compatibility**: Fixed build errors related to missing `.axaml.cs` files and project configuration.
- [x] **UI Foundation**: Implemented a modern Dark Theme with construction-themed accents and a global busy overlay.
- [x] **Login View**: Designed a high-end login screen with custom assets and transition logic.

## Remaining Tasks (Next Session)

### Phase 2: Environment & Connection Management
- [ ] **Local Settings**: Create `LocalSettingsService.cs` to persist User Email and Environment settings.
- [ ] **Environment Switcher**: Implement UI on the Login screen to switch between Development, Staging, and Production.
- [ ] **Connection Logic**: Link the switcher to the `ConnectionSettings` to update API base URLs dynamically.

### Phase 3: Premium UI & Global Styling
- [ ] **Global Styles**: Create `Styles/GlobalStyles.axaml` to centralize colors, font sizes, and common controls.
- [ ] **Refactoring**: Update `LoginView` and `DashboardView` to consume global styles.
- [ ] **Tablet Support**: Optimize layouts for larger screens (spacing, sizing).

### Phase 4: Authentication & User Roles
- [ ] **API Integration**: Connect `LoginViewModel` to the `OCC.API`.
- [ ] **Registration**: Implement `RegisterViewModel` and `RegisterView`.
- [ ] **Role-Based Navigation**: Logic to direct users to specific dashboards based on their role (Admin, Site Manager, etc.).

### Phase 5: Dynamic Dashboards
- [ ] **Admin Dashboard**: Metrics and high-level project overviews.
- [ ] **Field Dashboards**: Specialized views for Site Managers, Foremen, and Subcontractors.
- [ ] **Task Management**: Port hierarchical task logic from the WPF client.

## Technical Notes
- **Assets**: New logo and icons are located in `src/OCC.Mobile/Assets/`.
- **Busy State**: Use `IsBusy` and `BusyMessage` on `MainViewModel` to trigger the global loading overlay.
- **Navigation**: Inject `INavigationService` to navigate between ViewModels (e.g., `_navigationService.NavigateTo<DashboardViewModel>()`).

---
*Created by Antigravity on 2026-04-26*
