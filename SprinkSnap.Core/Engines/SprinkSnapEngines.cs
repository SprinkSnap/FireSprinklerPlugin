using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FireSprinklerPlugin.SprinkSnap.Core.Data;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using FireSprinklerPlugin.SprinkSnap.Core.Placement;
using FireSprinklerPlugin.SprinkSnap.Core.Reports;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;
using FireSprinklerPlugin.SprinkSnap.Core.WaterSupply;

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

        WaterSupplyInputValidationResult inputValidation = WaterSupplyValidationHelper.ValidateInput(input);
        foreach (string warning in inputValidation.Warnings)
        {
            result.Warnings.Add(warning);
        }

        if (!inputValidation.IsCompliant)
        {
            foreach (string error in inputValidation.Errors)
            {
                result.Warnings.Add(error);
            }

            result.IsAdequate = false;
            return result;
        }

        demand ??= new HydraulicCalculationResult();
        if (demand.TotalFlowGpm <= 0)
        {
            result.Warnings.Add(
                "System demand is not available. Approve hazards and generate layout before comparing supply to demand.");
            result.IsAdequate = false;
            return result;
        }

        result.Curve = WaterSupplyCurveCalculator.BuildCurve(input);
        result.DemandCurve = demand.DemandCurve?.ToList() ?? new List<WaterSupplyCurvePoint>();
        double availableAtDemand = WaterSupplyCurveCalculator.GetPressureAtFlow(input, demand.TotalFlowGpm);
        result.SafetyMarginPsi = availableAtDemand - demand.SystemDemandPsi;
        result.IsAdequate = result.SafetyMarginPsi >= 0;

        if (!result.IsAdequate)
        {
            result.Warnings.Add(
                "Available supply pressure at "
                + demand.TotalFlowGpm.ToString("N0")
                + " GPM is below calculated system demand by "
                + Math.Abs(result.SafetyMarginPsi).ToString("N1")
                + " PSI ("
                + Nfpa13Edition.References.HydraulicGraphSheet
                + ").");
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

    public HydraulicCalculationResult Calculate(
        IEnumerable<RoomInfo> rooms,
        WaterSupplyInput waterSupply,
        SprinklerPlacementSummary placementSummary = null,
        SchematicPipeRoutingSummary schematicPipeRouting = null,
        PipePlacementSummary pipePlacementSummary = null,
        HydraulicSupplyAnchor supplyAnchor = null,
        SprinkSnapProjectPreferences preferences = null)
    {
        double branchDiameterInches = PipeDiameterDefaults.ResolveBranchDiameterInches(preferences);
        double mainDiameterInches = PipeDiameterDefaults.ResolveMainDiameterInches(preferences);
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

        List<RoomInfo> controllingRooms = SelectControllingRooms(designRooms);
        Nfpa13HydraulicDesignCriteria controllingCriteria = SelectControllingCriteria(designRooms);
        result.ControllingHazardClassification = controllingCriteria.HazardClassification;
        result.DesignDensityGpmPerSqFt = controllingCriteria.DesignDensityGpmPerSqFt;
        result.RemoteAreaSquareFeet = controllingCriteria.RemoteAreaSquareFeet;
        result.HoseStreamAllowanceGpm = controllingCriteria.HoseStreamAllowanceGpm;
        result.NfpaReference = controllingCriteria.NfpaReference;
        result.UsesHighCeilingAdjustment = controllingCriteria.AppliesHighCeilingAdjustment;
        result.HighCeilingAdjustmentSummary = controllingCriteria.HighCeilingAdjustmentSummary ?? string.Empty;
        result.ControllingCeilingHeightFeet = ResolveControllingCeilingHeight(controllingRooms);

        if (Nfpa13HighCeilingDesignCriteriaAdjuster.RequiresHighCeilingEvaluation(controllingCriteria.HazardClassification)
            && result.ControllingCeilingHeightFeet <= 0)
        {
            result.Warnings.Add(
                "Controlling ceiling height is not available. "
                + Nfpa13Edition.References.HighCeilingDesignCriteria
                + " adjustments were not applied — verify room ceiling heights.");
        }
        else if (result.UsesHighCeilingAdjustment
            && !string.IsNullOrWhiteSpace(result.HighCeilingAdjustmentSummary))
        {
            result.Warnings.Add(result.HighCeilingAdjustmentSummary + " (" + Nfpa13Edition.References.HighCeilingDesignCriteria + ").");
        }

        AppendHighCeilingSprinklerSelectionWarnings(result, controllingRooms);

        SprinklerFamilyInfo representativeFamily = controllingRooms
            .Select(RemoteAreaHydraulicCalculator.ResolveRepresentativeFamily)
            .FirstOrDefault(family => family != null);
        result.MaxCoverageSquareFeet = RemoteAreaHydraulicCalculator.ResolveMaxCoverageSquareFeet(representativeFamily);

        int availableHeadCount = ResolveAvailableHeadCount(controllingRooms, placementSummary);
        result.OperatingSprinklerCount = RemoteAreaHydraulicCalculator.CalculateOperatingSprinklerCount(
            controllingCriteria.RemoteAreaSquareFeet,
            result.MaxCoverageSquareFeet,
            availableHeadCount);

        result.SprinklerDemandFlowGpm = controllingCriteria.DesignDensityGpmPerSqFt * controllingCriteria.RemoteAreaSquareFeet;
        result.FlowPerOperatingSprinklerGpm = result.SprinklerDemandFlowGpm / Math.Max(result.OperatingSprinklerCount, 1);
        result.TotalFlowGpm = result.SprinklerDemandFlowGpm + controllingCriteria.HoseStreamAllowanceGpm;

        double equivalentKFactor = ResolveEquivalentKFactor(controllingRooms, controllingCriteria.HazardClassification);
        result.EquivalentKFactor = equivalentKFactor;

        LayoutLinkedHydraulicPath layoutPath = LayoutLinkedHydraulicCalculator.Calculate(
            controllingRooms,
            result.OperatingSprinklerCount,
            result.FlowPerOperatingSprinklerGpm,
            controllingCriteria.HoseStreamAllowanceGpm,
            equivalentKFactor,
            branchDiameterInches,
            mainDiameterInches,
            schematicPipeRouting,
            pipePlacementSummary,
            supplyAnchor,
            controllingCriteria.RemoteAreaSquareFeet,
            result.MaxCoverageSquareFeet,
            preferences);

        result.UsesLayoutLinkedHydraulics = layoutPath.UsesLayoutGeometry;
        result.UsesSegmentGraphHydraulics = layoutPath.UsesSegmentGraphHydraulics;
        result.UsesProjectTrunk = layoutPath.UsesProjectTrunk;
        result.UsesRemoteAreaSelection = layoutPath.UsesRemoteAreaSelection;
        result.UsesUserSupplyAnchor = layoutPath.UsesUserSupplyAnchor;
        result.UserSupplyAnchorLabel = layoutPath.UserSupplyAnchorLabel ?? string.Empty;
        result.CriticalPathSegmentCount = layoutPath.CriticalPathSegmentCount;
        result.FittingFrictionPsi = layoutPath.FittingFrictionPsi;
        result.CriticalPathFittingCount = layoutPath.CriticalPathFittingCount;
        result.CriticalPathVelocityViolationCount = layoutPath.CriticalPathVelocityViolationCount;
        result.MaxCriticalPathVelocityFeetPerSecond = layoutPath.MaxCriticalPathVelocityFeetPerSecond;
        result.CriticalPathDiameterSuggestionCount = layoutPath.CriticalPathDiameterSuggestionCount;
        result.UsesAppliedPipeSizing = layoutPath.UsesAppliedPipeSizing;
        result.AppliedPipeSizingSegmentCount = layoutPath.AppliedPipeSizingSegmentCount;
        result.UsesSchematicPipeSizingWriteback = layoutPath.UsesSchematicPipeSizingWriteback;
        result.SchematicWritebackSegmentCount = layoutPath.SchematicWritebackSegmentCount;
        result.UsesPlacedPipeLengths = layoutPath.UsesPlacedPipeLengths;
        result.UsesPlacedPipeTopology = layoutPath.UsesPlacedPipeTopology;
        result.PipeLengthDataSource = layoutPath.PipeLengthDataSource ?? string.Empty;
        result.BranchLengthFeet = layoutPath.BranchLengthFeet;
        result.MainLengthFeet = layoutPath.MainLengthFeet;
        result.TotalPipeLengthFeet = layoutPath.TotalPipeLengthFeet;
        result.RemoteSprinklerLabel = layoutPath.MostRemoteSprinkler?.Label ?? string.Empty;
        result.SystemDemandPsi = layoutPath.UsesSegmentGraphHydraulics && layoutPath.CriticalPathDemandPsi > 0
            ? layoutPath.CriticalPathDemandPsi
            : layoutPath.JunctionPressurePsi + layoutPath.MainFrictionPsi + layoutPath.FittingFrictionPsi;
        result.FlowPerOperatingSprinklerGpm = layoutPath.OperatingSprinklers.Count > 0
            ? layoutPath.CalculatedSprinklerFlowGpm / layoutPath.OperatingSprinklers.Count
            : result.FlowPerOperatingSprinklerGpm;
        result.CriticalPath = layoutPath.CriticalPath?.ToList() ?? new List<HydraulicNode>();
        result.SprinklerDemandPressurePsi = Nfpa13HydraulicGraphCalculator.ComputeSprinklerDemandPressureAtSource(
            result.SystemDemandPsi,
            result.SprinklerDemandFlowGpm,
            result.TotalFlowGpm,
            layoutPath.MainDiameterInches > 0 ? layoutPath.MainDiameterInches : mainDiameterInches,
            layoutPath.MainLengthFeet);
        result.DemandCurve = Nfpa13HydraulicGraphCalculator.BuildDemandCurve(
            result.SprinklerDemandFlowGpm,
            result.SprinklerDemandPressurePsi,
            result.TotalFlowGpm,
            result.SystemDemandPsi);
        result.DemandFlowGpm = result.TotalFlowGpm;
        result.DemandPressurePsi = result.SystemDemandPsi;
        result.SupplyCurve = WaterSupplyCurveCalculator.BuildCurve(waterSupply);
        result.AvailablePressurePsi = WaterSupplyCurveCalculator.GetPressureAtFlow(waterSupply, result.TotalFlowGpm);
        result.SafetyMarginPsi = result.AvailablePressurePsi - result.SystemDemandPsi;

        foreach (string warning in layoutPath.Warnings)
        {
            result.Warnings.Add(warning);
        }

        if (!waterSupply.ResidualPressurePsi.HasValue)
        {
            result.Warnings.Add("Residual pressure is required to compare demand against the available water supply.");
        }
        else if (result.SafetyMarginPsi < 0)
        {
            result.Warnings.Add(
                "Calculated system demand exceeds available supply pressure at "
                + result.TotalFlowGpm.ToString("N0")
                + " GPM by "
                + Math.Abs(result.SafetyMarginPsi).ToString("N1")
                + " PSI.");
        }

        if (placementSummary != null && placementSummary.PlacedCount > 0)
        {
            result.Warnings.Add("Hydraulic demand uses " + result.OperatingSprinklerCount + " operating sprinkler(s) with placed Revit heads considered.");
        }
        else if (layoutPath.UsesPlacedPipeLengths)
        {
            result.Warnings.Add(
                "Critical path uses placed Revit pipe lengths for most remote head "
                + (string.IsNullOrWhiteSpace(result.RemoteSprinklerLabel) ? string.Empty : result.RemoteSprinklerLabel + " ")
                + "with "
                + result.BranchLengthFeet.ToString("N0")
                + " ft branch and "
                + result.MainLengthFeet.ToString("N0")
                + " ft main.");
        }
        else if (layoutPath.UsesLayoutGeometry)
        {
            result.Warnings.Add(
                "Critical path uses most remote layout head "
                + (string.IsNullOrWhiteSpace(result.RemoteSprinklerLabel) ? string.Empty : result.RemoteSprinklerLabel + " ")
                + "with "
                + result.BranchLengthFeet.ToString("N0")
                + " ft branch and "
                + result.MainLengthFeet.ToString("N0")
                + " ft main.");
        }
        else if (designRooms.Sum(room => room.ProposedSprinklers.Count) == 0)
        {
            result.Warnings.Add("No proposed sprinkler locations found. Remote area demand uses " + Nfpa13Edition.ShortLabel + " minimum area only.");
        }
        else
        {
            result.Warnings.Add(
                "Operating sprinklers in remote area: "
                + result.OperatingSprinklerCount
                + " at "
                + result.FlowPerOperatingSprinklerGpm.ToString("N1")
                + " GPM each.");
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

    private static List<RoomInfo> SelectControllingRooms(IEnumerable<RoomInfo> designRooms)
    {
        Nfpa13HydraulicDesignCriteria controllingCriteria = SelectControllingCriteria(designRooms);
        return designRooms
            .Where(room => string.Equals(
                Nfpa13HydraulicDesignTable.GetCriteria(room.ApprovedHazardClassification).HazardClassification,
                controllingCriteria.HazardClassification,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static int ResolveAvailableHeadCount(
        IEnumerable<RoomInfo> controllingRooms,
        SprinklerPlacementSummary placementSummary)
    {
        List<RoomInfo> rooms = controllingRooms?.ToList() ?? new List<RoomInfo>();
        if (placementSummary != null && placementSummary.PlacedCount > 0)
        {
            HashSet<int> placedRoomIds = placementSummary.RoomResults
                .Where(roomResult => roomResult.PlacedCount > 0)
                .Select(roomResult => roomResult.RoomRevitElementId)
                .ToHashSet();
            int placedInControlling = rooms
                .Where(room => placedRoomIds.Contains(room.RevitElementId))
                .Sum(room => room.ProposedSprinklers.Count);
            if (placedInControlling > 0)
            {
                return placedInControlling;
            }
        }

        return rooms.Sum(room => room.ProposedSprinklers.Count);
    }

    private static Nfpa13HydraulicDesignCriteria SelectControllingCriteria(IEnumerable<RoomInfo> rooms)
    {
        Nfpa13HydraulicDesignCriteria controlling = null;
        double highestDemand = 0.0;

        foreach (IGrouping<string, RoomInfo> hazardGroup in rooms.GroupBy(room =>
                     Nfpa13HydraulicDesignTable.NormalizeHazard(room.ApprovedHazardClassification)))
        {
            Nfpa13HydraulicDesignCriteria baseCriteria = Nfpa13HydraulicDesignTable.GetCriteria(hazardGroup.Key);
            double ceilingHeightFeet = ResolveGroupCeilingHeight(hazardGroup);
            double representativeKFactor = ResolveGroupKFactor(hazardGroup);
            bool usesExtendedCoverageK252 = ResolveGroupUsesExtendedCoverageK252(hazardGroup);
            Nfpa13HydraulicDesignCriteria criteria = Nfpa13HighCeilingDesignCriteriaAdjuster.Apply(
                hazardGroup.Key,
                baseCriteria,
                ceilingHeightFeet,
                representativeKFactor,
                usesExtendedCoverageK252);
            double demand = criteria.DesignDensityGpmPerSqFt * criteria.RemoteAreaSquareFeet + criteria.HoseStreamAllowanceGpm;
            if (demand > highestDemand)
            {
                highestDemand = demand;
                controlling = criteria;
            }
        }

        return controlling ?? Nfpa13HydraulicDesignTable.GetCriteria(HazardClassification.LightHazard);
    }

    private static double ResolveControllingCeilingHeight(IEnumerable<RoomInfo> controllingRooms)
    {
        return ResolveGroupCeilingHeight(controllingRooms ?? Enumerable.Empty<RoomInfo>());
    }

    private static double ResolveGroupCeilingHeight(IEnumerable<RoomInfo> rooms)
    {
        List<double> ceilingHeights = rooms
            .Select(room => room.CeilingHeightFeet)
            .Where(height => height > 0)
            .ToList();

        return ceilingHeights.Count > 0 ? ceilingHeights.Max() : 0.0;
    }

    private static double ResolveGroupKFactor(IEnumerable<RoomInfo> rooms)
    {
        List<double> kFactors = rooms
            .Select(ResolveRoomKFactor)
            .Where(kFactor => kFactor > 0)
            .ToList();

        return kFactors.Count > 0 ? kFactors.Max() : DefaultKFactor;
    }

    private static bool ResolveGroupUsesExtendedCoverageK252(IEnumerable<RoomInfo> rooms)
    {
        foreach (RoomInfo room in rooms)
        {
            SprinklerFamilyInfo family = ResolveRoomSprinklerFamily(room);
            if (Nfpa13HighCeilingDesignCriteriaAdjuster.UsesExtendedCoverageK252OrGreater(family))
            {
                return true;
            }
        }

        return false;
    }

    private static SprinklerFamilyInfo ResolveRoomSprinklerFamily(RoomInfo room)
    {
        string sprinklerName = string.IsNullOrWhiteSpace(room.SelectedSprinklerFamilyName)
            ? room.AutoSelectedSprinklerName
            : room.SelectedSprinklerFamilyName;

        if (string.IsNullOrWhiteSpace(sprinklerName))
        {
            return null;
        }

        return new SprinklerFamilySelector()
            .GetAvailableFamilies()
            .FirstOrDefault(item => string.Equals(item.DisplayName, sprinklerName, StringComparison.OrdinalIgnoreCase));
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
        SprinklerFamilyInfo family = ResolveRoomSprinklerFamily(room);
        return family?.KFactor > 0 ? family.KFactor : DefaultKFactor;
    }

    private static void AppendHighCeilingSprinklerSelectionWarnings(
        HydraulicCalculationResult result,
        IEnumerable<RoomInfo> controllingRooms)
    {
        IList<Nfpa13HighCeilingSprinklerSelectionResult> violations =
            Nfpa13HighCeilingSprinklerSelectionService.FindViolations(
                controllingRooms,
                ResolveRoomSprinklerFamily);

        result.HighCeilingSprinklerSelectionCompliant = violations.Count == 0;
        if (violations.Count == 0)
        {
            result.HighCeilingSprinklerViolationSummary = string.Empty;
            return;
        }

        result.HighCeilingSprinklerViolationSummary =
            violations.Count
            + " controlling room(s) violate "
            + Nfpa13Edition.References.HighCeilingSprinklerSelection
            + ".";

        foreach (RoomInfo room in controllingRooms ?? Enumerable.Empty<RoomInfo>())
        {
            if (room.CeilingHeightFeet <= Nfpa13HighCeilingDesignCriteriaAdjuster.HighCeilingThresholdFeet)
            {
                continue;
            }

            Nfpa13HighCeilingSprinklerSelectionResult validation =
                Nfpa13HighCeilingSprinklerSelectionValidator.Validate(room, ResolveRoomSprinklerFamily(room));
            if (!validation.IsCompliant)
            {
                string roomLabel = string.IsNullOrWhiteSpace(room.Number) ? room.Name : room.Number;
                foreach (string violation in validation.Violations)
                {
                    result.Warnings.Add("Room " + roomLabel + ": " + violation);
                }
            }
        }

        result.Warnings.Add(
            Nfpa13HighCeilingSprinklerSelectionService.ProjectViolationBlockReason
            + " ("
            + Nfpa13Edition.References.HighCeilingSprinklerSelection
            + ").");
    }
}

public sealed class ReportEngine : IReportEngine
{
    public ReportExportResult ExportAll(
        SprinkSnapProjectState projectState,
        HydraulicCalculationResult hydraulicResult,
        IReadOnlyList<MaterialTakeoffItem> materialTakeoff,
        ReportExportRequest request)
    {
        return SprinkSnapPdfReportExporter.ExportAll(
            projectState,
            hydraulicResult,
            materialTakeoff,
            request);
    }
}

