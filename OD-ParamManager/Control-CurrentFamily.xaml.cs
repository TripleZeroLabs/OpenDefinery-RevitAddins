using Autodesk.Revit.DB;
using OpenDefinery;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace OD_ParamManager
{
    /// <summary>A shared parameter discovered in the currently open Revit document.</summary>
    public class DocumentSharedParameter : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public Guid Guid { get; set; }
        public string DataType { get; set; }
        public string Description { get; set; }

        private bool _inSelectedCollection;

        /// <summary>True when this parameter already exists in the Collection chosen in the toolbar.</summary>
        public bool InSelectedCollection
        {
            get => _inSelectedCollection;
            set
            {
                if (_inSelectedCollection != value)
                {
                    _inSelectedCollection = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InSelectedCollection)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>One rendered line in the export preview: a number, the raw tab-delimited text,
    /// and its fields split on the tab so each can be shown (and selected) on its own.</summary>
    public class PreviewLine : INotifyPropertyChanged
    {
        public int Number { get; set; }
        public string Raw { get; set; }
        public List<string> Fields { get; set; }

        private bool _isRowSelected;
        public bool IsRowSelected
        {
            get => _isRowSelected;
            set
            {
                if (_isRowSelected != value)
                {
                    _isRowSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRowSelected)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>Natural (alphanumeric) sort for the document-parameter grid columns.</summary>
    internal class NaturalParamComparer : IComparer
    {
        private readonly string _path;
        private readonly ListSortDirection _direction;

        public NaturalParamComparer(string path, ListSortDirection direction)
        {
            _path = path;
            _direction = direction;
        }

        public int Compare(object x, object y)
        {
            var a = x as DocumentSharedParameter;
            var b = y as DocumentSharedParameter;

            int c;
            switch (_path)
            {
                case "DataType":
                    c = NaturalSort.Compare(a?.DataType, b?.DataType);
                    break;
                case "InSelectedCollection":
                    c = (a != null && a.InSelectedCollection).CompareTo(b != null && b.InSelectedCollection);
                    break;
                default:
                    c = NaturalSort.Compare(a?.Name, b?.Name);
                    break;
            }

            return _direction == ListSortDirection.Ascending ? c : -c;
        }
    }

    /// <summary>
    /// Reusable "Current Model" panel: lists the shared parameters in the active Revit document,
    /// validates them against an OpenDefinery Collection (green check), and offers Upload / Download
    /// (preview + save) / Delete for the selected parameters. Hosted both by the standalone Export
    /// window and by the Parameter Manager's "Current Model" tab, so the two stay identical.
    /// </summary>
    public partial class Control_CurrentFamily : UserControl
    {
        private const string AllDataTypesLabel = "All Data Types";
        private const string InCollAll = "All";
        private const string InCollYes = "In Collection";
        private const string InCollNo = "Not in Collection";

        private RvtConnector _connector;
        private Definery _session;

        private ICollectionView _view;
        private string _filterText = string.Empty;
        private string _dataTypeFilter = AllDataTypesLabel;
        private string _inCollectionFilter = InCollAll;

        public ObservableCollection<DocumentSharedParameter> Parameters { get; private set; }

        /// <summary>Raised after a new Collection is created here, so a host can refresh its own lists.</summary>
        public event EventHandler<Collection> CollectionCreated;

        public Control_CurrentFamily()
        {
            InitializeComponent();

            // Static options; set once so reloads don't reset the user's choice.
            InCollectionFilterCombo.ItemsSource = new[] { InCollAll, InCollYes, InCollNo };
            InCollectionFilterCombo.SelectedIndex = 0;
        }

        // ---------------------------------------------------------------- host wiring

        public void SetConnector(RvtConnector connector)
        {
            _connector = connector;

            if (_connector == null || _connector.Document == null)
            {
                MainContent.Visibility = System.Windows.Visibility.Collapsed;
                NotConnectedPanel.Visibility = System.Windows.Visibility.Visible;
                Parameters = new ObservableCollection<DocumentSharedParameter>();
                return;
            }

            MainContent.Visibility = System.Windows.Visibility.Visible;
            NotConnectedPanel.Visibility = System.Windows.Visibility.Collapsed;
            LoadDocumentParameters();
        }

        public void SetSession(Definery session)
        {
            _session = session;
            EnsureReferenceData();
            LoadCollections();
        }

        // ---------------------------------------------------------------- document params

        private void LoadDocumentParameters()
        {
            var found = new List<DocumentSharedParameter>();
            var doc = _connector.Document;

            try
            {
                if (doc.IsFamilyDocument)
                {
                    foreach (FamilyParameter fp in doc.FamilyManager.GetParameters())
                    {
                        if (!fp.IsShared) continue;

                        found.Add(new DocumentSharedParameter
                        {
                            Name = fp.Definition.Name,
                            Guid = fp.GUID,
                            DataType = RvtCompat.GetDataTypeToken(fp.Definition)
                        });
                    }
                }
                else
                {
                    var collector = new FilteredElementCollector(doc).OfClass(typeof(SharedParameterElement));

                    foreach (var element in collector)
                    {
                        var shared = element as SharedParameterElement;
                        if (shared == null) continue;

                        var definition = shared.GetDefinition();

                        found.Add(new DocumentSharedParameter
                        {
                            Name = definition.Name,
                            Guid = shared.GuidValue,
                            DataType = RvtCompat.GetDataTypeToken(definition)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Could Not Read Parameters",
                    "There was a problem reading shared parameters from the document:\n\n" + ex.Message);
            }

            Parameters = new ObservableCollection<DocumentSharedParameter>(found);
            DataGrid_Params.ItemsSource = Parameters;

            // Filterable/sortable view; default to a natural sort by name.
            _view = CollectionViewSource.GetDefaultView(Parameters);
            _view.Filter = MatchesFilter;

            if (_view is ListCollectionView lcv)
            {
                lcv.CustomSort = new NaturalParamComparer("Name", ListSortDirection.Ascending);
            }

            var nameColumn = DataGrid_Params.Columns.FirstOrDefault(c => c.SortMemberPath == "Name");
            if (nameColumn != null) nameColumn.SortDirection = ListSortDirection.Ascending;

            PopulateDataTypeFilter();
            UpdateCountText();
            UpdateSelectionActions();
        }

        private void PopulateDataTypeFilter()
        {
            var types = Parameters
                .Select(p => p.DataType)
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            var items = new List<string> { AllDataTypesLabel };
            items.AddRange(types);

            DataTypeFilterCombo.ItemsSource = items;
            DataTypeFilterCombo.SelectedIndex = 0;
        }

        private void UpdateCountText()
        {
            var count = Parameters.Count;
            CountText.Text = string.Format("{0} shared parameter{1}", count, count == 1 ? string.Empty : "s");
        }

        public int ParameterCount => Parameters?.Count ?? 0;

        public bool IsFamilyDocument => _connector != null && _connector.Document != null && _connector.Document.IsFamilyDocument;

        // ---------------------------------------------------------------- filtering / sorting

        private bool MatchesFilter(object item)
        {
            var p = item as DocumentSharedParameter;
            if (p == null) return false;

            if (!string.IsNullOrEmpty(_filterText))
            {
                var name = (p.Name ?? string.Empty).ToLowerInvariant();
                var guid = p.Guid.ToString().ToLowerInvariant();

                if (!name.Contains(_filterText) && !guid.Contains(_filterText))
                {
                    return false;
                }
            }

            if (_dataTypeFilter != AllDataTypesLabel && p.DataType != _dataTypeFilter)
            {
                return false;
            }

            if (_inCollectionFilter == InCollYes && !p.InSelectedCollection) return false;
            if (_inCollectionFilter == InCollNo && p.InSelectedCollection) return false;

            return true;
        }

        private void Filter_Changed(object sender, TextChangedEventArgs e)
        {
            _filterText = (FilterTextBox.Text ?? string.Empty).Trim().ToLowerInvariant();
            _view?.Refresh();
        }

        private void DataTypeFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            _dataTypeFilter = DataTypeFilterCombo.SelectedItem as string ?? AllDataTypesLabel;
            _view?.Refresh();
        }

        private void InCollectionFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            _inCollectionFilter = InCollectionFilterCombo.SelectedItem as string ?? InCollAll;
            _view?.Refresh();
        }

        /// <summary>Natural sort on column-header clicks (so Parameter #2 &lt; Parameter #10).</summary>
        private void DataGrid_Params_Sorting(object sender, DataGridSortingEventArgs e)
        {
            var column = e.Column;
            var path = column.SortMemberPath;
            if (string.IsNullOrEmpty(path)) return;

            e.Handled = true;

            var direction = column.SortDirection != ListSortDirection.Ascending
                ? ListSortDirection.Ascending
                : ListSortDirection.Descending;

            column.SortDirection = direction;
            foreach (var c in DataGrid_Params.Columns)
            {
                if (c != column) c.SortDirection = null;
            }

            if (_view is ListCollectionView lcv)
            {
                lcv.CustomSort = new NaturalParamComparer(path, direction);
            }
        }

        // ---------------------------------------------------------------- selection

        private void DataGrid_Params_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSelectionActions();
        }

        private void UpdateSelectionActions()
        {
            SelectionActions.Visibility = DataGrid_Params.SelectedItems.Count > 0
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }

        private List<DocumentSharedParameter> SelectedParameters()
        {
            return DataGrid_Params.SelectedItems.Cast<DocumentSharedParameter>().ToList();
        }

        // ---------------------------------------------------------------- reference data / collections

        private void EnsureReferenceData()
        {
            if (_session == null) return;

            try
            {
                if (_session.DataTypes == null || _session.DataTypes.Count == 0)
                {
                    Definery.LoadData(_session);
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Reference Data", "Could not load OpenDefinery reference data:\n\n" + ex.Message);
            }
        }

        private void LoadCollections()
        {
            if (_session == null) return;

            try
            {
                var collections = Collection.ByCurrentUser(_session) ?? new List<Collection>();

                var selectedId = (CollectionCombo.SelectedItem as Collection)?.Id;

                CollectionCombo.ItemsSource = collections;

                if (collections.Count > 0)
                {
                    var reselect = selectedId.HasValue
                        ? collections.FirstOrDefault(c => c.Id == selectedId.Value)
                        : null;

                    CollectionCombo.SelectedItem = reselect ?? collections[0];
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Could Not Load Collections", ex.Message);
            }
        }

        /// <summary>Reload the Collections list (e.g. after one is created or deleted elsewhere).</summary>
        public void RefreshCollections() => LoadCollections();

        private void CollectionCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            UpdateInCollectionFlags(CollectionCombo.SelectedItem as Collection);
        }

        /// <summary>
        /// Mark each document parameter that already exists in the chosen Collection, so the grid
        /// can show a green check. One lightweight API call per Collection selection.
        /// </summary>
        private void UpdateInCollectionFlags(Collection collection)
        {
            if (Parameters == null) return;

            var guids = new HashSet<Guid>();

            if (_session != null && collection != null)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                try
                {
                    var lite = Collection.GetIds(_session, collection);
                    if (lite != null)
                    {
                        foreach (var lp in lite)
                        {
                            guids.Add(lp.Guid);
                        }
                    }
                }
                catch
                {
                    // A failed lookup just means no checks - not worth interrupting the user.
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }

            foreach (var p in Parameters)
            {
                p.InSelectedCollection = guids.Contains(p.Guid);
            }

            // The "In Collection" filter depends on these flags, so re-apply it.
            _view?.Refresh();
        }

        private Collection SelectedValidationCollection => CollectionCombo.SelectedItem as Collection;

        // ---------------------------------------------------------------- new collection (tool row)

        private void NewCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null) { ShowMessage("Sign In Required", "Sign in to OpenDefinery to create a Collection."); return; }

            NewCollectionStatus.Text = string.Empty;
            NewCollectionName.Text = string.Empty;
            NewCollectionDescription.Text = string.Empty;
            NewCollectionPublic.IsChecked = false;

            OverlayGrid.Visibility = System.Windows.Visibility.Visible;
            NewCollectionName.Focus();
        }

        private void CancelNewCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            OverlayGrid.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void CreateCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            NewCollectionStatus.Text = string.Empty;

            var name = NewCollectionName.Text;
            if (string.IsNullOrWhiteSpace(name))
            {
                NewCollectionStatus.Text = "Enter a name for the Collection.";
                return;
            }

            try
            {
                var created = Collection.Create(
                    _session,
                    name.Trim(),
                    NewCollectionDescription.Text ?? string.Empty,
                    NewCollectionPublic.IsChecked ?? false);

                if (created == null)
                {
                    NewCollectionStatus.Text = "Could not create the Collection. Check your permissions and try again.";
                    return;
                }

                AddCollectionToCombo(created);
                OverlayGrid.Visibility = System.Windows.Visibility.Collapsed;

                CollectionCreated?.Invoke(this, created);
            }
            catch (Exception ex)
            {
                NewCollectionStatus.Text = "Error creating Collection: " + ex.Message;
            }
        }

        private void AddCollectionToCombo(Collection created)
        {
            var current = CollectionCombo.ItemsSource as IEnumerable<Collection>;
            var updated = current != null ? new List<Collection>(current) : new List<Collection>();

            if (updated.All(c => c.Id != created.Id)) updated.Add(created);

            CollectionCombo.ItemsSource = updated;
            CollectionCombo.SelectedItem = updated.First(c => c.Id == created.Id);
        }

        /// <summary>Escape closes the New Collection overlay when it's open. Returns true if handled.</summary>
        public bool HandleEscape()
        {
            if (OverlayGrid.Visibility == System.Windows.Visibility.Visible)
            {
                OverlayGrid.Visibility = System.Windows.Visibility.Collapsed;
                return true;
            }

            return false;
        }

        // ---------------------------------------------------------------- upload

        private void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null) { ShowMessage("Sign In Required", "Sign in to OpenDefinery to upload parameters."); return; }

            var selected = SelectedParameters();
            if (selected.Count == 0)
            {
                ShowMessage("Nothing Selected",
                    "Select one or more parameters to upload. Hold Ctrl or Shift to select several.");
                return;
            }

            var collections = CollectionCombo.ItemsSource as IEnumerable<Collection>;

            var picker = new Window_UploadCollection(_session, collections, SelectedValidationCollection, selected.Count)
            {
                Owner = OwnerWindow
            };

            var confirmed = picker.ShowDialog();

            // Reflect any newly-created Collection in the tab's own combo.
            if (picker.CollectionsChanged && picker.SelectedCollection != null)
            {
                AddCollectionToCombo(picker.SelectedCollection);
            }

            if (confirmed == true && picker.SelectedCollection != null)
            {
                RunUpload(picker.SelectedCollection, selected);
            }
        }

        private void RunUpload(Collection collection, List<DocumentSharedParameter> selected)
        {
            var results = new List<ExportResult>();

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                foreach (var param in selected)
                {
                    var result = new ExportResult
                    {
                        Name = param.Name,
                        Guid = param.Guid.ToString()
                    };

                    try
                    {
                        var existing = DefineryParameter.FromGuid(_session, param.Guid);

                        if (existing != null && existing.Count > 0)
                        {
                            var odParam = existing.First();

                            var inThisCollection =
                                !string.IsNullOrEmpty(odParam.CollectionsString) &&
                                odParam.CollectionsString
                                    .Split(',')
                                    .Select(s => s.Trim())
                                    .Contains(collection.Id.ToString());

                            if (inThisCollection)
                            {
                                result.Outcome = ExportOutcome.AlreadyExists;
                                result.Message = "Already in OpenDefinery and in this Collection.";
                            }
                            else
                            {
                                var addResponse = DefineryParameter.AddToCollection(_session, odParam, collection.Id);
                                var code = (int)addResponse.StatusCode;

                                if (code >= 200 && code < 300)
                                {
                                    result.Outcome = ExportOutcome.Added;
                                    result.Message = "Already in OpenDefinery - added to this Collection.";
                                }
                                else
                                {
                                    result.Outcome = ExportOutcome.Failed;
                                    result.Message = "Exists in OpenDefinery, but could not be added to this Collection.";
                                }
                            }
                        }
                        else
                        {
                            var dataType = DataType.GetFromName(_session, param.DataType);

                            if (dataType == null)
                            {
                                result.Outcome = ExportOutcome.Failed;
                                result.Message = string.Format(
                                    "Data type \"{0}\" is not available in OpenDefinery.",
                                    string.IsNullOrEmpty(param.DataType) ? "(unknown)" : param.DataType);
                            }
                            else
                            {
                                var newParam = new DefineryParameter
                                {
                                    Guid = param.Guid,
                                    Name = param.Name,
                                    DataType = param.DataType,
                                    Description = param.Description ?? string.Empty,
                                    Visible = "1",
                                    UserModifiable = "1"
                                };

                                var createdParam = DefineryParameter.Create(
                                    _session, newParam, collection.Id, null,
                                    param.Name, param.Description ?? string.Empty);

                                if (createdParam != null)
                                {
                                    result.Outcome = ExportOutcome.Added;
                                    result.Message = string.Empty;
                                }
                                else
                                {
                                    result.Outcome = ExportOutcome.Failed;
                                    result.Message = "OpenDefinery rejected the request (check the name, description, and your permissions).";
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Outcome = ExportOutcome.Failed;
                        result.Message = ex.Message;
                    }

                    results.Add(result);
                }
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            // Reflect the chosen Collection in the tab so the green checks match what was uploaded.
            AddCollectionToCombo(collection);
            UpdateInCollectionFlags(collection);

            Window_ExportResults.Show(OwnerWindow, collection.Name, results);
        }

        // ---------------------------------------------------------------- download (preview + save)

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            // Always the whole (filtered) list, never just the row selection.
            var all = DataGrid_Params.Items.Cast<DocumentSharedParameter>().ToList();
            if (all.Count == 0)
            {
                ShowMessage("No Parameters", "There are no parameters in the list to preview or save.");
                return;
            }

            var preview = new Window_ParamPreview(all) { Owner = OwnerWindow };
            preview.ShowDialog();
        }

        // ---------------------------------------------------------------- delete

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_connector == null || _connector.Document == null)
            {
                ShowMessage("No Model Open", "Open a family or project in Revit to delete parameters.");
                return;
            }

            var selected = SelectedParameters();
            if (selected.Count == 0)
            {
                ShowMessage("Nothing Selected", "Select one or more parameters to delete.");
                return;
            }

            var isFamily = _connector.Document.IsFamilyDocument;

            if (!Window_DeleteParams.Confirm(OwnerWindow, selected.Count, isFamily)) return;

            List<string> failures;
            int deleted;

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                deleted = _connector.DeleteParams(selected.Select(p => p.Guid).ToList(), out failures);
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                ShowMessage("Could Not Delete", ex.Message);
                return;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            // Refresh the list so the deleted parameters drop out, then re-validate.
            LoadDocumentParameters();
            UpdateInCollectionFlags(SelectedValidationCollection);

            var message = string.Format("Deleted {0} parameter{1} from the {2}.",
                deleted, deleted == 1 ? string.Empty : "s", isFamily ? "family" : "project");

            if (failures != null && failures.Count > 0)
            {
                message += "\n\nCould not delete:\n" + string.Join("\n", failures);
            }

            ShowMessage("Delete Complete", message);
        }

        // ---------------------------------------------------------------- helpers

        private Window OwnerWindow => Window.GetWindow(this);

        private void ShowMessage(string heading, string message)
        {
            Window_Message.Show(OwnerWindow, heading, message);
        }
    }
}
