using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.UI;

public sealed class HazardClassificationViewModel : INotifyPropertyChanged
{
    private string searchText = string.Empty;
    private string hazardFilter = "All";
    private string selectedBatchHazard = HazardClassification.LightHazard;
    private string validationMessage = string.Empty;
    private string staticPressurePsi = string.Empty;
    private string residualPressurePsi = string.Empty;
    private string flowGpm = string.Empty;
    private bool reviewOnlyExceptions;
    private SprinklerFamilyInfo selectedSprinklerFamily;
    private WaterDemandInfo approvedWaterDemand = new WaterDemandInfo();
    private readonly ICeilingIntelligenceService ceilingIntelligenceService = new CeilingIntelligenceService();
    private readonly ILayoutComplianceValidator complianceValidator = new LayoutComplianceValidator();
    private readonly ISprinklerLayoutOptimizer layoutOptimizer = new SprinklerLayoutOptimizer();

    public HazardClassificationViewModel(IEnumerable<RoomInfo> rooms)
    {
        SprinklerFamilies = new ObservableCollection<SprinklerFamilyInfo>(
            new SprinklerFamilySelector().GetAvailableFamilies());
        selectedSprinklerFamily = SprinklerFamilies.FirstOrDefault();

        Rooms = new ObservableCollection<RoomHazardReviewItem>(
            rooms.Select(PrepareRoomForAssistant).Select(room => new RoomHazardReviewItem(room)));

        HazardOptions = new ObservableCollection<string>(HazardClassification.All);
        HazardFilters = new ObservableCollection<string>(new[] { "All" }.Concat(HazardClassification.All));
        RoomsView = CollectionViewSource.GetDefaultView(Rooms);
        RoomsView.Filter = FilterRoom;

        AcceptAllSuggestionsCommand = new RelayCommand(_ => AcceptAllSuggestions(), _ => Rooms.Count > 0);
        ApplyCommand = new RelayCommand(_ => ApplyBatchOrCurrentOverrides(), _ => Rooms.Count > 0);
        ValidateCommand = new RelayCommand(_ => ValidateLayouts(), _ => Rooms.Count > 0);
        AutoLayoutCommand = new RelayCommand(_ => AutoLayoutRooms(), _ => Rooms.Count > 0);
        OverrideCommand = new RelayCommand(_ => OverrideVisibleExceptions(), _ => Rooms.Count > 0);
        SaveCommand = new RelayCommand(_ => Save(), _ => Rooms.Count > 0);
        CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(this, false));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public event EventHandler<bool> RequestClose;

    public ObservableCollection<RoomHazardReviewItem> Rooms { get; }

    public ICollectionView RoomsView { get; }

    public ObservableCollection<string> HazardOptions { get; }

    public ObservableCollection<string> HazardFilters { get; }

    public ObservableCollection<SprinklerFamilyInfo> SprinklerFamilies { get; }

    public ICommand AcceptAllSuggestionsCommand { get; }

    public ICommand ApplyCommand { get; }

    public ICommand ValidateCommand { get; }

    public ICommand AutoLayoutCommand { get; }

    public ICommand OverrideCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public int TotalRoomCount => Rooms.Count;

    public int ExceptionRoomCount => Rooms.Count(room => room.RequiresExceptionReview);

    public int AutoSolvedRoomCount => Rooms.Count(room =>
        string.Equals(room.LayoutStatus, LayoutStatus.Compliant, StringComparison.Ordinal)
        && !room.RequiresExceptionReview);

    public string AverageConfidenceText
    {
        get
        {
            if (Rooms.Count == 0)
            {
                return "0%";
            }

            return Rooms.Average(room => room.LayoutConfidenceScore).ToString("P0", CultureInfo.CurrentCulture);
        }
    }

