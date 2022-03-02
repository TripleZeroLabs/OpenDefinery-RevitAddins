using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OpenDefinery;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Navigation;

namespace OD_ParamManager
{
    public enum SelectedFilter { All, InCollection, NotInCollection }

    /// <summary>
    /// Interaction logic for Window_ParamManager.xaml
    /// </summary>
    public partial class Window_ParamManager : Window
    {
        private Definery Definery { get; set; }
        private RvtConnector RvtConnector { get; set; }
        private List<SharedParameter> RevitParameters { get; set; }
        private Collection SelectedCollection { get; set; }
        private static SelectedFilter SelectedFilter {get;set;}

        /// <summary>
        /// MainWindow constructor
        /// </summary>
        /// <param name="definery"></param>
        public Window_ParamManager(RvtConnector rvtConnector)
        {
            InitializeComponent();

            // Set the intial fields and UI
            Grid_Overlay.Visibility = System.Windows.Visibility.Visible;
            Grid_EditParams.Visibility = System.Windows.Visibility.Hidden;
            Grid_Details.Visibility = System.Windows.Visibility.Hidden;

            RvtConnector = rvtConnector;
            TextBlock_Version.Text = "Version " + typeof(Window_ParamManager).Assembly.GetName().Version.ToString();
            TextBlock_ParamsTitle.Text = "Parameters in " + RvtConnector.Document.PathName.Split('\\').Last();
            FocusManager.SetFocusedElement(this, TextBox_Username);
            
            SelectedFilter = SelectedFilter.All;
            ToggleFilterButtons();
            ToggleActionButtons();

            // Instantiate the main Definery object
            Definery = new Definery();

            // Set data passed from the Revit command
            RefreshRevitParams(Definery);

            // Update the UI
            Title = "OpenDefinery Parameter Manager" + " v" + Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        /// <summary>
        /// Login to OpenDefinery
        /// </summary>
        /// <returns>A boolean value of whether or not the login was successful</returns>
        private bool Login(Definery definery)
        {
            var username = TextBox_Username.Text;
            var password = PasswordBox_Password.Password;

            // Initialize the main Definery object
            Definery = Definery.Init(definery, username, password);

            // Only continue if the CSRF token was retrieved from OpenDefinery
            if (Definery != null && !string.IsNullOrEmpty(Definery.CsrfToken))
            {
                //TextBlock_Username.Text = "Logged in as " + Definery.CurrentUser.Name;

                return true;
            }
            else
            {
                MessageBox.Show(
                    "There was an error connecting to OpenDefinery. Please contact i@opendefinery.com if the problem persists.", 
                    "Login Error");

                return false;
            }
        }

        /// <summary>
        /// Populate the Collections combo box for user input
        /// </summary>
        /// <param name="definery"></param>
        private void PopulateCollectionsCombo(Definery definery)
        {
            // Add Collections to combobox and set the UI
            var collectionsCombo = new ObservableCollection<Collection>();

            if (definery.AllCollections != null)
            {
                foreach (var c in definery.AllCollections)
                {
                    collectionsCombo.Add(c);
                }

                Combo_Collections.ItemsSource = collectionsCombo;
            }
        }

        /// <summary>
        /// Reloads the DataGrid whenever parameters are added or modified.
        /// </summary>
        private void RefreshValidation()
        {
            // Reset previous data
            DataGrid_Main.ItemsSource = null;

            if (Combo_Collections.SelectedItem != null)
            {
                // Set the selected Collection
                SelectedCollection = Combo_Collections.SelectedItem as Collection;

                // Process the parameters to identify standard vs non-standard (boolean)
                Definery.ValidatedParams = Collection.ValidateParameters(
                    Definery,
                    SelectedCollection,
                    Definery.RevitParameters);

                // Set the data grid source to the new data set
                DataGrid_Main.ItemsSource = Definery.ValidatedParams;
                DataGrid_Main.Items.Refresh();

                // Maintain filter if one was applied prior to changing selection
                ApplyExistingFilter();
            }
        }

        /// <summary>
        /// User pressed enter key on login form
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SubmitLoginForm(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                Button_Login_Click(sender, e);
            }
        }

