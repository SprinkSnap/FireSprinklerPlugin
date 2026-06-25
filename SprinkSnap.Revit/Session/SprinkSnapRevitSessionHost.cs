using FireSprinklerPlugin.SprinkSnap.UI.Shell;

namespace FireSprinklerPlugin.SprinkSnap.Revit.Session;

public static class SprinkSnapRevitSessionHost
{
    private static SprinkSnapShellView sharedShellView;

    public static SprinkSnapShellView SharedShellView => sharedShellView;

    public static void RegisterShellView(SprinkSnapShellView shellView)
    {
        sharedShellView = shellView;
    }
}
