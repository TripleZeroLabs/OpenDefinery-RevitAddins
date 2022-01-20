﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using OpenDefinery;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace OD_ParamManager
{
    /// <summary>
    /// Interaction logic for Window_ParamManager.xaml
    /// </summary>
    public partial class Window_ParamManager : Window
    {
        private Definery Definery { get; set; }
        private Collection SelectedCollection { get; set; }

        /// <summary>
        /// MainWindow constructor
        /// </summary>
        /// <param name="definery"></param>
        public Window_ParamManager(List<SharedParameter> revitParams)
        {
            InitializeComponent();

            // Initialize the UI
            DataGrid_Detailed.Visibility = Visibility.Hidden;
            Grid_Overlay.Visibility = Visibility.Visible;
            FocusManager.SetFocusedElement(this, TextBox_Username);

            // Instantiate the main Definery object
            Definery = new Definery();

            // Set data passed from the Revit command
            Definery.RevitParameters = revitParams;

            // Update the UI
            DataGrid_Detailed.ItemsSource = Definery.RevitParameters;
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

                ComboCollections.ItemsSource = collectionsCombo;
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
            if (ComboCollections.SelectedItem != null)
            {
                // Set the selected Collection
                SelectedCollection = ComboCollections.SelectedItem as Collection;

                // Process the parameters to identify standard vs non-standard (boolean)
                Definery.ValidatedParams = Collection.ValidateParameters(
                    Definery, 
                    SelectedCollection, 
                    Definery.RevitParameters, 
                    Definery.DefineryParameters);

                // Set the data grid source to the new data set
                DataGrid_Main.ItemsSource = Definery.ValidatedParams;
            }
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
    }
}
