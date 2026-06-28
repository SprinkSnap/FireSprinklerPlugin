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

### Brand assets

The official SprinkSnap logo is `SprinkSnap.UI/sprinksnap-logo-transparent.png` (RGBA transparent PNG). UI surfaces load derived assets through `SprinkSnapBranding`:

- `sprinksnap-logo-header.png` — shell header (icon + wordmark)
- `sprinksnap-logo-compact.png` — AI assistant panel
- `sprinksnap-icon-mark.png` / `sprinksnap-revit-icon-16.png` / `sprinksnap-revit-icon-32.png` — Revit ribbon and window icons

Logo PNGs are **embedded inside `SprinkSnap.UI.dll`**. Rebuilding only `SprinkSnap.Revit` without rebuilding `SprinkSnap.UI`, or failing to deploy the updated DLL, will leave the old logo visible in Revit.

Regenerate derived PNGs from the master logo with:

```powershell
python scripts/export-sprinksnap-brand-assets.py
```

### Deploy to Revit after build

The add-in manifest (`SprinkSnap.Addin/SprinkSnapAI.addin`) loads from:

```text
%AppData%\Autodesk\Revit\Addins\2027\SprinkSnap.Revit.dll
```

Visual Studio build output alone does **not** update that folder unless you deploy.

**Recommended steps (close Revit first):**

```powershell
git pull origin main
dotnet clean FireSprinklerPlugin.sln
dotnet build FireSprinklerPlugin.sln -c Debug
.\scripts\deploy-sprinksnap-revit-addin.ps1 -Configuration Debug
```

Debug builds of `SprinkSnap.Revit` on Windows also auto-deploy to the Revit add-ins folder when Revit 2027 API is installed (`DeployRevitAddin=true` by default). Set `/p:DeployRevitAddin=false` to skip.

After deploying, **restart Revit completely** so ribbon icons and the dockable pane reload. Hover the header tagline — it should show tooltip `2025.06-header-lockup-v2` when the new branding is loaded.

Verify deployment: `%AppData%\Autodesk\Revit\Addins\2027\SprinkSnap.UI.dll` should be ~1.3 MB and recently timestamped after a logo update build.

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

- `SprinkSnap.Core/Models/WaterSupplyInputValidationResult.cs`
- `SprinkSnap.Core/WaterSupply/WaterSupplyValidationHelper.cs`
- `SprinkSnap.Core/NFPA13/Nfpa13WaterSupplyValidator.cs` (compatibility shim)
- `SprinkSnap.Core/Models/WorkflowModels.cs`
- `SprinkSnap.Core/Engines/SprinkSnapEngines.cs`

Delete this legacy file if it still exists locally (it causes duplicate type errors):

- `SprinkSnap.Core/WaterSupply/WaterSupplyInputValidator.cs`

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
