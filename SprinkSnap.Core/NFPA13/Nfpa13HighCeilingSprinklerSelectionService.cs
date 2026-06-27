using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.NFPA13;

public static class Nfpa13HighCeilingSprinklerSelectionService
{
    public const string ProjectViolationBlockReason =
        "One or more rooms with ceilings above 30 ft have sprinkler selections that violate NFPA 13 (2025) Section 19.2.3.2.5.1. "
        + "Update Sprinkler Review before generating design or running hydraulics.";

    public static IList<Nfpa13HighCeilingSprinklerSelectionResult> FindViolations(
        IEnumerable<RoomInfo> rooms,
        Func<RoomInfo, SprinklerFamilyInfo> familyResolver)
    {
        List<Nfpa13HighCeilingSprinklerSelectionResult> violations = new List<Nfpa13HighCeilingSprinklerSelectionResult>();
        foreach (RoomInfo room in rooms ?? Enumerable.Empty<RoomInfo>())
        {
            if (room.CeilingHeightFeet <= Nfpa13HighCeilingDesignCriteriaAdjuster.HighCeilingThresholdFeet)
            {
                continue;
            }

            SprinklerFamilyInfo family = familyResolver?.Invoke(room);
            Nfpa13HighCeilingSprinklerSelectionResult result = Nfpa13HighCeilingSprinklerSelectionValidator.Validate(room, family);
            if (!result.IsCompliant)
            {
                violations.Add(result);
            }
        }

        return violations;
    }

    public static bool IsProjectCompliant(
        IEnumerable<RoomInfo> rooms,
        Func<RoomInfo, SprinklerFamilyInfo> familyResolver)
    {
        return FindViolations(rooms, familyResolver).Count == 0;
    }

    public static SprinklerFamilyInfo ResolveSelectedFamily(
        RoomInfo room,
        IEnumerable<SprinklerFamilyInfo> catalog)
    {
        if (room == null)
        {
            return null;
        }

        string sprinklerName = string.IsNullOrWhiteSpace(room.SelectedSprinklerFamilyName)
            ? room.AutoSelectedSprinklerName
            : room.SelectedSprinklerFamilyName;

        if (string.IsNullOrWhiteSpace(sprinklerName))
        {
            return null;
        }

        return catalog?
            .FirstOrDefault(family => string.Equals(family.DisplayName, sprinklerName, StringComparison.OrdinalIgnoreCase));
    }
}
