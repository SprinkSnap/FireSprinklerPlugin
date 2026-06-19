using System;

namespace FireSprinklerPlugin.SprinkSnap.WpfPreview;

public static class Program
{
    [STAThread]
    public static int Main()
    {
        App app = new App();
        app.Run();
        return 0;
    }
}

