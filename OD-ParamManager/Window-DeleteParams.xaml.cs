using System.Windows;
using System.Windows.Input;

namespace OD_ParamManager
{
    /// <summary>
    /// High-friction confirmation for deleting shared parameters from the model. The Delete
    /// button stays disabled until the user checks "I understand the risks". The warning text
    /// differs for a family vs. a project (a project delete also affects loaded families).
    /// </summary>
    public partial class Window_DeleteParams : Window
    {
        public Window_DeleteParams()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Show the dialog. Returns true only if the user acknowledged the risk and clicked Delete.
        /// </summary>
        public static bool Confirm(Window owner, int count, bool isFamily)
        {
            var dialog = new Window_DeleteParams { Owner = owner };

            var plural = count == 1 ? string.Empty : "s";
            dialog.HeadingText.Text = string.Format("Delete {0} parameter{1}?", count, plural);

            dialog.WarningText.Text = isFamily
                ? string.Format(
                    "This permanently deletes {0} shared parameter{1} from this family. All values stored on those parameters will be lost.",
                    count, plural)
                : string.Format(
                    "This permanently deletes {0} shared parameter{1} from this project AND from every family and element loaded in it. All values stored on those parameters throughout the model will be lost.",
                    count, plural);

            return dialog.ShowDialog() == true;
        }

        private void Ack_Changed(object sender, RoutedEventArgs e)
        {
            DeleteButton.IsEnabled = AckCheckBox.IsChecked == true;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
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
