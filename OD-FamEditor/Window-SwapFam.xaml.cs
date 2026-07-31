using OpenDefinery;
using System.Collections.Generic;
using System.Windows;

namespace OD_FamEditor
{
    /// <summary>
    /// Picks the OpenDefinery parameter to swap a family shared parameter for. Candidates are
    /// pre-filtered to the same data type and to parameters not already in the family.
    /// </summary>
    public partial class Window_SwapFam : Window
    {
        /// <summary>The parameter the user chose to swap to (null if cancelled).</summary>
        public DefineryParameter SelectedTarget { get; private set; }

        public Window_SwapFam(FamParamRow source, List<DefineryParameter> candidates)
        {
            InitializeComponent();

            SourceName.Text = source.Name;
            SourceMeta.Text = string.Format("{0}   •   {1}", source.Guid, source.DataType);

            TargetCombo.ItemsSource = candidates;
            if (candidates.Count > 0) TargetCombo.SelectedIndex = 0;
        }

        private void SwapButton_Click(object sender, RoutedEventArgs e)
        {
            var target = TargetCombo.SelectedItem as DefineryParameter;
            if (target == null)
            {
                Window_Message_Show("No Target", "Choose a parameter to swap to.");
                return;
            }

            SelectedTarget = target;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Window_Message_Show(string heading, string message)
        {
            Window_Dialog.Show(this, heading, message);
        }
    }
}
