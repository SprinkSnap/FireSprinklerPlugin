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
dotnet run --project .\SprinkSnap.WpfPreview\SprinkSnap.WpfPreview.csproj
```

## Target framework

The preview targets `net8.0-windows` so it works with the current Visual Studio LTS tooling.
It is only a UI test harness and does not control the production Revit add-in target.

If Visual Studio reports that the .NET SDK is missing, install the .NET 8 SDK or use the Visual
Studio Installer to add the **.NET desktop development** workload.

## Notes

- This preview links the existing `SprinkSnap.Core` and `SprinkSnap.UI` files.
- It does not reference Autodesk Revit API assemblies.
- Save/Cancel closes the preview window only; no Revit parameters are written.
- The production Revit command remains `SprinkSnap.Revit/HazardClassificationCommand.cs`.

