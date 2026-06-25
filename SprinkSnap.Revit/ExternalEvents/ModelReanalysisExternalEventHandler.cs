using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.UI.Shell;

namespace FireSprinklerPlugin.SprinkSnap.Revit.ExternalEvents;

public sealed class ModelReanalysisExternalEventHandler : IExternalEventHandler
{
    private Document pendingDocument;
    private SprinkSnapShellContext pendingContext;
    private Action<RevitProjectLoadResult> pendingCallback;

    public static ModelReanalysisExternalEventHandler Instance { get; } = new ModelReanalysisExternalEventHandler();

    public static ExternalEvent ExternalEvent { get; private set; }

    public static void Register()
    {
        if (ExternalEvent == null)
        {
            ExternalEvent = Autodesk.Revit.UI.ExternalEvent.Create(Instance);
        }
    }

    public void RequestReanalysis(
        Document document,
        SprinkSnapShellContext context,
        Action<RevitProjectLoadResult> callback)
    {
        pendingDocument = document;
        pendingContext = context;
        pendingCallback = callback;
        ExternalEvent?.Raise();
    }

    public void Execute(UIApplication application)
    {
        RevitProjectLoadResult loadResult = new RevitProjectLoadResult();

        try
        {
            if (pendingDocument != null && pendingContext != null)
            {
                loadResult = RevitProjectLoader.Load(pendingDocument, runModelAnalysis: true);
                pendingContext.ApplyRevitLoad(loadResult, markAnalysisComplete: true);
                pendingContext.ApplyPostReanalysisInvalidation();
                RevitSessionPersistence.Save(pendingDocument, pendingContext.ProjectState);
                pendingContext.RequestWorkflowRefresh();
            }
        }
        catch (Exception ex)
        {
            loadResult.ModelAnalysis ??= new ModelAnalysisSummary();
            loadResult.ModelAnalysis.Warnings.Add(ex.Message);
        }

        pendingCallback?.Invoke(loadResult);
        pendingDocument = null;
        pendingContext = null;
        pendingCallback = null;
    }

    public string GetName()
    {
        return "SprinkSnap Reanalyze Model";
    }
}
