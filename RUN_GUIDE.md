# Graphene Trace – Run Guide (Separate from README)

This guide explains how to run the WPF app on any Windows PC without modifying the README.

## System Requirements
- Windows 10/11
- .NET 8 SDK (includes desktop runtime). Verify with: `dotnet --version`
- Optional: Visual Studio 2022 (17.8+) or VS Code + C# Dev Kit

## Folder Structure (keep as-is)
- `MyProject.sln`
- `myproject/` (WPF app)
- `myproject.Tests/` (xUnit tests)
- `Graphene_Trace_Logo_with_Hexagonal_Lattice-removebg-preview.png` (must be at repo root)
- Optional data: `GTLB-Data/` (CSV samples)

> Note: The logo file is linked in `myproject.csproj` using a relative path from the app project to the repo root. If this PNG is missing or moved, the app may not compile or the logo will not render.

## Quick Start (CLI)
1. Open PowerShell in the repo root.
2. Restore and build:
   - `dotnet restore`
   - `dotnet build MyProject.sln -c Debug`
3. Run the app (development):
   - `dotnet run --project myproject\myproject.csproj`

## Run the Built EXE (Debug/Release)
- After a build, the executable is here:
  - Debug: `myproject\bin\Debug\net8.0-windows\MyProject.exe`
  - Release: `myproject\bin\Release\net8.0-windows\MyProject.exe`

## Publish for Distribution
Create a folder with all dependencies to share:
- Runtime-dependent (smaller, requires .NET on target machine):
  - `dotnet publish myproject\myproject.csproj -c Release -r win-x64 --self-contained false`
  - Output: `myproject\bin\Release\net8.0-windows\win-x64\publish\`
- Self-contained (larger, no .NET required on target machine):
  - `dotnet publish myproject\myproject.csproj -c Release -r win-x64 --self-contained true`
  - Output: `myproject\bin\Release\net8.0-windows\win-x64\publish\`
- Share the `publish` folder (zip it) and run `MyProject.exe` inside it.

## Visual Studio
- Open `MyProject.sln`
- Set startup project: `myproject`
- Build → Run

## Tests
- Run all tests (8 total):
  - `dotnet test myproject.Tests\myproject.Tests.csproj -v minimal`

## First Run & App Data
- Local SQLite DB path: `%LOCALAPPDATA%\MyProject\users.db`
- Default admin (created on first run if none exists):
  - Username: `admin`
  - Temp password: `Admin@123!` (must change on first login)

## Using Sample CSVs (Optional)
- CSVs are provided in `GTLB-Data/`
- Use Dashboard → Import buttons to load data, view heatmaps & trends

## Troubleshooting
- Executable is locked during build:
  - Close the running app, or run: `Get-Process MyProject | Stop-Process -Force`
  - Then re-run `dotnet build`
- Missing logo:
  - Ensure `Graphene_Trace_Logo_with_Hexagonal_Lattice-removebg-preview.png` exists at the repo root (same folder as `MyProject.sln`)
- Wrong .NET version:
  - Install .NET 8 SDK from Microsoft, then verify `dotnet --version`
- Reset local DB (for a clean start):
  - Delete `%LOCALAPPDATA%\MyProject\users.db` and re-run the app

## Notes
- Do not rename the `myproject` directory or move files unless you update project references accordingly.
- The executable name remains `MyProject.exe` by design; branding is applied in window titles and headers.
- For offline sharing, prefer `dotnet publish` and ship the `publish` folder.