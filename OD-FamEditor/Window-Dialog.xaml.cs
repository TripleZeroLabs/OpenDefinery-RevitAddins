using System.Windows;

namespace OD_FamEditor
{
    /// <summary>
    /// OS2-styled replacement for the stock <see cref="MessageBox"/>. Use <see cref="Show"/> for a
    /// simple notice (single OK) and <see cref="Confirm"/> for an OK/Cancel decision.
    /// </summary>
    public partial class Window_Dialog : Window
    {
        private Window_Dialog(string title, string message, bool confirm, string okText, string cancelText,
            bool prompt = false, string initial = null)
        {
            InitializeComponent();

            Title = title;
            HeadingText.Text = title;
            MessageText.Text = message;
            OkButton.Content = okText;

            if (confirm || prompt)
            {
                CancelButton.Content = cancelText;
                CancelButton.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                CancelButton.Visibility = System.Windows.Visibility.Collapsed;
            }

            if (prompt)
            {
                InputBox.Text = initial ?? string.Empty;
                InputBox.Visibility = System.Windows.Visibility.Visible;
                Loaded += (s, e) => { InputBox.Focus(); InputBox.SelectAll(); };
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

        /// <summary>Ask for a line of text. Returns the entered value, or null if cancelled/empty.</summary>
        public static string Prompt(Window owner, string title, string message, string initial = null,
            string okText = "OK", string cancelText = "Cancel")
        {
            var dlg = new Window_Dialog(title, message, false, okText, cancelText, prompt: true, initial: initial) { Owner = owner };
            if (dlg.ShowDialog() != true) return null;
            var text = dlg.InputBox.Text?.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private void Ok_Click(object sender, RoutedEventArgs e) { DialogResult = true; }

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; }
    }
}
