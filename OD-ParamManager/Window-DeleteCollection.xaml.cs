using System.Windows;
using System.Windows.Input;
using OpenDefinery;

namespace OD_ParamManager
{
    /// <summary>
    /// High-friction confirmation for deleting a Collection: the user must type the exact
    /// Collection name before the Delete button enables. Escape / Cancel back out safely.
    /// </summary>
    public partial class Window_DeleteCollection : Window
    {
        private string _requiredName;

        public Window_DeleteCollection()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Show the dialog and return true only if the user typed the Collection name and
        /// clicked Delete.
        /// </summary>
        public static bool Confirm(Window owner, Collection collection)
        {
            var dialog = new Window_DeleteCollection { Owner = owner };

            dialog._requiredName = collection.Name ?? string.Empty;
            dialog.CollectionNameRun.Text = " \"" + collection.Name + "\"";
            dialog.ConfirmHintRun.Text = " \"" + collection.Name + "\" ";

            return dialog.ShowDialog() == true;
        }

        private void ConfirmTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Exact match (trimmed) so a stray space doesn't block a correct paste.
            DeleteButton.IsEnabled =
                string.Equals(ConfirmTextBox.Text.Trim(), _requiredName, System.StringComparison.Ordinal);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            // Escape cancels; it must not confirm.
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                e.Handled = true;
                return;
            }

            base.OnPreviewKeyDown(e);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
