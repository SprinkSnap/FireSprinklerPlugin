# SprinkSnap WPF Preview

This standalone WPF app launches `HazardClassificationView` with sample room data and sample
water demand values. It is intended for UI testing without Autodesk Revit.

## Run

From a Windows machine with Visual Studio 2022 and the **.NET desktop development** workload installed:

1. Open `SprinkSnap.WpfPreview/SprinkSnap.WpfPreview.csproj`.
2. Set `SprinkSnap.WpfPreview` as the startup project.
3. Build the project.
4. Press `F5` to launch the dialog with sample data.

You can also run it from the repository root:

```powershell
dotnet restore .\SprinkSnap.WpfPreview\SprinkSnap.WpfPreview.csproj
dotnet build .\SprinkSnap.WpfPreview\SprinkSnap.WpfPreview.csproj -c Release
dotnet run --project .\SprinkSnap.WpfPreview\SprinkSnap.WpfPreview.csproj
```

## Target framework

The preview targets `net10.0-windows`.
It is only a UI test harness and does not control the production Revit add-in target.

If Visual Studio reports that the .NET SDK is missing, install the .NET 10 SDK or use the Visual
Studio Installer to update the **.NET desktop development** workload.

## Startup crash diagnostics

If the preview exits immediately, it writes the startup exception to:

```text
%LOCALAPPDATA%\SprinkSnap\WpfPreview\startup-error.log
```

The app also displays the exception in a message box when possible.

## Notes

- This preview references `SprinkSnap.Core` and `SprinkSnap.UI` as normal projects so
  Visual Studio WPF temporary builds can resolve all namespaces.
- It does not reference Autodesk Revit API assemblies.
- Save/Cancel closes the preview window only; no Revit parameters are written.
- The production Revit command remains `SprinkSnap.Revit/HazardClassificationCommand.cs`.

