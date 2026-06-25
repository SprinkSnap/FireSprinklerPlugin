using System;
using Autodesk.Revit.Attributes;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Revit.Commands;
using FireSprinklerPlugin.SprinkSnap.Revit.Session;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

[Transaction(TransactionMode.Manual)]
public sealed class HazardClassificationCommand : SprinkSnapWorkflowCommandBase
{
    protected override SprinkSnapWorkflowStep WorkflowStep => SprinkSnapWorkflowStep.HazardReview;

    protected override string DisplayName => "Hazard Review";
}
