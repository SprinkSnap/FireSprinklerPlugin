// NFPA 13 defines occupancy hazard classifications based on quantity/combustibility of contents,
// heat release rates, and stockpile heights. This tool automates hazard classification for fire
// sprinkler design per NFPA 13 Section 5.

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Windows.Interop;
using Wpf = System.Windows;
using WpfControls = System.Windows.Controls;
using WpfData = System.Windows.Data;
using WpfMedia = System.Windows.Media;
using WpfThreading = System.Windows.Threading;

namespace FireSprinklerPlugin.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ExtractRoomsAndClassifyHazard : IExternalCommand
    {
        private const string HazardParameterName = "NFPA13_Hazard_Classification";
        private const string SharedParameterGroupName = "Fire Sprinkler Plugin";

        // NFPA 13 occupancy hazard groups drive sprinkler density, design area, and water demand.
        private static readonly IReadOnlyList<string> HazardOptions = new List<string>
        {
            "Light Hazard",
            "Ordinary Hazard Group 1",
            "Ordinary Hazard Group 2",
            "Extra Hazard Group 1",
            "Extra Hazard Group 2"
        };

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApplication = commandData.Application;
                UIDocument uiDocument = uiApplication.ActiveUIDocument;

                if (uiDocument == null)
                {
                    TaskDialog.Show("NFPA 13 Hazard Classification", "No active Revit document is available.");
                    return Result.Succeeded;
                }

                Document doc = uiDocument.Document;

                var rooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .ToList();

                if (rooms.Count == 0)
                {
                    TaskDialog.Show("NFPA 13 Hazard Classification", "No rooms were found in the active document.");
                    return Result.Succeeded;
                }

                List<RoomHazardData> extractedRooms = ExtractRoomsWithProgress(uiApplication, doc, rooms);

                HazardClassificationWindow dialog = new HazardClassificationWindow(extractedRooms, HazardOptions);
                SetRevitOwner(dialog, uiApplication);

                bool? dialogResult = dialog.ShowDialog();
                if (dialogResult != true)
                {
                    return Result.Succeeded;
                }

                using (Transaction transaction = new Transaction(doc, "Store NFPA 13 Hazard Classifications"))
                {
                    transaction.Start();

                    EnsureHazardClassificationSharedParameter(uiApplication.Application, doc);
                    doc.Regenerate();
                    StoreHazardClassifications(doc, dialog.Rooms);

                    transaction.Commit();
                }

                string report = BuildSummaryReport(dialog.Rooms);
                TaskDialog.Show("NFPA 13 Hazard Classification Summary", report);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show(
                    "NFPA 13 Hazard Classification Error",
                    "The command could not complete." + Environment.NewLine + Environment.NewLine + ex.Message);

                return Result.Failed;
            }
        }

        private static List<RoomHazardData> ExtractRoomsWithProgress(UIApplication uiApplication, Document doc, IList<Room> rooms)
        {
            RoomExtractionProgressWindow progressWindow = null;
            List<RoomHazardData> extractedRooms = new List<RoomHazardData>();

            try
            {
                progressWindow = new RoomExtractionProgressWindow(rooms.Count);
                SetRevitOwner(progressWindow, uiApplication);
                progressWindow.Show();
                progressWindow.UpdateProgress(0, rooms.Count, "Starting room extraction...");

                for (int index = 0; index < rooms.Count; index++)
                {
                    Room room = rooms[index];
                    RoomHazardData roomData = ExtractRoomData(room);
                    extractedRooms.Add(roomData);

                    string status = string.Format(
                        CultureInfo.CurrentCulture,
                        "Extracting {0} - {1}",
                        roomData.Number,
                        roomData.Name);
                    progressWindow.UpdateProgress(index + 1, rooms.Count, status);
                }
            }
            finally
            {
                if (progressWindow != null)
                {
                    progressWindow.Close();
                }
            }

            return extractedRooms;
        }

        private static RoomHazardData ExtractRoomData(Room room)
        {
            Parameter nameParameter = room.get_Parameter(BuiltInParameter.ROOM_NAME);
            Parameter numberParameter = room.get_Parameter(BuiltInParameter.ROOM_NUMBER);
            string roomName = GetParameterDisplayValue(nameParameter, room.Name);
            string roomNumber = GetParameterDisplayValue(numberParameter, room.Number);
            double areaSquareFeet = room.Area;
            double areaSquareMeters = UnitUtils.ConvertFromInternalUnits(areaSquareFeet, UnitTypeId.SquareMeters);
            IList<IList<BoundarySegment>> roomBoundary = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
            ElementId roomLevelId = room.LevelId;
            double roomHeight = GetRoomHeight(room);

            return new RoomHazardData
            {
                RoomId = room.Id,
                Name = string.IsNullOrWhiteSpace(roomName) ? "<Unnamed Room>" : roomName,
                Number = string.IsNullOrWhiteSpace(roomNumber) ? "<No Number>" : roomNumber,
                AreaSquareFeet = areaSquareFeet,
                AreaSquareMeters = areaSquareMeters,
                BoundarySegments = roomBoundary,
                LevelId = roomLevelId,
                HeightFeet = roomHeight,
                SelectedHazard = HazardOptions[0]
            };
        }

        private static string GetParameterDisplayValue(Parameter parameter, string fallbackValue)
        {
            if (parameter == null)
            {
                return fallbackValue;
            }

            string value = parameter.AsValueString();
            if (string.IsNullOrWhiteSpace(value))
            {
                value = parameter.AsString();
            }

            return string.IsNullOrWhiteSpace(value) ? fallbackValue : value;
        }

        private static double GetRoomHeight(Room room)
        {
            // Some prompt specifications and legacy references describe room height as
            // room.get_Parameter(BuiltInParameter.ROOM_HEIGHT).AsDouble(). Current Revit APIs expose
            // Room.UnboundedHeight, so look up ROOM_HEIGHT dynamically first and then fall back safely.
            if (Enum.TryParse("ROOM_HEIGHT", out BuiltInParameter roomHeightBuiltInParameter))
            {
                Parameter heightParameter = room.get_Parameter(roomHeightBuiltInParameter);
                if (heightParameter != null && heightParameter.StorageType == StorageType.Double)
                {
                    return heightParameter.AsDouble();
                }
            }

            return room.UnboundedHeight;
        }

        private static void EnsureHazardClassificationSharedParameter(
            Autodesk.Revit.ApplicationServices.Application application,
            Document doc)
        {
            Category roomCategory = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Rooms);
            Definition existingDefinition = FindExistingProjectParameterDefinition(
                doc,
                HazardParameterName,
                out ElementBinding existingBinding);

            if (existingDefinition != null)
            {
                if (!BindingContainsCategory(existingBinding, roomCategory))
                {
                    CategorySet categorySet = CopyBoundCategories(application, existingBinding);
                    categorySet.Insert(roomCategory);

                    InstanceBinding instanceBinding = application.Create.NewInstanceBinding(categorySet);
                    if (!doc.ParameterBindings.ReInsert(existingDefinition, instanceBinding, BuiltInParameterGroup.PG_DATA))
                    {
                        throw new InvalidOperationException(
                            "Unable to bind the existing NFPA 13 hazard classification parameter to rooms.");
                    }
                }

                return;
            }

            Definition sharedParameterDefinition = GetOrCreateSharedParameterDefinition(application);
            CategorySet roomCategorySet = application.Create.NewCategorySet();
            roomCategorySet.Insert(roomCategory);

            InstanceBinding roomBinding = application.Create.NewInstanceBinding(roomCategorySet);
            bool inserted = doc.ParameterBindings.Insert(
                sharedParameterDefinition,
                roomBinding,
                BuiltInParameterGroup.PG_DATA);

            if (!inserted && !doc.ParameterBindings.ReInsert(sharedParameterDefinition, roomBinding, BuiltInParameterGroup.PG_DATA))
            {
                throw new InvalidOperationException(
                    "Unable to create or bind the NFPA 13 hazard classification shared parameter.");
            }
        }

        private static Definition FindExistingProjectParameterDefinition(
            Document doc,
            string parameterName,
            out ElementBinding binding)
        {
            DefinitionBindingMapIterator iterator = doc.ParameterBindings.ForwardIterator();
            iterator.Reset();

            while (iterator.MoveNext())
            {
                Definition definition = iterator.Key;
                if (definition != null && string.Equals(definition.Name, parameterName, StringComparison.Ordinal))
                {
                    binding = iterator.Current as ElementBinding;
                    return definition;
                }
            }

            binding = null;
            return null;
        }

        private static bool BindingContainsCategory(ElementBinding binding, Category category)
        {
            if (binding == null || category == null)
            {
                return false;
            }

            foreach (Category boundCategory in binding.Categories)
            {
                if (boundCategory != null && boundCategory.Id.Equals(category.Id))
                {
                    return true;
                }
            }

            return false;
        }

        private static CategorySet CopyBoundCategories(
            Autodesk.Revit.ApplicationServices.Application application,
            ElementBinding existingBinding)
        {
            CategorySet categorySet = application.Create.NewCategorySet();

            if (existingBinding == null)
            {
                return categorySet;
            }

            foreach (Category category in existingBinding.Categories)
            {
                if (category != null && !CategorySetContains(categorySet, category))
                {
                    categorySet.Insert(category);
                }
            }

            return categorySet;
        }

        private static bool CategorySetContains(CategorySet categorySet, Category category)
        {
            foreach (Category existingCategory in categorySet)
            {
                if (existingCategory != null && existingCategory.Id.Equals(category.Id))
                {
                    return true;
                }
            }

            return false;
        }

        private static Definition GetOrCreateSharedParameterDefinition(
            Autodesk.Revit.ApplicationServices.Application application)
        {
            string originalSharedParameterFile = application.SharedParametersFilename;
            bool changedSharedParameterFile = false;

            try
            {
                if (string.IsNullOrWhiteSpace(originalSharedParameterFile) || !File.Exists(originalSharedParameterFile))
                {
                    string sharedParameterFile = Path.Combine(
                        Path.GetTempPath(),
                        "FireSprinklerPlugin_SharedParameters.txt");

                    if (!File.Exists(sharedParameterFile))
                    {
                        File.WriteAllLines(
                            sharedParameterFile,
                            new[]
                            {
                                "# This is a Revit shared parameter file.",
                                "# Do not edit manually.",
                                "*META\tVERSION\tMINVERSION",
                                "META\t2\t1",
                                "*GROUP\tID\tNAME",
                                "*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\tHIDEWHENNOVALUE"
                            });
                    }

                    application.SharedParametersFilename = sharedParameterFile;
                    changedSharedParameterFile = true;
                }

                DefinitionFile definitionFile = application.OpenSharedParameterFile();
                if (definitionFile == null)
                {
                    throw new InvalidOperationException("Unable to open a Revit shared parameter file.");
                }

                DefinitionGroup definitionGroup = GetOrCreateDefinitionGroup(definitionFile, SharedParameterGroupName);
                Definition existingDefinition = FindDefinition(definitionGroup, HazardParameterName);
                if (existingDefinition != null)
                {
                    return existingDefinition;
                }

                ExternalDefinitionCreationOptions creationOptions =
                    new ExternalDefinitionCreationOptions(HazardParameterName, SpecTypeId.String.Text)
                    {
                        Description = "NFPA 13 occupancy hazard classification for fire sprinkler design automation.",
                        UserModifiable = true,
                        Visible = true
                    };

                return definitionGroup.Definitions.Create(creationOptions);
            }
            finally
            {
                if (changedSharedParameterFile && !string.IsNullOrWhiteSpace(originalSharedParameterFile))
                {
                    application.SharedParametersFilename = originalSharedParameterFile;
                }
            }
        }

        private static DefinitionGroup GetOrCreateDefinitionGroup(DefinitionFile definitionFile, string groupName)
        {
            foreach (DefinitionGroup group in definitionFile.Groups)
            {
                if (string.Equals(group.Name, groupName, StringComparison.Ordinal))
                {
                    return group;
                }
            }

            return definitionFile.Groups.Create(groupName);
        }

        private static Definition FindDefinition(DefinitionGroup definitionGroup, string definitionName)
        {
            foreach (Definition definition in definitionGroup.Definitions)
            {
                if (definition != null && string.Equals(definition.Name, definitionName, StringComparison.Ordinal))
                {
                    return definition;
                }
            }

            return null;
        }

        private static void StoreHazardClassifications(Document doc, IEnumerable<RoomHazardData> rooms)
        {
            foreach (RoomHazardData roomData in rooms)
            {
                Room room = doc.GetElement(roomData.RoomId) as Room;
                if (room == null)
                {
                    continue;
                }

                Parameter hazardParameter = room.LookupParameter(HazardParameterName);
                if (hazardParameter == null)
                {
                    throw new InvalidOperationException(
                        "The NFPA 13 hazard classification parameter is not available on room " +
                        roomData.Number +
                        " - " +
                        roomData.Name +
                        ".");
                }

                if (hazardParameter.IsReadOnly)
                {
                    throw new InvalidOperationException(
                        "The NFPA 13 hazard classification parameter is read-only on room " +
                        roomData.Number +
                        " - " +
                        roomData.Name +
                        ".");
                }

                hazardParameter.Set(roomData.SelectedHazard);
            }
        }

        private static string BuildSummaryReport(IEnumerable<RoomHazardData> rooms)
        {
            List<RoomHazardData> roomList = rooms.ToList();
            List<string> reportLines = new List<string>
            {
                "Total rooms processed: " + roomList.Count.ToString(CultureInfo.CurrentCulture),
                string.Empty,
                "Counts and total area by NFPA 13 hazard classification:"
            };

            foreach (string hazardOption in HazardOptions)
            {
                List<RoomHazardData> hazardRooms = roomList
                    .Where(room => string.Equals(room.SelectedHazard, hazardOption, StringComparison.Ordinal))
                    .ToList();

                double totalSquareFeet = hazardRooms.Sum(room => room.AreaSquareFeet);
                double totalSquareMeters = hazardRooms.Sum(room => room.AreaSquareMeters);

                reportLines.Add(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "{0}: {1} room(s), {2:N2} sq ft / {3:N2} sq m",
                        hazardOption,
                        hazardRooms.Count,
                        totalSquareFeet,
                        totalSquareMeters));
            }

            reportLines.Add(string.Empty);
            reportLines.Add("Room hazard assignments:");

            foreach (RoomHazardData room in roomList.OrderBy(room => room.Number).ThenBy(room => room.Name))
            {
                reportLines.Add(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "{0} - {1}: {2} ({3:N2} sq ft / {4:N2} sq m)",
                        room.Number,
                        room.Name,
                        room.SelectedHazard,
                        room.AreaSquareFeet,
                        room.AreaSquareMeters));
            }

            return string.Join(Environment.NewLine, reportLines);
        }

        private static void SetRevitOwner(Wpf.Window window, UIApplication uiApplication)
        {
            IntPtr mainWindowHandle = uiApplication.MainWindowHandle;
            if (mainWindowHandle != IntPtr.Zero)
            {
                WindowInteropHelper helper = new WindowInteropHelper(window);
                helper.Owner = mainWindowHandle;
            }
        }

        private sealed class RoomHazardData : INotifyPropertyChanged
        {
            private string selectedHazard;

            public event PropertyChangedEventHandler PropertyChanged;

            public ElementId RoomId { get; set; }

            public string Name { get; set; }

            public string Number { get; set; }

            public double AreaSquareFeet { get; set; }

            public double AreaSquareMeters { get; set; }

            public IList<IList<BoundarySegment>> BoundarySegments { get; set; }

            public ElementId LevelId { get; set; }

            public double HeightFeet { get; set; }

            public string SelectedHazard
            {
                get
                {
                    return selectedHazard;
                }

                set
                {
                    if (!string.Equals(selectedHazard, value, StringComparison.Ordinal))
                    {
                        selectedHazard = value;
                        OnPropertyChanged();
                    }
                }
            }

            private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private sealed class HazardClassificationWindow : Wpf.Window
        {
            private readonly IReadOnlyList<string> hazardOptions;

            public HazardClassificationWindow(IEnumerable<RoomHazardData> rooms, IReadOnlyList<string> hazardOptions)
            {
                this.hazardOptions = hazardOptions;
                Rooms = new ObservableCollection<RoomHazardData>(rooms);

                Title = "NFPA 13 Room Hazard Classification";
                Width = 1000;
                Height = 650;
                MinWidth = 760;
                MinHeight = 460;
                WindowStartupLocation = Wpf.WindowStartupLocation.CenterOwner;
                Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(245, 247, 250));
                Content = BuildContent();
            }

            public ObservableCollection<RoomHazardData> Rooms { get; }

            private Wpf.UIElement BuildContent()
            {
                WpfControls.Grid root = new WpfControls.Grid
                {
                    Margin = new Wpf.Thickness(18)
                };

                root.RowDefinitions.Add(new WpfControls.RowDefinition { Height = Wpf.GridLength.Auto });
                root.RowDefinitions.Add(new WpfControls.RowDefinition { Height = Wpf.GridLength.Auto });
                root.RowDefinitions.Add(new WpfControls.RowDefinition { Height = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
                root.RowDefinitions.Add(new WpfControls.RowDefinition { Height = Wpf.GridLength.Auto });

                WpfControls.StackPanel headerPanel = new WpfControls.StackPanel
                {
                    Margin = new Wpf.Thickness(0, 0, 0, 12)
                };

                headerPanel.Children.Add(new WpfControls.TextBlock
                {
                    Text = "NFPA 13 Hazard Classification",
                    FontSize = 20,
                    FontWeight = Wpf.FontWeights.SemiBold,
                    Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(31, 41, 55))
                });

                headerPanel.Children.Add(new WpfControls.TextBlock
                {
                    Text = "Review extracted rooms and assign the occupancy hazard group used for sprinkler design.",
                    Margin = new Wpf.Thickness(0, 6, 0, 0),
                    Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(75, 85, 99))
                });

                WpfControls.Grid.SetRow(headerPanel, 0);
                root.Children.Add(headerPanel);

                WpfControls.StackPanel progressPanel = new WpfControls.StackPanel
                {
                    Margin = new Wpf.Thickness(0, 0, 0, 12)
                };

                progressPanel.Children.Add(new WpfControls.TextBlock
                {
                    Text = "Room extraction complete",
                    Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(22, 101, 52)),
                    Margin = new Wpf.Thickness(0, 0, 0, 4)
                });

                progressPanel.Children.Add(new WpfControls.ProgressBar
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = 100,
                    Height = 8,
                    Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(34, 197, 94)),
                    Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(229, 231, 235))
                });

                WpfControls.Grid.SetRow(progressPanel, 1);
                root.Children.Add(progressPanel);

                WpfControls.DataGrid roomGrid = CreateRoomGrid();
                WpfControls.Grid.SetRow(roomGrid, 2);
                root.Children.Add(roomGrid);

                WpfControls.StackPanel buttonPanel = new WpfControls.StackPanel
                {
                    Orientation = WpfControls.Orientation.Horizontal,
                    HorizontalAlignment = Wpf.HorizontalAlignment.Right,
                    Margin = new Wpf.Thickness(0, 14, 0, 0)
                };

                WpfControls.Button saveButton = new WpfControls.Button
                {
                    Content = "Save All",
                    IsDefault = true,
                    MinWidth = 110,
                    Padding = new Wpf.Thickness(16, 8, 16, 8),
                    Margin = new Wpf.Thickness(0, 0, 10, 0),
                    Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(37, 99, 235)),
                    Foreground = WpfMedia.Brushes.White,
                    BorderBrush = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(29, 78, 216))
                };
                saveButton.Click += OnSaveAllClicked;

                WpfControls.Button cancelButton = new WpfControls.Button
                {
                    Content = "Cancel",
                    IsCancel = true,
                    MinWidth = 100,
                    Padding = new Wpf.Thickness(16, 8, 16, 8)
                };
                cancelButton.Click += OnCancelClicked;

                buttonPanel.Children.Add(saveButton);
                buttonPanel.Children.Add(cancelButton);

                WpfControls.Grid.SetRow(buttonPanel, 3);
                root.Children.Add(buttonPanel);

                return root;
            }

            private WpfControls.DataGrid CreateRoomGrid()
            {
                Wpf.Style rightAlignedTextStyle = new Wpf.Style(typeof(WpfControls.TextBlock));
                rightAlignedTextStyle.Setters.Add(
                    new Wpf.Setter(WpfControls.TextBlock.TextAlignmentProperty, Wpf.TextAlignment.Right));

                WpfControls.DataGrid roomGrid = new WpfControls.DataGrid
                {
                    AutoGenerateColumns = false,
                    CanUserAddRows = false,
                    CanUserDeleteRows = false,
                    HeadersVisibility = WpfControls.DataGridHeadersVisibility.Column,
                    ItemsSource = Rooms,
                    RowHeaderWidth = 0,
                    AlternatingRowBackground = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(249, 250, 251)),
                    GridLinesVisibility = WpfControls.DataGridGridLinesVisibility.Horizontal,
                    BorderBrush = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(209, 213, 219)),
                    Background = WpfMedia.Brushes.White
                };

                roomGrid.Columns.Add(new WpfControls.DataGridTextColumn
                {
                    Header = "Room Number",
                    Binding = new WpfData.Binding(nameof(RoomHazardData.Number)),
                    Width = new WpfControls.DataGridLength(120)
                });

                roomGrid.Columns.Add(new WpfControls.DataGridTextColumn
                {
                    Header = "Room Name",
                    Binding = new WpfData.Binding(nameof(RoomHazardData.Name)),
                    Width = new WpfControls.DataGridLength(1, WpfControls.DataGridLengthUnitType.Star)
                });

                roomGrid.Columns.Add(new WpfControls.DataGridTextColumn
                {
                    Header = "Area (sq ft)",
                    Binding = new WpfData.Binding(nameof(RoomHazardData.AreaSquareFeet))
                    {
                        StringFormat = "{0:N2}"
                    },
                    ElementStyle = rightAlignedTextStyle,
                    Width = new WpfControls.DataGridLength(120)
                });

                roomGrid.Columns.Add(new WpfControls.DataGridTextColumn
                {
                    Header = "Area (sq m)",
                    Binding = new WpfData.Binding(nameof(RoomHazardData.AreaSquareMeters))
                    {
                        StringFormat = "{0:N2}"
                    },
                    ElementStyle = rightAlignedTextStyle,
                    Width = new WpfControls.DataGridLength(120)
                });

                roomGrid.Columns.Add(new WpfControls.DataGridComboBoxColumn
                {
                    Header = "NFPA 13 Hazard Classification",
                    ItemsSource = hazardOptions,
                    SelectedItemBinding = new WpfData.Binding(nameof(RoomHazardData.SelectedHazard))
                    {
                        Mode = WpfData.BindingMode.TwoWay,
                        UpdateSourceTrigger = WpfData.UpdateSourceTrigger.PropertyChanged
                    },
                    Width = new WpfControls.DataGridLength(260)
                });

                return roomGrid;
            }

            private void OnSaveAllClicked(object sender, Wpf.RoutedEventArgs e)
            {
                if (Rooms.Any(room => string.IsNullOrWhiteSpace(room.SelectedHazard)))
                {
                    Wpf.MessageBox.Show(
                        this,
                        "Please select a hazard classification for every room before saving.",
                        "Missing Hazard Classification",
                        Wpf.MessageBoxButton.OK,
                        Wpf.MessageBoxImage.Warning);
                    return;
                }

                DialogResult = true;
                Close();
            }

            private void OnCancelClicked(object sender, Wpf.RoutedEventArgs e)
            {
                DialogResult = false;
                Close();
            }
        }

        private sealed class RoomExtractionProgressWindow : Wpf.Window
        {
            private readonly WpfControls.ProgressBar progressBar;
            private readonly WpfControls.TextBlock statusText;

            public RoomExtractionProgressWindow(int totalRooms)
            {
                Title = "Extracting Rooms";
                Width = 420;
                Height = 150;
                ResizeMode = Wpf.ResizeMode.NoResize;
                WindowStartupLocation = Wpf.WindowStartupLocation.CenterOwner;
                Background = WpfMedia.Brushes.White;

                WpfControls.StackPanel panel = new WpfControls.StackPanel
                {
                    Margin = new Wpf.Thickness(18)
                };

                panel.Children.Add(new WpfControls.TextBlock
                {
                    Text = "Extracting Revit room data",
                    FontSize = 16,
                    FontWeight = Wpf.FontWeights.SemiBold,
                    Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(31, 41, 55))
                });

                statusText = new WpfControls.TextBlock
                {
                    Text = totalRooms > 0 ? "Preparing room extraction..." : "No rooms found.",
                    Margin = new Wpf.Thickness(0, 10, 0, 6),
                    TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
                    Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(75, 85, 99))
                };
                panel.Children.Add(statusText);

                progressBar = new WpfControls.ProgressBar
                {
                    Minimum = 0,
                    Maximum = totalRooms > 0 ? totalRooms : 1,
                    Height = 12,
                    IsIndeterminate = totalRooms == 0,
                    Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(37, 99, 235)),
                    Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(229, 231, 235))
                };
                panel.Children.Add(progressBar);

                Content = panel;
            }

            public void UpdateProgress(int currentRoom, int totalRooms, string status)
            {
                progressBar.Maximum = totalRooms > 0 ? totalRooms : 1;
                progressBar.Value = Math.Min(currentRoom, progressBar.Maximum);
                progressBar.IsIndeterminate = totalRooms == 0;
                statusText.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} ({1}/{2})",
                    status,
                    Math.Min(currentRoom, totalRooms),
                    totalRooms);

                Dispatcher.Invoke(
                    WpfThreading.DispatcherPriority.Background,
                    new Action(delegate
                    {
                    }));
            }
        }
    }
}
