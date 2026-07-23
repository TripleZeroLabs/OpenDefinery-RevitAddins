using Autodesk.Revit.DB;
using Microsoft.Win32;
using OpenDefinery;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
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

    /// <summary>One rendered line in the export preview: a number, the raw tab-delimited
    /// text, and its fields split on the tab so each can be shown (and selected) on its own.</summary>
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

    /// <summary>
    /// Reusable "Current Family" panel: lists the shared parameters in the active Revit
    /// document, previews the exact shared-parameter text lines, and either uploads the
    /// selected ones to an OpenDefinery Collection or saves them as a local .txt file. Hosted
    /// both by the standalone Export Parameters window and by the Parameter Manager's
    /// "Current Family" tab, so the two stay identical.
    /// </summary>
    public partial class Control_CurrentFamily : UserControl
    {
        private const string AllDataTypesLabel = "All Data Types";

        private RvtConnector _connector;
        private Definery _session;

        private ICollectionView _view;
        private string _filterText = string.Empty;
        private string _dataTypeFilter = AllDataTypesLabel;
        private List<PreviewLine> _previewLines;

        public ObservableCollection<DocumentSharedParameter> Parameters { get; private set; }

        /// <summary>Raised after a new Collection is created here, so a host can refresh its own lists.</summary>
        public event EventHandler<Collection> CollectionCreated;

        public Control_CurrentFamily()
        {
            InitializeComponent();
        }

        // ---------------------------------------------------------------- host wiring

        /// <summary>
        /// Point the panel at the live Revit connection and read its shared parameters. Pass
        /// null when there is no open document - the panel shows a friendly empty state instead.
        /// </summary>
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

        /// <summary>Supply the authenticated session and load the user's Collections.</summary>
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

            Parameters = new ObservableCollection<DocumentSharedParameter>(found.OrderBy(p => p.Name));
            DataGrid_Params.ItemsSource = Parameters;

            // Filterable/sortable view over the same collection the grid shows.
            _view = CollectionViewSource.GetDefaultView(Parameters);
            _view.Filter = MatchesFilter;

            PopulateDataTypeFilter();
            UpdateCountText();
            UpdatePreview();
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
            var isFamily = _connector != null && _connector.Document != null && _connector.Document.IsFamilyDocument;
            var count = Parameters.Count;

            CountText.Text = string.Format(
                "This {0} contains {1} shared parameter{2}. Select parameters to upload to OpenDefinery or save as a local text file.",
                isFamily ? "family" : "Revit model",
                count,
                count == 1 ? string.Empty : "s");
        }

        /// <summary>Count of shared parameters found, so a host can title itself.</summary>
        public int ParameterCount => Parameters?.Count ?? 0;

        public bool IsFamilyDocument => _connector != null && _connector.Document != null && _connector.Document.IsFamilyDocument;

        // ---------------------------------------------------------------- filtering

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

        // ---------------------------------------------------------------- preview

        private void DataGrid_Params_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePreview();
        }

        /// <summary>
        /// The exact tab-delimited PARAM line Revit writes, matching
        /// DefineryParameter.CreateParamTable: PARAM, GUID, NAME, DATATYPE, DATACATEGORY,
        /// GROUP, VISIBLE, DESCRIPTION, USERMODIFIABLE.
        /// </summary>
        private static string BuildParamLine(DocumentSharedParameter p)
        {
            return string.Join("\t",
                "PARAM",
                p.Guid.ToString(),
                p.Name,
                p.DataType,
                string.Empty,           // data category (n/a for document params)
                "1",                    // group (Default)
                "1",                    // visible
                p.Description ?? string.Empty,
                "1");                   // user modifiable
        }

        private void UpdatePreview()
        {
            if (PreviewList == null) return;

            if (Parameters == null) { PreviewList.ItemsSource = null; return; }

            var selectedSet = new HashSet<DocumentSharedParameter>(SelectedParameters());

            var lines = new List<PreviewLine>();
            var number = 1;

            // Iterate in display order for a stable preview regardless of click order.
            foreach (var p in Parameters)
            {
                if (selectedSet.Contains(p))
                {
                    var raw = BuildParamLine(p);
                    lines.Add(new PreviewLine
                    {
                        Number = number++,
                        Raw = raw,
                        Fields = raw.Split('\t').ToList()
                    });
                }
            }

            _previewLines = lines;
            PreviewList.ItemsSource = _previewLines;
        }

        /// <summary>Clicking a line number copies the whole raw line and highlights that row.</summary>
        private void PreviewGutter_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var line = (sender as FrameworkElement)?.DataContext as PreviewLine;
            if (line == null) return;

            if (_previewLines != null)
            {
                foreach (var l in _previewLines)
                {
                    l.IsRowSelected = ReferenceEquals(l, line);
                }
            }

            try { Clipboard.SetText(line.Raw); }
            catch { /* the clipboard can be transiently locked by another app; ignore */ }

            e.Handled = true;
        }

        /// <summary>
        /// Double-click selects the entire field. Because each tab-delimited field is its own
        /// box, this selects the whole GUID or whole name regardless of hyphens or spaces -
        /// the tab is effectively the only delimiter.
        /// </summary>
        private void PreviewField_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is TextBox tb)
            {
                tb.Focus();
                tb.SelectAll();
                e.Handled = true;
            }
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
        }

        // ---------------------------------------------------------------- new collection

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

                var current = CollectionCombo.ItemsSource as IEnumerable<Collection>;
                var updated = current != null ? new List<Collection>(current) : new List<Collection>();
                updated.Add(created);

                CollectionCombo.ItemsSource = updated;
                CollectionCombo.SelectedItem = created;

                OverlayGrid.Visibility = System.Windows.Visibility.Collapsed;

                CollectionCreated?.Invoke(this, created);

                ShowMessage("Collection Created",
                    string.Format("Created \"{0}\" and selected it as the upload target.", created.Name));
            }
            catch (Exception ex)
            {
                NewCollectionStatus.Text = "Error creating Collection: " + ex.Message;
            }
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

        // ---------------------------------------------------------------- upload (to OpenDefinery)

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

            var collection = CollectionCombo.SelectedItem as Collection;
            if (collection == null)
            {
                ShowMessage("No Collection Selected",
                    "Choose a Collection to upload to, or create a new one.");
                return;
            }

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

                    // Every parameter is handled independently: one failure must never stop
                    // the rest of the batch.
                    try
                    {
                        var existing = DefineryParameter.FromGuid(_session, param.Guid);

                        if (existing != null && existing.Count > 0)
                        {
                            // Already in OpenDefinery. Rather than skip it outright, make sure it
                            // also belongs to the chosen Collection - adding it there if it isn't.
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
                                // AddToCollection rebuilds field_collections from CollectionsString,
                                // so the parameter's other Collections are preserved and this one
                                // is simply added.
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

            // Newly-uploaded parameters are now in the Collection: refresh the green checks.
            UpdateInCollectionFlags(collection);

            Window_ExportResults.Show(OwnerWindow, collection.Name, results);
        }

        // ---------------------------------------------------------------- export (local .txt)

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = SelectedParameters();
            if (selected.Count == 0)
            {
                ShowMessage("Nothing Selected",
                    "Select one or more parameters to save. Hold Ctrl or Shift to select several.");
                return;
            }

            // Reuse the shared-parameter file format used everywhere else.
            var defineryParams = selected.Select(p => new DefineryParameter
            {
                Guid = p.Guid,
                Name = p.Name,
                DataType = p.DataType,
                Description = p.Description ?? string.Empty,
                Visible = "1",
                UserModifiable = "1"
            }).ToList();

            var content = DefineryParameter.CreateParamTable(defineryParams);

            // Not auto-named: these parameters may not belong to any Collection.
            var dialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt",
                DefaultExt = ".txt",
                FileName = "SharedParameters.txt",
                RestoreDirectory = true
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                File.WriteAllText(dialog.FileName, content);
                ShowMessage("File Saved",
                    string.Format("Saved {0} parameter{1} to:\n\n{2}",
                        selected.Count, selected.Count == 1 ? string.Empty : "s", dialog.FileName));
            }
            catch (Exception ex)
            {
                ShowMessage("Could Not Save File", ex.Message);
            }
        }

        // ---------------------------------------------------------------- helpers

        private Window OwnerWindow => Window.GetWindow(this);

        private void ShowMessage(string heading, string message)
        {
            Window_Message.Show(OwnerWindow, heading, message);
        }
    }
}
