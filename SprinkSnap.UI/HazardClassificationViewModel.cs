using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.UI;

public sealed class HazardClassificationViewModel : INotifyPropertyChanged
{
    private string searchText = string.Empty;
    private string hazardFilter = "All";
    private string selectedBatchHazard = HazardClassification.LightHazard;
    private string validationMessage = string.Empty;

    public HazardClassificationViewModel(IEnumerable<RoomInfo> rooms)
    {
        Rooms = new ObservableCollection<RoomHazardReviewItem>(
            rooms.Select(room => new RoomHazardReviewItem(room)));

        HazardOptions = new ObservableCollection<string>(HazardClassification.All);
        HazardFilters = new ObservableCollection<string>(new[] { "All" }.Concat(HazardClassification.All));
        RoomsView = CollectionViewSource.GetDefaultView(Rooms);
        RoomsView.Filter = FilterRoom;

        AcceptAllSuggestionsCommand = new RelayCommand(_ => AcceptAllSuggestions(), _ => Rooms.Count > 0);
        ApplyCommand = new RelayCommand(_ => ApplyBatchOrCurrentOverrides(), _ => Rooms.Count > 0);
        SaveCommand = new RelayCommand(_ => Save(), _ => Rooms.Count > 0);
        CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(this, false));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public event EventHandler<bool> RequestClose;

    public ObservableCollection<RoomHazardReviewItem> Rooms { get; }

    public ICollectionView RoomsView { get; }

    public ObservableCollection<string> HazardOptions { get; }

    public ObservableCollection<string> HazardFilters { get; }

    public ICommand AcceptAllSuggestionsCommand { get; }

    public ICommand ApplyCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

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

        return matchesSearch && matchesHazard;
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

    private void Save()
    {
        List<RoomHazardReviewItem> invalidRooms = Rooms
            .Where(room => !room.IsApproved || !HazardClassification.IsSupported(room.UserOverride))
            .ToList();

        if (invalidRooms.Count > 0)
        {
            ValidationMessage = "Designer approval is required for every room before saving. Remaining rooms: "
                + invalidRooms.Count
                + ".";
            return;
        }

        ValidationMessage = string.Empty;
        RequestClose?.Invoke(this, true);
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

