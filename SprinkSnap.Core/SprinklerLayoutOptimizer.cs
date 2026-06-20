using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core;

public interface ISprinklerLayoutOptimizer
{
    AutomaticLayoutResult GenerateBestLayout(RoomInfo room, SprinklerFamilyInfo family);
}

public sealed class SprinklerLayoutOptimizer : ISprinklerLayoutOptimizer
{
    private readonly ILayoutComplianceValidator validator;

    public SprinklerLayoutOptimizer()
        : this(new LayoutComplianceValidator())
    {
    }

    public SprinklerLayoutOptimizer(ILayoutComplianceValidator validator)
    {
        this.validator = validator;
    }

    public AutomaticLayoutResult GenerateBestLayout(RoomInfo room, SprinklerFamilyInfo family)
    {
        LayoutValidationResult inputValidation = validator.ValidateInputs(room, family);
        if (!inputValidation.IsCompliant)
        {
            return CreateReviewResult(inputValidation);
        }

        IReadOnlyList<SprinklerPlacementCandidate> candidates = GenerateConstrainedGrid(room, family);
        LayoutValidationResult layoutValidation = validator.ValidateLayout(room, family, candidates);

        if (!layoutValidation.IsCompliant)
        {
            return CreateReviewResult(layoutValidation);
        }

        return new AutomaticLayoutResult
        {
            CanPlaceAutomatically = true,
            Status = LayoutStatus.Compliant,
            ConfidenceScore = layoutValidation.ConfidenceScore,
            Candidates = candidates.ToList(),
            PreviewMarkers = layoutValidation.Markers,
            Messages = layoutValidation.Messages
        };
    }

    private static IReadOnlyList<SprinklerPlacementCandidate> GenerateConstrainedGrid(
        RoomInfo room,
        SprinklerFamilyInfo family)
    {
        double maxArea = Math.Max(1.0, family.MaxCoverageAreaSquareFeet);
        double maxSpacing = Math.Max(1.0, family.MaxSpacingFeet);
        int columns = Math.Max(1, (int)Math.Ceiling(room.LengthFeet / maxSpacing));
        int rows = Math.Max(1, (int)Math.Ceiling(room.WidthFeet / maxSpacing));

        while (room.AreaSquareFeet / (columns * rows) > maxArea)
        {
            if (columns <= rows)
            {
                columns++;
            }
            else
            {
                rows++;
            }
        }

        double minX = room.BoundaryPolygon.Min(point => point.X);
        double maxX = room.BoundaryPolygon.Max(point => point.X);
        double minY = room.BoundaryPolygon.Min(point => point.Y);
        double maxY = room.BoundaryPolygon.Max(point => point.Y);
        double stepX = (maxX - minX) / columns;
        double stepY = (maxY - minY) / rows;
        double targetElevation = CalculateTargetSprinklerElevation(room);

        List<SprinklerPlacementCandidate> candidates = new List<SprinklerPlacementCandidate>();
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                double x = minX + stepX * (column + 0.5);
                double y = minY + stepY * (row + 0.5);
                candidates.Add(new SprinklerPlacementCandidate
                {
                    Location = new Point3D(x, y, targetElevation),
                    CandidateType = family.Orientation,
                    Basis = "Generated after input compliance checks using listed-family spacing and area constraints."
                });
            }
        }

        return candidates;
    }

    private static AutomaticLayoutResult CreateReviewResult(LayoutValidationResult validation)
    {
        return new AutomaticLayoutResult
        {
            CanPlaceAutomatically = false,
            Status = LayoutStatus.ReviewRequired,
            ConfidenceScore = validation.ConfidenceScore,
            PreviewMarkers = validation.Markers,
            Messages = validation.Messages
        };
    }

    private static double CalculateTargetSprinklerElevation(RoomInfo room)
    {
        if (room.DeckElevationFeet > 0.0)
        {
            return room.DeckElevationFeet - 1.0;
        }

        if (room.CeilingElevationFeet > 0.0)
        {
            return room.CeilingElevationFeet;
        }

        return room.FloorElevationFeet + Math.Max(0.0, room.HeightFeet - 1.0);
    }
}

