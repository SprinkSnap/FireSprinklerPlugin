using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class Nfpa13HighCeilingSprinklerSelectionValidatorTests
{
    [Fact]
    public void Validate_DoesNotApply_WhenCeilingIs30FeetOrBelow()
    {
        RoomInfo room = CreateHighCeilingRoom(HazardClassification.OrdinaryHazardGroup2, ceilingHeightFeet: 30);
        SprinklerFamilyInfo family = CreateFamily(kFactor: 5.6, orientation: "Sidewall");

        Nfpa13HighCeilingSprinklerSelectionResult result = Nfpa13HighCeilingSprinklerSelectionValidator.Validate(room, family);

        Assert.True(result.IsCompliant);
        Assert.False(result.AppliesHighCeilingRules);
    }

    [Fact]
    public void Validate_RejectsSidewall_ForOrdinaryHazardGroup1Above30Feet()
    {
        RoomInfo room = CreateHighCeilingRoom(HazardClassification.OrdinaryHazardGroup1);
        SprinklerFamilyInfo family = CreateFamily(kFactor: 11.2, orientation: "Sidewall", category: "Standard Spray Quick Response");

        Nfpa13HighCeilingSprinklerSelectionResult result = Nfpa13HighCeilingSprinklerSelectionValidator.Validate(room, family);

        Assert.False(result.IsCompliant);
        Assert.True(result.AppliesHighCeilingRules);
        Assert.Contains("Sidewall", result.Violations[0]);
        Assert.Contains("10.3.2", result.Violations[0]);
    }

    [Fact]
    public void Validate_RejectsLowKFactor_ForOrdinaryHazardGroup2Above30Feet()
    {
        RoomInfo room = CreateHighCeilingRoom(HazardClassification.OrdinaryHazardGroup2);
        SprinklerFamilyInfo family = CreateFamily(kFactor: 8.0);

        Nfpa13HighCeilingSprinklerSelectionResult result = Nfpa13HighCeilingSprinklerSelectionValidator.Validate(room, family);

        Assert.False(result.IsCompliant);
        Assert.Contains("11.2", result.Violations[0]);
        Assert.Contains("9.4.5", result.Violations[0]);
    }

    [Fact]
    public void Validate_RejectsExtendedCoverageK224OrLess_ForOrdinaryHazardGroup2Above30Feet()
    {
        RoomInfo room = CreateHighCeilingRoom(HazardClassification.OrdinaryHazardGroup2);
        SprinklerFamilyInfo family = CreateFamily(kFactor: 22.4, coverageType: "Extended Coverage");

        Nfpa13HighCeilingSprinklerSelectionResult result = Nfpa13HighCeilingSprinklerSelectionValidator.Validate(room, family);

        Assert.False(result.IsCompliant);
        Assert.Contains("Extended-coverage", result.Violations[0]);
        Assert.Contains("11.2.1.1", result.Violations[0]);
    }

    [Fact]
    public void Validate_RejectsStandardResponseStandardCoverage_ForOrdinaryHazardGroup2Above30Feet()
    {
        RoomInfo room = CreateHighCeilingRoom(HazardClassification.OrdinaryHazardGroup2);
        SprinklerFamilyInfo family = CreateFamily(
            kFactor: 11.2,
            responseType: "Standard Response",
            coverageType: "Standard Coverage");

        Nfpa13HighCeilingSprinklerSelectionResult result = Nfpa13HighCeilingSprinklerSelectionValidator.Validate(room, family);

        Assert.False(result.IsCompliant);
        Assert.Contains("Standard-response standard-coverage", result.Violations[0]);
        Assert.Contains("10.2.5", result.Violations[0]);
    }

    [Fact]
    public void Validate_AcceptsQuickResponseK112_ForOrdinaryHazardGroup2Above30Feet()
    {
        RoomInfo room = CreateHighCeilingRoom(HazardClassification.OrdinaryHazardGroup2);
        SprinklerFamilyInfo family = CreateFamily(
            kFactor: 11.2,
            responseType: "Quick Response",
            coverageType: "Standard Coverage");

        Nfpa13HighCeilingSprinklerSelectionResult result = Nfpa13HighCeilingSprinklerSelectionValidator.Validate(room, family);

        Assert.True(result.IsCompliant);
        Assert.True(result.AppliesHighCeilingRules);
    }

    [Fact]
    public void FindViolations_ReturnsOnlyNoncompliantHighCeilingRooms()
    {
        RoomInfo compliantRoom = CreateHighCeilingRoom(HazardClassification.OrdinaryHazardGroup2, roomNumber: "101");
        compliantRoom.SelectedSprinklerFamilyName = "QR K-11.2";

        RoomInfo violatingRoom = CreateHighCeilingRoom(HazardClassification.OrdinaryHazardGroup2, roomNumber: "102");
        violatingRoom.SelectedSprinklerFamilyName = "SR K-5.6";

        IList<Nfpa13HighCeilingSprinklerSelectionResult> violations =
            Nfpa13HighCeilingSprinklerSelectionService.FindViolations(
                new[] { compliantRoom, violatingRoom },
                room => ResolveFamilyByName(room.SelectedSprinklerFamilyName));

        Assert.Single(violations);
        Assert.False(violations[0].IsCompliant);
    }

    private static RoomInfo CreateHighCeilingRoom(
        string hazard,
        double ceilingHeightFeet = 35,
        string roomNumber = "100")
    {
        return new RoomInfo
        {
            Number = roomNumber,
            ApprovedHazardClassification = hazard,
            CeilingHeightFeet = ceilingHeightFeet,
            CeilingClassification = CeilingClassification.Flat
        };
    }

    private static SprinklerFamilyInfo CreateFamily(
        double kFactor,
        string orientation = "Pendent",
        string category = "Standard Spray Quick Response",
        string responseType = "Quick Response",
        string coverageType = "Standard Coverage")
    {
        return new SprinklerFamilyInfo
        {
            Manufacturer = "Test",
            Model = "T" + kFactor.ToString("0"),
            FamilyName = responseType + " K-" + kFactor.ToString("0.0"),
            Category = category,
            Orientation = orientation,
            ResponseType = responseType,
            CoverageType = coverageType,
            KFactor = kFactor,
            SupportedHazardClassifications =
            {
                HazardClassification.OrdinaryHazardGroup1,
                HazardClassification.OrdinaryHazardGroup2,
                HazardClassification.ExtraHazardGroup1,
                HazardClassification.ExtraHazardGroup2
            },
            SupportedCeilingClassifications = { CeilingClassification.Flat }
        };
    }

    private static SprinklerFamilyInfo ResolveFamilyByName(string displayName)
    {
        if (string.Equals(displayName, "QR K-11.2", System.StringComparison.Ordinal))
        {
            return CreateFamily(kFactor: 11.2);
        }

        if (string.Equals(displayName, "SR K-5.6", System.StringComparison.Ordinal))
        {
            return CreateFamily(kFactor: 5.6, responseType: "Standard Response");
        }

        return null;
    }
}
