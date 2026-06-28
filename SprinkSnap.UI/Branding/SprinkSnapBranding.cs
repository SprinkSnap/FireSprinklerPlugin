using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FireSprinklerPlugin.SprinkSnap.UI.Branding;

public static class SprinkSnapBranding
{
    private const string UiAssemblyName = "SprinkSnap.UI";

    public const string ProductName = "SprinkSnap AI";

    public const string Tagline = "NFPA 13 design workflow for Revit";

    public static ImageSource LogoImage { get; } = LoadImage("Assets/sprinksnap-ai-logo.png", 480);

    public static ImageSource IconImage { get; } = LoadImage("Assets/sprinksnap-ai-icon.png", 64);

    public static ImageSource LoadLogo(double decodePixelWidth = 0)
    {
        return LoadImage("Assets/sprinksnap-ai-logo.png", decodePixelWidth);
    }

    public static ImageSource LoadIcon(double decodePixelWidth = 0)
    {
        return LoadImage("Assets/sprinksnap-ai-icon.png", decodePixelWidth);
    }

    private static ImageSource LoadImage(string relativePath, double decodePixelWidth)
    {
        Uri resourceUri = new Uri(
            "pack://application:,,,/" + UiAssemblyName + ";component/" + relativePath,
            UriKind.Absolute);

        BitmapImage image = new BitmapImage();
        image.BeginInit();
        image.UriSource = resourceUri;
        image.CacheOption = BitmapCacheOption.OnLoad;
        if (decodePixelWidth > 0)
        {
            image.DecodePixelWidth = (int)decodePixelWidth;
        }

        image.EndInit();
        image.Freeze();
        return image;
    }
}
