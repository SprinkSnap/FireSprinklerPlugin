# FireSprinklerPlugin

SprinkSnap Revit add-in and WPF preview for fire sprinkler design, hydraulics, and takeoff.

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) (10.0.301 or newer)
- Visual Studio 2022 17.14+ (optional, for WPF/Revit development on Windows)
- Revit 2027 API assemblies (Revit project only): set `REVIT_2027_API` to your Revit install folder

## Build

Open `FireSprinklerPlugin.sln` in Visual Studio, or from the repo root:

```powershell
dotnet restore FireSprinklerPlugin.sln
dotnet build FireSprinklerPlugin.sln -c Release
```

If you see **NETSDK1004** (`project.assets.json` not found), NuGet packages have not been restored yet. Run `dotnet restore` before building.

### WPF preview only

```powershell
dotnet restore .\SprinkSnap.WpfPreview\SprinkSnap.WpfPreview.csproj
dotnet run --project .\SprinkSnap.WpfPreview\SprinkSnap.WpfPreview.csproj
```

### Tests

```powershell
dotnet test SprinkSnap.Core.Tests\SprinkSnap.Core.Tests.csproj -c Release
```
