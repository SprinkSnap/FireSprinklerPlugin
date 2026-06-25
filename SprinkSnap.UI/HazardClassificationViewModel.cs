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
using FireSprinklerPlugin.SprinkSnap.Core.Mapping;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;
using FireSprinklerPlugin.SprinkSnap.Core.Persistence;
using FireSprinklerPlugin.SprinkSnap.Core.Workflow;

namespace FireSprinklerPlugin.SprinkSnap.UI;

public sealed class HazardClassificationViewModel : INotifyPropertyChanged
{
    private const string AllFilter = "All";

    private string searchText = string.Empty;
    private string hazardFilter = "All";
    private string sprinklerSearchText = string.Empty;
    private string selectedManufacturer = "Viking";
    private string selectedCategory = "Standard Spray Quick Response";
    private string selectedOrientation = "Pendent";
    private string selectedKFactor = "5.6";
    private string selectedBatchHazard = HazardClassification.LightHazard;
    private string validationMessage = string.Empty;
    private string staticPressurePsi = string.Empty;
    private string residualPressurePsi = string.Empty;
    private string flowGpm = string.Empty;
    private bool allowAlternateManufacturers = true;
    private bool reviewOnlyExceptions;
    private bool isEmbeddedInShell;
    private RoomHazardReviewItem selectedRoom;
    private SprinklerFamilyInfo selectedSprinklerFamily;
    private WaterDemandInfo approvedWaterDemand = new WaterDemandInfo();
    private readonly IReadOnlyList<SprinklerFamilyInfo> allSprinklerFamilies;
    private readonly ICeilingIntelligenceService ceilingIntelligenceService = new CeilingIntelligenceService();
    private readonly ILayoutComplianceValidator complianceValidator = new LayoutComplianceValidator();
    private readonly ICompatibleSprinklerSelector compatibleSprinklerSelector = new CompatibleSprinklerSelector();
    private readonly ISprinklerLayoutOptimizer layoutOptimizer = new SprinklerLayoutOptimizer();

