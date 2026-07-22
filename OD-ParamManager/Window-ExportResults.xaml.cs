using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OD_ParamManager
{
    /// <summary>Outcome of exporting a single shared parameter.</summary>
    public enum ExportOutcome
    {
        /// <summary>Created in OpenDefinery and added to the Collection (green check).</summary>
        Added,

        /// <summary>Already present in OpenDefinery, so it was skipped (blue check).</summary>
        AlreadyExists,

        /// <summary>Could not be exported (red X); see <see cref="ExportResult.Message"/>.</summary>
        Failed
    }

    /// <summary>One row in the export results table.</summary>
    public class ExportResult
    {
        public string Name { get; set; }
        public string Guid { get; set; }
        public ExportOutcome Outcome { get; set; }
        public string Message { get; set; }

        /// <summary>Human-readable outcome, used as the status icon's tooltip.</summary>
        public string OutcomeLabel
        {
            get
            {
                switch (Outcome)
                {
                    case ExportOutcome.Added: return "Added";
                    case ExportOutcome.AlreadyExists: return "Already in OpenDefinery";
                    default: return "Failed";
                }
            }
        }
    }

    /// <summary>
    /// Themed results dialog: one row per parameter with a status icon, GUID, and (for
    /// failures) the reason it could not be exported.
    /// </summary>
    public partial class Window_ExportResults : Window
    {
        public Window_ExportResults()
        {
            InitializeComponent();
        }

        public static void Show(Window owner, string collectionName, IList<ExportResult> results)
        {
            var dialog = new Window_ExportResults { Owner = owner };

            var added = results.Count(r => r.Outcome == ExportOutcome.Added);
            var existed = results.Count(r => r.Outcome == ExportOutcome.AlreadyExists);
            var failed = results.Count(r => r.Outcome == ExportOutcome.Failed);

            dialog.SummaryText.Text = string.Format(
                "Collection \"{0}\"  —  {1} added, {2} already existed, {3} failed.",
                collectionName, added, existed, failed);

            // Failures first so problems are immediately visible.
            dialog.ResultsGrid.ItemsSource = results
                .OrderByDescending(r => r.Outcome == ExportOutcome.Failed)
                .ThenBy(r => r.Name)
                .ToList();

            dialog.ShowDialog();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
