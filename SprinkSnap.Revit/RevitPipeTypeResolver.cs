using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

internal static class RevitPipeTypeResolver
{
    public static PipingSystemType ResolveSystemType(Document document)
    {
        List<PipingSystemType> systemTypes = new FilteredElementCollector(document)
            .OfClass(typeof(PipingSystemType))
            .Cast<PipingSystemType>()
            .ToList();

        PipingSystemType preferred = systemTypes.FirstOrDefault(type =>
            type.SystemClassification == MEPSystemClassification.FireProtectWet
            || type.SystemClassification == MEPSystemClassification.FireProtectDry
            || type.SystemClassification == MEPSystemClassification.FireProtectPreaction);

        return preferred ?? systemTypes.FirstOrDefault();
    }

    public static PipeType ResolvePipeType(Document document, double diameterInches)
    {
        List<PipeType> pipeTypes = new FilteredElementCollector(document)
            .OfClass(typeof(PipeType))
            .Cast<PipeType>()
            .ToList();

        if (pipeTypes.Count == 0)
        {
            return null;
        }

        PipeType exactMatch = pipeTypes
            .OrderBy(type => Math.Abs(GetNominalDiameterInches(type) - diameterInches))
            .FirstOrDefault(type => Math.Abs(GetNominalDiameterInches(type) - diameterInches) < 0.05);

        return exactMatch ?? pipeTypes
            .OrderBy(type => Math.Abs(GetNominalDiameterInches(type) - diameterInches))
            .First();
    }

    private static double GetNominalDiameterInches(PipeType pipeType)
    {
        Parameter parameter = pipeType.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
        if (parameter == null)
        {
            return 0.0;
        }

        return parameter.AsDouble();
    }
}
