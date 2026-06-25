using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;

namespace FireSprinklerPlugin.SprinkSnap.Core.Engines;

public sealed class ModelAnalysisEngine : IModelAnalysisEngine
{
    public ModelAnalysisSummary Analyze(SprinkSnapProjectState projectState)
    {
        return new ModelAnalysisSummary
        {
            RoomCount = projectState.Rooms.Count,
            SlopedCeilingCount = projectState.Rooms.Count(room => room.HasSlopedCeiling),
            MissingCeilingCount = projectState.Rooms.Count(room => room.CeilingHeightFeet <= 0),
            ObstructionZoneCount = projectState.Rooms.Sum(room => room.ObstructionCount),
            Warnings = projectState.Warnings.Select(warning => warning.Message).ToList()
        };
    }

    public string ExportJson(ModelAnalysisSummary summary)
    {
        return JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
    }
}

public sealed class HazardClassificationEngine : IHazardClassificationEngine
{
    private readonly IHazardClassifier classifier = new HazardClassifier();

    public HazardClassificationResult Classify(RoomInfo room)
    {
        return classifier.SuggestClassification(room);
    }
}

public sealed class ManufacturerRecommendationEngine : IManufacturerRecommendationEngine
{
    private readonly ICompatibleSprinklerSelector selector = new CompatibleSprinklerSelector();

    public CompatibleSprinklerSelection Recommend(
        RoomInfo room,
        IEnumerable<SprinklerFamilyInfo> catalog,
        ProjectSprinklerStandard projectStandard)
    {
        return selector.SelectForRoom(room, catalog, projectStandard);
    }
}

public sealed class WaterSupplyEngine : IWaterSupplyEngine
{
    public WaterSupplyValidationResult Validate(WaterSupplyInput input, HydraulicCalculationResult demand)
    {
        WaterSupplyValidationResult result = new WaterSupplyValidationResult();

        if (!input.StaticPressurePsi.HasValue || !input.ResidualPressurePsi.HasValue || !input.FlowAtResidualGpm.HasValue)
        {
            result.Warnings.Add("Static pressure, residual pressure, and flow at residual are required.");
            return result;
        }

        result.Curve.Add(new WaterSupplyCurvePoint { FlowGpm = 0, PressurePsi = input.StaticPressurePsi.Value });
        result.Curve.Add(new WaterSupplyCurvePoint { FlowGpm = input.FlowAtResidualGpm.Value, PressurePsi = input.ResidualPressurePsi.Value });
        result.SafetyMarginPsi = input.ResidualPressurePsi.Value - demand.SystemDemandPsi;
        result.IsAdequate = result.SafetyMarginPsi >= 0;

        if (!result.IsAdequate)
        {
            result.Warnings.Add("Available water supply is below calculated system demand.");
        }

        return result;
    }
}

public sealed class LayoutEngine : ILayoutEngine
{
    private readonly ISprinklerLayoutOptimizer optimizer = new SprinklerLayoutOptimizer();

    public AutomaticLayoutResult Generate(RoomInfo room, SprinklerFamilyInfo sprinklerFamily)
    {
        return optimizer.GenerateBestLayout(room, sprinklerFamily);
    }
}

public sealed class HydraulicEngine : IHydraulicEngine
{
    private const double DefaultKFactor = 5.6;
    private const double BranchDiameterInches = 1.25;
    private const double BranchLengthFeet = 60.0;
    private const double MainDiameterInches = 4.0;
    private const double MainLengthFeet = 120.0;

    public HydraulicCalculationResult Calculate(IEnumerable<RoomInfo> rooms, WaterSupplyInput waterSupply)
    {
        List<RoomInfo> roomList = rooms?.ToList() ?? new List<RoomInfo>();
        HydraulicCalculationResult result = new HydraulicCalculationResult();

        List<RoomInfo> designRooms = roomList
            .Where(room => room.DesignerApproved && !string.IsNullOrWhiteSpace(room.ApprovedHazardClassification))
            .ToList();

        if (designRooms.Count == 0)
        {
            result.Warnings.Add("No designer-approved rooms with hazard classifications are available for hydraulic calculation.");
            return result;
        }

        Nfpa13HydraulicDesignCriteria controllingCriteria = SelectControllingCriteria(designRooms);
        result.ControllingHazardClassification = controllingCriteria.HazardClassification;
        result.DesignDensityGpmPerSqFt = controllingCriteria.DesignDensityGpmPerSqFt;
        result.RemoteAreaSquareFeet = controllingCriteria.RemoteAreaSquareFeet;
        result.HoseStreamAllowanceGpm = controllingCriteria.HoseStreamAllowanceGpm;
        result.NfpaReference = controllingCriteria.NfpaReference;

        result.SprinklerDemandFlowGpm = controllingCriteria.DesignDensityGpmPerSqFt * controllingCriteria.RemoteAreaSquareFeet;
        result.TotalFlowGpm = result.SprinklerDemandFlowGpm + controllingCriteria.HoseStreamAllowanceGpm;

        double equivalentKFactor = ResolveEquivalentKFactor(designRooms, controllingCriteria.HazardClassification);
        result.EquivalentKFactor = equivalentKFactor;

        double remoteSprinklerPressurePsi = Math.Pow(result.SprinklerDemandFlowGpm / Math.Max(equivalentKFactor, 0.1), 2.0);
        double branchFrictionPsi = HazenWilliamsCalculator.FrictionLossPsi(
            result.TotalFlowGpm,
            BranchDiameterInches,
            BranchLengthFeet);
        double mainFrictionPsi = HazenWilliamsCalculator.FrictionLossPsi(
            result.TotalFlowGpm,
            MainDiameterInches,
            MainLengthFeet);

        result.SystemDemandPsi = remoteSprinklerPressurePsi + branchFrictionPsi + mainFrictionPsi;
        result.AvailablePressurePsi = waterSupply.ResidualPressurePsi ?? 0.0;
        result.SafetyMarginPsi = result.AvailablePressurePsi - result.SystemDemandPsi;

        result.CriticalPath.Add(new HydraulicNode
        {
            NodeId = "Remote Sprinkler",
            PressurePsi = remoteSprinklerPressurePsi,
            FlowGpm = result.SprinklerDemandFlowGpm
        });
        result.CriticalPath.Add(new HydraulicNode
        {
            NodeId = "Branch Segment",
            PressurePsi = remoteSprinklerPressurePsi + branchFrictionPsi,
            FlowGpm = result.TotalFlowGpm
        });
        result.CriticalPath.Add(new HydraulicNode
        {
            NodeId = "Riser / Source",
            PressurePsi = result.SystemDemandPsi,
            FlowGpm = result.TotalFlowGpm
        });

        if (!waterSupply.ResidualPressurePsi.HasValue)
        {
            result.Warnings.Add("Residual pressure is required to compare demand against the available water supply.");
        }
        else if (result.SafetyMarginPsi < 0)
        {
            result.Warnings.Add(
                "Calculated system demand exceeds available residual pressure by "
                + Math.Abs(result.SafetyMarginPsi).ToString("N1")
                + " PSI.");
        }

        if (designRooms.Sum(room => room.ProposedSprinklers.Count) == 0)
        {
            result.Warnings.Add("No proposed sprinkler locations found. Remote area demand uses NFPA 13 minimum area only.");
        }

        result.Warnings.Add(
            "Controlling hazard: "
            + controllingCriteria.HazardClassification
            + " at "
            + controllingCriteria.DesignDensityGpmPerSqFt.ToString("0.00")
            + " gpm/sq ft over "
            + controllingCriteria.RemoteAreaSquareFeet.ToString("N0")
            + " sq ft remote area.");

        return result;
    }

