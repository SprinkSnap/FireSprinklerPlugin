using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FireSprinklerPlugin.SprinkSnap.UI.Branding;

public static class SprinkSnapBranding
{
    private const string UiAssemblyName = "SprinkSnap.UI";

    private const string MasterLogoResourcePath = "sprinksnap-logo-transparent.png";

    private const string IconMarkResourcePath = "sprinksnap-icon-mark.png";

    private const string RevitIcon32ResourcePath = "sprinksnap-revit-icon-32.png";

    private const string RevitIcon16ResourcePath = "sprinksnap-revit-icon-16.png";

    public const string ProductName = "SprinkSnap AI";

    public const string BrandingAssetVersion = "2025.06-official-logo-white-bg";

    public const string Tagline = "SMARTER DESIGN. CODE CONFIDENT.";

    /// <summary>
    /// Pure white — recommended logo background. The master PNG uses navy gradient text
    /// and a gray tagline; white gives correct contrast without tinting brand colors.
    /// </summary>
    public static readonly Brush LogoBackground = CreateBrush("#FFFFFF");

    public static readonly Brush HeaderBackground = LogoBackground;

    public static readonly Brush HeaderBorder = CreateBrush("#E2E8F0");

    public static readonly Brush HeaderSubtitleForeground = CreateBrush("#64748B");

    public static readonly Brush TaglineForeground = CreateBrush("#475569");

    public static readonly Brush WorkflowChipBackground = CreateBrush("#EFF6FF");

    public static readonly Brush WorkflowChipBorder = CreateBrush("#BFDBFE");

    public static readonly Brush WorkflowChipForeground = CreateBrush("#0F172A");

    public static readonly Brush WorkflowChipSubtextForeground = CreateBrush("#475569");

    public const double ShellHeaderLogoHeight = 104;

    public const double AssistantLogoHeight = 64;

    public static ImageSource LogoImage { get; } = LoadImage(MasterLogoResourcePath, 1000);

    public static ImageSource IconImage { get; } = LoadImage(IconMarkResourcePath, 128);

    public static ImageSource LoadLogo(double decodePixelWidth = 0)
    {
        return decodePixelWidth > 0
            ? LoadImage(MasterLogoResourcePath, decodePixelWidth)
            : LogoImage;
    }

    public static ImageSource LoadIcon(double decodePixelWidth = 0)
    {
        return decodePixelWidth > 0
            ? LoadImage(IconMarkResourcePath, decodePixelWidth)
            : IconImage;
    }

    public static ImageSource LoadRevitRibbonImage(int pixelSize)
    {
        return pixelSize >= 24
            ? LoadImage(RevitIcon32ResourcePath, 0)
            : LoadImage(RevitIcon16ResourcePath, 0);
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

    private static SolidColorBrush CreateBrush(string hexColor)
    {
        SolidColorBrush brush = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(hexColor));
        brush.Freeze();
        return brush;
    }
}
