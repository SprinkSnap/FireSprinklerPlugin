using System;
using Autodesk.Revit.UI;
using FireSprinklerPlugin.SprinkSnap.Revit.Session;
using FireSprinklerPlugin.SprinkSnap.UI.Shell;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public static class SprinkSnapDockablePaneRegistration
{
    public static readonly DockablePaneId PaneId = new DockablePaneId(
        new Guid("6F31BB56-A555-4D8E-9A32-16BE2A6C9487"));

    public static void Register(UIControlledApplication application)
    {
        application.RegisterDockablePane(PaneId, "SprinkSnap AI", new SprinkSnapPaneProvider());
    }
}

public sealed class SprinkSnapPaneProvider : IDockablePaneProvider
{
    public void SetupDockablePane(DockablePaneProviderData data)
    {
        SprinkSnapShellView shellView = new SprinkSnapShellView();
        SprinkSnapRevitSessionHost.RegisterShellView(shellView);
        data.FrameworkElement = shellView;
        data.InitialState = new DockablePaneState
        {
            DockPosition = DockPosition.Right
        };
    }
}
