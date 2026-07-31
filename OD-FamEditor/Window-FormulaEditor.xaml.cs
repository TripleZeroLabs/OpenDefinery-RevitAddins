using System.Windows;

namespace OD_FamEditor
{
    /// <summary>
    /// Multiline formula editor. Revit stores formulas on a single line, so the entered text is
    /// collapsed to one line on save. Returns the formula via <see cref="Formula"/>.
    /// </summary>
    public partial class Window_FormulaEditor : Window
    {
        /// <summary>The formula the user saved (empty string clears it). Null if cancelled.</summary>
        public string Formula { get; private set; }

        public Window_FormulaEditor(string paramName, string currentFormula)
        {
            InitializeComponent();

            HeadingText.Text = "Formula — " + paramName;
            FormulaBox.Text = currentFormula ?? string.Empty;
            FormulaBox.Focus();
            FormulaBox.CaretIndex = FormulaBox.Text.Length;
        }

        private static string CollapseToSingleLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            // Revit formulas are single-line; fold any line breaks into spaces.
            return System.Text.RegularExpressions.Regex.Replace(text, "\\s*[\\r\\n]+\\s*", " ").Trim();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Formula = CollapseToSingleLine(FormulaBox.Text);
            DialogResult = true;
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            Formula = string.Empty;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
