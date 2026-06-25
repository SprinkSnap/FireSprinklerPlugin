using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FireSprinklerPlugin.SprinkSnap.Core.Mapping;
using FireSprinklerPlugin.SprinkSnap.UI.Shell;

namespace FireSprinklerPlugin.SprinkSnap.Revit.ExternalEvents;

public sealed class LoadedSprinklerSymbolScanExternalEventHandler : IExternalEventHandler
{
    private Document pendingDocument;
    private SprinkSnapShellContext pendingContext;
    private Action<IList<LoadedRevitSymbolOption>> pendingCallback;

    public static LoadedSprinklerSymbolScanExternalEventHandler Instance { get; } =
        new LoadedSprinklerSymbolScanExternalEventHandler();

    public static ExternalEvent ExternalEvent { get; private set; }

    public static void Register()
    {
        if (ExternalEvent == null)
        {
            ExternalEvent = Autodesk.Revit.UI.ExternalEvent.Create(Instance);
        }
    }

    public void RequestScan(
        Document document,
        SprinkSnapShellContext context,
        Action<IList<LoadedRevitSymbolOption>> callback)
    {
        pendingDocument = document;
        pendingContext = context;
        pendingCallback = callback;
        ExternalEvent?.Raise();
    }

    public void Execute(UIApplication application)
    {
        IList<LoadedRevitSymbolOption> symbols = new List<LoadedRevitSymbolOption>();

        try
        {
            if (pendingDocument != null && pendingContext != null)
            {
                ISprinklerFamilyScanner scanner = new SprinklerFamilyScanner();
                symbols = scanner.ScanLoadedSymbolOptions(pendingDocument);
                pendingContext.ApplyLoadedSprinklerSymbols(symbols);
                RevitSessionPersistence.Save(pendingDocument, pendingContext.ProjectState);
            }
        }
        catch (Exception)
        {
            symbols = new List<LoadedRevitSymbolOption>();
        }

        pendingCallback?.Invoke(symbols);
        pendingDocument = null;
        pendingContext = null;
        pendingCallback = null;
    }

    public string GetName()
    {
        return "SprinkSnap Scan Loaded Sprinkler Symbols";
    }
}
