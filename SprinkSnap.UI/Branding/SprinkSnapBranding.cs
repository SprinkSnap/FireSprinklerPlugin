using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FireSprinklerPlugin.SprinkSnap.UI.Branding;

public static class SprinkSnapBranding
{
    private const string UiAssemblyName = "SprinkSnap.UI";

    private const string MasterLogoResourcePath = "sprinksnap-logo-transparent.png";

    private const string HeaderLogoResourcePath = "sprinksnap-logo-header.png";

    private const string CompactLogoResourcePath = "sprinksnap-logo-compact.png";

    private const string IconMarkResourcePath = "sprinksnap-icon-mark.png";

    private const string RevitIcon32ResourcePath = "sprinksnap-revit-icon-32.png";

    private const string RevitIcon16ResourcePath = "sprinksnap-revit-icon-16.png";

    public const string ProductName = "SprinkSnap AI";

    public const string Tagline = "SMARTER DESIGN. CODE CONFIDENT.";

    public const string HeaderBackground = "#FFFFFF";

    public const string HeaderBorder = "#E2E8F0";

    public const string HeaderSubtitleForeground = "#64748B";

    public const string TaglineForeground = "#475569";

    public const string WorkflowChipBackground = "#EFF6FF";

    public const string WorkflowChipBorder = "#BFDBFE";

    public const string WorkflowChipForeground = "#0F172A";

    public const string WorkflowChipSubtextForeground = "#475569";

    public const string LogoPlateBackground = "#F0F9FF";

    public const string LogoPlateBorder = "#BFDBFE";

    public const double ShellHeaderLogoHeight = 72;

    public const double AssistantLogoHeight = 48;

    public static ImageSource HeaderLogoImage { get; } = LoadImage(HeaderLogoResourcePath, 640);

    public static ImageSource CompactLogoImage { get; } = LoadImage(CompactLogoResourcePath, 420);

    public static ImageSource IconImage { get; } = LoadImage(IconMarkResourcePath, 128);

    public static ImageSource LogoImage { get; } = HeaderLogoImage;

    public static ImageSource LoadLogo(double decodePixelWidth = 0)
    {
        return decodePixelWidth > 0
            ? LoadImage(HeaderLogoResourcePath, decodePixelWidth)
            : HeaderLogoImage;
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

    public static ImageSource LoadMasterLogo(double decodePixelWidth = 0)
    {
        return LoadImage(MasterLogoResourcePath, decodePixelWidth);
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
