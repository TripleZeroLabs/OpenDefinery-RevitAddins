using OpenDefinery;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OD_ParamManager
{
    /// <summary>Instance vs. Type binding for a family parameter.</summary>
    public enum FamilyBinding
    {
        Instance,
        Type
    }

    /// <summary>One row in the Add-to-Family grid: a parameter plus its chosen binding and group.</summary>
    public class AddToFamilyRow : INotifyPropertyChanged
    {
        public DefineryParameter Param { get; set; }

        public string Name => Param?.Name;
        public string Guid => Param?.Guid.ToString();

        private FamilyBinding _binding = FamilyBinding.Type;
        public FamilyBinding Binding
        {
            get => _binding;
            set { if (_binding != value) { _binding = value; Raise(nameof(Binding)); } }
        }

        private ParamGroupOption _group;
        public ParamGroupOption Group
        {
            get => _group;
            set { if (!ReferenceEquals(_group, value)) { _group = value; Raise(nameof(Group)); } }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Assigns a binding (Instance/Type) and built-in parameter group to each selected parameter,
    /// then adds them to the active family. Changing a dropdown while several rows are selected
    /// applies the value to all of them.
    /// </summary>
    public partial class Window_AddToFamily : Window
    {
        private readonly RvtConnector _connector;

        public ObservableCollection<AddToFamilyRow> Rows { get; }
        public FamilyBinding[] BindingOptions { get; } = { FamilyBinding.Instance, FamilyBinding.Type };
        public List<ParamGroupOption> GroupOptions { get; }

        // Guards the bulk-apply so programmatic updates to other rows don't recurse.
        private bool _bulkApplying;

        public Window_AddToFamily(RvtConnector connector, IEnumerable<DefineryParameter> parameters)
        {
            InitializeComponent();

            _connector = connector;

            GroupOptions = RvtCompat.GetAvailableGroups();
            var defaultGroup = GroupOptions.FirstOrDefault(
                g => g.Id.TypeId == RvtCompat.IdentityGroup.TypeId) ?? GroupOptions.FirstOrDefault();

            Rows = new ObservableCollection<AddToFamilyRow>(
                parameters.Select(p => new AddToFamilyRow
                {
                    Param = p,
                    Binding = FamilyBinding.Type,
                    Group = defaultGroup
                }));

            DataContext = this;
            DataGrid_Rows.ItemsSource = Rows;

            var target = _connector != null && _connector.Document != null && _connector.Document.IsFamilyDocument
                ? "Adding to the current family."
                : "No family is open - this requires an active family document.";
            TargetText.Text = string.Format("{0} parameter(s). {1}", Rows.Count, target);
        }

        // ---------------------------------------------------------------- bulk apply

        private void BindingCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_bulkApplying) return;

            var combo = sender as ComboBox;
            var row = combo?.DataContext as AddToFamilyRow;
            if (row == null || !(combo.SelectedItem is FamilyBinding value)) return;

            ApplyToSelection(row, r => r.Binding = value);
        }

        private void GroupCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_bulkApplying) return;

            var combo = sender as ComboBox;
            var row = combo?.DataContext as AddToFamilyRow;
            if (row == null || !(combo.SelectedItem is ParamGroupOption value)) return;

            ApplyToSelection(row, r => r.Group = value);
        }

        /// <summary>
        /// When the edited row is part of a multi-row selection, apply the change to every
        /// selected row; otherwise it only affects the row that was edited.
        /// </summary>
        private void ApplyToSelection(AddToFamilyRow origin, System.Action<AddToFamilyRow> apply)
        {
            var selected = DataGrid_Rows.SelectedItems.Cast<AddToFamilyRow>().ToList();
            if (selected.Count <= 1 || !selected.Contains(origin)) return;

            _bulkApplying = true;
            try
            {
                foreach (var r in selected) apply(r);
            }
            finally
            {
                _bulkApplying = false;
            }
        }

        // ---------------------------------------------------------------- actions

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var requests = Rows.Select(r => new FamilyParamRequest
            {
                Param = r.Param,
                Group = r.Group?.Id ?? RvtCompat.IdentityGroup,
                IsInstance = r.Binding == FamilyBinding.Instance
            }).ToList();

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                _connector.CreateParamsWithBinding(requests);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            var results = (_connector.LastLoadResults ?? new List<ParamLoadResult>())
                .Select(r => new ExportResult
                {
                    Name = r.Name,
                    Guid = r.Guid,
                    Message = r.Message,
                    Outcome = r.Outcome == ParamLoadOutcome.Added
                        ? ExportOutcome.Added
                        : r.Outcome == ParamLoadOutcome.AlreadyExists
                            ? ExportOutcome.AlreadyExists
                            : ExportOutcome.Failed
                })
                .ToList();

            Window_ExportResults.Show(this, "Added to Family", "Current family", results);
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
