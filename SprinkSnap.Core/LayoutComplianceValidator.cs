using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core;

public interface ILayoutComplianceValidator
{
    LayoutValidationResult ValidateInputs(RoomInfo room, SprinklerFamilyInfo family);

    LayoutValidationResult ValidateLayout(
        RoomInfo room,
        SprinklerFamilyInfo family,
        IReadOnlyList<SprinklerPlacementCandidate> candidates);
}

public sealed class LayoutComplianceValidator : ILayoutComplianceValidator
{
    public LayoutValidationResult ValidateInputs(RoomInfo room, SprinklerFamilyInfo family)
    {
        LayoutValidationResult result = new LayoutValidationResult
        {
            Status = LayoutStatus.Ready,
            IsCompliant = true,
            ConfidenceScore = Math.Min(0.95, room.LayoutConfidenceScore)
        };

        if (!room.HasCriticalGeometry)
        {
            AddFailure(result, "Critical room geometry is missing or uncertain.");
        }

        if (family == null)
        {
            AddFailure(result, "No listed sprinkler family was selected.");
            return result;
        }

        if (!HazardClassification.IsSupported(room.ApprovedHazardClassification))
        {
            AddFailure(result, "No approved NFPA 13 hazard classification is available.");
        }

        if (!family.SupportedHazardClassifications.Contains(room.ApprovedHazardClassification))
        {
            AddFailure(result, "Selected family is not listed for " + room.ApprovedHazardClassification + ".");
        }

        if (!family.SupportedCeilingClassifications.Contains(room.CeilingClassification))
        {
            AddFailure(result, "Selected family is not configured for " + room.CeilingClassification + " ceilings.");
        }

        if (room.RequiresExceptionReview)
        {
            AddFailure(result, room.ExceptionReason);
        }

        return result;
    }

    public LayoutValidationResult ValidateLayout(
        RoomInfo room,
        SprinklerFamilyInfo family,
        IReadOnlyList<SprinklerPlacementCandidate> candidates)
    {
        LayoutValidationResult result = ValidateInputs(room, family);
        if (!result.IsCompliant)
        {
            result.Status = LayoutStatus.ReviewRequired;
            return result;
        }

        if (candidates == null || candidates.Count == 0)
        {
            AddFailure(result, "No sprinkler candidates were generated.");
            return result;
        }

        double coveragePerHead = room.AreaSquareFeet / candidates.Count;
        if (coveragePerHead > family.MaxCoverageAreaSquareFeet)
        {
            AddFailure(result, "Coverage per head exceeds selected family/listing constraints.");
        }

        foreach (SprinklerPlacementCandidate candidate in candidates)
        {
            bool markerCompliant = PointIsInsideBoundingBox(room, candidate.Location)
                && coveragePerHead <= family.MaxCoverageAreaSquareFeet;
            result.Markers.Add(new LayoutMarker
            {
                Location = candidate.Location,
                IsCompliant = markerCompliant,
                Message = markerCompliant ? "Compliant preview marker" : "Noncompliant preview marker"
            });
        }

        if (result.IsCompliant)
        {
            result.Status = LayoutStatus.Compliant;
            result.ConfidenceScore = Math.Min(0.98, Math.Max(0.0, room.LayoutConfidenceScore));
            result.Messages.Add("Layout passed conservative pre-placement validation.");
        }

        return result;
    }

    private static void AddFailure(LayoutValidationResult result, string message)
    {
        result.IsCompliant = false;
        result.Status = LayoutStatus.ReviewRequired;
        result.ConfidenceScore = Math.Min(result.ConfidenceScore, 0.45);
        if (!string.IsNullOrWhiteSpace(message))
        {
            result.Messages.Add(message);
        }
    }

    private static bool PointIsInsideBoundingBox(RoomInfo room, Point3D point)
    {
        if (room.BoundaryPolygon.Count < 3)
        {
            return false;
        }

        double minX = room.BoundaryPolygon.Min(p => p.X);
        double maxX = room.BoundaryPolygon.Max(p => p.X);
        double minY = room.BoundaryPolygon.Min(p => p.Y);
        double maxY = room.BoundaryPolygon.Max(p => p.Y);

        return point.X >= minX && point.X <= maxX && point.Y >= minY && point.Y <= maxY;
    }
}

