using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public static class RevitHydraulicSupplyPickService
{
    public static HydraulicSupplyAnchor PickSupplyAnchor(UIDocument uiDocument)
    {
        if (uiDocument?.Document == null)
        {
            return CreateCancelledAnchor("No active Revit document is available.");
        }

        try
        {
            Reference reference = uiDocument.Selection.PickObject(
                ObjectType.Element,
                new PipeSelectionFilter(),
                "Select the supply riser or main pipe to anchor hydraulic calculations.");
            Element element = uiDocument.Document.GetElement(reference);
            if (element == null)
            {
                return CreateCancelledAnchor("Selected element could not be resolved.");
            }

            if (element is Pipe pipe)
            {
                return CreateAnchorFromPipe(pipe);
            }

            if (element.Location is LocationPoint locationPoint)
            {
                Point3D point = ToPoint3D(locationPoint.Point);
                return new HydraulicSupplyAnchor
                {
                    IsSet = true,
                    RevitElementId = element.Id.IntegerValue,
                    ElementLabel = BuildElementLabel(element),
                    SupplyPoint = point,
                    HeaderPoint = point,
                    SourceKind = "UserPick"
                };
            }

            return CreateCancelledAnchor("Selected element does not expose pipe curve or point geometry.");
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return CreateCancelledAnchor(string.Empty);
        }
    }

    private static HydraulicSupplyAnchor CreateAnchorFromPipe(Pipe pipe)
    {
        if (pipe?.Location is not LocationCurve locationCurve || locationCurve.Curve == null)
        {
            return CreateCancelledAnchor("Selected pipe does not expose curve geometry.");
        }

        XYZ start = locationCurve.Curve.GetEndPoint(0);
        XYZ end = locationCurve.Curve.GetEndPoint(1);
        return HydraulicSupplyAnchorService.CreateFromPipeEndpoints(
            pipe.Id.IntegerValue,
            BuildElementLabel(pipe),
            ToPoint3D(start),
            ToPoint3D(end),
            "UserPick");
    }

    private static string BuildElementLabel(Element element)
    {
        string name = element?.Name ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name + " (" + element.Id.IntegerValue + ")";
        }

        return "Element " + element.Id.IntegerValue;
    }

    private static Point3D ToPoint3D(XYZ point)
    {
        return new Point3D(point.X, point.Y, point.Z);
    }

    private static HydraulicSupplyAnchor CreateCancelledAnchor(string message)
    {
        return new HydraulicSupplyAnchor
        {
            IsSet = false,
            ElementLabel = message ?? string.Empty
        };
    }

    private sealed class PipeSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            if (element is Pipe)
            {
                return true;
            }

            BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;
            return category == BuiltInCategory.OST_PipeFitting
                || category == BuiltInCategory.OST_PipeAccessory;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
