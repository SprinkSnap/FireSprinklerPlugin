using System.Collections.Generic;
using Autodesk.Revit.DB;
using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Engines;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.UI.Shell;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public static class RevitProjectLoader
{
    public static RevitProjectLoadResult Load(Document document, bool runModelAnalysis = false)
    {
        RevitProjectLoadResult result = new RevitProjectLoadResult
        {
            DocumentKey = RevitDocumentKey.Create(document),
            DocumentTitle = document.Title
        };

        IHazardClassificationParameterStorage parameterStorage = new HazardClassificationParameterStorage();
        IRoomExtractor roomExtractor = new RoomExtractor(
            new RoomBoundaryExtractor(),
            new RoomAnalyzer(),
            new HazardClassifier(),
            parameterStorage);

        foreach (RoomInfo room in roomExtractor.ExtractRooms(document))
        {
            result.Rooms.Add(room);
        }

        ISprinklerFamilyScanner sprinklerFamilyScanner = new SprinklerFamilyScanner();
        result.SprinklerFamilies = RevitSprinklerCatalogMerger.Merge(
            new SprinklerFamilySelector().GetAvailableFamilies(),
            sprinklerFamilyScanner.ScanLoadedSprinklerFamilies(document));

        result.ModelAnalysis.LinkedModelCount = new FilteredElementCollector(document)
            .OfClass(typeof(RevitLinkInstance))
            .GetElementCount();
        result.ModelAnalysis.ExistingSprinklerCount = new FilteredElementCollector(document)
            .OfCategory(BuiltInCategory.OST_Sprinklers)
            .WhereElementIsNotElementType()
            .GetElementCount();
        result.ModelAnalysis.RoomCount = result.Rooms.Count;

        if (runModelAnalysis)
        {
            SprinkSnapProjectState scratchState = new SprinkSnapProjectState();
            foreach (RoomInfo room in result.Rooms)
            {
                scratchState.Rooms.Add(room);
            }

            scratchState.ModelAnalysis.LinkedModelCount = result.ModelAnalysis.LinkedModelCount;
            scratchState.ModelAnalysis.ExistingSprinklerCount = result.ModelAnalysis.ExistingSprinklerCount;

            IModelAnalysisEngine engine = new ModelAnalysisEngine();
            ModelAnalysisSummary summary = engine.Analyze(scratchState);
            summary.LinkedModelCount = result.ModelAnalysis.LinkedModelCount;
            summary.ExistingSprinklerCount = result.ModelAnalysis.ExistingSprinklerCount;
            summary.RoomCount = result.Rooms.Count;
            result.ModelAnalysis = summary;
        }

        return result;
    }
}

public static class RevitDocumentKey
{
    public static string Create(Document document)
    {
        if (document == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(document.PathName))
        {
            return document.PathName;
        }

        return document.Title + "#" + document.GetHashCode();
    }
}
