# SprinkSnap WPF Preview

This standalone WPF app launches the `SprinkSnapShellView` so you can preview the SprinkSnap AI
ribbon/workflow panels without Autodesk Revit.

## Run

From a Windows machine with Visual Studio 2022 and the **.NET desktop development** workload installed:

1. Open `SprinkSnap.WpfPreview/SprinkSnap.WpfPreview.csproj`.
2. Set `SprinkSnap.WpfPreview` as the startup project.
3. Build the project.
4. Press `F5` to launch the SprinkSnap AI shell preview.

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
- The preview shows the nine workflow/ribbon panels: Analyze Model, Hazard Review, Sprinkler Review,
  Water Supply, Generate Design, Hydraulics, Materials, Reports, and Settings.
- The preview loads sample project data and opens the Analyze Model workspace with real module fields.
- Click any module in the left navigation or module tiles to open that module's actual input screens in the center workspace.
- The production Revit add-in entry point is `SprinkSnap.Revit/SprinkSnapApplication.cs`.

