using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Placement;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;
using FireSprinklerPlugin.SprinkSnap.Core.Mapping;
using FireSprinklerPlugin.SprinkSnap.Revit.ExternalEvents;
using FireSprinklerPlugin.SprinkSnap.UI.Shell;

namespace FireSprinklerPlugin.SprinkSnap.Revit.Session;

public sealed class SprinkSnapRevitSession
{
    private static readonly Dictionary<string, SprinkSnapRevitSession> SessionsByDocumentKey =
        new Dictionary<string, SprinkSnapRevitSession>();

    private SprinkSnapRevitSession(Document document, SprinkSnapShellContext context)
    {
        Document = document;
        Context = context;
        Context.PersistToRevitRequested = PersistApprovedHazardsToRevit;
        Context.RequestPlaceSprinklers = RequestPlaceSprinklersInRevit;
        Context.RequestPlacePipes = RequestPlacePipesInRevit;
        Context.RequestClashDetection = RequestClashDetectionInRevit;
        Context.RequestReanalyze = RequestReanalyzeInRevit;
        Context.RequestRefreshLoadedSprinklerSymbols = RequestRefreshLoadedSprinklerSymbolsInRevit;
        Context.RequestShowClashInRevit = ShowClashInRevit;
        Context.RequestShowRoomInRevit = ShowRoomInRevit;
    }

    public Document Document { get; }

    public SprinkSnapShellContext Context { get; }

    public string DocumentKey => RevitDocumentKey.Create(Document);

    public static SprinkSnapRevitSession GetOrCreate(UIApplication uiApplication)
    {
        UIDocument uiDocument = uiApplication.ActiveUIDocument;
        if (uiDocument == null)
        {
            return null;
        }

        Document document = uiDocument.Document;
        string documentKey = RevitDocumentKey.Create(document);
        if (SessionsByDocumentKey.TryGetValue(documentKey, out SprinkSnapRevitSession existingSession)
            && existingSession.Document.IsValidObject)
        {
            return existingSession;
        }

        SprinkSnapShellContext context = new SprinkSnapShellContext(new SprinkSnapProjectState());
        SprinkSnapRevitSession session = new SprinkSnapRevitSession(document, context);
        SessionsByDocumentKey[documentKey] = session;
        return session;
    }

    public static SprinkSnapRevitSession Activate(UIApplication uiApplication)
    {
        SprinkSnapRevitSession session = GetOrCreate(uiApplication);
        if (session == null)
        {
            return null;
        }

        SprinkSnapShellView shellView = SprinkSnapRevitSessionHost.SharedShellView;
        shellView?.AttachContext(session.Context);
        return session;
    }

    public bool EnsureLoaded(bool refreshRooms, bool runModelAnalysis = false)
    {
        string currentDocumentKey = RevitDocumentKey.Create(Document);
        bool documentChanged = !string.Equals(Context.DocumentKey, currentDocumentKey, StringComparison.OrdinalIgnoreCase);
        if (!refreshRooms && !documentChanged && Context.ProjectState.Rooms.Count > 0)
        {
            return true;
        }

        RevitProjectLoadResult loadResult = RevitProjectLoader.Load(Document, runModelAnalysis);
        Context.ApplyRevitLoad(loadResult, runModelAnalysis);
        return Context.ProjectState.Rooms.Count > 0;
    }

    public void ShowWorkflowStep(UIApplication uiApplication, SprinkSnapWorkflowStep step, string feedback = null)
    {
        DockablePane pane = uiApplication.GetDockablePane(SprinkSnapDockablePaneRegistration.PaneId);
        pane.Show();

        SprinkSnapShellView shellView = SprinkSnapRevitSessionHost.SharedShellView;
        if (shellView == null)
        {
            return;
        }

        shellView.AttachContext(Context);
        if (!string.IsNullOrWhiteSpace(feedback))
        {
            shellView.ViewModel?.SetActionFeedback(feedback);
        }

        shellView.OpenModule(step);
    }

    private void PersistApprovedHazardsToRevit()
    {
        RevitHazardPersistence.SaveApprovedHazards(Document, Context.ProjectState.Rooms);
        RevitSessionPersistence.Save(Document, Context.ProjectState);
    }

    private void RequestPlaceSprinklersInRevit(Action<SprinklerPlacementSummary> callback)
    {
        SprinklerPlacementExternalEventHandler.Instance.RequestPlacement(Document, Context, callback);
    }

    private void RequestPlacePipesInRevit(Action<PipePlacementSummary> callback)
    {
        PipePlacementExternalEventHandler.Instance.RequestPlacement(Document, Context, callback);
    }

    private void RequestClashDetectionInRevit(Action<ClashDetectionSummary> callback)
    {
        ClashDetectionExternalEventHandler.Instance.RequestDetect(Document, Context, callback);
    }

    private void RequestReanalyzeInRevit(Action<RevitProjectLoadResult> callback)
    {
        ModelReanalysisExternalEventHandler.Instance.RequestReanalysis(Document, Context, callback);
    }

    private void RequestRefreshLoadedSprinklerSymbolsInRevit(Action<IList<LoadedRevitSymbolOption>> callback)
    {
        LoadedSprinklerSymbolScanExternalEventHandler.Instance.RequestScan(Document, Context, callback);
    }

    private void ShowClashInRevit(SprinklerClashRecord clash)
    {
        RevitNavigationExternalEventHandler.Instance.RequestShowClash(clash);
    }

    private void ShowRoomInRevit(int roomRevitElementId)
    {
        RevitNavigationExternalEventHandler.Instance.RequestShowRoom(roomRevitElementId);
    }
}
