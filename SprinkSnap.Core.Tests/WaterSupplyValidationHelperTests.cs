using System;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;
using FireSprinklerPlugin.SprinkSnap.Core.WaterSupply;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class WaterSupplyValidationHelperTests
{
    [Fact]
    public void ValidateInput_IsCompliant_ForCompleteHydrantTest()
    {
        WaterSupplyInput input = CreateValidInput();

        WaterSupplyInputValidationResult result = WaterSupplyValidationHelper.ValidateInput(input);

        Assert.True(result.IsCompliant);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.Contains(Nfpa13Edition.References.WaterSupplyInformation, result.Summary);
    }

    [Fact]
    public void ValidateInput_RejectsMissingRequiredFields()
    {
        WaterSupplyInputValidationResult result = WaterSupplyValidationHelper.ValidateInput(new WaterSupplyInput());

        Assert.False(result.IsCompliant);
        Assert.Equal(4, result.Errors.Count);
    }

    [Fact]
    public void HasInputValidationErrors_DetectsHydrantInputMessages()
    {
        WaterSupplyValidationResult result = new WaterSupplyValidationResult
        {
            Warnings = { "Enter the measured static pressure (PSI) from the hydrant flow test." }
        };

        Assert.True(WaterSupplyValidationHelper.HasInputValidationErrors(result));
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