    public string SelectedFamilyListingSummary
    {
        get
        {
            if (SelectedSprinklerFamily == null)
            {
                return "Select a listed sprinkler family before validating or generating layout candidates.";
            }

            return SelectedSprinklerFamily.FamilyName
                + " | "
                + SelectedSprinklerFamily.Orientation
                + " | K"
                + SelectedSprinklerFamily.KFactor.ToString("N1", CultureInfo.CurrentCulture)
                + " | Max spacing "
                + SelectedSprinklerFamily.MaxSpacingFeet.ToString("N0", CultureInfo.CurrentCulture)
                + " ft | Max area "
                + SelectedSprinklerFamily.MaxCoverageAreaSquareFeet.ToString("N0", CultureInfo.CurrentCulture)
                + " sq ft";
        }
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetField(ref searchText, value))
            {
                RoomsView.Refresh();
            }
        }
    }

    public string HazardFilter
    {
        get => hazardFilter;
        set
        {
            if (SetField(ref hazardFilter, value))
            {
                RoomsView.Refresh();
            }
        }
    }

    public string SelectedBatchHazard
    {
        get => selectedBatchHazard;
        set => SetField(ref selectedBatchHazard, value);
    }

    public string ValidationMessage
    {
        get => validationMessage;
        set => SetField(ref validationMessage, value);
    }

    public string StaticPressurePsi
    {
        get => staticPressurePsi;
        set => SetField(ref staticPressurePsi, value);
    }

    public string ResidualPressurePsi
    {
        get => residualPressurePsi;
        set => SetField(ref residualPressurePsi, value);
    }

    public string FlowGpm
    {
        get => flowGpm;
        set => SetField(ref flowGpm, value);
    }

    public SprinklerFamilyInfo SelectedSprinklerFamily
    {
        get => selectedSprinklerFamily;
        set
        {
            if (SetField(ref selectedSprinklerFamily, value))
            {
                OnPropertyChanged(nameof(SelectedFamilyListingSummary));
            }
        }
    }

    public bool ReviewOnlyExceptions
    {
        get => reviewOnlyExceptions;
        set
        {
            if (SetField(ref reviewOnlyExceptions, value))
            {
                RoomsView.Refresh();
            }
        }
    }

    public WaterDemandInfo ApprovedWaterDemand => approvedWaterDemand;

    public IReadOnlyList<RoomInfo> ApprovedRooms
    {
        get
        {
            foreach (RoomHazardReviewItem item in Rooms)
            {
                item.Room.ApprovedHazardClassification = item.UserOverride;
                item.Room.DesignerApproved = item.IsApproved;
            }

            return Rooms.Select(item => item.Room).ToList();
        }
    }

    private RoomInfo PrepareRoomForAssistant(RoomInfo room)
    {
        if (string.IsNullOrWhiteSpace(room.CeilingIntelligenceSummary))
        {
            CeilingIntelligenceResult ceilingResult = ceilingIntelligenceService.Analyze(room);
            room.CeilingClassification = ceilingResult.Classification;
            room.CeilingIntelligenceSummary = ceilingResult.Summary;
            room.LayoutConfidenceScore = ceilingResult.ConfidenceScore;
            room.RequiresExceptionReview = room.RequiresExceptionReview || ceilingResult.RequiresReview;
            if (ceilingResult.RequiresReview && string.IsNullOrWhiteSpace(room.ExceptionReason))
            {
                room.ExceptionReason = ceilingResult.Summary;
            }
        }

        room.LayoutStatus = room.RequiresExceptionReview ? LayoutStatus.ReviewRequired : LayoutStatus.Ready;
        return room;
    }

    private bool FilterRoom(object item)
    {
        RoomHazardReviewItem room = item as RoomHazardReviewItem;
        if (room == null)
        {
            return false;
        }

        bool matchesSearch = string.IsNullOrWhiteSpace(SearchText)
            || Contains(room.Number, SearchText)
            || Contains(room.Name, SearchText)
            || Contains(room.OccupancyClassification, SearchText)
            || Contains(room.CeilingType, SearchText);

        bool matchesHazard = string.Equals(HazardFilter, "All", StringComparison.Ordinal)
            || string.Equals(room.SuggestedHazard, HazardFilter, StringComparison.Ordinal)
            || string.Equals(room.UserOverride, HazardFilter, StringComparison.Ordinal);

        bool matchesExceptionFilter = !ReviewOnlyExceptions || room.RequiresExceptionReview;

        return matchesSearch && matchesHazard && matchesExceptionFilter;
    }

    private void AcceptAllSuggestions()
    {
        foreach (RoomHazardReviewItem room in Rooms)
        {
            room.UserOverride = room.SuggestedHazard;
            room.IsApproved = true;
        }

        ValidationMessage = "All suggested classifications were accepted. Review remains editable until Save.";
    }

    private void ApplyBatchOrCurrentOverrides()
    {
        int updatedCount = 0;
        foreach (RoomHazardReviewItem room in RoomsView.Cast<RoomHazardReviewItem>())
        {
            if (!string.IsNullOrWhiteSpace(SelectedBatchHazard)
                && HazardClassification.IsSupported(SelectedBatchHazard))
            {
                room.UserOverride = SelectedBatchHazard;
            }

            if (HazardClassification.IsSupported(room.UserOverride))
            {
                room.IsApproved = true;
                updatedCount++;
            }
        }

        ValidationMessage = updatedCount + " visible room(s) marked approved.";
    }

    private void ValidateLayouts()
    {
        foreach (RoomHazardReviewItem item in Rooms)
        {
            item.Room.ApprovedHazardClassification = item.UserOverride;
            LayoutValidationResult result = complianceValidator.ValidateLayout(
                item.Room,
                SelectedSprinklerFamily,
                item.Room.ProposedSprinklers.ToList());
            ApplyLayoutResult(item, result);
        }

        RoomsView.Refresh();
        NotifyDashboardState();
        ValidationMessage = "Validation complete. Enable 'Review only exceptions' to focus on unresolved rooms.";
    }

    private void AutoLayoutRooms()
    {
        foreach (RoomHazardReviewItem item in Rooms)
        {
            item.Room.ApprovedHazardClassification = item.UserOverride;
            item.Room.SelectedSprinklerFamilyName = SelectedSprinklerFamily?.DisplayName ?? string.Empty;

            AutomaticLayoutResult result = layoutOptimizer.GenerateBestLayout(item.Room, SelectedSprinklerFamily);
            item.Room.ProposedSprinklers = result.Candidates;
            item.Room.LayoutPreviewMarkers = result.PreviewMarkers;
            item.Room.LayoutStatus = result.Status;
            item.Room.LayoutConfidenceScore = result.ConfidenceScore;
            item.Room.RequiresExceptionReview = !result.CanPlaceAutomatically;
            item.Room.ExceptionReason = string.Join("; ", result.Messages);

            if (result.CanPlaceAutomatically)
            {
                item.IsApproved = true;
            }

            item.RefreshAssistantState();
        }

        RoomsView.Refresh();
        NotifyDashboardState();
        ValidationMessage = "Auto-layout complete. Confident rooms were solved automatically; exceptions remain for review.";
    }

    private void OverrideVisibleExceptions()
    {
        int count = 0;
        foreach (RoomHazardReviewItem item in RoomsView.Cast<RoomHazardReviewItem>())
        {
            if (!item.RequiresExceptionReview)
            {
                continue;
            }

            item.Room.RequiresExceptionReview = false;
            item.Room.LayoutStatus = LayoutStatus.Compliant;
            item.Room.ExceptionReason = "Designer override accepted for preview workflow.";
            item.IsApproved = true;
            item.RefreshAssistantState();
            count++;
        }

        RoomsView.Refresh();
        NotifyDashboardState();
        ValidationMessage = count + " visible exception room(s) were overridden by the designer.";
    }

    private static void ApplyLayoutResult(RoomHazardReviewItem item, LayoutValidationResult result)
    {
        item.Room.LayoutStatus = result.Status;
        item.Room.LayoutConfidenceScore = result.ConfidenceScore;
        item.Room.LayoutPreviewMarkers = result.Markers;
        item.Room.RequiresExceptionReview = !result.IsCompliant;
        item.Room.ExceptionReason = string.Join("; ", result.Messages);
        item.RefreshAssistantState();
    }

    private void Save()
    {
        List<RoomHazardReviewItem> invalidRooms = Rooms
            .Where(room => !HazardClassification.IsSupported(room.UserOverride)
                || (room.RequiresExceptionReview && !room.IsApproved))
            .ToList();

        if (invalidRooms.Count > 0)
        {
            ValidationMessage = "Designer approval is required for unresolved exceptions before saving. Remaining rooms: "
                + invalidRooms.Count
                + ".";
            return;
        }

        if (!TryCreateWaterDemandInfo(out WaterDemandInfo waterDemandInfo))
        {
            return;
        }

        approvedWaterDemand = waterDemandInfo;
        ValidationMessage = string.Empty;
        RequestClose?.Invoke(this, true);
    }

    private bool TryCreateWaterDemandInfo(out WaterDemandInfo waterDemandInfo)
    {
        waterDemandInfo = new WaterDemandInfo();

        if (!TryParseOptionalNonNegativeDouble(StaticPressurePsi, "Static pressure PSI", out double? staticPressure))
        {
            return false;
        }

        if (!TryParseOptionalNonNegativeDouble(ResidualPressurePsi, "Residual pressure PSI", out double? residualPressure))
        {
            return false;
        }

        if (!TryParseOptionalNonNegativeDouble(FlowGpm, "Flow GPM", out double? flow))
        {
            return false;
        }

        if (staticPressure.HasValue
            && residualPressure.HasValue
            && residualPressure.Value > staticPressure.Value)
        {
            ValidationMessage = "Residual pressure PSI cannot exceed static pressure PSI.";
            return false;
        }

        waterDemandInfo.StaticPressurePsi = staticPressure;
        waterDemandInfo.ResidualPressurePsi = residualPressure;
        waterDemandInfo.FlowGpm = flow;
        return true;
    }

    private bool TryParseOptionalNonNegativeDouble(string value, string label, out double? parsedValue)
    {
        parsedValue = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out double numericValue))
        {
            ValidationMessage = label + " must be a numeric value.";
            return false;
        }

        if (numericValue < 0.0)
        {
            ValidationMessage = label + " cannot be negative.";
            return false;
        }

        parsedValue = numericValue;
        return true;
    }

    private static bool Contains(string value, string search)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void NotifyDashboardState()
    {
        OnPropertyChanged(nameof(TotalRoomCount));
        OnPropertyChanged(nameof(ExceptionRoomCount));
        OnPropertyChanged(nameof(AutoSolvedRoomCount));
        OnPropertyChanged(nameof(AverageConfidenceText));
    }
}

