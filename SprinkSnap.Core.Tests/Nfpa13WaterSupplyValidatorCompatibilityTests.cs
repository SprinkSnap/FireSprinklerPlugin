using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class Nfpa13WaterSupplyValidatorCompatibilityTests
{
    [Fact]
    public void ValidateInput_DelegatesToWaterSupplyInputValidator()
    {
        WaterSupplyInput input = new WaterSupplyInput
        {
            StaticPressurePsi = 85,
            ResidualPressurePsi = 65,
            FlowAtResidualGpm = 1200,
            HydrantTestDate = System.DateTime.Today.AddMonths(-1)
        };

        Nfpa13WaterSupplyInputValidationResult result = Nfpa13WaterSupplyValidator.ValidateInput(input);

        Assert.True(result.IsCompliant);
        Assert.Empty(result.Errors);
    }
}
