using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FireSprinklerPlugin.SprinkSnap.UI.Branding;

public static class SprinkSnapBranding
{
    private const string UiAssemblyName = "SprinkSnap.UI";

    private const string LogoResourcePath = "sprinksnap-logo-transparent.png";

    public const string ProductName = "SprinkSnap AI";

    public const string Tagline = "SMARTER DESIGN. CODE CONFIDENT.";

    public static ImageSource LogoImage { get; } = LoadImage(LogoResourcePath, 520);

    public static ImageSource IconImage { get; } = LoadImage(LogoResourcePath, 96);

    public static ImageSource LoadLogo(double decodePixelWidth = 0)
    {
        return LoadImage(LogoResourcePath, decodePixelWidth);
    }

    public static ImageSource LoadIcon(double decodePixelWidth = 0)
    {
        return LoadImage(LogoResourcePath, decodePixelWidth > 0 ? decodePixelWidth : 96);
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
