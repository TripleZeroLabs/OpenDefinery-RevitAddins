using OpenDefinery;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace OD_ParamManager
{
    /// <summary>One checkable model category in the picker.</summary>
    public class CategoryCheck : INotifyPropertyChanged
    {
        public ModelCategoryOption Option { get; set; }
        public string Name => Option?.Name;

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set { if (_isChecked != value) { _isChecked = value; Raise(nameof(IsChecked)); } }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    /// <summary>Modal checklist for choosing one or more model categories, with search and select-all.</summary>
    public partial class Window_CategoryPicker : Window
    {
        private readonly List<CategoryCheck> _all;
        private readonly ICollectionView _view;
        private string _search = string.Empty;

        /// <summary>The chosen categories, set when the user clicks OK.</summary>
        public List<ModelCategoryOption> SelectedCategories { get; private set; }

        public Window_CategoryPicker(IEnumerable<ModelCategoryOption> options, IEnumerable<ModelCategoryOption> preselected)
        {
            InitializeComponent();

            var chosen = new HashSet<ModelCategoryOption>(preselected ?? Enumerable.Empty<ModelCategoryOption>());
            _all = (options ?? Enumerable.Empty<ModelCategoryOption>())
                .Select(o => new CategoryCheck { Option = o, IsChecked = chosen.Contains(o) })
                .ToList();

            _view = CollectionViewSource.GetDefaultView(_all);
            _view.Filter = Matches;
            CategoryList.ItemsSource = _view;
        }

        private bool Matches(object obj)
        {
            if (string.IsNullOrEmpty(_search)) return true;
            var c = obj as CategoryCheck;
            return (c?.Name ?? string.Empty).ToLowerInvariant().Contains(_search);
        }

        private void Search_Changed(object sender, TextChangedEventArgs e)
        {
            _search = (SearchBox.Text ?? string.Empty).Trim().ToLowerInvariant();
            _view?.Refresh();
        }

        // Select/clear operate on the currently visible (filtered) categories.
        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var c in _all) if (Matches(c)) c.IsChecked = true;
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var c in _all) if (Matches(c)) c.IsChecked = false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedCategories = _all.Where(c => c.IsChecked).Select(c => c.Option).ToList();
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
