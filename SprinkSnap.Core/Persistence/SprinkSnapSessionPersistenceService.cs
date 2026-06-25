using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;
using FireSprinklerPlugin.SprinkSnap.Core.Data;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Mapping;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using FireSprinklerPlugin.SprinkSnap.Core.Placement;

namespace FireSprinklerPlugin.SprinkSnap.Core.Persistence;

public static class SprinkSnapSessionPersistenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static SprinkSnapSessionSnapshot CreateSnapshot(SprinkSnapProjectState state, string documentKey)
    {
        if (state == null)
        {
            return new SprinkSnapSessionSnapshot();
        }

        return new SprinkSnapSessionSnapshot
        {
            SchemaVersion = SprinkSnapSessionSnapshot.CurrentSchemaVersion,
            SavedUtc = DateTime.UtcNow,
            DocumentKey = documentKey ?? string.Empty,
            Fingerprint = ComputeFingerprint(state.Rooms, state.ModelAnalysis.LinkedModelCount),
            SessionProgress = CloneSessionProgress(state.SessionProgress),
            WaterSupply = CloneWaterSupply(state.WaterSupply),
            FamilyMappingOverrides = state.FamilyMappingOverrides.ToList(),
            LinkedModelScanOptions = state.LinkedModelScanOptions.ToList(),
            Rooms = state.Rooms.Select(CreateRoomSnapshot).ToList(),
            ClashSummary = state.ClashSummary,
            PlacementSummary = state.PlacementSummary,
            HydraulicResult = state.HydraulicResult,
            WaterSupplyValidation = state.WaterSupplyValidation,
            SchematicPipeRouting = state.SchematicPipeRouting,
            PipePlacementSummary = state.PipePlacementSummary,
            HydraulicSupplyAnchor = CloneSupplyAnchor(state.HydraulicSupplyAnchor),
            Preferences = ClonePreferences(state.Preferences),
            ReportExport = CloneReportExport(state.ReportExport)
        };
    }

    public static void ApplySnapshot(SprinkSnapProjectState state, SprinkSnapSessionSnapshot snapshot)
    {
        if (state == null || snapshot == null)
        {
            return;
        }

        if (snapshot.SessionProgress != null)
        {
            state.SessionProgress = CloneSessionProgress(snapshot.SessionProgress);
        }

        if (snapshot.WaterSupply != null)
        {
            state.WaterSupply = CloneWaterSupply(snapshot.WaterSupply);
        }

        state.FamilyMappingOverrides = snapshot.FamilyMappingOverrides?.ToList() ?? new List<SprinklerFamilyMappingOverride>();
        state.LinkedModelScanOptions = snapshot.LinkedModelScanOptions?.ToList() ?? new List<LinkedModelScanOption>();
        state.ClashSummary = snapshot.ClashSummary ?? new ClashDetectionSummary();
        state.PlacementSummary = snapshot.PlacementSummary ?? new SprinklerPlacementSummary();
        state.HydraulicResult = snapshot.HydraulicResult ?? new HydraulicCalculationResult();
        state.WaterSupplyValidation = snapshot.WaterSupplyValidation ?? new WaterSupplyValidationResult();
        state.SchematicPipeRouting = snapshot.SchematicPipeRouting ?? new SchematicPipeRoutingSummary();
        state.PipePlacementSummary = snapshot.PipePlacementSummary ?? new PipePlacementSummary();
        state.HydraulicSupplyAnchor = CloneSupplyAnchor(snapshot.HydraulicSupplyAnchor);
        state.Preferences = ClonePreferences(snapshot.Preferences);
        state.ReportExport = CloneReportExport(snapshot.ReportExport);

        Dictionary<int, PersistedRoomSnapshot> roomSnapshots = (snapshot.Rooms ?? new List<PersistedRoomSnapshot>())
            .Where(room => room.RevitElementId > 0)
            .GroupBy(room => room.RevitElementId)
            .ToDictionary(group => group.Key, group => group.Last());

        foreach (RoomInfo room in state.Rooms)
        {
            if (!roomSnapshots.TryGetValue(room.RevitElementId, out PersistedRoomSnapshot persistedRoom))
            {
                continue;
            }

            ApplyRoomSnapshot(room, persistedRoom);
        }
    }

    public static ModelChangeAssessment AssessModelChangeFromSnapshot(
        SprinkSnapSessionSnapshot snapshot,
        IEnumerable<RoomInfo> currentRooms,
        int currentLinkedModelCount)
    {
        ModelChangeAssessment assessment = new ModelChangeAssessment();
        if (snapshot?.Fingerprint == null || string.IsNullOrWhiteSpace(snapshot.Fingerprint.Hash))
        {
            assessment.Messages.Add("No saved SprinkSnap analysis baseline found for this project.");
            return assessment;
        }

        assessment.HasBaseline = true;
        List<RoomInfo> rooms = currentRooms?.ToList() ?? new List<RoomInfo>();
        ModelAnalysisFingerprint currentFingerprint = ComputeFingerprint(rooms, currentLinkedModelCount);
        if (string.Equals(snapshot.Fingerprint.Hash, currentFingerprint.Hash, StringComparison.Ordinal))
        {
            assessment.Messages.Add("Revit model matches the last saved SprinkSnap analysis baseline.");
            return assessment;
        }

        assessment.IsStale = true;
        Dictionary<int, PersistedRoomSnapshot> savedRooms = (snapshot.Rooms ?? new List<PersistedRoomSnapshot>())
            .Where(room => room.RevitElementId > 0)
            .GroupBy(room => room.RevitElementId)
            .ToDictionary(group => group.Key, group => group.Last());

        HashSet<int> currentRoomIds = rooms.Select(room => room.RevitElementId).ToHashSet();
        foreach (RoomInfo room in rooms)
        {
            if (savedRooms.TryGetValue(room.RevitElementId, out PersistedRoomSnapshot savedRoom)
                && HasRoomGeometryChanged(room, savedRoom))
            {
                assessment.ChangedRoomRevitElementIds.Add(room.RevitElementId);
            }
        }

        assessment.AddedRoomCount = rooms.Count(room => !savedRooms.ContainsKey(room.RevitElementId));
        assessment.RemovedRoomCount = savedRooms.Keys.Count(roomId => !currentRoomIds.Contains(roomId));
        assessment.ChangedRoomCount = assessment.ChangedRoomRevitElementIds.Count;

        if (snapshot.Fingerprint.LinkedModelCount != currentLinkedModelCount)
        {
            assessment.Messages.Add(
                "Linked model count changed from "
                + snapshot.Fingerprint.LinkedModelCount
                + " to "
                + currentLinkedModelCount
                + ".");
        }

        if (snapshot.Fingerprint.RoomCount != rooms.Count)
        {
            assessment.Messages.Add(
                "Room count changed from "
                + snapshot.Fingerprint.RoomCount
                + " to "
                + rooms.Count
                + ".");
        }

        if (assessment.ChangedRoomCount > 0)
        {
            assessment.Messages.Add(assessment.ChangedRoomCount + " room(s) changed area, ceiling height, or number since last save.");
        }

        if (assessment.AddedRoomCount > 0)
        {
            assessment.Messages.Add(assessment.AddedRoomCount + " new room(s) were added since last save.");
        }

        if (assessment.RemovedRoomCount > 0)
        {
            assessment.Messages.Add(assessment.RemovedRoomCount + " room(s) from the saved session are no longer in the model.");
        }

        if (assessment.Messages.Count == 0)
        {
            assessment.Messages.Add("Revit model changed since the last SprinkSnap save. Re-run analysis and review hazard, layout, and clash modules.");
        }

        return assessment;
    }

    public static string Serialize(SprinkSnapSessionSnapshot snapshot)
    {
        return JsonSerializer.Serialize(snapshot ?? new SprinkSnapSessionSnapshot(), JsonOptions);
    }

    public static SprinkSnapSessionSnapshot Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SprinkSnapSessionSnapshot>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static ModelAnalysisFingerprint ComputeFingerprint(IEnumerable<RoomInfo> rooms, int linkedModelCount)
    {
        List<RoomInfo> roomList = rooms?.ToList() ?? new List<RoomInfo>();
        StringBuilder builder = new StringBuilder();
        builder.Append("links=").Append(linkedModelCount).Append('|');
        foreach (RoomInfo room in roomList.OrderBy(item => item.RevitElementId))
        {
            builder.Append(room.RevitElementId)
                .Append(':')
                .Append(room.Number ?? string.Empty)
                .Append(':')
                .Append(room.AreaSquareFeet.ToString("F2"))
                .Append(':')
                .Append(room.CeilingHeightFeet.ToString("F2"))
                .Append(';');
        }

        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return new ModelAnalysisFingerprint
        {
            Hash = Convert.ToHexString(hashBytes),
            CapturedUtc = DateTime.UtcNow,
            RoomCount = roomList.Count,
            LinkedModelCount = linkedModelCount
        };
    }

    private static PersistedRoomSnapshot CreateRoomSnapshot(RoomInfo room)
    {
        return new PersistedRoomSnapshot
        {
            RevitElementId = room.RevitElementId,
            Number = room.Number,
            AreaSquareFeet = room.AreaSquareFeet,
            CeilingHeightFeet = room.CeilingHeightFeet,
            ApprovedHazardClassification = room.ApprovedHazardClassification,
            SuggestedHazardClassification = room.SuggestedHazardClassification,
            DesignerApproved = room.DesignerApproved,
            SelectedSprinklerFamilyName = room.SelectedSprinklerFamilyName,
            AutoSelectedSprinklerName = room.AutoSelectedSprinklerName,
            SprinklerSelectionStatus = room.SprinklerSelectionStatus,
            SprinklerSelectionReason = room.SprinklerSelectionReason,
            RevitFamilyMappingStatus = room.RevitFamilyMappingStatus,
            LayoutStatus = room.LayoutStatus,
            LayoutConfidenceScore = room.LayoutConfidenceScore,
            RequiresExceptionReview = room.RequiresExceptionReview,
            ExceptionReason = room.ExceptionReason,
            ProposedSprinklers = room.ProposedSprinklers?.ToList() ?? new List<SprinklerPlacementCandidate>()
        };
    }

    private static void ApplyRoomSnapshot(RoomInfo room, PersistedRoomSnapshot snapshot)
    {
        room.ApprovedHazardClassification = snapshot.ApprovedHazardClassification;
        room.SuggestedHazardClassification = string.IsNullOrWhiteSpace(snapshot.SuggestedHazardClassification)
            ? room.SuggestedHazardClassification
            : snapshot.SuggestedHazardClassification;
        room.DesignerApproved = snapshot.DesignerApproved;
        room.SelectedSprinklerFamilyName = snapshot.SelectedSprinklerFamilyName;
        room.AutoSelectedSprinklerName = snapshot.AutoSelectedSprinklerName;
        room.SprinklerSelectionStatus = snapshot.SprinklerSelectionStatus;
        room.SprinklerSelectionReason = snapshot.SprinklerSelectionReason;
        room.RevitFamilyMappingStatus = snapshot.RevitFamilyMappingStatus;
        room.LayoutStatus = snapshot.LayoutStatus;
        room.LayoutConfidenceScore = snapshot.LayoutConfidenceScore;
        room.RequiresExceptionReview = snapshot.RequiresExceptionReview;
        room.ExceptionReason = snapshot.ExceptionReason;
        room.ProposedSprinklers = snapshot.ProposedSprinklers?.ToList() ?? new List<SprinklerPlacementCandidate>();
    }

    private static bool HasRoomGeometryChanged(RoomInfo room, PersistedRoomSnapshot snapshot)
    {
        return !string.Equals(room.Number, snapshot.Number, StringComparison.OrdinalIgnoreCase)
            || Math.Abs(room.AreaSquareFeet - snapshot.AreaSquareFeet) > 0.01
            || Math.Abs(room.CeilingHeightFeet - snapshot.CeilingHeightFeet) > 0.01;
    }

    private static SprinkSnapSessionProgress CloneSessionProgress(SprinkSnapSessionProgress progress)
    {
        return new SprinkSnapSessionProgress
        {
            ModelAnalysisComplete = progress.ModelAnalysisComplete,
            HazardReviewComplete = progress.HazardReviewComplete,
            SprinklerReviewComplete = progress.SprinklerReviewComplete,
            WaterSupplyComplete = progress.WaterSupplyComplete,
            DesignGenerated = progress.DesignGenerated,
            ClashDetectionComplete = progress.ClashDetectionComplete,
            SprinklersPlacedInRevit = progress.SprinklersPlacedInRevit,
            HydraulicsComplete = progress.HydraulicsComplete,
            MaterialsComplete = progress.MaterialsComplete,
            ReportsExported = progress.ReportsExported,
            ReconciliationRequired = progress.ReconciliationRequired
        };
    }

    private static WaterSupplyInput CloneWaterSupply(WaterSupplyInput input)
    {
        return new WaterSupplyInput
        {
            StaticPressurePsi = input.StaticPressurePsi,
            ResidualPressurePsi = input.ResidualPressurePsi,
            FlowAtResidualGpm = input.FlowAtResidualGpm,
            HydrantTestDate = input.HydrantTestDate,
            ImportedSourcePath = input.ImportedSourcePath
        };
    }

    private static ReportExportRequest CloneReportExport(ReportExportRequest request)
    {
        if (request == null)
        {
            return new ReportExportRequest();
        }

        return new ReportExportRequest
        {
            OutputFolder = request.OutputFolder,
            IncludeDesignSummary = request.IncludeDesignSummary,
            IncludeHydraulicReport = request.IncludeHydraulicReport,
            IncludeNodeDiagram = request.IncludeNodeDiagram,
            IncludeMaterialTakeoff = request.IncludeMaterialTakeoff
        };
    }

    private static SprinkSnapProjectPreferences ClonePreferences(SprinkSnapProjectPreferences preferences)
    {
        if (preferences == null)
        {
            return new SprinkSnapProjectPreferences();
        }

        return new SprinkSnapProjectPreferences
        {
            PreferredManufacturer = preferences.PreferredManufacturer,
            DefaultCategory = preferences.DefaultCategory,
            DefaultOrientation = preferences.DefaultOrientation,
            DefaultKFactor = preferences.DefaultKFactor,
            DefaultBranchDiameterInches = preferences.DefaultBranchDiameterInches,
            DefaultMainDiameterInches = preferences.DefaultMainDiameterInches,
            AllowAlternateManufacturers = preferences.AllowAlternateManufacturers,
            CatalogPath = preferences.CatalogPath
        };
    }

    private static HydraulicSupplyAnchor CloneSupplyAnchor(HydraulicSupplyAnchor anchor)
    {
        if (anchor == null)
        {
            return new HydraulicSupplyAnchor();
        }

        return new HydraulicSupplyAnchor
        {
            IsSet = anchor.IsSet,
            RevitElementId = anchor.RevitElementId,
            ElementLabel = anchor.ElementLabel ?? string.Empty,
            SupplyPoint = anchor.SupplyPoint,
            HeaderPoint = anchor.HeaderPoint,
            SourceKind = anchor.SourceKind ?? string.Empty
        };
    }
}
