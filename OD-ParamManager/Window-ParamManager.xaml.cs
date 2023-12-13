using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OpenDefinery;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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
        private List<SharedParameter> ParamsToEdit { get; set; }
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
            Grid_AddCollection.Visibility = System.Windows.Visibility.Hidden;


            RvtConnector = rvtConnector;
            TextBlock_Version.Text = "Version " + typeof(Window_ParamManager).Assembly.GetName().Version.ToString();
            TextBlock_ParamsTitle.Text = "Parameters in " + RvtConnector.Document.PathName.Split('\\').Last();
            FocusManager.SetFocusedElement(this, TextBox_Username);
            
            SelectedFilter = SelectedFilter.All;
            ToggleFilterButtons();
            ToggleMainActionButtons();

            // Instantiate the main Definery object
            Definery = new Definery();

            // Update the UI
            Title = "OpenDefinery Parameter Manager" + " v" + Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        /// <summary>
        /// Helper method to reload all Definery data and update the UI
        /// </summary>
        /// <param name="definery"></param>
        private void ReloadDataAndUi(Definery definery)
        {
            // Load all of the things to the main Definery object
            Definery = Definery.LoadData(definery);

            RefreshCollections(definery);
        }

        /// <summary>
        /// Reloads the Collections data and repopulates UI
        /// </summary>
        /// <param name="definery"></param>
        private void RefreshCollections(Definery definery)
        {
            // Retrieve Data
            definery.PublishedCollections = Collection.GetPublished(definery);
            definery.MyCollections = Collection.ByCurrentUser(definery);

            // Update the UI
            PopulateCollectionsCombo(definery);
            PopulateCollectionNav(definery.PublishedCollections, definery.MyCollections);
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

            if (definery.PublishedCollections != null)
            {
                foreach (var c in definery.PublishedCollections)
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

                // Refresh the parameters from the Revit model
                RefreshRevitParams();

                // Process the parameters to identify standard vs non-standard (boolean)
                Definery.ValidatedParams = Collection.ValidateParameters(
                    Definery,
                    SelectedCollection,
                    Definery.RevitParameters);

                // Update the DataGrid
                DataGrid_Main.ItemsSource = Definery.ValidatedParams;
                DataGrid_Main.Items.Refresh();

                // Maintain filter if one was applied prior to changing selection
                ApplyExistingFilter();
            }
            else
            {
                DataGrid_Main.ItemsSource = Definery.RevitParameters;
                DataGrid_Main.Items.Refresh();
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
                ReloadDataAndUi(Definery);

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

        /// <summary>
        /// User clicked the Add to Collection button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

            // Populate the edit form
            PopulateEditForm(paramsToEdit);
        }

        /// <summary>
        /// Populate the fields for the Shared Paramater Edit form
        /// </summary>
        /// <param name="parameters"></param>
        private void PopulateEditForm(List<SharedParameter> parameters)
        {
            // Clear any existing UI elements from previous processes
            Stack_EditParamForm.Children.Clear();

            // Instantiate a list of UI elements to add to Grid later
            var formElements = new List<UIElement>();

            // Add to list of parameters for later use
            ParamsToEdit = new List<SharedParameter>();

            foreach (var p in parameters)
            {
                // Create form elements if the parameter isn't already in the Collection
                if (!p.IsStandard)
                {
                    ParamsToEdit.Add(p);

                    // Create a card to store all of the fields
                    var card = new StackPanel();
                    card.Name = "Card_" + p.Guid.ToString().Replace('-','_');

                    var header = new TextBlock();
                    header.Text = string.Format("{0} ({1})", p.Name, p.DataType);
                    header.Style = Resources["FormHeader"] as Style;
                    card.Children.Add(header);

                    var subHeader = new TextBlock();
                    subHeader.Text = string.Format("{0}", p.Guid);
                    subHeader.Style = Resources["FormSubHeader"] as Style;
                    card.Children.Add(subHeader);

                    var fieldName = new System.Windows.Controls.TextBox();
                    fieldName.Text = p.Name;
                    fieldName.Style = Resources["FormInput"] as Style;
                    fieldName.Name = "NewName_" + p.Guid.ToString().Replace('-', '_');
                    card.Children.Add(fieldName);

                    var labelName = new TextBlock();
                    labelName.Text = "New Name";
                    labelName.Style = Resources["FormCaption"] as Style;
                    card.Children.Add(labelName);

                    var fieldDesc = new System.Windows.Controls.TextBox();
                    fieldDesc.Text = p.Description;
                    fieldDesc.Style = Resources["FormInput"] as Style;
                    fieldDesc.Name = "NewDesc_" + p.Guid.ToString().Replace('-', '_');
                    card.Children.Add(fieldDesc);

                    var labelDesc = new TextBlock();
                    labelDesc.Text = "New Description";
                    labelDesc.Style = Resources["FormCaption"] as Style;
                    card.Children.Add(labelDesc);

                    // Add the card to the list
                    formElements.Add(card);
                }
                else
                {
                    // Create a card to store all of the fields
                    var card = new StackPanel();
                    card.Name = "Card_" + p.Guid.ToString().Replace('-', '_');

                    var header = new TextBlock();
                    header.Text = string.Format("{0} is already in the Collection.", p.Name, p.Guid);
                    header.Style = Resources["StatusSuccess"] as Style;
                    card.Children.Add(header);

                    var subHeader = new TextBlock();
                    subHeader.Text = string.Format("{0}", p.Guid);
                    subHeader.Style = Resources["FormSubHeader"] as Style;
                    card.Children.Add(subHeader);

                    formElements.Add(card);
                }
            }

            // Add form elements to the stack panel
            foreach (var e in formElements)
            {
                Stack_EditParamForm.Children.Add(e);
            }
        }

        /// <summary>
        /// User clicked the Save button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_SaveParams_Click(object sender, RoutedEventArgs e)
        {
            var newParams = new List<SharedParameter>();

            foreach (var p in ParamsToEdit)
            {
                var formattedGuid = p.Guid.ToString().Replace('-', '_');

                var paramEditCard = LogicalTreeHelper.FindLogicalNode(
                    Stack_EditParamForm, "Card_" + formattedGuid) 
                    as StackPanel;

                if (paramEditCard != null)
                {
                    // Retrieve form values
                    var newName = LogicalTreeHelper.FindLogicalNode(paramEditCard, "NewName_" + formattedGuid) as System.Windows.Controls.TextBox;
                    var newDesc = LogicalTreeHelper.FindLogicalNode(paramEditCard, "NewDesc_" + formattedGuid) as System.Windows.Controls.TextBox;

                    if (!string.IsNullOrEmpty(newName.Text))
                    {
                        var newParam = SharedParameter.Create(Definery, p, SelectedCollection.Id, p.DefineryId, newName.Text, newDesc.Text);

                        newParams.Add(newParam);
                    }
                    else
                    {
                        MessageBox.Show(string.Format("The New Name field for \"{0}\"cannot be blank.", p.Name), "Name Required");
                    }
                }

            }

            MessageBox.Show(string.Format(
                "Successfully added {0} of {1} parameters.",
                newParams.Count.ToString(),
                ParamsToEdit.Count.ToString()));

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
            Stack_EditParamForm.Children.Clear();
            Grid_EditParams.Visibility = System.Windows.Visibility.Hidden;
            Grid_Overlay.Visibility = System.Windows.Visibility.Hidden;
        }

        /// <summary>
        /// Retrieve Shared Parameters from Revit model
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <returns></returns>
        private void RefreshRevitParams()
        {
            // Instantiate a list to store the shared parameters from the current Revit model
            var revitParams = new List<SharedParameter>();

            // Collect shared parameters as elements
            FilteredElementCollector collector
                = new FilteredElementCollector(RvtConnector.Document)
                .WhereElementIsNotElementType();

            collector.OfClass(typeof(SharedParameterElement));

            // Add each parameter to the list
            foreach (Element e in collector)
            {
                SharedParameterElement param = e as SharedParameterElement;
                Definition def = param.GetDefinition();

                var dataType = DataType.GetByParamTypeName(def.ParameterType.ToString(), Definery.DataTypes);

                // Cast the SharedParameterElement to a "lite" OpenDefinery SharedParameter
                // TODO: Retrieve all Revit parameter data such as the DATACATEGORY
                var castedParam = new SharedParameter(
                    param.GuidValue, def.Name, dataType.Name, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty
                    );

                castedParam.ElementId = Convert.ToInt32(e.Id.IntegerValue);

                revitParams.Add(castedParam);
            }

            Definery.RevitParameters = revitParams;

            // Show the Parameters on the UI
            DataGrid_Main.ItemsSource = Definery.RevitParameters;
            InitCollectionView();
        }

        /// <summary>
        /// Helper method to sort datagrid
        /// </summary>
        /// <returns></returns>
        private ICollectionView InitCollectionView()
        {
            ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Main.ItemsSource);

            if (cv != null && cv.SortDescriptions.Count() == 0)
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
        private void ToggleMainActionButtons()
        {
            if (DataGrid_Main.SelectedItems.Count > 0)
            {
                // Cast the selected items to Shared Parameters
                var selectedParams = new List<SharedParameter>();

                foreach (var i in DataGrid_Main.SelectedItems)
                {
                    selectedParams.Add(i as SharedParameter);
                }

                // Toggle buttons
                Button_AddToCollection.IsEnabled = true;
                Button_Purge.IsEnabled = true;
            }
            else
            {
                Button_AddToCollection.IsEnabled = false;
                Button_Purge.IsEnabled = false;
            }
        }

        /// <summary>
        /// User changes selection in the DataGrid
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DataGrid_Main_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ToggleMainActionButtons();
        }

        /// <summary>
        /// User clicks the Purge button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Purge_Click(object sender, RoutedEventArgs e)
        {
            if (DataGrid_Main.SelectedItems.Count > 0)
            {
                // Instantiate a list of Shared Parameters for later use
                var selectedParams = new List<SharedParameter>();

                foreach (var i in DataGrid_Main.SelectedItems)
                {
                    var selectedParam = i as SharedParameter;

                    selectedParams.Add(selectedParam);
                }

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

                td.MainInstruction = string.Format(
                        "Are you sure you want to purge the selected {0} Shared Parameters from the current model?",
                        selectedParams.Count.ToString()
                        );

                td.MainContent = "Warning: This may result in data loss within this model. Click \"See Details\" below to confirm the Shared Parameters to be deleted.";

                td.ExpandedContent = "";

                foreach (var p in selectedParams)
                {
                    td.ExpandedContent += p.Name + " [" + p.Guid + "]\n\n";
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

                    // Refresh parameters in the main Definery object
                    RefreshRevitParams();
                    RefreshValidation();
                    InitCollectionView();

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
        
        /// <summary>
        /// Method to evaluate if parameters are in use prior to purging
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
                    var details = RvtCommand.GetParamDetails(RvtConnector, selectedParam);

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

                        // Refresh parameters in the main Definery object
                        RefreshRevitParams();

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

        /// <summary>
        /// Populate the Collections menu with Buttons
        /// </summary>
        /// <param name="publishedCollections"></param>
        /// <param name="myCollections"></param>
        private void PopulateCollectionNav(List<Collection> publishedCollections, List<Collection> myCollections)
        {
            var navContainer = LogicalTreeHelper.FindLogicalNode(ScrollViewer_SubNav, "Stack_SubNav") as StackPanel;

            // Always clear the Children in case it is being reloaded
            navContainer.Children.Clear();

            var myCollHeader = new TextBlock();
            myCollHeader.Text = "My Collections";
            myCollHeader.Style = Resources["SubNavHeader"] as Style;
            navContainer.Children.Add(myCollHeader);

            // Add each Collection to the navigation
            foreach (var c in myCollections)
            {
                var button = new Button();
                button.Name = "Button_" + c.Id.ToString();
                button.Content = c.Name;
                button.Style = Resources["SubNavButton"] as Style;
                button.Click += new RoutedEventHandler(Button_Collection_Click);
                button.ToolTip = c.Name;

                if (c.IsPublic == true)
                {
                    button.Content += "*";
                    button.ToolTip += " (public)";
                }

                navContainer.Children.Add(button);
            }

            var publicCollHeader = new TextBlock();
            publicCollHeader.Text = "Public Collections";
            publicCollHeader.Style = Resources["SubNavHeader"] as Style;
            navContainer.Children.Add(publicCollHeader);

            // Add each Collection to the navigation
            foreach (var c in publishedCollections)
            {
                if (!myCollections.Exists(o => o.Id == c.Id))
                {
                    var button = new Button();
                    button.Name = "Button_" + c.Id.ToString();
                    button.Content = c.Name;
                    button.Style = Resources["SubNavButton"] as Style;
                    button.Click += new RoutedEventHandler(Button_Collection_Click);
                    button.ToolTip = c.Name;

                    navContainer.Children.Add(button);
                }
            }
        }

        /// <summary>
        /// User clicks Collection in subnav
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Collection_Click(object sender, RoutedEventArgs e)
        {
            // Toggle the GUI
            Button_ToggleActive(sender, e);

            // Retrieve Collection from button context
            var clickedButton = sender as Button;

            var clickedButtonId = clickedButton.Name;
            var selectedCollection = Definery.PublishedCollections.Where(
                c => c.Id.ToString() == clickedButtonId.Split('_')[1]).FirstOrDefault();

            // Retrieve the Shared Parameters from the Collection
            var collectionParams = new ObservableCollection<SharedParameter>();

            if (selectedCollection != null)
            {
                collectionParams = Collection.GetParameters(Definery, selectedCollection);
            }
            else
            {
                selectedCollection = Definery.MyCollections.Where(
                    c => c.Id.ToString() == clickedButtonId.Split('_')[1]).FirstOrDefault();

                if (selectedCollection != null)
                {
                    collectionParams = Collection.GetParameters(Definery, selectedCollection);
                }
                else
                {
                    MessageBox.Show(
                        string.Format(
                            "There was an error retrieving {0} from OpenDefinery. " +
                            "If the problem persists, please contact i@opendefinery.com.", 
                            clickedButtonId
                            )
                        );
                }
            }

            // Update the DataGrid
            DataGrid_CollectionParams.ItemsSource = collectionParams;
            DataGrid_CollectionParams.Items.Refresh();
        }

        /// <summary>
        /// Helper method to assign resources for subnav buttons
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_ToggleActive(object sender, RoutedEventArgs e)
        {
            // Clear any previously selected button
            foreach (var i in Stack_SubNav.Children)
            {
                if (i.GetType() == typeof(Button))
                {
                    var navButton = i as Button;

                    navButton.Style = Resources["SubNavButton"] as Style;

                    // Set the active style to the clicked button
                    var clickedButton = sender as Button;
                    clickedButton.Style = Resources["SubNavButton_Active"] as Style;
                }
            }
        }

        /// <summary>
        /// User clicks the New Collection Button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_NewCollection_Click(object sender, RoutedEventArgs e)
        {
            // Clear previous form data
            TextBox_AddCollName.Text = string.Empty;
            TextBox_AddCollDescription.Text = string.Empty;
            CheckBox_AddCollPublic.IsChecked = false;

            // Toggle UI visibility
            Grid_AddCollection.Visibility = System.Windows.Visibility.Visible;
            Grid_Overlay.Visibility = System.Windows.Visibility.Visible;
        }

        /// <summary>
        /// User clicks close Add Collection button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_CloseAddCollection_Click(object sender, RoutedEventArgs e)
        {
            Grid_AddCollection.Visibility = System.Windows.Visibility.Hidden;
            Grid_Overlay.Visibility = System.Windows.Visibility.Hidden;
        }

        /// <summary>
        /// Helper method to check for special characters
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        private bool ContainsSpecialChars(string val)
        {
            var allowedChars = new List<char> { '-', '_', '.' };
            var invalid = false;

            foreach (char c in val)
            {
                if (!char.IsWhiteSpace(c))
                {
                    if (!char.IsLetterOrDigit(c))
                    {

                        if (!allowedChars.Contains(c))
                        {
                            invalid = true;

                            break;
                        }
                    }
                }
            }

            if (invalid)
            {
                var specialCharList = string.Empty;

                foreach (var c in allowedChars)
                {
                    specialCharList += c + " ";
                }

                MessageBox.Show(string.Format("The Collection name can only include the following special characters: \n{0}", specialCharList));
            }

            return invalid;
        }

        /// <summary>
        /// User clicks ADd to Collection Save button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_AddCollSave_Click(object sender, RoutedEventArgs e)
        {
            // Retrieve data from form
            var inputName = TextBox_AddCollName.Text;
            var inputDesc = TextBox_AddCollDescription.Text;
            var isPublic = CheckBox_AddCollPublic.IsChecked;


            if (!ContainsSpecialChars(inputName))
            {
                if (!string.IsNullOrEmpty(inputName) && !string.IsNullOrEmpty(inputDesc))
                {
                    // Create the Collection on OpenDefinery
                    var collection = Collection.Create(Definery, inputName, inputDesc, isPublic);

                    if (collection != null)
                    {
                        RefreshCollections(Definery);

                        Grid_AddCollection.Visibility = System.Windows.Visibility.Hidden;
                        Grid_Overlay.Visibility = System.Windows.Visibility.Hidden;
                    }
                    else
                    {
                        MessageBox.Show("There was an issue creating the Collection. Please try again.");
                    }
                }
                else
                {
                    MessageBox.Show("The Collection name and description are required.");
                }
            }
        }

        /// <summary>
        /// User clicks the Add To Model button from the Collections screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_AddToModel_Click(object sender, RoutedEventArgs e)
        {
            RvtConnector.AddSelectedParams(DataGrid_CollectionParams);
        }

        /// <summary>
        /// User changes the selection in DataGrid_CollectionParams
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DataGrid_CollectionParams_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGrid_CollectionParams.SelectedItems.Count > 0)
            {
                Button_AddToModel.IsEnabled = true;
            }
            else { Button_AddToModel.IsEnabled = false;}
        }

        /// <summary>
        /// User clicks the Model Parameters tab
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Tab_ModelParameters_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            RefreshRevitParams();
            RefreshValidation();
            InitCollectionView();
        }
    }
}