        /// <summary>
        /// User clicked a link
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.ToString());
        }

        /// <summary>
        /// User selected a new Collection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ComboCollections_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshValidation();
            InitCollectionView();
        }

        /// <summary>
        /// User clicked the login button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Login_Click(object sender, RoutedEventArgs e)
        {
            Login(Definery);

            if (!string.IsNullOrEmpty(Definery.CsrfToken))
            {
                // Load all of the things to the main Definery object
                Definery = Definery.LoadData(Definery);

                // Update the UI
                PopulateCollectionsCombo(Definery);
                Grid_Overlay.Visibility = System.Windows.Visibility.Hidden;
                Grid_Login.Visibility = System.Windows.Visibility.Hidden;
            }
        }

        /// <summary>
        /// User clicked the close button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Button_AddToCollection_Click(object sender, RoutedEventArgs e)
        {
            // Toggle the UI
            Grid_Overlay.Visibility = System.Windows.Visibility.Visible;
            Grid_EditParams.Visibility = System.Windows.Visibility.Visible;

            // Get the selected Collection as a Collection object
            SelectedCollection = Combo_Collections.SelectedItem as Collection;

            // Instantiate a list of parameters to pass to the data grid
            var paramsToEdit = new List<SharedParameter>();

            // Add each selected row to the Collection
            foreach (var p in DataGrid_Main.SelectedItems)
            {
                // Get current Shared Parameter as a SharedParameter object
                var selectedParam = p as SharedParameter;

                paramsToEdit.Add(selectedParam);
            }

            // Refresh the DataGrid
            DataGrid_EditParams.ItemsSource = paramsToEdit;
        }

        private void Button_SaveParams_Click(object sender, RoutedEventArgs e)
        {
            foreach (var p in DataGrid_EditParams.Items)
            {
                // Get current Shared Parameter as a SharedParameter object
                var selectedParam = p as SharedParameter;

                // Prompt the user to fork the parameter if it doesn't exist in OpenDefinery
                if (selectedParam.DefineryId == 0)
                {
                    // Fork the parameter
                    SharedParameter.Create(
                        Definery,
                        selectedParam,
                        SelectedCollection.Id,
                        selectedParam.DefineryId,
                        selectedParam.Name,
                        selectedParam.Description
                        );
                }
                else
                {
                    // Add the parameter to the selected Collection
                    SharedParameter.AddToCollection(Definery, selectedParam, SelectedCollection.Id);
                }
            }

            // Refresh the UI
            Grid_EditParams.Visibility = System.Windows.Visibility.Hidden;
            Grid_Overlay.Visibility = System.Windows.Visibility.Hidden;

            // Validate parameters again and reload the DataGrid
            RefreshValidation();
        }

        /// <summary>
        /// Close button on EditParams is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_CloseEditParams_Click(object sender, RoutedEventArgs e)
        {
            DataGrid_EditParams.ItemsSource = null;
            Grid_EditParams.Visibility = System.Windows.Visibility.Hidden;
            Grid_Overlay.Visibility = System.Windows.Visibility.Hidden;
        }

        /// <summary>
        /// Retrieve Shared Parameters from Revit model
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <returns></returns>
        private void RefreshRevitParams(Definery definery)
        {
            // Instantiate a list to store the shared parameters from the current Revit model
            var revitParams = new List<SharedParameter>();

            // Collect shared parameters as elements
            FilteredElementCollector collector2
                = new FilteredElementCollector(RvtConnector.Document)
                .WhereElementIsNotElementType();

            collector2.OfClass(typeof(SharedParameterElement));

            // Add each parameter to the list
            foreach (Element e in collector2)
            {
                SharedParameterElement param = e as SharedParameterElement;
                Definition def = param.GetDefinition();

                //Debug.WriteLine("[" + e.Id + "]\t" + def.Name + "\t(" + param.GuidValue + ")");

                // Cast the SharedParameterElement to a "lite" OpenDefinery SharedParameter
                // TODO: Retrieve all Revit parameter data such as the DATAGAATEGORY
                var castedParam = new SharedParameter(
                    param.GuidValue, def.Name, def.ParameterType.ToString(), string.Empty, string.Empty, string.Empty, string.Empty, string.Empty
                    );

                castedParam.ElementId = Convert.ToInt32(e.Id.IntegerValue);

                revitParams.Add(castedParam);
            }

            definery.RevitParameters = revitParams;

            // Show the Parameters on the UI
            DataGrid_Main.ItemsSource = definery.RevitParameters;
            InitCollectionView();
        }

        /// <summary>
        /// Helper method to sort datagrid
        /// </summary>
        /// <returns></returns>
        private ICollectionView InitCollectionView()
        {
            ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Main.ItemsSource);

            if (cv.SortDescriptions.Count() == 0)
            {
                cv.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
                cv.Refresh();
            }

            return cv;
        }

        /// <summary>
        /// Helper method to toggle filter UI after user interaction
        /// </summary>
        private void ToggleFilterButtons()
        {
            if (SelectedFilter == SelectedFilter.All)
            {
                Button_FilterAll.IsEnabled = false;
                Button_FilterInCollection.IsEnabled = true;
                Button_FilterNotInCollection.IsEnabled = true;
            }
            else if (SelectedFilter == SelectedFilter.InCollection)
            {
                Button_FilterAll.IsEnabled = true;
                Button_FilterInCollection.IsEnabled = false;
                Button_FilterNotInCollection.IsEnabled = true;

            }
            else if (SelectedFilter == SelectedFilter.NotInCollection)
            {
                Button_FilterAll.IsEnabled = true;
                Button_FilterInCollection.IsEnabled = true;
                Button_FilterNotInCollection.IsEnabled = false;

            }
        }

        /// <summary>
        /// Helper method to maintain the parameter filter based on which filter button is disabled
        /// </summary>
        private void ApplyExistingFilter()
        {
            if (!Button_FilterAll.IsEnabled)
            {
                Button_FilterAll.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            if(!Button_FilterInCollection.IsEnabled)
            {
                Button_FilterInCollection.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            if (!Button_FilterNotInCollection.IsEnabled)
            {
                Button_FilterNotInCollection.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

        /// <summary>
        /// Filter All button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_FilterAll_Click(object sender, RoutedEventArgs e)
        {
            SelectedFilter = SelectedFilter.All;
            ToggleFilterButtons();

            var cv = InitCollectionView();

            cv.Filter = o =>
            {
                SharedParameter p = o as SharedParameter;

                return p != null;
            };
        }

        /// <summary>
        /// Filter In Collection button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_FilterInCollection_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCollection != null)
            {
                SelectedFilter = SelectedFilter.InCollection;
                ToggleFilterButtons();

                var cv = InitCollectionView();

                cv.Filter = o =>
                {
                    SharedParameter p = o as SharedParameter;

                    return p.IsStandard == true;
                };
            }
            else
            {
                MessageBox.Show("Select a Collection to use this filter.", "No Collection Selected");
            }
        }

        /// <summary>
        /// Filter Not in Collection button clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_FilterNotInCollection_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCollection != null)
            {
                SelectedFilter = SelectedFilter.NotInCollection;
                ToggleFilterButtons();

                var cv = InitCollectionView();

                cv.Filter = o =>
                {
                    SharedParameter p = o as SharedParameter;

                    return p.IsStandard == false;
                };
            }
            else
            {
                MessageBox.Show("Select a Collection to use this filter.", "No Collection Selected");
            }
        }
    
        /// <summary>
        /// Helper method to toggle the UI for action buttons
        /// </summary>
        private void ToggleActionButtons()
        {
            if (DataGrid_Main.SelectedItems != null)
            {
                // Cast the selected items to Shared Parameters
                var selectedParams = new List<SharedParameter>();

                foreach (var i in DataGrid_Main.SelectedItems)
                {
                    selectedParams.Add(i as SharedParameter);
                }

                // Disable the Add to Collection button if any selected Parameters are already in the Collection
                if (selectedParams.Any(p => p.IsStandard == true))
                {
                    Button_AddToCollection.IsEnabled = false;
                }
                else
                {
                    Button_AddToCollection.IsEnabled = true;
                }

                // Toggle the Details button
                Button_Details.IsEnabled = true;
            }
            else
            {
                Button_AddToCollection.IsEnabled = false;
                Button_Details.IsEnabled = false;
            }
        }

        /// <summary>
        /// User changes selection in the DataGrid
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DataGrid_Main_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ToggleActionButtons();
        }

        /// <summary>
        /// User clicks the Details button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Evaluate_Click(object sender, RoutedEventArgs e)
        {
            if (DataGrid_Main.SelectedItems.Count > 0)
            {
                TreeView_Details.Items.Clear();

                var listOfDetails = new Dictionary<string, List<string>>();

                // Instantiate a list of Shared Parameters for later use
                var selectedParams = new List<SharedParameter>();

                foreach (var i in DataGrid_Main.SelectedItems)
                {
                    var selectedParam = i as SharedParameter;

                    selectedParams.Add(selectedParam);

                    // Retrieve the details/usage of the Shared Parameter for UI
                    var details = Command.GetParamDetails(RvtConnector, selectedParam);

                    if (details.Count > 0)
                    {
                        foreach (var detail in details)
                        {
                            listOfDetails.Add(detail.Key, detail.Value);
                        }
                    }
                }

                if (listOfDetails.Count > 0)
                {
                    // Populate TreeView
                    foreach (var detail in listOfDetails)
                    {
                        var parentItem = new TreeViewItem();
                        parentItem.Header = detail.Key.ToString();

                        // Add children
                        foreach (var value in detail.Value)
                        {
                            var childItem = new TreeViewItem();
                            childItem.Header = value.ToString();
                            parentItem.Items.Add(childItem);
                        }

                        TreeView_Details.Items.Add(parentItem);
                    }

                    // Show the TreeView
                    Grid_Overlay.Visibility = System.Windows.Visibility.Visible;
                    Grid_Details.Visibility = System.Windows.Visibility.Visible;
                }

                // Prompt the user to purge the parameters if all are not used
                else
                {
                    // Instantiate a collection of parameters to delete from the model
                    ICollection<ElementId> elementIds = new List<ElementId>();

                    // Retrieve Element IDs
                    foreach (var p in selectedParams)
                    {
                        ElementId id = new ElementId(p.ElementId);

                        elementIds.Add(id);
                    }

                    var td = new TaskDialog("Purge Selected Shared Parameters");
                    td.Id = "PurgeParams";
                    td.MainIcon = TaskDialogIcon.TaskDialogIconInformation;
                    td.TitleAutoPrefix = false;
                    td.AllowCancellation = true;

                    td.MainInstruction = "Would you like to purge these unused parameters?";

                    td.MainContent = string.Format(
                        "There are no families which use the selected {0} parameters. " +
                        "Would you like to purge them from the model?",
                        DataGrid_Main.SelectedItems.Count.ToString());

                    td.ExpandedContent = "";

                    foreach (var p in selectedParams)
                    {
                        td.ExpandedContent += p.Name + " [" + p.Guid + "]\n";
                    }

                    td.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                    TaskDialogResult result = td.Show();

                    // Delete Shared Parameters from the model if the user confirms
                    if (result == TaskDialogResult.Yes)
                    {
                        Transaction trans = new Transaction(RvtConnector.Document, "Purge Shared Parameters");
                        trans.Start();

                        // Instantiate lists of results to write to the log
                        var successfulDeletes = new List<string>();
                        var failedDeletes = new List<string>();

                        foreach (var eId in elementIds)
                        {
                            try
                            {
                                RvtConnector.Document.Delete(eId);

                                successfulDeletes.Add("DELETE SUCCESSFUL\t" + eId.ToString());
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(string.Format("Error deleting {0}:\n\n" + ex.ToString(), eId.ToString()));

                                failedDeletes.Add("DELETE FAILED\t" + eId.ToString());
                            }
                        }

                        trans.Commit();

                        var tdConfirmation = new TaskDialog("Purged Parameters");

                        tdConfirmation.MainInstruction =
                            string.Format("Successfully purged {0} shared parameters from the model.",
                            successfulDeletes.Count.ToString());

                        tdConfirmation.ExpandedContent = "";

                        foreach (var fd in failedDeletes)
                        {
                            tdConfirmation.ExpandedContent += fd + "\n";
                        }

                        tdConfirmation.Show();

                        // Load all of the things to the main Definery object
                        RefreshRevitParams(Definery);

                        // Switch back to the Main Window
                        Activate();
                    }
                    // Switch back to the main window
                    else
                    {
                        Activate();
                    }
                }
            }
        }

        /// <summary>
        /// Helper method for using the mousewheel to scroll
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListViewScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            ScrollViewer scv = (ScrollViewer)sender;
            scv.ScrollToVerticalOffset(scv.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        /// <summary>
        /// User clicks Close Details button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_CloseDetails_Click(object sender, RoutedEventArgs e)
        {
            // Hide the TreeView
            Grid_Overlay.Visibility = System.Windows.Visibility.Hidden;
            Grid_Details.Visibility = System.Windows.Visibility.Hidden;
        }
    }
}
