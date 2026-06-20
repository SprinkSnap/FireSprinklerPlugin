using System;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core;

public interface ICeilingIntelligenceService
{
    CeilingIntelligenceResult Analyze(RoomInfo room);
}

public sealed class CeilingIntelligenceService : ICeilingIntelligenceService
{
    public CeilingIntelligenceResult Analyze(RoomInfo room)
    {
        if (room.BoundaryPolygon.Count < 3 || room.AreaSquareFeet <= 0.0 || room.CeilingHeightFeet <= 0.0)
        {
            return CreateResult(
                CeilingClassification.Uncertain,
                "Critical room geometry or ceiling height is missing.",
                0.10,
                true);
        }

        string ceilingText = string.Join(" ", room.CeilingType, room.CeilingTileInformation);
        bool tBar = ContainsAny(ceilingText, "t-bar", "t bar", "suspended", "act", "acoustic", "lay-in", "tile");
        bool openStructure = ContainsAny(ceilingText, "open", "structure", "deck", "exposed");
        bool mixed = tBar && openStructure || ContainsAny(ceilingText, "mixed", "cloud", "soffit");
        bool sloped = room.HasSlopedCeiling || Math.Abs(room.Slope) >= 0.02;

        if (mixed)
        {
            return CreateResult(
                CeilingClassification.Mixed,
                "Mixed ceiling indicators were detected; designer review is required before automatic placement.",
                0.45,
                true);
        }

        if (sloped)
        {
            return CreateResult(
                CeilingClassification.Sloped,
                "Ceiling slope was detected from elevation and room analysis.",
                0.70,
                room.HasMultiSlopeCeiling);
        }

        if (tBar)
        {
            room.CeilingGridDetected = true;
            return CreateResult(
                CeilingClassification.TBarSuspended,
                "Suspended/T-bar ceiling indicators were detected.",
                0.82,
                false);
        }

        if (openStructure)
        {
            return CreateResult(
                CeilingClassification.OpenStructure,
                "Open-structure ceiling indicators were detected.",
                0.78,
                false);
        }

        if (room.HasFlatCeiling)
        {
            return CreateResult(
                CeilingClassification.Flat,
                "Flat ceiling inferred from room slope and elevation data.",
                0.68,
                false);
        }

        return CreateResult(
            CeilingClassification.Uncertain,
            "Ceiling type could not be classified confidently.",
            0.35,
            true);
    }

    private static CeilingIntelligenceResult CreateResult(
        string classification,
        string summary,
        double confidenceScore,
        bool requiresReview)
    {
        return new CeilingIntelligenceResult
        {
            Classification = classification,
            Summary = summary,
            ConfidenceScore = confidenceScore,
            RequiresReview = requiresReview
        };
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        foreach (string token in tokens)
        {
            if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}