public sealed class RoomHazardReviewItem : INotifyPropertyChanged
{
    private string userOverride;
    private bool isApproved;

    public RoomHazardReviewItem(RoomInfo room)
    {
        Room = room;
        userOverride = string.IsNullOrWhiteSpace(room.ApprovedHazardClassification)
            ? room.SuggestedHazardClassification
            : room.ApprovedHazardClassification;
        isApproved = room.DesignerApproved;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public RoomInfo Room { get; }

    public int RevitElementId => Room.RevitElementId;

    public string Number => Room.Number;

    public string Name => Room.Name;

    public double AreaSquareFeet => Room.AreaSquareFeet;

    public double HeightFeet => Room.HeightFeet;

    public double CeilingHeightFeet => Room.CeilingHeightFeet;

    public string CeilingType => Room.CeilingType;

    public string OccupancyClassification => Room.OccupancyClassification;

    public string SuggestedHazard => Room.SuggestedHazardClassification;

    public string SuggestionReason => Room.SuggestionReason;

    public string ExistingHazard => Room.ExistingHazardClassification;

    public string CeilingClassification => Room.CeilingClassification;

    public string CeilingIntelligenceSummary => Room.CeilingIntelligenceSummary;

    public string LayoutStatus => Room.LayoutStatus;

    public double LayoutConfidenceScore => Room.LayoutConfidenceScore;

    public bool RequiresExceptionReview
    {
        get => Room.RequiresExceptionReview;
        set
        {
            if (Room.RequiresExceptionReview == value)
            {
                return;
            }

            Room.RequiresExceptionReview = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RequiresExceptionReview)));
        }
    }

    public string ExceptionReason => Room.ExceptionReason;

    public int ProposedSprinklerCount => Room.ProposedSprinklers.Count;

    public string PreviewMarkers
    {
        get
        {
            int compliant = Room.LayoutPreviewMarkers.Count(marker => marker.IsCompliant);
            int noncompliant = Room.LayoutPreviewMarkers.Count(marker => !marker.IsCompliant);
            return compliant + " compliant / " + noncompliant + " noncompliant";
        }
    }

    public string UserOverride
    {
        get => userOverride;
        set
        {
            if (SetField(ref userOverride, value))
            {
                IsApproved = false;
            }
        }
    }

    public bool IsApproved
    {
        get => isApproved;
        set => SetField(ref isApproved, value);
    }

    public void RefreshAssistantState()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CeilingClassification)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CeilingIntelligenceSummary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LayoutStatus)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LayoutConfidenceScore)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RequiresExceptionReview)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExceptionReason)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProposedSprinklerCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PreviewMarkers)));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Action<object> execute;
    private readonly Predicate<object> canExecute;

    public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
    {
        this.execute = execute;
        this.canExecute = canExecute;
    }

    public event EventHandler CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object parameter)
    {
        return canExecute == null || canExecute(parameter);
    }

    public void Execute(object parameter)
    {
        execute(parameter);
    }
}

