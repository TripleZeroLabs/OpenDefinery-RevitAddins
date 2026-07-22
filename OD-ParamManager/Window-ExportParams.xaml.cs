using Autodesk.Revit.DB;
using OpenDefinery;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace OD_ParamManager
{
    /// <summary>A shared parameter discovered in the currently open Revit document.</summary>
    public class DocumentSharedParameter
    {
        public string Name { get; set; }
        public Guid Guid { get; set; }
        public string DataType { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Lists the shared parameters in the active family (or project) and exports the selected
    /// ones into an OpenDefinery Collection.
    /// </summary>
    public partial class Window_ExportParams : Window
    {
        private RvtConnector RvtConnector { get; set; }

        // Named "Session" rather than "Definery" to keep the static Definery.Current
        // reference unambiguous.
        private Definery Session { get; set; }

        public ObservableCollection<DocumentSharedParameter> Parameters { get; set; }

        // A message raised while constructing (before the window is on screen) is shown
        // once the window has loaded, so the dialog has a visible owner.
        private string _pendingHeading;
        private string _pendingMessage;

        public Window_ExportParams(RvtConnector rvtConnector)
        {
            InitializeComponent();

            RvtConnector = rvtConnector;

            Loaded += Window_ExportParams_Loaded;

            LoadDocumentParameters();

            // Reuse the sign-in from this Revit session if there is one, otherwise prompt.
            if (Definery.Current != null && !string.IsNullOrEmpty(Definery.Current.AuthCode))
            {
                Session = Definery.Current;
                EnsureReferenceData();
                LoadCollections();
            }
            else
            {
                ShowLogin();
            }
        }

        private void Window_ExportParams_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_pendingMessage))
            {
                ShowMessage(_pendingHeading ?? "OpenDefinery", _pendingMessage);
                _pendingHeading = null;
                _pendingMessage = null;
            }
        }

        /// <summary>
        /// Escape must not close this window (Revit add-in windows shouldn't vanish on a
        /// stray key press). It only dismisses the New Collection card when that's open.
        /// </summary>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (NewCollectionCard.Visibility == System.Windows.Visibility.Visible)
                {
                    CloseOverlay();
                }

                e.Handled = true;
                return;
            }

            base.OnPreviewKeyDown(e);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ---------------------------------------------------------------- document params

        /// <summary>
        /// Collect shared parameters from the active document. Families expose them through the
        /// FamilyManager; projects through SharedParameterElement.
        /// </summary>
        private void LoadDocumentParameters()
        {
            var found = new List<DocumentSharedParameter>();
            var doc = RvtConnector.Document;

            try
            {
                if (doc.IsFamilyDocument)
                {
                    foreach (FamilyParameter fp in doc.FamilyManager.GetParameters())
                    {
                        if (!fp.IsShared) continue;

                        found.Add(new DocumentSharedParameter
                        {
                            Name = fp.Definition.Name,
                            Guid = fp.GUID,
                            DataType = RvtCompat.GetDataTypeToken(fp.Definition)
                        });
                    }
                }
                else
                {
                    var collector = new FilteredElementCollector(doc).OfClass(typeof(SharedParameterElement));

                    foreach (var element in collector)
                    {
                        var shared = element as SharedParameterElement;
                        if (shared == null) continue;

                        var definition = shared.GetDefinition();

                        found.Add(new DocumentSharedParameter
                        {
                            Name = definition.Name,
                            Guid = shared.GuidValue,
                            DataType = RvtCompat.GetDataTypeToken(definition)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _pendingHeading = "Could Not Read Parameters";
                _pendingMessage = "There was a problem reading shared parameters from the document:\n\n" + ex.Message;
            }

            Parameters = new ObservableCollection<DocumentSharedParameter>(found.OrderBy(p => p.Name));
            DataGrid_Params.ItemsSource = Parameters;

            Title = string.Format("Export Shared Parameters  —  {0} in {1}",
                Parameters.Count,
                doc.IsFamilyDocument ? "this family" : "this project");
        }

        // ---------------------------------------------------------------- session / login

        private void ShowLogin()
        {
            OverlayGrid.Visibility = System.Windows.Visibility.Visible;
            LoginCard.Visibility = System.Windows.Visibility.Visible;
            NewCollectionCard.Visibility = System.Windows.Visibility.Collapsed;
            UsernameTextBox.Focus();
        }

        private void CloseOverlay()
        {
            OverlayGrid.Visibility = System.Windows.Visibility.Collapsed;
            LoginCard.Visibility = System.Windows.Visibility.Collapsed;
            NewCollectionCard.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            LoginStatusText.Text = string.Empty;

            var username = UsernameTextBox.Text;
            var password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                LoginStatusText.Text = "Enter a username and password.";
                return;
            }

            Definery session;
            try
            {
                session = Definery.Init(new Definery(), username, password);
            }
            catch (Exception ex)
            {
                LoginStatusText.Text = "Login error: " + ex.Message;
                return;
            }

            if (session == null || string.IsNullOrEmpty(session.CsrfToken))
            {
                LoginStatusText.Text = "Login failed. Check your credentials and try again.";
                return;
            }

            Session = session;
            EnsureReferenceData();
            CloseOverlay();
            LoadCollections();
        }

        /// <summary>Data types are required to create parameters, so make sure they're loaded.</summary>
        private void EnsureReferenceData()
        {
            try
            {
                if (Session.DataTypes == null || Session.DataTypes.Count == 0)
                {
                    Definery.LoadData(Session);
                }
            }
            catch (Exception ex)
            {
                QueueOrShow("Reference Data", "Could not load OpenDefinery reference data:\n\n" + ex.Message);
            }
        }

        // ---------------------------------------------------------------- collections

        private void LoadCollections()
        {
            try
            {
                var collections = Collection.ByCurrentUser(Session) ?? new List<Collection>();

                CollectionCombo.ItemsSource = collections;

                if (collections.Count > 0)
                {
                    CollectionCombo.SelectedIndex = 0;
                }
                else
                {
                    QueueOrShow("No Collections",
                        "You don't have any Collections yet. Use \"New Collection\" to create one.");
                }
            }
            catch (Exception ex)
            {
                QueueOrShow("Could Not Load Collections", ex.Message);
            }
        }

        private void NewCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (Session == null) { ShowLogin(); return; }

            NewCollectionStatus.Text = string.Empty;
            NewCollectionName.Text = string.Empty;
            NewCollectionDescription.Text = string.Empty;
            NewCollectionPublic.IsChecked = false;

            OverlayGrid.Visibility = System.Windows.Visibility.Visible;
            LoginCard.Visibility = System.Windows.Visibility.Collapsed;
            NewCollectionCard.Visibility = System.Windows.Visibility.Visible;
            NewCollectionName.Focus();
        }

        private void CancelNewCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            CloseOverlay();
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
                    Session,
                    name.Trim(),
                    NewCollectionDescription.Text ?? string.Empty,
                    NewCollectionPublic.IsChecked ?? false);

                if (created == null)
                {
                    NewCollectionStatus.Text = "Could not create the Collection. Check your permissions and try again.";
                    return;
                }

                // Append and select directly, so selection doesn't depend on the new
                // Collection already being visible from the list endpoint.
                var current = CollectionCombo.ItemsSource as IEnumerable<Collection>;
                var updated = current != null ? new List<Collection>(current) : new List<Collection>();
                updated.Add(created);

                CollectionCombo.ItemsSource = updated;
                CollectionCombo.SelectedItem = created;

                CloseOverlay();
                ShowMessage("Collection Created",
                    string.Format("Created \"{0}\" and selected it as the export target.", created.Name));
            }
            catch (Exception ex)
            {
                NewCollectionStatus.Text = "Error creating Collection: " + ex.Message;
            }
        }

        // ---------------------------------------------------------------- export

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (Session == null) { ShowLogin(); return; }

            var selected = DataGrid_Params.SelectedItems.Cast<DocumentSharedParameter>().ToList();
            if (selected.Count == 0)
            {
                ShowMessage("Nothing Selected",
                    "Select one or more parameters to export. Hold Ctrl or Shift to select several.");
                return;
            }

            var collection = CollectionCombo.SelectedItem as Collection;
            if (collection == null)
            {
                ShowMessage("No Collection Selected",
                    "Choose a Collection to export to, or create a new one.");
                return;
            }

            var results = new List<ExportResult>();

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                foreach (var param in selected)
                {
                    var result = new ExportResult
                    {
                        Name = param.Name,
                        Guid = param.Guid.ToString()
                    };

                    // Every parameter is handled independently: one failure must never stop
                    // the rest of the batch.
                    try
                    {
                        var existing = DefineryParameter.FromGuid(Session, param.Guid);

                        if (existing != null && existing.Count > 0)
                        {
                            // Already in OpenDefinery - skip it rather than trying to modify it.
                            result.Outcome = ExportOutcome.AlreadyExists;
                            result.Message = "Already in OpenDefinery - skipped.";
                        }
                        else
                        {
                            // A known data type is required to create the parameter.
                            var dataType = DataType.GetFromName(Session, param.DataType);

                            if (dataType == null)
                            {
                                result.Outcome = ExportOutcome.Failed;
                                result.Message = string.Format(
                                    "Data type \"{0}\" is not available in OpenDefinery.",
                                    string.IsNullOrEmpty(param.DataType) ? "(unknown)" : param.DataType);
                            }
                            else
                            {
                                var newParam = new DefineryParameter
                                {
                                    Guid = param.Guid,
                                    Name = param.Name,
                                    DataType = param.DataType,
                                    Description = param.Description ?? string.Empty,
                                    Visible = "1",
                                    UserModifiable = "1"
                                };

                                var createdParam = DefineryParameter.Create(
                                    Session, newParam, collection.Id, null,
                                    param.Name, param.Description ?? string.Empty);

                                if (createdParam != null)
                                {
                                    result.Outcome = ExportOutcome.Added;
                                    result.Message = string.Empty;
                                }
                                else
                                {
                                    result.Outcome = ExportOutcome.Failed;
                                    result.Message = "OpenDefinery rejected the request (check the name, description, and your permissions).";
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Outcome = ExportOutcome.Failed;
                        result.Message = ex.Message;
                    }

                    results.Add(result);
                }
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            Window_ExportResults.Show(this, collection.Name, results);
        }

        // ---------------------------------------------------------------- helpers

        private void ShowMessage(string heading, string message)
        {
            Window_Message.Show(this, heading, message);
        }

        /// <summary>
        /// Show the message now if the window is up; otherwise hold it until Loaded (a dialog
        /// raised from the constructor would have no visible owner).
        /// </summary>
        private void QueueOrShow(string heading, string message)
        {
            if (IsLoaded)
            {
                ShowMessage(heading, message);
            }
            else
            {
                _pendingHeading = heading;
                _pendingMessage = message;
            }
        }
    }
}
