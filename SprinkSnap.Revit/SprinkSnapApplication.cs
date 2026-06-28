using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using FireSprinklerPlugin.SprinkSnap.UI.Branding;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public sealed class SprinkSnapApplication : IExternalApplication
{
    public const string RibbonTabName = SprinkSnapBranding.ProductName;

    public Result OnStartup(UIControlledApplication application)
    {
        TryCreateRibbonTab(application);
        CreateRibbonPanels(application);
        SprinkSnapDockablePaneRegistration.Register(application);
        ExternalEvents.SprinklerPlacementExternalEventHandler.Register();
        ExternalEvents.PipePlacementExternalEventHandler.Register();
        ExternalEvents.PipeMeasurementExternalEventHandler.Register();
        ExternalEvents.PipeDiameterSyncExternalEventHandler.Register();
        ExternalEvents.ClashDetectionExternalEventHandler.Register();
        ExternalEvents.ModelReanalysisExternalEventHandler.Register();
        ExternalEvents.LoadedSprinklerSymbolScanExternalEventHandler.Register();
        ExternalEvents.RevitNavigationExternalEventHandler.Register();
        ExternalEvents.HydraulicSupplyPickExternalEventHandler.Register();
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        return Result.Succeeded;
    }

    private static void TryCreateRibbonTab(UIControlledApplication application)
    {
        try
        {
            application.CreateRibbonTab(RibbonTabName);
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException)
        {
            // Tab already exists in this Revit session.
        }
    }

    private static void CreateRibbonPanels(UIControlledApplication application)
    {
        string assemblyPath = typeof(SprinkSnapApplication).Assembly.Location;
        IReadOnlyList<RibbonButtonDefinition> buttons = new List<RibbonButtonDefinition>
        {
            new RibbonButtonDefinition("Analyze Model", "Analyze\nModel", typeof(Commands.AnalyzeModelCommand).FullName),
            new RibbonButtonDefinition("Hazard Review", "Hazard\nReview", typeof(HazardClassificationCommand).FullName),
            new RibbonButtonDefinition("Sprinkler Review", "Sprinkler\nReview", typeof(Commands.SprinklerReviewCommand).FullName),
            new RibbonButtonDefinition("Water Supply", "Water\nSupply", typeof(Commands.WaterSupplyCommand).FullName),
            new RibbonButtonDefinition("Generate Design", "Generate\nDesign", typeof(Commands.GenerateDesignCommand).FullName),
            new RibbonButtonDefinition("Clash Detection", "Clash\nDetection", typeof(Commands.ClashDetectionCommand).FullName),
            new RibbonButtonDefinition("Place Sprinklers", "Place\nSprinklers", typeof(Commands.PlaceSprinklersCommand).FullName),
            new RibbonButtonDefinition("Hydraulics", "Hydraulics", typeof(Commands.HydraulicsCommand).FullName),
            new RibbonButtonDefinition("Materials", "Materials", typeof(Commands.MaterialsCommand).FullName),
            new RibbonButtonDefinition("Reports", "Reports", typeof(Commands.ReportsCommand).FullName),
            new RibbonButtonDefinition("Settings", "Settings", typeof(Commands.SettingsCommand).FullName)
        };

        foreach (RibbonButtonDefinition definition in buttons)
        {
            RibbonPanel panel = application.CreateRibbonPanel(RibbonTabName, definition.PanelName);
            PushButtonData buttonData = new PushButtonData(
                definition.PanelName.Replace(" ", string.Empty),
                definition.ButtonText,
                assemblyPath,
                definition.CommandClassName)
            {
                ToolTip = "Open " + definition.PanelName + " in " + SprinkSnapBranding.ProductName,
                LargeImage = LoadRibbonImage(32),
                Image = LoadRibbonImage(16)
            };
            panel.AddItem(buttonData);
        }
    }

    private sealed class RibbonButtonDefinition
    {
        public RibbonButtonDefinition(string panelName, string buttonText, string commandClassName)
        {
            PanelName = panelName;
            ButtonText = buttonText;
            CommandClassName = commandClassName;
        }

        public string PanelName { get; }

        public string ButtonText { get; }

        public string CommandClassName { get; }
    }

    private static ImageSource LoadRibbonImage(double pixelWidth)
    {
        return SprinkSnapBranding.LoadIcon(pixelWidth);
    }
}

