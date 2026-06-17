# SprinkSnap WPF Preview

This standalone WPF app launches `HazardClassificationView` with sample room data and sample
water demand values. It is intended for UI testing without Autodesk Revit.

## Run

From a Windows machine with the .NET SDK installed:

```powershell
dotnet run --project .\SprinkSnap.WpfPreview\SprinkSnap.WpfPreview.csproj
```

## Notes

- This preview links the existing `SprinkSnap.Core` and `SprinkSnap.UI` files.
- It does not reference Autodesk Revit API assemblies.
- Save/Cancel closes the preview window only; no Revit parameters are written.
- The production Revit command remains `SprinkSnap.Revit/HazardClassificationCommand.cs`.

