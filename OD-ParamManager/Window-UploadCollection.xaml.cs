using OpenDefinery;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OD_ParamManager
{
    /// <summary>
    /// Collection picker shown when the user clicks Upload on the Current Model tab. Lets the
    /// user choose (or create) the target Collection, pre-selected to match the tab. On Upload
    /// it closes with <see cref="SelectedCollection"/> set; the caller performs the actual upload.
    /// </summary>
    public partial class Window_UploadCollection : Window
    {
        private readonly Definery _session;

        /// <summary>The Collection the user chose to upload into (null if cancelled).</summary>
        public Collection SelectedCollection { get; private set; }

        /// <summary>True if the user created a Collection here, so the caller can refresh its own list.</summary>
        public bool CollectionsChanged { get; private set; }

        public Window_UploadCollection(Definery session, IEnumerable<Collection> collections, Collection preselected, int paramCount)
        {
            InitializeComponent();

            _session = session;

            SummaryText.Text = string.Format(
                "Uploading {0} selected parameter{1}. Choose a Collection, or create a new one.",
                paramCount, paramCount == 1 ? string.Empty : "s");

            var list = collections != null ? new List<Collection>(collections) : new List<Collection>();
            CollectionCombo.ItemsSource = list;

            if (preselected != null)
            {
                CollectionCombo.SelectedItem = list.FirstOrDefault(c => c.Id == preselected.Id) ?? list.FirstOrDefault();
            }
            else if (list.Count > 0)
            {
                CollectionCombo.SelectedIndex = 0;
            }
        }

        private void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            var collection = CollectionCombo.SelectedItem as Collection;
            if (collection == null)
            {
                Window_Message.Show(this, "No Collection Selected", "Choose a Collection to upload to, or create a new one.");
                return;
            }

            SelectedCollection = collection;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // ---------------------------------------------------------------- new collection

        private void NewCollectionButton_Click(object sender, RoutedEventArgs e)
        {
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
                CollectionsChanged = true;

                OverlayGrid.Visibility = System.Windows.Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                NewCollectionStatus.Text = "Error creating Collection: " + ex.Message;
            }
        }
    }
}