    private static Nfpa13HydraulicDesignCriteria SelectControllingCriteria(IEnumerable<RoomInfo> rooms)
    {
        Nfpa13HydraulicDesignCriteria controlling = null;
        double highestDemand = 0.0;

        foreach (IGrouping<string, RoomInfo> hazardGroup in rooms.GroupBy(room =>
                     Nfpa13HydraulicDesignTable.NormalizeHazard(room.ApprovedHazardClassification)))
        {
            Nfpa13HydraulicDesignCriteria criteria = Nfpa13HydraulicDesignTable.GetCriteria(hazardGroup.Key);
            double demand = criteria.DesignDensityGpmPerSqFt * criteria.RemoteAreaSquareFeet + criteria.HoseStreamAllowanceGpm;
            if (demand > highestDemand)
            {
                highestDemand = demand;
                controlling = criteria;
            }
        }

        return controlling ?? Nfpa13HydraulicDesignTable.GetCriteria(HazardClassification.LightHazard);
    }

    private static double ResolveEquivalentKFactor(IEnumerable<RoomInfo> rooms, string controllingHazardDisplay)
    {
        List<double> kFactors = rooms
            .Where(room => string.Equals(
                Nfpa13HydraulicDesignTable.GetCriteria(room.ApprovedHazardClassification).HazardClassification,
                controllingHazardDisplay,
                StringComparison.OrdinalIgnoreCase))
            .Select(ResolveRoomKFactor)
            .Where(kFactor => kFactor > 0)
            .ToList();

        return kFactors.Count > 0 ? kFactors.Average() : DefaultKFactor;
    }

    private static double ResolveRoomKFactor(RoomInfo room)
    {
        string sprinklerName = string.IsNullOrWhiteSpace(room.SelectedSprinklerFamilyName)
            ? room.AutoSelectedSprinklerName
            : room.SelectedSprinklerFamilyName;

        if (string.IsNullOrWhiteSpace(sprinklerName))
        {
            return 0.0;
        }

        SprinklerFamilyInfo family = new SprinklerFamilySelector()
            .GetAvailableFamilies()
            .FirstOrDefault(item => string.Equals(item.DisplayName, sprinklerName, StringComparison.OrdinalIgnoreCase));

        return family?.KFactor > 0 ? family.KFactor : 0.0;
    }
}

public sealed class MaterialTakeoffEngine : IMaterialTakeoffEngine
{
    public IReadOnlyList<MaterialTakeoffItem> Generate(IEnumerable<RoomInfo> rooms)
    {
        int sprinklerCount = rooms.Sum(room => room.ProposedSprinklers.Count);
        return new List<MaterialTakeoffItem>
        {
            new MaterialTakeoffItem
            {
                ItemType = "Sprinkler",
                Description = "Proposed sprinkler heads",
                Quantity = sprinklerCount,
                Unit = "EA"
            }
        };
    }
}

public sealed class ReportEngine : IReportEngine
{
    public IReadOnlyList<string> ExportAll(
        SprinkSnapProjectState projectState,
        HydraulicCalculationResult hydraulicResult,
        IReadOnlyList<MaterialTakeoffItem> materialTakeoff,
        ReportExportRequest request)
    {
        List<string> reports = new List<string>();
        if (request.IncludeDesignSummary) reports.Add("Design Summary PDF");
        if (request.IncludeHydraulicReport) reports.Add("Hydraulic Report PDF");
        if (request.IncludeNodeDiagram) reports.Add("Node Diagram PDF");
        if (request.IncludeMaterialTakeoff) reports.Add("Material Takeoff PDF");
        return reports;
    }
}

