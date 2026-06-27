using System;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class Nfpa13WaterSupplyValidatorTests
{
    [Fact]
    public void ValidateInput_IsCompliant_ForCompleteHydrantTest()
    {
        WaterSupplyInput input = CreateValidInput();

        Nfpa13WaterSupplyInputValidationResult result = Nfpa13WaterSupplyValidator.ValidateInput(input);

        Assert.True(result.IsCompliant);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.Contains(Nfpa13Edition.References.WaterSupplyInformation, result.Summary);
    }

    [Fact]
    public void ValidateInput_RejectsMissingRequiredFields()
    {
        Nfpa13WaterSupplyInputValidationResult result = Nfpa13WaterSupplyValidator.ValidateInput(new WaterSupplyInput());

        Assert.False(result.IsCompliant);
        Assert.Equal(4, result.Errors.Count);
    }

    [Fact]
    public void ValidateInput_RejectsResidualPressureAboveStatic()
    {
        WaterSupplyInput input = CreateValidInput();
        input.StaticPressurePsi = 60;
        input.ResidualPressurePsi = 75;

        Nfpa13WaterSupplyInputValidationResult result = Nfpa13WaterSupplyValidator.ValidateInput(input);

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Errors, error => error.Contains("Residual pressure"));
    }

    [Fact]
    public void ValidateInput_RejectsFutureTestDate()
    {
        WaterSupplyInput input = CreateValidInput();
        input.HydrantTestDate = DateTime.Today.AddDays(1);

        Nfpa13WaterSupplyInputValidationResult result = Nfpa13WaterSupplyValidator.ValidateInput(input);

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Errors, error => error.Contains("future"));
    }

    [Fact]
    public void ValidateInput_WarnsWhenTestDateIsOlderThanTwelveMonths()
    {
        WaterSupplyInput input = CreateValidInput();
        input.HydrantTestDate = DateTime.Today.AddMonths(-13);

        Nfpa13WaterSupplyInputValidationResult result = Nfpa13WaterSupplyValidator.ValidateInput(input);

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
