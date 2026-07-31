using System.Windows;

namespace OD_FamEditor
{
    /// <summary>
    /// OS2-styled replacement for the stock <see cref="MessageBox"/>. Use <see cref="Show"/> for a
    /// simple notice (single OK) and <see cref="Confirm"/> for an OK/Cancel decision.
    /// </summary>
    public partial class Window_Dialog : Window
    {
        private Window_Dialog(string title, string message, bool confirm, string okText, string cancelText)
        {
            InitializeComponent();

            Title = title;
            HeadingText.Text = title;
            MessageText.Text = message;
            OkButton.Content = okText;

            if (confirm)
            {
                CancelButton.Content = cancelText;
                CancelButton.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                CancelButton.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        /// <summary>Show an informational notice with a single OK button.</summary>
        public static void Show(Window owner, string title, string message)
        {
            var dlg = new Window_Dialog(title, message, false, "OK", null) { Owner = owner };
            dlg.ShowDialog();
        }

        /// <summary>Ask an OK/Cancel question. Returns true only if the user confirms.</summary>
        public static bool Confirm(Window owner, string title, string message,
            string okText = "OK", string cancelText = "Cancel")
        {
            var dlg = new Window_Dialog(title, message, true, okText, cancelText) { Owner = owner };
            return dlg.ShowDialog() == true;
        }

        private void Ok_Click(object sender, RoutedEventArgs e) { DialogResult = true; }

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; }
    }
}
