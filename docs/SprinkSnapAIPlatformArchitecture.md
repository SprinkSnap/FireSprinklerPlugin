# SPRINKSNAP AI Platform Architecture

## Product goal

SPRINKSNAP AI is a single Autodesk Revit 2027 add-in for AI-assisted NFPA 13 sprinkler design.
It automates model analysis, hazard suggestions, sprinkler recommendations, layout preparation,
hydraulic calculations, material takeoff, and reports while requiring designer approval for
engineering decisions.

## Revit add-in structure

- Add-in: `SprinkSnapAI.addin`
- Application class: `FireSprinklerPlugin.SprinkSnap.Revit.SprinkSnapApplication`
- Single ribbon tab: `SprinkSnap AI`
- Ribbon panels:
  1. Analyze Model
  2. Hazard Review
  3. Sprinkler Review
  4. Water Supply
  5. Generate Design
  6. Clash Detection
  7. Hydraulics
  8. Materials
  9. Reports
  10. Settings
- Dockable pane: `SprinkSnap AI`

## Folder structure

```text
SprinkSnap.Core
  AI
  Data
  Engines
  Models

SprinkSnap.Revit
  Commands
  DockablePane
  RoomExtractor.cs
  RoomBoundaryExtractor.cs
  SprinklerFamilyScanner.cs
  SprinkSnapApplication.cs

SprinkSnap.UI
  Modules
    HazardReviewModuleView.xaml
    SprinklerReviewModuleView.xaml
    LayoutReviewModuleView.xaml
    ClashDetectionModuleView.xaml
  Shell
    SprinkSnapShellView.xaml
    AiAssistantPanelView.xaml

SprinkSnap.WpfPreview
SprinkSnap.Addin
docs
```

## Module architecture

```text
Model Analysis Engine
  Extracts rooms, spaces, geometry, levels, linked models, phases, existing sprinklers, and obstructions.

Hazard Classification Engine
  Suggests Light, OH1, OH2, EH1, EH2 with confidence and reasoning. Designer approval required.

Manufacturer Recommendation Engine
  Filters listed sprinkler catalog by hazard, ceiling, geometry, storage use, project standard, and room overrides.

Water Supply Engine
  Stores static pressure, residual pressure, flow at residual, hydrant date, and validates adequacy against demand.

Layout Engine
  Generates deterministic sprinkler placement candidates after compliance checks.

Hydraulic Engine
  Builds node graph and deterministic Hazen-Williams calculation pipeline.

Material Takeoff Engine
  Counts sprinklers, pipe, fittings, valves, and risers.

Report Engine
  Produces design summary, hydraulic report, node diagram, and material takeoff reports.

AI Services Layer
  Suggests and explains only. It may not override designers or replace deterministic calculations.
```

## Database schema

Initial local data files:

```text
sprinkler_catalog.json
project_preferences.json
family_symbol_mappings.json
analysis_cache.json
```

Core entities:

- `SprinklerCatalogRecord`
- `SprinkSnapProjectPreferences`
- `RoomInfo`
- `WaterSupplyInput`
- `HydraulicNode`
- `MaterialTakeoffItem`
- `ReportExportRequest`

## Manufacturer catalog schema

Each listed head stores:

- ListedFamilyId
- Manufacturer
- Category
- Series
- Model
- SIN
- SprinklerType
- ResponseType
- Orientation
- KFactor
- CoverageType
- StorageUse
- AllowedHazards
- AllowedCeilingTypes
- MaxSpacingFeet
- MaxCoverageAreaSquareFeet
- TemperatureRatings
- FinishOptions
- RevitFamilyPath
- RevitTypeName
- TechnicalDataSheetUrl

## Revit family recognition

Priority order:

1. Read SprinkSnap shared parameters on loaded sprinkler `FamilySymbol`:
   - `SS_Manufacturer`
   - `SS_Category`
   - `SS_Model`
   - `SS_SIN`
   - `SS_KFactor`
   - `SS_Orientation`
   - `SS_ResponseType`
   - `SS_CoverageType`
   - `SS_HazardCompatibility`
   - `SS_CeilingCompatibility`
   - `SS_MaxSpacingFt`
   - `SS_MaxCoverageAreaSqFt`
   - `SS_TechnicalDataSheetUrl`
   - `SS_ListedFamilyId`
2. Match family/type names to catalog records.
3. Mark unmapped Revit families for manual mapping.

## UI wireframe

```text
TOP: Workflow Progress Bar
Analyze Model -> Hazard Review -> Sprinkler Review -> Water Supply -> Generate Design -> Hydraulics -> Materials -> Reports

LEFT: Project Navigation
Analyze Model
Hazard Review
Sprinkler Review
Water Supply
Generate Design
Hydraulics
Materials
Reports
Settings

CENTER: Main Workspace
Module-specific work surface

RIGHT: Summary Panel
Compliance Status
Warnings
Exceptions
Progress
```

## Implementation roadmap

1. Stabilize single Revit add-in shell, ribbon, and dockable pane.
2. Complete model analysis extraction and JSON export.
3. Persist project state and room analysis cache.
4. Expand manufacturer catalog and family scanner.
5. Complete hazard approval workflow.
6. Implement deterministic layout placement into Revit.
7. Implement hydraulic graph and Hazen-Williams calculations.
8. Implement material takeoff extraction and Excel/PDF export.
9. Implement report generation.
10. Add AI explanation and recommendation services behind deterministic checks.

## Engineering guardrails

- AI cannot approve code compliance.
- AI cannot override designer selections.
- Hydraulic calculations must be deterministic.
- Do not place sprinklers when required geometry or listing data is missing.
- Store approvals and overrides explicitly for auditability.

