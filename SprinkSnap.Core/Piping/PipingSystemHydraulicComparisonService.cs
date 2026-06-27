using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Data;
using FireSprinklerPlugin.SprinkSnap.Core.Engines;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public static class PipingSystemHydraulicComparisonService
{
    public static PipingSystemHydraulicComparisonResult CompareTreeAndGrid(
        SprinkSnapProjectState projectState,
        IHydraulicEngine hydraulicEngine)
    {
        PipingSystemHydraulicComparisonResult comparison = new PipingSystemHydraulicComparisonResult
        {
            NfpaReference = Nfpa13Edition.References.HydraulicCalculationProcedures
                + "; "
                + Nfpa13Edition.References.HazenWilliamsCFactors
        };

        if (projectState == null || hydraulicEngine == null)
        {
            comparison.ComparisonSummary = "Project state and hydraulic engine are required for piping scenario comparison.";
            return comparison;
        }

        if (!HasMinimumInputs(projectState, out string missingInputMessage))
        {
            comparison.ComparisonSummary = missingInputMessage;
            return comparison;
        }

        comparison.Scenarios.Add(EvaluateScenario(
            projectState,
            hydraulicEngine,
            PipingSystemTypes.Tree,
            PipeScheduleTypes.Schedule40));
        comparison.Scenarios.Add(EvaluateScenario(
            projectState,
            hydraulicEngine,
            PipingSystemTypes.Grid,
            PipeScheduleTypes.Schedule10));

        PipingSystemScenarioResult recommended = SelectRecommendedScenario(comparison.Scenarios);
        recommended.IsRecommended = true;
        comparison.RecommendedPipingSystemType = recommended.PipingSystemType;
        comparison.RecommendedPipeSchedule = recommended.PipeSchedule;
        comparison.ComparisonSummary = BuildComparisonSummary(comparison.Scenarios, recommended);
        comparison.ComparedAtUtc = DateTime.UtcNow;
        return comparison;
    }

    public static void ApplyScenario(
        SprinkSnapProjectState projectState,
        PipingSystemScenarioResult scenario,
        IHydraulicEngine hydraulicEngine = null)
    {
        if (projectState == null || scenario == null)
        {
            return;
        }

        if (projectState.Preferences == null)
        {
            projectState.Preferences = new SprinkSnapProjectPreferences();
        }

        projectState.Preferences.PipingSystemType = PipingSystemTypes.Normalize(scenario.PipingSystemType);
        projectState.Preferences.DefaultPipeSchedule = PipeScheduleTypes.Normalize(scenario.PipeSchedule);
        projectState.Preferences.HazenWilliamsC = scenario.HazenWilliamsC > 0
            ? scenario.HazenWilliamsC
            : PipeScheduleDefaults.ResolveHazenWilliamsC(projectState.Preferences);
        PipeScheduleDefaults.ApplyRecommendedDefaults(projectState.Preferences);

        SchematicPipeRoutingSummary routing = SchematicPipeRouter.RouteProject(
            projectState.Rooms,
            projectState.Preferences);
        ProjectTrunkRouter.EnsureProjectTrunk(
            routing,
            projectState.Rooms,
            projectState.HydraulicSupplyAnchor,
            PipeDiameterDefaults.ResolveMainDiameterInches(projectState.Preferences));
        projectState.SchematicPipeRouting = routing;

        HydraulicCalculationResult hydraulicResult = scenario.HydraulicResult;
        if (hydraulicEngine != null)
        {
            hydraulicResult = hydraulicEngine.Calculate(
                projectState.Rooms,
                projectState.WaterSupply,
                projectState.PlacementSummary,
                routing,
                projectState.PipePlacementSummary,
                projectState.HydraulicSupplyAnchor,
                projectState.Preferences);
        }

        projectState.HydraulicResult = hydraulicResult;
        projectState.PipingSystemComparison = new PipingSystemHydraulicComparisonResult
        {
            Scenarios = new List<PipingSystemScenarioResult> { scenario },
            RecommendedPipingSystemType = scenario.PipingSystemType,
            RecommendedPipeSchedule = scenario.PipeSchedule,
            ComparisonSummary = "Applied "
                + scenario.PipingSystemType
                + " / "
                + scenario.PipeSchedule
                + " routing to the project.",
            NfpaReference = scenario.NfpaReference,
            ComparedAtUtc = DateTime.UtcNow
        };
        HydraulicCalculationPipelineService.FinalizeSession(projectState, hydraulicResult);
    }

    private static PipingSystemScenarioResult EvaluateScenario(
        SprinkSnapProjectState projectState,
        IHydraulicEngine hydraulicEngine,
        string pipingSystemType,
        string pipeSchedule)
    {
        SprinkSnapProjectPreferences scenarioPreferences = ClonePreferencesForScenario(
            projectState.Preferences,
            pipingSystemType,
            pipeSchedule);

        SchematicPipeRoutingSummary routing = SchematicPipeRouter.RouteProject(
            projectState.Rooms,
            scenarioPreferences);
        ProjectTrunkRouter.EnsureProjectTrunk(
            routing,
            projectState.Rooms,
            projectState.HydraulicSupplyAnchor,
            PipeDiameterDefaults.ResolveMainDiameterInches(scenarioPreferences));

        HydraulicCalculationResult hydraulicResult = hydraulicEngine.Calculate(
            projectState.Rooms,
            projectState.WaterSupply,
            projectState.PlacementSummary,
            routing,
            projectState.PipePlacementSummary,
            projectState.HydraulicSupplyAnchor,
            scenarioPreferences);

        PipingSystemScenarioResult scenario = new PipingSystemScenarioResult
        {
            PipingSystemType = pipingSystemType,
            PipeSchedule = pipeSchedule,
            HazenWilliamsC = PipeScheduleDefaults.ResolveHazenWilliamsC(scenarioPreferences),
            NfpaReference = Nfpa13Edition.References.HydraulicCalculationProcedures,
            SchematicSegmentCount = routing.TotalSegmentCount,
            SchematicPipeLengthFeet = routing.TotalLengthFeet,
            HydraulicResult = hydraulicResult,
            Summary = BuildScenarioSummary(pipingSystemType, pipeSchedule, hydraulicResult, routing)
        };

        return scenario;
    }

    private static SprinkSnapProjectPreferences ClonePreferencesForScenario(
        SprinkSnapProjectPreferences source,
        string pipingSystemType,
        string pipeSchedule)
    {
        SprinkSnapProjectPreferences preferences = source == null
            ? new SprinkSnapProjectPreferences()
            : new SprinkSnapProjectPreferences
            {
                PreferredManufacturer = source.PreferredManufacturer,
                DefaultCategory = source.DefaultCategory,
                DefaultOrientation = source.DefaultOrientation,
                DefaultKFactor = source.DefaultKFactor,
                DefaultBranchDiameterInches = source.DefaultBranchDiameterInches,
                DefaultMainDiameterInches = source.DefaultMainDiameterInches,
                BranchVelocityLimitFeetPerSecond = source.BranchVelocityLimitFeetPerSecond,
                MainVelocityLimitFeetPerSecond = source.MainVelocityLimitFeetPerSecond,
                AllowAlternateManufacturers = source.AllowAlternateManufacturers,
                CatalogPath = source.CatalogPath
            };

        preferences.PipingSystemType = PipingSystemTypes.Normalize(pipingSystemType);
        preferences.DefaultPipeSchedule = PipeScheduleTypes.Normalize(pipeSchedule);
        preferences.HazenWilliamsC = PipeScheduleDefaults.ResolveHazenWilliamsC(preferences);
        PipeScheduleDefaults.ApplyRecommendedDefaults(preferences);
        return preferences;
    }

    private static PipingSystemScenarioResult SelectRecommendedScenario(IList<PipingSystemScenarioResult> scenarios)
    {
        PipingSystemScenarioResult bestPassing = scenarios
            .Where(scenario => scenario.MeetsSupplyDemand)
            .OrderByDescending(scenario => scenario.HydraulicResult.SafetyMarginPsi)
            .ThenBy(scenario => scenario.SchematicPipeLengthFeet)
            .FirstOrDefault();

        if (bestPassing != null)
        {
            return bestPassing;
        }

        return scenarios
            .OrderByDescending(scenario => scenario.HydraulicResult.SafetyMarginPsi)
            .ThenBy(scenario => scenario.SchematicPipeLengthFeet)
            .First();
    }

    private static string BuildScenarioSummary(
        string pipingSystemType,
        string pipeSchedule,
        HydraulicCalculationResult hydraulicResult,
        SchematicPipeRoutingSummary routing)
    {
        return pipingSystemType
            + " / "
            + pipeSchedule
            + ": "
            + hydraulicResult.TotalFlowGpm.ToString("N0")
            + " GPM at "
            + hydraulicResult.SystemDemandPsi.ToString("N1")
            + " PSI demand; margin "
            + hydraulicResult.SafetyMarginPsi.ToString("N1")
            + " PSI; "
            + routing.TotalSegmentCount
            + " schematic segment(s), "
            + routing.TotalLengthFeet.ToString("N0")
            + " ft.";
    }

    private static string BuildComparisonSummary(
        IList<PipingSystemScenarioResult> scenarios,
        PipingSystemScenarioResult recommended)
    {
        if (scenarios.Count == 0)
        {
            return "No piping scenarios were evaluated.";
        }

        string marginSummary = string.Join(
            "; ",
            scenarios.Select(scenario =>
                scenario.PipingSystemType
                + " margin "
                + scenario.HydraulicResult.SafetyMarginPsi.ToString("N1")
                + " PSI"));

        return "Compared "
            + scenarios.Count
            + " "
            + Nfpa13Edition.ShortLabel
            + " routing scenario(s) using the same water supply and remote-area demand. "
            + marginSummary
            + ". Recommended: "
            + recommended.PipingSystemType
            + " / "
            + recommended.PipeSchedule
            + ".";
    }

    private static bool HasMinimumInputs(SprinkSnapProjectState projectState, out string message)
    {
        message = string.Empty;
        if (projectState.Rooms == null || projectState.Rooms.Count == 0)
        {
            message = "Analyze Model and approve hazards before comparing piping scenarios.";
            return false;
        }

        bool hasDesignRooms = projectState.Rooms.Any(room =>
            room.DesignerApproved
            && !string.IsNullOrWhiteSpace(room.ApprovedHazardClassification)
            && room.ProposedSprinklers.Count > 0);
        if (!hasDesignRooms)
        {
            message = "Generate sprinkler layout candidates before comparing tree and grid routing scenarios.";
            return false;
        }

        if (!projectState.WaterSupply.StaticPressurePsi.HasValue
            || !projectState.WaterSupply.ResidualPressurePsi.HasValue
            || !projectState.WaterSupply.FlowAtResidualGpm.HasValue)
        {
            message = "Enter static pressure, residual pressure, and flow at residual in Water Supply before comparing scenarios.";
            return false;
        }

        return true;
    }
}
