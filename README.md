# Graphene Trace (WPF Pressure Sensor Dashboard)

Visualize pressure sensor CSV data as heatmaps and trends, generate alerts, and manage role-based workflows (Patient, Clinician, Admin). Built with .NET 8 and WPF.

## Overview
- CSV import and playback for pressure sessions
- Heatmap and trend visualizations
- Alerts and clinician comment replies
- Admin approvals, password resets, and audit logging
- Role-based UI and backend enforcement

## Requirements
- Windows 10/11
- .NET 8 SDK or Desktop Runtime
  - Verify: `dotnet --version` shows `8.x`

## Getting Started
1. Clone or extract this repository.
2. Optional: place sample CSV files in `GTLB-Data/` (a few are included).
3. Build:
   - `dotnet build MyProject.sln -c Release`
4. Run:
  - `myproject\bin\Release\net8.0-windows\MyProject.exe` (executable name remains `MyProject.exe`)

### Default Admin
- On first run, the app seeds an Admin if none exists:
  - Username: `admin`
  - Temporary password: `Admin@123!`
  - Must change password on first login
- The local database is created at `%LOCALAPPDATA%\MyProject\users.db`.
  - Note: Internal app data folder name stays `MyProject` for compatibility.

### Quick Role Workflow
- Admin:
  - Log in with `admin` / `Admin@123!`, change password.
  - Approve clinician registrations and manage assignments.
- Clinician:
  - Register, wait for admin approval.
  - Import CSV (`Dashboard` → Import buttons), view heatmaps/trends, reply to comments.
- Patient:
  - Log in to view session playback and add comments.

## Project Structure
```
GTLB-Data/                  # Sample CSV files
myproject/                  # WPF application
  Controllers/              # Alerts, Auth, Comments, CsvImport
  Data/                     # SQLite repository and schema
  Models/                   # User, Session, SensorData, Alert, Comment
  Views/                    # WPF windows and styles
  myproject.csproj
myproject.Tests/            # xUnit test project
  AlertsControllerTests.cs
  CsvImportControllerTests.cs
  DatabaseTests.cs
  UsersRepositoryTests.cs
  myproject.Tests.csproj
MyProject.sln               # Solution file
.gitignore                  # Repo-level ignore rules
```

## Team Logbook
- Weekly progress and reflections are documented in `Team_Logbook.md`.

## Build & Test
- Build solution:
  - `dotnet build MyProject.sln -v minimal`
- Run tests:
  - `dotnet test myproject.Tests\myproject.Tests.csproj -v minimal`
- Current status: 8 tests, all passing.

## Data & Screenshots
- CSVs: Large datasets are included under `GTLB-Data/`. If sharing publicly, consider keeping only a small sample.
- Screenshots: By default, `screenshots/` is ignored in the repo-level `.gitignore`. If you need to include screenshots, remove that ignore entry or provide them in a separate archive.

## Notes on Role Enforcement
- Backend:
  - Strict role constraint: `Admin | Clinician | Patient` (SQLite `CHECK`).
  - Admin approvals required for clinician logins.
  - Admin actions logged to `AdminActions`.
- UI:
  - CSV import and reports visible to clinicians only.
  - Admin panels for registrations and assignments.
  - Patients can view playback/heatmaps and add comments; no import access.

## Troubleshooting
- Ensure `.NET 8` installed; WPF requires the desktop runtime.
- Delete `%LOCALAPPDATA%\MyProject\users.db` if you want a clean auth database.
- If tests fail, run `dotnet restore` and try again.

## License
- Provide a license if sharing publicly (MIT/Apache-2.0 recommended). If omitted, default is “All rights reserved”.