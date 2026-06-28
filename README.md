# FireSprinklerPlugin

SprinkSnap Revit add-in and WPF preview for fire sprinkler design, hydraulics, and takeoff.

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) (10.0.301 or newer)
- Visual Studio 2022 17.14+ (optional, for WPF/Revit development on Windows)
- Revit 2027 API assemblies (Revit project only): set `REVIT_2027_API` to your Revit install folder, or install Revit 2027 to the default path `C:\Program Files\Autodesk\Revit 2027`

## Build

Open `FireSprinklerPlugin.sln` in Visual Studio, or from the repo root:

```powershell
dotnet restore FireSprinklerPlugin.sln
dotnet build FireSprinklerPlugin.sln -c Release
```

If you see **NETSDK1004** (`project.assets.json` not found), NuGet packages have not been restored yet. Run `dotnet restore` before building.

### Water supply / NFPA 13 build errors in Visual Studio

If you see water supply build errors, your `SprinkSnap.Core` and `SprinkSnap.UI` projects are out of sync. All water supply validation lives in the same solution — **Core must build before UI**.

From the repo root:

```powershell
git checkout main
git pull
dotnet clean FireSprinklerPlugin.sln
dotnet restore FireSprinklerPlugin.sln
dotnet build FireSprinklerPlugin.sln
```

In Visual Studio use **Build → Rebuild Solution** (not Build only UI). Confirm these files exist and are up to date:

- `SprinkSnap.Core/WaterSupply/WaterSupplyValidationHelper.cs`
- `SprinkSnap.Core/NFPA13/Nfpa13WaterSupplyValidator.cs` (compatibility shim)
- `SprinkSnap.Core/Models/WorkflowModels.cs`
- `SprinkSnap.Core/Engines/SprinkSnapEngines.cs`

`SprinkSnap.Revit` compiles only when Revit 2027 API DLLs are found. Other projects build without Revit installed.

### WPF preview only

```powershell
dotnet restore .\SprinkSnap.WpfPreview\SprinkSnap.WpfPreview.csproj
dotnet run --project .\SprinkSnap.WpfPreview\SprinkSnap.WpfPreview.csproj
```

### Tests

```powershell
dotnet test SprinkSnap.Core.Tests\SprinkSnap.Core.Tests.csproj -c Release
```
