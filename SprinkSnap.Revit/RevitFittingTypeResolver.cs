using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

internal static class RevitFittingTypeResolver
{
    public static FamilySymbol ResolveFitting(Document document, string jointType, double diameterInches)
    {
        if (document == null || string.IsNullOrWhiteSpace(jointType))
        {
            return null;
        }

        BuiltInCategory category = string.Equals(jointType, PipeJointTypes.Valve, StringComparison.OrdinalIgnoreCase)
            ? BuiltInCategory.OST_PipeAccessory
            : BuiltInCategory.OST_PipeFitting;

        List<FamilySymbol> symbols = new FilteredElementCollector(document)
            .OfCategory(category)
            .WhereElementIsElementType()
            .OfType<FamilySymbol>()
            .ToList();

        if (symbols.Count == 0)
        {
            return null;
        }

        string[] keywords = ResolveKeywords(jointType);
        IEnumerable<FamilySymbol> keywordMatches = symbols.Where(symbol =>
            keywords.Any(keyword =>
                (symbol.FamilyName ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0
                || (symbol.Name ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0));

        FamilySymbol diameterMatch = keywordMatches
            .OrderBy(symbol => Math.Abs(GetNominalDiameterInches(symbol) - diameterInches))
            .FirstOrDefault(symbol => Math.Abs(GetNominalDiameterInches(symbol) - diameterInches) < 0.25);

        return diameterMatch
            ?? keywordMatches.FirstOrDefault()
            ?? symbols.OrderBy(symbol => Math.Abs(GetNominalDiameterInches(symbol) - diameterInches)).FirstOrDefault();
    }

    private static string[] ResolveKeywords(string jointType)
    {
        if (string.Equals(jointType, PipeJointTypes.Tee, StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "tee", "t ", " t" };
        }

        if (string.Equals(jointType, PipeJointTypes.Valve, StringComparison.OrdinalIgnoreCase))
        {
            return new[] { "os&y", "os y", "gate", "control", "valve" };
        }

        return new[] { "elbow", "ell", "90" };
    }

    private static double GetNominalDiameterInches(FamilySymbol symbol)
    {
        Parameter parameter = symbol.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)
            ?? symbol.LookupParameter("Diameter")
            ?? symbol.LookupParameter("Nominal Diameter");
        if (parameter == null)
        {
            return 0.0;
        }

        return parameter.AsDouble();
    }
}
