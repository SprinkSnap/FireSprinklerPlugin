using System;

namespace FireSprinklerPlugin.SprinkSnap.WpfPreview;

public static class Program
{
    [STAThread]
    public static int Main()
    {
        WpfPreviewApplication app = new WpfPreviewApplication();
        app.Run();
        return 0;
    }
}

