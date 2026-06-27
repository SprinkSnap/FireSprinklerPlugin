using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;
using FireSprinklerPlugin.SprinkSnap.Core.WaterSupply;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class WaterSupplyInputValidatorTests
{
    [Fact]
    public void Validate_IsCompliant_ForCompleteHydrantTest()
    {
        WaterSupplyInput input = CreateValidInput();

        WaterSupplyInputValidationResult result = WaterSupplyInputValidator.Validate(input);

        Assert.True(result.IsCompliant);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.Contains(Nfpa13Edition.References.WaterSupplyInformation, result.Summary);
    }

    [Fact]
    public void Validate_RejectsMissingRequiredFields()
    {
        WaterSupplyInputValidationResult result = WaterSupplyInputValidator.Validate(new WaterSupplyInput());

        Assert.False(result.IsCompliant);
        Assert.Equal(4, result.Errors.Count);
    }

    [Fact]
    public void Validate_RejectsResidualPressureAboveStatic()
    {
        WaterSupplyInput input = CreateValidInput();
        input.StaticPressurePsi = 60;
        input.ResidualPressurePsi = 75;

        WaterSupplyInputValidationResult result = WaterSupplyInputValidator.Validate(input);

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Errors, error => error.Contains("Residual pressure"));
    }

    [Fact]
    public void Validate_RejectsFutureTestDate()
    {
        WaterSupplyInput input = CreateValidInput();
        input.HydrantTestDate = DateTime.Today.AddDays(1);

        WaterSupplyInputValidationResult result = WaterSupplyInputValidator.Validate(input);

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Errors, error => error.Contains("future"));
    }

    [Fact]
    public void Validate_WarnsWhenTestDateIsOlderThanTwelveMonths()
    {
        WaterSupplyInput input = CreateValidInput();
        input.HydrantTestDate = DateTime.Today.AddMonths(-13);

        WaterSupplyInputValidationResult result = WaterSupplyInputValidator.Validate(input);

        Assert.True(result.IsCompliant);
        Assert.Single(result.Warnings);
        Assert.Contains("12 months", result.Warnings[0]);
    }

    private static WaterSupplyInput CreateValidInput()
    {
        return new WaterSupplyInput
        {
            StaticPressurePsi = 85,
            ResidualPressurePsi = 65,
            FlowAtResidualGpm = 1200,
            HydrantTestDate = DateTime.Today.AddMonths(-2)
        };
    }
}
