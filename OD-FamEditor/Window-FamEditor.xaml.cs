using Autodesk.Revit.DB;
using OD_Common;
using OpenDefinery;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace OD_FamEditor
{
    /// <summary>
    /// Family Editor shell: family types in the left sidebar; parameters on the right in a grid
    /// grouped by parameter group (collapsible), with name search and a data-type filter.
    /// Read-only in this phase - inline editing, collection column, and swap come next.
    /// </summary>
    public partial class Window_FamEditor : Window
    {
        private const string AllDataTypesLabel = "All Data Types";
        private const string AllGroupsLabel = "All Groups";
        private const string AllBindingsLabel = "All Bindings";

        private readonly RvtConnector _connector;
        private readonly Document _doc;

        private List<FamParamRow> _rows = new List<FamParamRow>();
        private ICollectionView _view;

        private string _search = string.Empty;
        private string _dataTypeFilter = AllDataTypesLabel;
        private string _groupFilter = AllGroupsLabel;
        private string _bindingFilter = AllBindingsLabel;

        private Definery _session;

        public Window_FamEditor(RvtConnector connector)
        {
            InitializeComponent();

            _connector = connector;
            _doc = connector?.Document;

            if (_doc == null || !_doc.IsFamilyDocument)
            {
                MainContent.Visibility = System.Windows.Visibility.Collapsed;
                NotFamilyPanel.Visibility = System.Windows.Visibility.Visible;
                return;
            }

            Load();
        }

        private void Load()
        {
            // Types
            var types = FamEditorReader.ReadTypes(_doc);
            TypesList.ItemsSource = types;

            // Parameters
            _rows = FamEditorReader.ReadParams(_doc);

            RebuildView();

            PopulateDataTypeFilter();
            PopulateGroupFilter();
            PopulateBindingFilter();

            HeaderText.Text = string.Format(
                "{0} — {1} parameter{2}, {3} type{4}",
                _doc.Title,
                _rows.Count, _rows.Count == 1 ? "" : "s",
                types.Count, types.Count == 1 ? "" : "s");

            // Default to the current type so values populate immediately.
            var current = _doc.FamilyManager.CurrentType;
            TypesList.SelectedItem = current != null
                ? types.FirstOrDefault(t => t.Type.Name == current.Name)
                : types.FirstOrDefault();
            if (TypesList.SelectedItem == null) TypesList.SelectedItem = types.FirstOrDefault();

            SetupSession();
        }

        // ---------------------------------------------------------------- session / collections

        private void SetupSession()
        {
            // Reuse the session established in Parameter Manager (shared static) if present.
            _session = Definery.Current;
            EnsureSessionData();
            UpdateSessionUi();
            if (_session != null) LoadCollections();
        }

        /// <summary>
        /// Make sure the session has the reference data the Collection calls depend on. A lightweight
        /// sign-in (Definery.Init) only sets the auth tokens - it does NOT load DataTypes (needed to
        /// match parameter types for Swap) or PublishedCollections (needed by Collection.GetParameters,
        /// which otherwise throws and returns an empty list). Hydrate both once, on demand.
        /// </summary>
        private void EnsureSessionData()
        {
            if (_session == null) return;
            try
            {
                if (_session.DataTypes == null || _session.DataTypes.Count == 0)
                    Definery.LoadData(_session);
                if (_session.PublishedCollections == null)
                    _session.PublishedCollections = Collection.GetPublished(_session);
            }
            catch (System.Exception ex)
            {
                ShowMessage("Could Not Load OpenDefinery Data", ex.Message);
            }
        }

        private void UpdateSessionUi()
        {
            var signedIn = _session != null && !string.IsNullOrEmpty(_session.AuthCode);
            SignInButton.Visibility = signedIn ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            CollectionCombo.IsEnabled = signedIn;
        }

        private void LoadCollections()
        {
            try
            {
                var collections = Collection.ByCurrentUser(_session) ?? new List<Collection>();
                CollectionCombo.ItemsSource = collections;

                // Preselect the Collection last used in any add-in.
                var lastId = SharedSettings.LastCollectionId;
                var pick = lastId.HasValue ? collections.FirstOrDefault(c => c.Id == lastId.Value) : null;
                CollectionCombo.SelectedItem = pick ?? collections.FirstOrDefault();
            }
            catch (System.Exception ex)
            {
                ShowMessage("Could Not Load Collections", ex.Message);
            }
        }

        private void CollectionCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Remember the choice so every add-in window preselects the same Collection.
            var collection = CollectionCombo.SelectedItem as Collection;
            if (collection != null) SharedSettings.LastCollectionId = collection.Id;

            UpdateMembership();
        }

        private void UpdateMembership()
        {
            var collection = CollectionCombo.SelectedItem as Collection;
            var guids = FamEditorCollections.MembershipGuids(_session, collection);

            foreach (var r in _rows)
            {
                r.InSelectedCollection = r.IsShared && guids.Contains(r.Guid);
            }
        }

        private void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            LoginStatus.Text = string.Empty;
            LoginOverlay.Visibility = System.Windows.Visibility.Visible;
            UsernameTextBox.Focus();
        }

        private void LoginCancel_Click(object sender, RoutedEventArgs e)
        {
            LoginOverlay.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            LoginStatus.Text = string.Empty;

            var username = UsernameTextBox.Text;
            var password = PasswordBox.Password;
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                LoginStatus.Text = "Enter a username and password.";
                return;
            }

            Definery session;
            try { session = Definery.Init(new Definery(), username, password); }
            catch (System.Exception ex) { LoginStatus.Text = "Login error: " + ex.Message; return; }

            if (session == null || string.IsNullOrEmpty(session.CsrfToken))
            {
                LoginStatus.Text = "Login failed. Check your credentials and try again.";
                return;
            }

            _session = session;
            EnsureSessionData();
            LoginOverlay.Visibility = System.Windows.Visibility.Collapsed;
            UpdateSessionUi();
            LoadCollections();
        }

        // ---------------------------------------------------------------- add to collection / swap

        private void AddToCollection_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null) { SignInButton_Click(sender, e); return; }

            var row = (sender as FrameworkElement)?.DataContext as FamParamRow;
            var collection = CollectionCombo.SelectedItem as Collection;
            if (row == null) return;
            if (collection == null) { ShowMessage("No Collection", "Choose a Collection first."); return; }

            Mouse.OverrideCursor = Cursors.Wait;
            bool ok; string error;
            try { ok = FamEditorCollections.AddToCollection(_session, row, collection, out error); }
            finally { Mouse.OverrideCursor = null; }

            if (ok)
            {
                row.InSelectedCollection = true;
                ShowToast(string.Format("Added \"{0}\" to {1}.", row.Name, collection.Name), row, collection);
            }
            else
            {
                ShowMessage("Could Not Add to Collection", error);
            }
        }

        private void Swap_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null) { SignInButton_Click(sender, e); return; }

            var row = (sender as FrameworkElement)?.DataContext as FamParamRow;
            var collection = CollectionCombo.SelectedItem as Collection;
            if (row == null) return;
            if (collection == null) { ShowMessage("No Collection", "Choose a Collection to swap from first."); return; }

            var familyGuids = new HashSet<System.Guid>(_rows.Where(r => r.IsShared).Select(r => r.Guid));

            Mouse.OverrideCursor = Cursors.Wait;
            List<DefineryParameter> candidates;
            try { candidates = FamEditorCollections.SwapCandidates(_session, collection, row.DataType, familyGuids); }
            finally { Mouse.OverrideCursor = null; }

            if (candidates.Count == 0)
            {
                ShowMessage("No Swap Targets",
                    "The selected Collection has no parameters of type \"" + row.DataType + "\" that aren't already in this family.");
                return;
            }

            var picker = new Window_SwapFam(row, candidates) { Owner = this };
            if (picker.ShowDialog() != true || picker.SelectedTarget == null) return;

            Mouse.OverrideCursor = Cursors.Wait;
            bool ok; string error;
            try { ok = FamEditorOps.ReplaceWithShared(_doc, _connector.App, row.Param, picker.SelectedTarget, out error); }
            finally { Mouse.OverrideCursor = null; }

            if (!ok) { ShowMessage("Swap Failed", error); return; }

            // Reload just the grid so the swapped-in parameter and updated formulas show.
            ReloadParams();
            ShowMessage("Swap Complete",
                string.Format("Swapped \"{0}\" for \"{1}\". Review any dependent formulas.", row.Name, picker.SelectedTarget.Name));
        }

        private void PopulateDataTypeFilter()
        {
            var items = new List<string> { AllDataTypesLabel };
            items.AddRange(_rows.Select(r => r.DataType)
                                .Where(t => !string.IsNullOrEmpty(t))
                                .Distinct()
                                .OrderBy(t => t));

            DataTypeFilter.ItemsSource = items;
            DataTypeFilter.SelectedIndex = 0;
        }

        private void PopulateGroupFilter()
        {
            var items = new List<string> { AllGroupsLabel };
            items.AddRange(_rows.Select(r => r.GroupLabel)
                                .Where(g => !string.IsNullOrEmpty(g))
                                .Distinct()
                                .OrderBy(g => g));

            GroupFilter.ItemsSource = items;
            GroupFilter.SelectedIndex = 0;
        }

        private void PopulateBindingFilter()
        {
            // Type/Instance describe the binding; Shared/Family/Built-in describe the kind.
            BindingFilter.ItemsSource = new List<string>
            {
                AllBindingsLabel, "Type", "Instance", "Shared", "Family", "Built-in"
            };
            BindingFilter.SelectedIndex = 0;
        }

        private void TypesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = TypesList.SelectedItem as FamTypeItem;
            FamEditorReader.ApplyTypeValues(_doc, item?.Type, _rows);
        }

        // ---------------------------------------------------------------- filtering

        private bool MatchesFilter(object obj)
        {
            var row = obj as FamParamRow;
            if (row == null) return false;

            if (!string.IsNullOrEmpty(_search) &&
                (row.Name ?? string.Empty).ToLowerInvariant().IndexOf(_search) < 0)
            {
                return false;
            }

            if (_dataTypeFilter != AllDataTypesLabel && row.DataType != _dataTypeFilter)
            {
                return false;
            }

            if (_groupFilter != AllGroupsLabel && row.GroupLabel != _groupFilter)
            {
                return false;
            }

            if (_bindingFilter != AllBindingsLabel)
            {
                // Type/Instance match the binding column; Shared/Family/Built-in match the kind.
                var matches = row.Binding == _bindingFilter || row.Kind == _bindingFilter;
                if (!matches) return false;
            }

            return true;
        }

        private void Search_Changed(object sender, TextChangedEventArgs e)
        {
            _search = (SearchBox.Text ?? string.Empty).Trim().ToLowerInvariant();
            _view?.Refresh();
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            _dataTypeFilter = DataTypeFilter.SelectedItem as string ?? AllDataTypesLabel;
            _groupFilter = GroupFilter.SelectedItem as string ?? AllGroupsLabel;
            _bindingFilter = BindingFilter.SelectedItem as string ?? AllBindingsLabel;
            _view?.Refresh();
        }

        // ---------------------------------------------------------------- editing

        private FamilyType CurrentType => (TypesList.SelectedItem as FamTypeItem)?.Type;

        /// <summary>Commit an edited text value on focus loss (only when it actually changed).</summary>
        private void Value_Committed(object sender, KeyboardFocusChangedEventArgs e)
        {
            var tb = sender as TextBox;
            var row = tb?.DataContext as FamParamRow;
            if (row == null) return;
            if (tb.Text == (row.Value ?? string.Empty)) return;

            string error;
            if (!FamEditorOps.SetValue(_doc, CurrentType, row.Param, tb.Text, out error))
            {
                ShowMessage("Edit Failed", error);
            }
            RefreshDynamic();
        }

        /// <summary>Commit a Yes/No tri-state checkbox change.</summary>
        private void YesNo_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.DataContext as FamParamRow;
            if (row == null) return;

            string error;
            if (!FamEditorOps.SetYesNo(_doc, CurrentType, row.Param, row.BoolValue, out error))
            {
                ShowMessage("Edit Failed", error);
            }
            RefreshDynamic();
        }

        /// <summary>Open the formula modal and apply the result.</summary>
        private void Formula_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.DataContext as FamParamRow;
            if (row == null) return;

            var dlg = new Window_FormulaEditor(row.Name, row.Formula) { Owner = this };
            if (dlg.ShowDialog() != true) return;

            string error;
            if (!FamEditorOps.SetFormula(_doc, row.Param, dlg.Formula, out error))
            {
                ShowMessage("Formula Error", FriendlyFormulaError(error));
                return;
            }
            RefreshDynamic();
        }

        /// <summary>Copy a cell/GUID value (button Tag) to the clipboard.</summary>
        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            var text = (sender as FrameworkElement)?.Tag as string;
            if (string.IsNullOrEmpty(text)) return;
            try { Clipboard.SetText(text); } catch { }
        }

        /// <summary>Rename a family parameter (family, non-built-in only), then reload the grid.</summary>
        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.DataContext as FamParamRow;
            if (row == null || !row.CanEditName) return;

            var newName = Window_Dialog.Prompt(this, "Rename Parameter",
                "New name for \"" + row.Name + "\":", row.Name);
            if (newName == null || newName == row.Name) return;

            string error;
            if (!FamEditorOps.Rename(_doc, row.Param, newName, out error))
            {
                ShowMessage("Rename Failed", error);
                return;
            }
            ReloadParams();
        }

        /// <summary>
        /// Reload only the parameter grid — rows, values, membership, and the header count — without
        /// rebuilding the type sidebar / filters / session, so an edit doesn't flash the whole window.
        /// Preserves the selected family type and the active filters.
        /// </summary>
        private void ReloadParams()
        {
            var selectedType = TypesList.SelectedItem as FamTypeItem;

            _rows = FamEditorReader.ReadParams(_doc);
            RebuildView();

            if (selectedType?.Type != null)
                FamEditorReader.ApplyTypeValues(_doc, selectedType.Type, _rows);

            UpdateMembership();
            UpdateHeaderCounts();
        }

        private void UpdateHeaderCounts()
        {
            var typeCount = TypesList.Items.Count;
            HeaderText.Text = string.Format(
                "{0} — {1} parameter{2}, {3} type{4}",
                _doc.Title,
                _rows.Count, _rows.Count == 1 ? "" : "s",
                typeCount, typeCount == 1 ? "" : "s");
        }

        // ---------------------------------------------------------------- kind (family <-> shared)

        private bool _suppressKind;

        private void Kind_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressKind) return;

            var combo = sender as ComboBox;
            var row = combo?.DataContext as FamParamRow;
            if (row == null) return;

            var newKind = combo.SelectedValue as string;
            var actual = row.IsBuiltIn ? "Built-in" : (row.Param.IsShared ? "Shared" : "Family");
            if (newKind == null || newKind == actual) return;

            // Defer so the combo's own selection settles before we open a modal, and so the
            // displayed "Shared" reverts cleanly unless a valid shared parameter is chosen. The
            // Kind is only OneWay-bound, so the actual value doesn't change until the reload.
            Dispatcher.BeginInvoke(new System.Action(() => HandleKindChange(combo, row, actual, newKind)));
        }

        private void HandleKindChange(ComboBox combo, FamParamRow row, string actual, string newKind)
        {
            if (actual != "Family" || newKind != "Shared")
            {
                ShowMessage("Not Supported",
                    "Converting a shared parameter back to a non-shared family parameter isn't supported by the Revit API. Only Family → Shared is available.");
                RevertKindCombo(combo, actual);
                return;
            }

            if (_session == null)
            {
                // Never leave the dropdown on "Shared" - roll it back and prompt sign-in.
                RevertKindCombo(combo, actual);
                SignInButton_Click(null, null);
                return;
            }

            var collection = CollectionCombo.SelectedItem as Collection;
            var collections = CollectionCombo.ItemsSource as IEnumerable<Collection>;
            var familyGuids = new HashSet<System.Guid>(_rows.Where(r => r.IsShared).Select(r => r.Guid));

            var picker = new Window_MakeShared(row.Name, _session, collections, collection, familyGuids) { Owner = this };
            if (picker.ShowDialog() != true || picker.SelectedTarget == null)
            {
                RevertKindCombo(combo, actual);
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;
            bool ok; string error;
            try { ok = FamEditorOps.ReplaceWithShared(_doc, _connector.App, row.Param, picker.SelectedTarget, out error); }
            finally { Mouse.OverrideCursor = null; }

            if (!ok)
            {
                ShowMessage("Make Shared Failed", error);
                RevertKindCombo(combo, actual);
                return;
            }

            // Success: reload just the grid so Kind (and the GUID, swap button, etc.) reflect the new shared parameter.
            ReloadParams();
        }

        private void RevertKindCombo(ComboBox combo, string kind)
        {
            _suppressKind = true;
            try { combo.SelectedValue = kind; }
            finally { _suppressKind = false; }
        }

        // ---------------------------------------------------------------- binding (type <-> instance)

        private bool _suppressBinding;

        private void Binding_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressBinding) return;

            var combo = sender as ComboBox;
            var row = combo?.DataContext as FamParamRow;
            if (row == null) return;

            var newBinding = combo.SelectedValue as string;
            var actual = row.Param.IsInstance ? "Instance" : "Type";
            if (newBinding == null || newBinding == actual) return;

            Dispatcher.BeginInvoke(new System.Action(() => HandleBindingChange(combo, row, actual)));
        }

        private void HandleBindingChange(ComboBox combo, FamParamRow row, string actual)
        {
            // Toggling binding means Revit has to remove and re-add the parameter. That can't preserve
            // a formula across a type/instance change, so we block the toggle whenever a formula is at
            // stake (ToggleBinding decides) and leave the dropdown on its original value.
            var target = actual == "Type" ? "an instance" : "a type";
            if (!Window_Dialog.Confirm(this, "Change Binding",
                    string.Format("Change \"{0}\" to {1} parameter?\n\nRevit has no in-place toggle, so it's removed and re-added under the new binding. Its per-type values are preserved and the change is undoable.",
                        row.Name, target),
                    "Change", "Cancel"))
            {
                RevertBindingCombo(combo, actual);
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;
            bool ok; string error, warning;
            try { ok = FamEditorOps.ToggleBinding(_doc, _connector.App, row.Param, row.DataType, out error, out warning); }
            finally { Mouse.OverrideCursor = null; }

            if (!ok)
            {
                // Blocked or failed: nothing changed — snap the dropdown back to the real binding.
                ShowMessage("Binding Not Changed", error);
                RevertBindingCombo(combo, actual);
                return;
            }

            if (!string.IsNullOrEmpty(warning)) ShowMessage("Binding Changed — Check Formulas", warning);

            ReloadParams();
        }

        private void RevertBindingCombo(ComboBox combo, string binding)
        {
            _suppressBinding = true;
            try { combo.SelectedValue = binding; }
            finally { _suppressBinding = false; }
        }

        /// <summary>Clicking a formula-driven value cell opens the formula editor.</summary>
        private void FormulaResult_Click(object sender, MouseButtonEventArgs e)
        {
            Formula_Click(sender, e);
        }

        // ---------------------------------------------------------------- grouping / toast

        private void ShowGroups_Changed(object sender, RoutedEventArgs e)
        {
            // Rebuild the whole view instead of mutating a live one's GroupDescriptions, which
            // throws (Dispatcher re-entrancy) while the grid is rendering group headers.
            if (_rows == null) return;
            RebuildView();
        }

        /// <summary>Build a fresh grouped/sorted/filtered view and bind it, honoring "Show Groups".</summary>
        private void RebuildView()
        {
            // Setting ShowGroupsCheck's IsChecked="True" in XAML fires Checked during
            // InitializeComponent, before ParamGrid exists. Ignore that early call; Load() rebuilds later.
            if (ParamGrid == null) return;

            var grouped = ShowGroupsCheck.IsChecked != false;

            var cvs = new CollectionViewSource { Source = _rows };
            if (grouped)
            {
                cvs.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FamParamRow.GroupLabel)));
                cvs.SortDescriptions.Add(new SortDescription(nameof(FamParamRow.GroupLabel), ListSortDirection.Ascending));
            }
            cvs.SortDescriptions.Add(new SortDescription(nameof(FamParamRow.Name), ListSortDirection.Ascending));

            _view = cvs.View;
            _view.Filter = MatchesFilter;
            ParamGrid.ItemsSource = _view;
        }

        private FamParamRow _toastRow;
        private Collection _toastCollection;
        private System.Windows.Threading.DispatcherTimer _toastTimer;

        private void ShowToast(string text, FamParamRow row, Collection collection)
        {
            ToastText.Text = text;
            _toastRow = row;
            _toastCollection = collection;
            Toast.Visibility = System.Windows.Visibility.Visible;

            if (_toastTimer == null)
            {
                _toastTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = System.TimeSpan.FromSeconds(6)
                };
                _toastTimer.Tick += (s, a) => { _toastTimer.Stop(); Toast.Visibility = System.Windows.Visibility.Collapsed; };
            }
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        private void ToastUndo_Click(object sender, RoutedEventArgs e)
        {
            _toastTimer?.Stop();
            Toast.Visibility = System.Windows.Visibility.Collapsed;

            if (_session != null && _toastRow != null && _toastCollection != null)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                try { FamEditorCollections.RemoveFromCollection(_session, _toastRow.Guid, _toastCollection.Id); }
                finally { Mouse.OverrideCursor = null; }

                _toastRow.InSelectedCollection = false;
            }
        }

        private void RefreshDynamic()
        {
            var selectedType = (TypesList.SelectedItem as FamTypeItem)?.Type;
            foreach (var r in _rows) r.Formula = r.Param.Formula;
            FamEditorReader.ApplyTypeValues(_doc, selectedType, _rows);
        }

        private void ShowMessage(string heading, string message)
        {
            Window_Dialog.Show(this, heading, message);
        }

        /// <summary>Strip Revit's ".NET argument-name" noise and add hints for common formula rejects.</summary>
        private static string FriendlyFormulaError(string raw)
        {
            var msg = (raw ?? string.Empty);
            var cut = msg.IndexOf("Parameter name:", System.StringComparison.OrdinalIgnoreCase);
            if (cut >= 0) msg = msg.Substring(0, cut).Trim();

            if (msg.IndexOf("invalid formula", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                msg += "\n\nRevit rejected the formula. Check that:\n" +
                       "• every referenced parameter exists and is spelled exactly (names are case-sensitive)\n" +
                       "• the result's units fit this parameter — e.g. Length × Length is an Area, not a Length\n" +
                       "• the formula doesn't reference an instance parameter from a type parameter (or vice versa)";
            }
            return msg;
        }
    }
}
