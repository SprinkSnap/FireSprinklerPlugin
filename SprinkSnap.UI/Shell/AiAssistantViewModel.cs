using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;
using FireSprinklerPlugin.SprinkSnap.Core.Workflow;

namespace FireSprinklerPlugin.SprinkSnap.UI.Shell;

public sealed class AiAssistantViewModel : INotifyPropertyChanged
{
    private readonly SprinkSnapShellContext context;
    private string inputText = string.Empty;

    public AiAssistantViewModel(SprinkSnapShellContext context)
    {
        this.context = context;
        Messages = new ObservableCollection<AiChatMessage>
        {
            new AiChatMessage(
                "assistant",
                "I'm your SprinkSnap design assistant. Ask about NFPA 13 hazards, spacing, clashes, hydraulics, or your current workflow step. I suggest — you approve.")
        };
        SendCommand = new ShellRelayCommand(_ => SendMessage(), _ => !string.IsNullOrWhiteSpace(InputText));
        AskAboutWorkflowCommand = new ShellRelayCommand(_ => AskAboutWorkflow());
        AskAboutNfpaCommand = new ShellRelayCommand(_ => AskAboutNfpa());
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<AiChatMessage> Messages { get; }

    public ICommand SendCommand { get; }

    public ICommand AskAboutWorkflowCommand { get; }

    public ICommand AskAboutNfpaCommand { get; }

    public string InputText
    {
        get => inputText;
        set
        {
            inputText = value;
            OnPropertyChanged();
        }
    }

    private void SendMessage()
    {
        string question = InputText.Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            return;
        }

        Messages.Add(new AiChatMessage("user", question));
        InputText = string.Empty;
        Messages.Add(new AiChatMessage("assistant", BuildResponse(question)));
    }

    private void AskAboutWorkflow()
    {
        InputText = "What should I do next in this project?";
        SendMessage();
    }

    private void AskAboutNfpa()
    {
        InputText = "Explain NFPA 13 obstruction rules for sprinkler layout.";
        SendMessage();
    }

    private string BuildResponse(string question)
    {
        string normalized = question.ToLowerInvariant();
        SprinkSnapProjectState state = context.ProjectState;

        if (normalized.Contains("next") || normalized.Contains("workflow") || normalized.Contains("step"))
        {
            if (!SprinkSnapWorkflowGate.IsAnalyzeComplete(state))
            {
                return "Start with Analyze Model to extract rooms, ceilings, and obstructions from Revit.";
            }

            if (!SprinkSnapWorkflowGate.IsHazardReviewComplete(state))
            {
                int pending = state.Rooms.Count(room => !room.DesignerApproved);
                return "Open Hazard Review and approve NFPA 13 hazard classifications. "
                    + pending
                    + " room(s) still need designer approval.";
            }

            if (!SprinkSnapWorkflowGate.IsSprinklerReviewComplete(state))
            {
                return "Open Sprinkler Review to set your project manufacturer standard and confirm listed heads per room.";
            }

            if (!SprinkSnapWorkflowGate.IsWaterSupplyComplete(state))
            {
                return "Enter hydrant test data in Water Supply before generating layout or hydraulics.";
            }

            if (!SprinkSnapWorkflowGate.IsDesignGenerated(state))
            {
                return "Use Generate Design to create NFPA 13 layout candidates. Review exceptions before clash detection.";
            }

            if (!SprinkSnapWorkflowGate.IsClashDetectionComplete(state))
            {
                return "Run Clash Detection to find duct, beam, and fixture conflicts, then resolve and update layout.";
            }

            if (!state.SessionProgress.HydraulicsComplete)
            {
                return "Run Hydraulics to verify Hazen-Williams demand against your water supply curve.";
            }

            return "Core workflow steps look complete. Export reports or refine exceptions in Settings.";
        }

        if (normalized.Contains("hazard") || normalized.Contains("classification") || normalized.Contains("occupancy"))
        {
            Nfpa13CodeReference reference = Nfpa13CodeReferenceLibrary.GetHazardReference("Light Hazard");
            return reference.Section
                + " — "
                + reference.Summary
                + " "
                + reference.DesignerNote;
        }

        if (normalized.Contains("clash") || normalized.Contains("obstruction") || normalized.Contains("duct"))
        {
            return "NFPA 13 Section 10.2.6 requires sprinklers to be located so discharge patterns are not obstructed. "
                + "After Generate Design, use Clash Detection to flag conflicts and automatically reposition heads where possible.";
        }

        if (normalized.Contains("spacing") || normalized.Contains("coverage"))
        {
            return "Spacing limits come from NFPA 13 Table 10.2.4.2.1(a) and the sprinkler listing. "
                + "SprinkSnap validates listing constraints during layout generation and clash resolution.";
        }

        if (normalized.Contains("hydraulic") || normalized.Contains("pressure") || normalized.Contains("flow"))
        {
            return "NFPA 13 Chapter 28 governs hydraulic calculations. Compare calculated system demand to the available "
                + "water supply curve with adequate safety margin before final sign-off.";
        }

        if (normalized.Contains("water supply") || normalized.Contains("hydrant"))
        {
            return "NFPA 13 Section 24.2 requires reliable water supply data including static pressure, residual pressure, "
                + "and flow at residual. Enter test data in the Water Supply module before hydraulics.";
        }

        int roomCount = state.Rooms.Count;
        int approved = state.Rooms.Count(room => room.DesignerApproved);
        return "SprinkSnap tracks "
            + roomCount
            + " room(s) with "
            + approved
            + " hazard approval(s). Ask about hazards, spacing, clashes, hydraulics, or say \"what's next\" for workflow guidance. "
            + "All recommendations require designer approval — I never override your decisions.";
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class AiChatMessage
{
    public AiChatMessage(string role, string text)
    {
        Role = role;
        Text = text;
        Timestamp = DateTime.Now.ToString("h:mm tt");
    }

    public string Role { get; }

    public string Text { get; }

    public string Timestamp { get; }

    public bool IsUser => string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);
}
