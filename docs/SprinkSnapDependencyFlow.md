# SprinkSnap Hazard Classification Dependency Flow

Recommended command rename: `HazardClassificationCommand.cs`.

The command owns Revit workflow orchestration only. Extraction, geometry analysis, hazard suggestion,
parameter persistence, and designer review are split into services to keep the system testable and
ready for future sprinkler layout and hydraulic modules.

```mermaid
flowchart TD
    Revit[Autodesk Revit Document] --> Cmd[SprinkSnap.Revit HazardClassificationCommand]
    Cmd --> Extractor[SprinkSnap.Revit RoomExtractor]
    Cmd --> Storage[SprinkSnap.Revit HazardClassificationParameterStorage]
    Cmd --> View[SprinkSnap.UI HazardClassificationView]
    View --> VM[SprinkSnap.UI HazardClassificationViewModel]
    VM --> RoomInfo[SprinkSnap.Core Models RoomInfo]
    Extractor --> Boundary[SprinkSnap.Revit RoomBoundaryExtractor]
    Extractor --> Analyzer[SprinkSnap.Core RoomAnalyzer]
    Extractor --> Classifier[SprinkSnap.Core HazardClassifier]
    Boundary --> Geometry[SprinkSnap.Core Geometry Primitives]
    Analyzer --> RoomInfo
    Classifier --> Rules[SprinkSnap.Core NFPA13Rules]
    Classifier --> Result[SprinkSnap.Core Models HazardClassificationResult]
    Cmd --> Candidates[SprinkSnap.Core SprinklerPlacementCandidateGenerator]
    Candidates --> Future[Future sprinkler spacing, branch line, and hydraulic engines]
```

## Boundary rules

- `SprinkSnap.Core` has no Revit or WPF dependencies.
- `SprinkSnap.Revit` adapts Autodesk Revit API elements into Core models and writes approved data back to Revit.
- `SprinkSnap.UI` owns designer review and approval state via MVVM.
- Hazard classification remains suggestion-only; `SS_HazardClassification` stores the designer-approved value.
- Sprinkler placement candidates are conceptual room data only. Final sprinkler placement and NFPA 13 compliance checks are future modules.