    public HazardClassificationViewModel(
        IEnumerable<RoomInfo> rooms,
        IEnumerable<SprinklerFamilyInfo> sprinklerFamilies = null)
    {
        allSprinklerFamilies = sprinklerFamilies?.ToList() ?? new SprinklerFamilySelector().GetAvailableFamilies();
        SprinklerFamilies = new ObservableCollection<SprinklerFamilyInfo>(
            allSprinklerFamilies);
        ManufacturerOptions = new ObservableCollection<string>();
        CategoryOptions = new ObservableCollection<string>();
        OrientationOptions = new ObservableCollection<string>();
        KFactorOptions = new ObservableCollection<string>();
        RebuildSprinklerFilterOptions();
        ApplySprinklerFamilyFilters();

        Rooms = new ObservableCollection<RoomHazardReviewItem>(
            rooms.Select(PrepareRoomForAssistant).Select(room => new RoomHazardReviewItem(room, NotifyWorkflowProgressChanged)));

        if (Rooms.Count > 0)
        {
            selectedRoom = Rooms[0];
        }

        UpdateActiveCodeReference();

        HazardOptions = new ObservableCollection<string>(HazardClassification.All);
        HazardFilters = new ObservableCollection<string>(new[] { "All" }.Concat(HazardClassification.All));
        RoomsView = CollectionViewSource.GetDefaultView(Rooms);
        RoomsView.Filter = FilterRoom;
        UpdateRoomSprinklerSelections();

        AcceptAllSuggestionsCommand = new RelayCommand(_ => AcceptAllSuggestions(), _ => Rooms.Count > 0);
        ApplyCommand = new RelayCommand(_ => ApplyBatchOrCurrentOverrides(), _ => Rooms.Count > 0);
        ValidateCommand = new RelayCommand(_ => ValidateLayouts(), _ => Rooms.Count > 0);
        AutoLayoutCommand = new RelayCommand(_ => AutoLayoutRooms(), _ => Rooms.Count > 0);
        OverrideCommand = new RelayCommand(_ => OverrideVisibleExceptions(), _ => Rooms.Count > 0);
        ResetSprinklerOverridesCommand = new RelayCommand(_ => ResetSprinklerOverridesToProjectDefault(), _ => Rooms.Count > 0);
        SaveCommand = new RelayCommand(_ => Save(), _ => Rooms.Count > 0);
        ShowRoomInRevitCommand = new RelayCommand(_ => ShowRoomInRevit(), _ => SelectedRoom != null);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public event EventHandler WorkflowProgressChanged;

    public event EventHandler<bool> RequestClose;

    public Action PersistToRevitRequested { get; set; }

    public ObservableCollection<RoomHazardReviewItem> Rooms { get; }

    public ICollectionView RoomsView { get; }

    public ObservableCollection<string> HazardOptions { get; }

    public ObservableCollection<string> HazardFilters { get; }

    public ObservableCollection<SprinklerFamilyInfo> SprinklerFamilies { get; }

    public ObservableCollection<string> ManufacturerOptions { get; }

    public ObservableCollection<string> CategoryOptions { get; }

    public ObservableCollection<string> OrientationOptions { get; }

    public ObservableCollection<string> KFactorOptions { get; }

    public ICommand AcceptAllSuggestionsCommand { get; }

    public ICommand ApplyCommand { get; }

    public ICommand ValidateCommand { get; }

    public ICommand AutoLayoutCommand { get; }

    public ICommand OverrideCommand { get; }

    public ICommand ResetSprinklerOverridesCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand ShowRoomInRevitCommand { get; }

    public Action<int> ShowRoomInRevitRequested { get; set; }

    public int TotalRoomCount
    {
        get => Rooms.Count;
        set
        {
            // Display-only binding compatibility for WPF controls that attempt source updates.
        }
    }

    public int ExceptionRoomCount
    {
        get => Rooms.Count(room => room.RequiresExceptionReview);
        set
        {
            // Display-only binding compatibility for WPF controls that attempt source updates.
        }
    }

    public int ApprovedRoomCount
    {
        get => Rooms.Count(room => room.IsApproved && HazardClassification.IsSupported(room.UserOverride));
        set { }
    }

    public RoomHazardReviewItem SelectedRoom
    {
        get => selectedRoom;
        set
        {
            if (ReferenceEquals(selectedRoom, value))
            {
                return;
            }

            selectedRoom = value;
            UpdateActiveCodeReference();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowRoomInRevitCommand));
        }
    }

    public string ActiveCodeReferenceSection
    {
        get => activeCodeReference?.Section ?? "NFPA 13 Chapter 5";
        set { }
    }

    public string ActiveCodeReferenceSummary
    {
        get => activeCodeReference?.Summary ?? "Select a room hazard to view the applicable NFPA 13 reference.";
        set { }
    }

    public string ActiveCodeReferenceDesignerNote
    {
        get => activeCodeReference?.DesignerNote ?? string.Empty;
        set { }
    }

    private Nfpa13CodeReference activeCodeReference;

    public int AutoSolvedRoomCount
    {
        get => Rooms.Count(room =>
            string.Equals(room.LayoutStatus, LayoutStatus.Compliant, StringComparison.Ordinal)
            && !room.RequiresExceptionReview);
        set
        {
            // Display-only binding compatibility for WPF controls that attempt source updates.
        }
    }

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

        set
        {
            // Display-only binding compatibility for WPF controls that attempt source updates.
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
                + Environment.NewLine
                + "Manufacturer: "
                + SelectedSprinklerFamily.Manufacturer
                + " | Model/SIN: "
                + SelectedSprinklerFamily.Model
                + " / "
                + SelectedSprinklerFamily.Sin
                + Environment.NewLine
                + "Type: "
                + SelectedSprinklerFamily.Category
                + " | "
                + SelectedSprinklerFamily.Orientation
                + " | K"
                + SelectedSprinklerFamily.KFactor.ToString("N1", CultureInfo.CurrentCulture)
                + " | "
                + SelectedSprinklerFamily.ResponseType
                + Environment.NewLine
                + "Allowed hazards: "
                + string.Join(", ", SelectedSprinklerFamily.SupportedHazardClassifications)
                + Environment.NewLine
                + "Max spacing: "
                + SelectedSprinklerFamily.MaxSpacingFeet.ToString("N0", CultureInfo.CurrentCulture)
                + " ft | Max coverage: "
                + SelectedSprinklerFamily.MaxCoverageAreaSquareFeet.ToString("N0", CultureInfo.CurrentCulture)
                + " sq ft"
                + Environment.NewLine
                + "Ceiling compatibility: "
                + string.Join(", ", SelectedSprinklerFamily.SupportedCeilingClassifications)
                + Environment.NewLine
                + "Data sheet: "
                + SelectedSprinklerFamily.TechnicalDataSheetUrl
                + Environment.NewLine
                + "Revit family: "
                + SelectedSprinklerFamily.RevitFamilyPath
                + " | Type: "
                + SelectedSprinklerFamily.RevitTypeName;
        }

        set
        {
            // Display-only binding compatibility for WPF controls that attempt source updates.
        }
    }

    public string SprinklerSearchText
    {
        get => sprinklerSearchText;
        set
        {
            if (SetField(ref sprinklerSearchText, value))
            {
                ApplySprinklerFamilyFilters();
            }
        }
    }

    public string SelectedManufacturer
    {
        get => selectedManufacturer;
        set
        {
            if (SetField(ref selectedManufacturer, value))
            {
                ApplySprinklerFamilyFilters();
            }
        }
    }

    public string SelectedCategory
    {
        get => selectedCategory;
        set
        {
            if (SetField(ref selectedCategory, value))
            {
                ApplySprinklerFamilyFilters();
            }
        }
    }

    public string SelectedOrientation
    {
        get => selectedOrientation;
        set
        {
            if (SetField(ref selectedOrientation, value))
            {
                ApplySprinklerFamilyFilters();
            }
        }
    }

    public string SelectedKFactor
    {
        get => selectedKFactor;
        set
        {
            if (SetField(ref selectedKFactor, value))
            {
                ApplySprinklerFamilyFilters();
            }
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

    public bool AllowAlternateManufacturers
    {
        get => allowAlternateManufacturers;
        set
        {
            if (SetField(ref allowAlternateManufacturers, value))
            {
                UpdateRoomSprinklerSelections();
            }
        }
    }

    public SprinklerFamilyInfo SelectedSprinklerFamily
    {
        get => selectedSprinklerFamily;
        set
        {
            if (SetField(ref selectedSprinklerFamily, value))
            {
                OnPropertyChanged(nameof(SelectedFamilyListingSummary));
                UpdateRoomSprinklerSelections();
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

    public bool IsEmbeddedInShell
    {
        get => isEmbeddedInShell;
        set => SetField(ref isEmbeddedInShell, value);
    }

    public SprinkSnapProjectState EmbeddedProjectState { get; set; }

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

    private void RebuildSprinklerFilterOptions()
    {
        ReplaceOptions(ManufacturerOptions, allSprinklerFamilies.Select(family => family.Manufacturer));
        ReplaceOptions(CategoryOptions, allSprinklerFamilies.Select(family => family.Category).Concat(SprinklerFamilySelector.VikingCategories));
        ReplaceOptions(OrientationOptions, allSprinklerFamilies.Select(family => family.Orientation));
        ReplaceOptions(
            KFactorOptions,
            allSprinklerFamilies
                .Select(family => family.KFactor.ToString("N1", CultureInfo.CurrentCulture)));
    }

    private void ApplySprinklerFamilyFilters()
    {
        SprinklerFamilyInfo previousSelection = SelectedSprinklerFamily;
        List<SprinklerFamilyInfo> filteredFamilies = allSprinklerFamilies
            .Where(MatchesSprinklerFilters)
            .OrderBy(family => family.Manufacturer)
            .ThenBy(family => family.Category)
            .ThenBy(family => family.KFactor)
            .ThenBy(family => family.Model)
            .ToList();

        SprinklerFamilies.Clear();
        foreach (SprinklerFamilyInfo family in filteredFamilies)
        {
            SprinklerFamilies.Add(family);
        }

        SelectedSprinklerFamily = previousSelection != null && filteredFamilies.Contains(previousSelection)
            ? previousSelection
            : SprinklerFamilies.FirstOrDefault();
    }

    private void UpdateRoomSprinklerSelections()
    {
        if (Rooms == null)
        {
            return;
        }

        ProjectSprinklerStandard projectStandard = CreateProjectSprinklerStandard();
        foreach (RoomHazardReviewItem room in Rooms)
        {
            room.Room.ApprovedHazardClassification = room.UserOverride;
            CompatibleSprinklerSelection selection = compatibleSprinklerSelector.SelectForRoom(
                room.Room,
                allSprinklerFamilies,
                projectStandard);
            ApplySprinklerSelection(room, selection);
        }

        SprinklerFamilyMappingService.UpdateRoomMappingStatuses(
            Rooms.Select(item => item.Room),
            allSprinklerFamilies);
        foreach (RoomHazardReviewItem room in Rooms)
        {
            room.RefreshMappingStatus();
        }

        RoomsView?.Refresh();
        NotifyWorkflowProgressChanged();
    }

    private ProjectSprinklerStandard CreateProjectSprinklerStandard()
    {
        return new ProjectSprinklerStandard
        {
            PreferredManufacturer = SelectedManufacturer,
            PreferredCategory = SelectedCategory,
            PreferredOrientation = SelectedOrientation,
            PreferredKFactor = TryParseKFactor(SelectedKFactor),
            AllowAlternateManufacturers = AllowAlternateManufacturers
        };
    }

    private static double? TryParseKFactor(string selectedKFactor)
    {
        if (string.IsNullOrWhiteSpace(selectedKFactor)
            || string.Equals(selectedKFactor, AllFilter, StringComparison.Ordinal))
        {
            return null;
        }

        return double.TryParse(selectedKFactor, NumberStyles.Float, CultureInfo.CurrentCulture, out double value)
            ? value
            : null;
    }

    private static void ApplySprinklerSelection(
        RoomHazardReviewItem room,
        CompatibleSprinklerSelection selection)
    {
        room.SetCompatibleSprinklerOptions(selection.CompatibleFamilies, selection.SelectedFamily);
        room.Room.AutoSelectedSprinklerName = selection.SelectedFamily?.DisplayName ?? "No compatible sprinkler";
        room.Room.SprinklerSelectionStatus = selection.Status;
        room.Room.SprinklerSelectionReason = selection.Reason;
        room.Room.CompatibleSprinklerCount = selection.CompatibleFamilies.Count;
        room.Room.AlternateSprinklerSummary = selection.AlternateFamilies.Count == 0
            ? "No alternates"
            : string.Join(
                Environment.NewLine,
                selection.AlternateFamilies
                    .Take(4)
                    .Select(family => "- " + family.DisplayName));

        if (selection.AlternateFamilies.Count > 4)
        {
            room.Room.AlternateSprinklerSummary += Environment.NewLine
                + "+ "
                + (selection.AlternateFamilies.Count - 4).ToString(CultureInfo.CurrentCulture)
                + " more compatible alternates";
        }

        if (selection.SelectedFamily == null)
        {
            room.Room.RequiresExceptionReview = true;
            room.Room.ExceptionReason = selection.Reason;
        }

        room.RefreshAssistantState();
    }

    private bool MatchesSprinklerFilters(SprinklerFamilyInfo family)
    {
        return MatchesFilter(SelectedManufacturer, family.Manufacturer)
            && MatchesFilter(SelectedCategory, family.Category)
            && MatchesFilter(SelectedOrientation, family.Orientation)
            && MatchesFilter(
                SelectedKFactor,
                family.KFactor.ToString("N1", CultureInfo.CurrentCulture))
            && MatchesSprinklerSearch(family);
    }

    private bool MatchesSprinklerSearch(SprinklerFamilyInfo family)
    {
        if (string.IsNullOrWhiteSpace(SprinklerSearchText))
        {
            return true;
        }

        string searchText = SprinklerSearchText;
        return Contains(family.Manufacturer, searchText)
            || Contains(family.Category, searchText)
            || Contains(family.Model, searchText)
            || Contains(family.Sin, searchText)
            || Contains(family.FamilyName, searchText)
            || Contains(family.Orientation, searchText);
    }

    private static bool MatchesFilter(string selectedValue, string candidateValue)
    {
        return string.IsNullOrWhiteSpace(selectedValue)
            || string.Equals(selectedValue, AllFilter, StringComparison.Ordinal)
            || string.Equals(selectedValue, candidateValue, StringComparison.Ordinal);
    }

    private static void ReplaceOptions(ObservableCollection<string> options, IEnumerable<string> values)
    {
        options.Clear();
        options.Add(AllFilter);
        foreach (string value in values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().OrderBy(value => value))
        {
            options.Add(value);
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

        UpdateRoomSprinklerSelections();
        ValidationMessage = "All suggested classifications were accepted. Review remains editable until Save.";
        NotifyWorkflowProgressChanged();
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

        UpdateRoomSprinklerSelections();
        ValidationMessage = updatedCount + " visible room(s) marked approved.";
        NotifyWorkflowProgressChanged();
    }

    private void ValidateLayouts()
    {
        foreach (RoomHazardReviewItem item in Rooms)
        {
            item.Room.ApprovedHazardClassification = item.UserOverride;
            SprinklerFamilyInfo roomSprinklerFamily = item.SelectedRoomSprinklerFamily ?? SelectedSprinklerFamily;
            LayoutValidationResult result = complianceValidator.ValidateLayout(
                item.Room,
                roomSprinklerFamily,
                item.Room.ProposedSprinklers.ToList());
            ApplyLayoutResult(item, result);
        }

        RoomsView.Refresh();
        NotifyDashboardState();
        ValidationMessage = "Validation complete. Enable 'Review only exceptions' to focus on unresolved rooms.";
    }

    private void AutoLayoutRooms()
    {
        if (TryBlockIfModelStale("Layout generation"))
        {
            return;
        }

        foreach (RoomHazardReviewItem item in Rooms)
        {
            item.Room.ApprovedHazardClassification = item.UserOverride;
            SprinklerFamilyInfo roomSprinklerFamily = item.SelectedRoomSprinklerFamily ?? SelectedSprinklerFamily;
            item.Room.SelectedSprinklerFamilyName = roomSprinklerFamily?.DisplayName ?? string.Empty;

            AutomaticLayoutResult result = layoutOptimizer.GenerateBestLayout(item.Room, roomSprinklerFamily);
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

    private void ResetSprinklerOverridesToProjectDefault()
    {
        foreach (RoomHazardReviewItem item in Rooms)
        {
            item.ClearSprinklerOverride();
        }

        UpdateRoomSprinklerSelections();
        ValidationMessage = "Room sprinkler overrides were reset to the project default recommendation.";
        NotifyWorkflowProgressChanged();
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

    private bool TryBlockIfModelStale(string actionName)
    {
        if (!IsEmbeddedInShell || EmbeddedProjectState == null)
        {
            return false;
        }

        if (!SprinkSnapWorkflowGate.IsModelStale(EmbeddedProjectState))
        {
            return false;
        }

        ValidationMessage = actionName + " blocked. " + SprinkSnapWorkflowGate.StaleModelBlockReason;
        return true;
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

        if (!IsEmbeddedInShell)
        {
            if (!TryCreateWaterDemandInfo(out WaterDemandInfo waterDemandInfo))
            {
                return;
            }

            approvedWaterDemand = waterDemandInfo;
        }

        ValidationMessage = string.Empty;

        foreach (RoomHazardReviewItem room in Rooms)
        {
            room.Room.ApprovedHazardClassification = room.UserOverride;
            room.Room.DesignerApproved = room.IsApproved;
        }

        if (IsEmbeddedInShell)
        {
            PersistToRevitRequested?.Invoke();
            ValidationMessage = "Hazard and sprinkler selections saved to SprinkSnap and persisted in the Revit project.";
            NotifyWorkflowProgressChanged();
            return;
        }

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

    private void ShowRoomInRevit()
    {
        if (SelectedRoom?.Room == null)
        {
            return;
        }

        ShowRoomInRevitRequested?.Invoke(SelectedRoom.Room.RevitElementId);
    }

    public void NotifyExternalRefresh()
    {
        foreach (RoomHazardReviewItem item in Rooms)
        {
            item.RefreshAssistantState();
        }

        RefreshFamilyMappingStatuses();
        NotifyWorkflowProgressChanged();
        OnPropertyChanged(nameof(AutoSolvedRoomCount));
    }

    public void ApplyModelChangeAssessment(ModelChangeAssessment assessment)
    {
        HashSet<int> changedRoomIds = assessment?.ChangedRoomRevitElementIds?.ToHashSet() ?? new HashSet<int>();
        foreach (RoomHazardReviewItem item in Rooms)
        {
            item.RefreshModelChangeState(changedRoomIds);
        }
    }

    public void RefreshFamilyMappingStatuses()
    {
        SprinklerFamilyMappingService.UpdateRoomMappingStatuses(
            Rooms.Select(item => item.Room),
            allSprinklerFamilies);
        foreach (RoomHazardReviewItem item in Rooms)
        {
            item.RefreshMappingStatus();
        }
    }

    private void NotifyWorkflowProgressChanged()
    {
        UpdateActiveCodeReference();
        OnPropertyChanged(nameof(ApprovedRoomCount));
        OnPropertyChanged(nameof(ExceptionRoomCount));
        WorkflowProgressChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateActiveCodeReference()
    {
        string hazard = selectedRoom?.UserOverride ?? selectedRoom?.SuggestedHazard ?? string.Empty;
        activeCodeReference = Nfpa13CodeReferenceLibrary.GetHazardReference(hazard);
        OnPropertyChanged(nameof(ActiveCodeReferenceSection));
        OnPropertyChanged(nameof(ActiveCodeReferenceSummary));
        OnPropertyChanged(nameof(ActiveCodeReferenceDesignerNote));
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
    private readonly Action workflowSyncRequested;
    private string userOverride;
    private bool isApproved;
    private bool updatingCompatibleSprinklerOptions;
    private SprinklerFamilyInfo selectedRoomSprinklerFamily;

    public RoomHazardReviewItem(RoomInfo room, Action workflowSyncRequested = null)
    {
        Room = room;
        this.workflowSyncRequested = workflowSyncRequested;
        userOverride = string.IsNullOrWhiteSpace(room.ApprovedHazardClassification)
            ? room.SuggestedHazardClassification
            : room.ApprovedHazardClassification;
        isApproved = room.DesignerApproved;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public RoomInfo Room { get; }

    public ObservableCollection<SprinklerFamilyInfo> CompatibleSprinklerOptions { get; } =
        new ObservableCollection<SprinklerFamilyInfo>();

    public int RevitElementId
    {
        get => Room.RevitElementId;
        set => Room.RevitElementId = value;
    }

    public string Number
    {
        get => Room.Number;
        set => Room.Number = value;
    }

    public string Name
    {
        get => Room.Name;
        set => Room.Name = value;
    }

    public double AreaSquareFeet
    {
        get => Room.AreaSquareFeet;
        set => Room.AreaSquareFeet = value;
    }

    public double HeightFeet
    {
        get => Room.HeightFeet;
        set => Room.HeightFeet = value;
    }

    public double CeilingHeightFeet
    {
        get => Room.CeilingHeightFeet;
        set => Room.CeilingHeightFeet = value;
    }

    public string CeilingType
    {
        get => Room.CeilingType;
        set => Room.CeilingType = value;
    }

    public string OccupancyClassification
    {
        get => Room.OccupancyClassification;
        set => Room.OccupancyClassification = value;
    }

    public string SuggestedHazard
    {
        get => Room.SuggestedHazardClassification;
        set => Room.SuggestedHazardClassification = value;
    }

    public string SuggestionReason
    {
        get => Room.SuggestionReason;
        set => Room.SuggestionReason = value;
    }

    public string ExistingHazard
    {
        get => Room.ExistingHazardClassification;
        set => Room.ExistingHazardClassification = value;
    }

    public string CeilingClassification
    {
        get => Room.CeilingClassification;
        set => Room.CeilingClassification = value;
    }

    public string CeilingIntelligenceSummary
    {
        get => Room.CeilingIntelligenceSummary;
        set => Room.CeilingIntelligenceSummary = value;
    }

    public string LayoutStatus
    {
        get => Room.LayoutStatus;
        set => Room.LayoutStatus = value;
    }

    public double LayoutConfidenceScore
    {
        get => Room.LayoutConfidenceScore;
        set
        {
            if (Math.Abs(Room.LayoutConfidenceScore - value) < 0.0001)
            {
                return;
            }

            Room.LayoutConfidenceScore = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LayoutConfidenceScore)));
        }
    }

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

    public string ExceptionReason
    {
        get => Room.ExceptionReason;
        set => Room.ExceptionReason = value;
    }

    public string AutoSelectedSprinklerName
    {
        get => Room.AutoSelectedSprinklerName;
        set => Room.AutoSelectedSprinklerName = value;
    }

    public string RevitFamilyMappingStatus => Room.RevitFamilyMappingStatus;

    public bool IsModelChanged
    {
        get => isModelChanged;
        private set
        {
            if (isModelChanged == value)
            {
                return;
            }

            isModelChanged = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsModelChanged)));
        }
    }

    private bool isModelChanged;

    public void RefreshModelChangeState(ISet<int> changedRoomRevitElementIds)
    {
        IsModelChanged = changedRoomRevitElementIds != null
            && changedRoomRevitElementIds.Contains(Room.RevitElementId);
    }

    public void RefreshMappingStatus()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RevitFamilyMappingStatus)));
    }

    public SprinklerFamilyInfo SelectedRoomSprinklerFamily
    {
        get => selectedRoomSprinklerFamily;
        set
        {
            if (ReferenceEquals(selectedRoomSprinklerFamily, value))
            {
                return;
            }

            selectedRoomSprinklerFamily = value;
            if (selectedRoomSprinklerFamily != null)
            {
                Room.AutoSelectedSprinklerName = selectedRoomSprinklerFamily.DisplayName;
                Room.SelectedSprinklerFamilyName = selectedRoomSprinklerFamily.DisplayName;

                if (!updatingCompatibleSprinklerOptions)
                {
                    Room.SprinklerSelectionStatus = "Designer Override";
                    Room.SprinklerSelectionReason = "Designer selected a compatible alternate manufacturer/model for this room.";
                }
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedRoomSprinklerFamily)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutoSelectedSprinklerName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SprinklerSelectionStatus)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SprinklerSelectionReason)));
        }
    }

    public string SprinklerSelectionStatus
    {
        get => Room.SprinklerSelectionStatus;
        set => Room.SprinklerSelectionStatus = value;
    }

    public string SprinklerSelectionReason
    {
        get => Room.SprinklerSelectionReason;
        set => Room.SprinklerSelectionReason = value;
    }

    public int CompatibleSprinklerCount
    {
        get => Room.CompatibleSprinklerCount;
        set => Room.CompatibleSprinklerCount = value;
    }

    public string AlternateSprinklerSummary
    {
        get => Room.AlternateSprinklerSummary;
        set => Room.AlternateSprinklerSummary = value;
    }

    public int ProposedSprinklerCount
    {
        get => Room.ProposedSprinklers.Count;
        set
        {
            // Display-only binding compatibility for WPF controls that attempt source updates.
        }
    }

    public string PreviewMarkers
    {
        get
        {
            int compliant = Room.LayoutPreviewMarkers.Count(marker => marker.IsCompliant);
            int noncompliant = Room.LayoutPreviewMarkers.Count(marker => !marker.IsCompliant);
            return compliant + " compliant / " + noncompliant + " noncompliant";
        }

        set
        {
            // Display-only binding compatibility for WPF controls that attempt source updates.
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
                SyncApprovedHazardToRoom();
                workflowSyncRequested?.Invoke();
            }
        }
    }

    public bool IsApproved
    {
        get => isApproved;
        set
        {
            if (SetField(ref isApproved, value))
            {
                Room.DesignerApproved = value;
                SyncApprovedHazardToRoom();
                workflowSyncRequested?.Invoke();
            }
        }
    }

    private void SyncApprovedHazardToRoom()
    {
        if (isApproved && HazardClassification.IsSupported(userOverride))
        {
            Room.ApprovedHazardClassification = userOverride;
        }
    }

    public void SetCompatibleSprinklerOptions(
        IEnumerable<SprinklerFamilyInfo> compatibleFamilies,
        SprinklerFamilyInfo selectedFamily)
    {
        updatingCompatibleSprinklerOptions = true;
        try
        {
            CompatibleSprinklerOptions.Clear();
            foreach (SprinklerFamilyInfo family in compatibleFamilies)
            {
                CompatibleSprinklerOptions.Add(family);
            }

            SelectedRoomSprinklerFamily = selectedFamily;
        }
        finally
        {
            updatingCompatibleSprinklerOptions = false;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CompatibleSprinklerOptions)));
    }

    public void ClearSprinklerOverride()
    {
        selectedRoomSprinklerFamily = null;
        Room.SprinklerSelectionStatus = "Project Default";
        Room.SprinklerSelectionReason = "Room override cleared; SprinkSnap will use the project default manufacturer preference and compatible listing logic.";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedRoomSprinklerFamily)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SprinklerSelectionStatus)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SprinklerSelectionReason)));
    }

    public void RefreshAssistantState()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CeilingClassification)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CeilingIntelligenceSummary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LayoutStatus)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LayoutConfidenceScore)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RequiresExceptionReview)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExceptionReason)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutoSelectedSprinklerName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedRoomSprinklerFamily)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SprinklerSelectionStatus)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SprinklerSelectionReason)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CompatibleSprinklerCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AlternateSprinklerSummary)));
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

