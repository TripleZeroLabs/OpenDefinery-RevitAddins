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
    /// <summary>
    /// One row in the Add-to-Project grid: a parameter plus its chosen binding, parameter group,
    /// and model category. (Reuses <see cref="FamilyBinding"/> from the Add-to-Family window.)
    /// </summary>
    public class AddToProjectRow : INotifyPropertyChanged
    {
        public DefineryParameter Param { get; set; }

        public string Name => Param?.Name;
        public string Guid => Param?.Guid.ToString();

        private FamilyBinding _binding = FamilyBinding.Instance;
        public FamilyBinding Binding
        {
            get => _binding;
            set { if (_binding != value) { _binding = value; Raise(nameof(Binding)); RaiseWarning(); } }
        }

        private ParamGroupOption _group;
        public ParamGroupOption Group
        {
            get => _group;
            set { if (!ReferenceEquals(_group, value)) { _group = value; Raise(nameof(Group)); } }
        }

        private List<ModelCategoryOption> _categories = new List<ModelCategoryOption>();
        public List<ModelCategoryOption> Categories => _categories;

        /// <summary>Replace the bound categories (each row keeps its own copy) and refresh the UI.</summary>
        public void SetCategories(IEnumerable<ModelCategoryOption> cats)
        {
            _categories = cats != null ? cats.ToList() : new List<ModelCategoryOption>();
            Raise(nameof(Categories));
            Raise(nameof(CategoriesSummary));
            RaiseWarning();
        }

        /// <summary>Button caption for the categories cell.</summary>
        public string CategoriesSummary =>
            _categories.Count == 0 ? "Choose categories…"
            : _categories.Count == 1 ? _categories[0].Name
            : _categories.Count + " categories";

        /// <summary>Non-null when Type binding is chosen but some category only supports Instance (e.g. Project Information).</summary>
        public string Warning
        {
            get
            {
                if (_binding != FamilyBinding.Type) return null;

                var bad = _categories
                    .Where(c => c?.Category != null && !RvtCompat.AllowsTypeBinding(c.Category))
                    .ToList();
                if (bad.Count == 0) return null;

                return "Type binding is not allowed for " +
                       string.Join(", ", bad.Select(b => "\"" + b.Name + "\"")) + " — use Instance.";
            }
        }

        public bool HasWarning => Warning != null;

        private void RaiseWarning() { Raise(nameof(Warning)); Raise(nameof(HasWarning)); }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Assigns a binding (Instance/Type), parameter group, and model category to each selected
    /// parameter, then adds them to the active project as project parameters. Changing a dropdown
    /// while several rows are selected applies the value to all of them.
    /// </summary>
    public partial class Window_AddToProject : Window
    {
        private readonly RvtConnector _connector;

        public ObservableCollection<AddToProjectRow> Rows { get; }
        public FamilyBinding[] BindingOptions { get; } = { FamilyBinding.Instance, FamilyBinding.Type };
        public List<ParamGroupOption> GroupOptions { get; }
        public List<ModelCategoryOption> CategoryOptions { get; }

        // Guards the bulk-apply so programmatic updates to other rows don't recurse.
        private bool _bulkApplying;

        public Window_AddToProject(RvtConnector connector, IEnumerable<DefineryParameter> parameters)
        {
            InitializeComponent();

            _connector = connector;

            GroupOptions = RvtCompat.GetAvailableGroups();
            var defaultGroup = GroupOptions.FirstOrDefault(
                g => g.Id.TypeId == RvtCompat.IdentityGroup.TypeId) ?? GroupOptions.FirstOrDefault();

            CategoryOptions = _connector != null && _connector.Document != null
                ? RvtCompat.GetBindableModelCategories(_connector.Document)
                : new List<ModelCategoryOption>();

            Rows = new ObservableCollection<AddToProjectRow>(
                parameters.Select(p => new AddToProjectRow
                {
                    Param = p,
                    Binding = FamilyBinding.Instance,
                    Group = defaultGroup
                }));

            DataContext = this;
            DataGrid_Rows.ItemsSource = Rows;

            var target = _connector != null && _connector.Document != null && !_connector.Document.IsFamilyDocument
                ? "Adding to the current project."
                : "No project is open - this requires an active project document.";
            TargetText.Text = string.Format("{0} parameter(s). {1}", Rows.Count, target);
        }

        // ---------------------------------------------------------------- bulk apply

        private void BindingCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_bulkApplying) return;

            var combo = sender as ComboBox;
            var row = combo?.DataContext as AddToProjectRow;
            if (row == null || !(combo.SelectedItem is FamilyBinding value)) return;

            ApplyToSelection(row, r => r.Binding = value);
        }

        private void GroupCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_bulkApplying) return;

            var combo = sender as ComboBox;
            var row = combo?.DataContext as AddToProjectRow;
            if (row == null || !(combo.SelectedItem is ParamGroupOption value)) return;

            ApplyToSelection(row, r => r.Group = value);
        }

        /// <summary>
        /// Open the model-category picker for a row. If several rows are selected, the chosen
        /// category set is applied to all of them (each keeps its own copy).
        /// </summary>
        private void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.DataContext as AddToProjectRow;
            if (row == null) return;

            // Capture the selection before the modal opens (the button click may not change it).
            var selected = DataGrid_Rows.SelectedItems.Cast<AddToProjectRow>().ToList();

            var picker = new Window_CategoryPicker(CategoryOptions, row.Categories) { Owner = this };
            if (picker.ShowDialog() != true) return;

            var chosen = picker.SelectedCategories;

            if (selected.Count > 1 && selected.Contains(row))
            {
                foreach (var r in selected) r.SetCategories(chosen);
            }
            else
            {
                row.SetCategories(chosen);
            }
        }

        /// <summary>
        /// When the edited row is part of a multi-row selection, apply the change to every
        /// selected row; otherwise it only affects the row that was edited.
        /// </summary>
        private void ApplyToSelection(AddToProjectRow origin, System.Action<AddToProjectRow> apply)
        {
            var selected = DataGrid_Rows.SelectedItems.Cast<AddToProjectRow>().ToList();
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
            if (Rows.Any(r => r.Categories.Count == 0))
            {
                Window_Message.Show(this, "Model Category Required",
                    "Choose at least one model category for every parameter before adding.");
                return;
            }

            // Block invalid binding/category combinations (e.g. Type binding on Project Information).
            var invalid = Rows.Where(r => r.HasWarning).ToList();
            if (invalid.Count > 0)
            {
                var list = string.Join("\n", invalid.Select(r => "• " + r.Name));
                Window_Message.Show(this, "Invalid Binding for Category",
                    "These parameters use Type binding with a category that only supports Instance binding. " +
                    "Switch them to Instance and try again:\n\n" + list);
                return;
            }

            var requests = Rows.Select(r => new ProjectParamRequest
            {
                Param = r.Param,
                Group = r.Group?.Id ?? RvtCompat.IdentityGroup,
                IsInstance = r.Binding == FamilyBinding.Instance,
                Categories = r.Categories.Select(c => c.Category).ToList()
            }).ToList();

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                _connector.CreateProjectParams(requests);
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

            Window_ExportResults.Show(this, "Added to Project", "Current project", results);
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
