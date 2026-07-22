using System.Windows;

namespace OD_ParamManager
{
    /// <summary>
    /// Simple themed message dialog with a single OK button, used instead of an inline status
    /// bar so results and errors are impossible to miss.
    /// </summary>
    public partial class Window_Message : Window
    {
        public Window_Message()
        {
            InitializeComponent();
        }

        /// <summary>Show a modal themed message over the given owner window.</summary>
        public static void Show(Window owner, string heading, string message)
        {
            var dialog = new Window_Message
            {
                Owner = owner,
                Title = heading
            };

            dialog.HeadingText.Text = heading;
            dialog.MessageText.Text = message;

            dialog.ShowDialog();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
