using OpenDefinery;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace OD_FamEditor
{
    /// <summary>One selectable shared parameter in the Make-Shared / Swap picker.</summary>
    public class SharedPickRow
    {
        public DefineryParameter Param { get; set; }
        public string Name { get; set; }
        public string Guid { get; set; }
        public string DataTypeName { get; set; }
        public string Description { get; set; }
        public bool InFamily { get; set; }
    }

    /// <summary>
    /// Picks the OpenDefinery shared parameter to convert a family parameter into (or swap to).
    /// Shows every parameter in the chosen Collection with name/GUID/data type/description;
    /// parameters already in the family are greyed out and unselectable.
    /// </summary>
    public partial class Window_MakeShared : Window
    {
        private const string AllDataTypesLabel = "All Data Types";

        private readonly Definery _session;
        private readonly HashSet<Guid> _familyGuids;

        private List<SharedPickRow> _rows = new List<SharedPickRow>();
        private ICollectionView _view;

        private string _search = string.Empty;
        private string _dataTypeFilter = AllDataTypesLabel;

        public DefineryParameter SelectedTarget { get; private set; }

        public Window_MakeShared(string sourceName, Definery session,
            IEnumerable<Collection> collections, Collection preselected, HashSet<Guid> familyGuids)
        {
            InitializeComponent();

            _session = session;
            _familyGuids = familyGuids ?? new HashSet<Guid>();

            HeadingText.Text = "Make \"" + sourceName + "\" shared";

            var list = collections != null ? new List<Collection>(collections) : new List<Collection>();
            CollectionCombo.ItemsSource = list;
            if (preselected != null)
                CollectionCombo.SelectedItem = list.FirstOrDefault(c => c.Id == preselected.Id) ?? list.FirstOrDefault();
            else if (list.Count > 0)
                CollectionCombo.SelectedIndex = 0;
        }

        private void CollectionCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            LoadParams();
        }

        private void LoadParams()
        {
            var collection = CollectionCombo.SelectedItem as Collection;
            _rows = new List<SharedPickRow>();

            if (_session != null && collection != null)
            {
                try
                {
                    foreach (var p in Collection.GetParameters(_session, collection))
                    {
                        if (p == null) continue;
                        _rows.Add(new SharedPickRow
                        {
                            Param = p,
                            Name = p.Name,
                            Guid = p.Guid.ToString(),
                            DataTypeName = ResolveDataTypeName(p.DataType),
                            Description = p.Description,
                            InFamily = _familyGuids.Contains(p.Guid)
                        });
                    }
                }
                catch { }
            }

            _view = CollectionViewSource.GetDefaultView(_rows);
            _view.Filter = MatchesFilter;
            ParamGrid.ItemsSource = _view;

            PopulateDataTypeFilter();
        }

        private string ResolveDataTypeName(string dataType)
        {
            if (string.IsNullOrEmpty(dataType)) return string.Empty;
            // Collection params store the data type as an OpenDefinery id; map it to a name.
            var match = _session?.DataTypes?.FirstOrDefault(d => d.Id.ToString() == dataType);
            return match != null ? match.Name : dataType;
        }

        private void PopulateDataTypeFilter()
        {
            var items = new List<string> { AllDataTypesLabel };
            items.AddRange(_rows.Select(r => r.DataTypeName)
                                .Where(t => !string.IsNullOrEmpty(t))
                                .Distinct()
                                .OrderBy(t => t));

            DataTypeFilter.ItemsSource = items;
            DataTypeFilter.SelectedIndex = 0;
        }

        private bool MatchesFilter(object obj)
        {
            var row = obj as SharedPickRow;
            if (row == null) return false;

            if (!string.IsNullOrEmpty(_search))
            {
                var name = (row.Name ?? string.Empty).ToLowerInvariant();
                var guid = (row.Guid ?? string.Empty).ToLowerInvariant();
                if (name.IndexOf(_search, StringComparison.Ordinal) < 0 &&
                    guid.IndexOf(_search, StringComparison.Ordinal) < 0)
                {
                    return false;
                }
            }

            if (_dataTypeFilter != AllDataTypesLabel && row.DataTypeName != _dataTypeFilter)
            {
                return false;
            }

            return true;
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            _search = (SearchBox.Text ?? string.Empty).Trim().ToLowerInvariant();
            _dataTypeFilter = DataTypeFilter.SelectedItem as string ?? AllDataTypesLabel;
            _view?.Refresh();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            var row = ParamGrid.SelectedItem as SharedPickRow;
            if (row == null)
            {
                Window_Dialog.Show(this, "Nothing Selected", "Choose a parameter to use.");
                return;
            }
            if (row.InFamily) return; // disabled rows can't reach here, but guard anyway

            SelectedTarget = row.Param;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
