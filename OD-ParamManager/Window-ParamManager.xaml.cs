using OpenDefinery;
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
        private Collection SelectedCollection { get; set; }
        private static SelectedFilter SelectedFilter {get;set;}

        /// <summary>
        /// MainWindow constructor
        /// </summary>
        /// <param name="definery"></param>
        public Window_ParamManager(List<SharedParameter> revitParams)
        {
            InitializeComponent();

            // Set the intial fields and UI
            Grid_Overlay.Visibility = Visibility.Visible;
            Grid_EditParams.Visibility = Visibility.Hidden;

            TextBlock_Version.Text = "Version " + typeof(Window_ParamManager).Assembly.GetName().Version.ToString();
            FocusManager.SetFocusedElement(this, TextBox_Username);
            
            SelectedFilter = SelectedFilter.All;
            ToggleFilterButtons();
            ToggleActionButtons();

            // Instantiate the main Definery object
            Definery = new Definery();

            // Set data passed from the Revit command
            Definery.RevitParameters = revitParams;

            // Set the initial DataGrid prior to validating so the table is not empty
            DataGrid_Main.ItemsSource = Definery.RevitParameters;
            InitCollectionView();

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
                Grid_Overlay.Visibility = Visibility.Hidden;
                Grid_Login.Visibility = Visibility.Hidden;
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
            Grid_Overlay.Visibility = Visibility.Visible;
            Grid_EditParams.Visibility = Visibility.Visible;

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
            Grid_EditParams.Visibility = Visibility.Hidden;
            Grid_Overlay.Visibility = Visibility.Hidden;

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
            Grid_EditParams.Visibility = Visibility.Hidden;
            Grid_Overlay.Visibility = Visibility.Hidden;
        }

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
            if (DataGrid_Main.SelectedItem != null)
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
            }
            else
            {
                Button_AddToCollection.IsEnabled = false;
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

        private void Button_Purge_Click(object sender, RoutedEventArgs e)
        {
            // Retrieve selected items and cast to Paramaters
            if (DataGrid_Main.SelectedItems.Count > 0)
            {
                var parameters = new List<SharedParameter>();

                foreach (var i in DataGrid_Main.SelectedItems)
                {
                    var sharedParam = i as SharedParameter;

                    if (sharedParam.ElementId != 0)
                    {
                        parameters.Add(sharedParam);
                    }

                }

                // Delete the Parameters from the model
                Command.PurgeParameters(this, Command.Document, parameters);
            }
        }
    }
}
