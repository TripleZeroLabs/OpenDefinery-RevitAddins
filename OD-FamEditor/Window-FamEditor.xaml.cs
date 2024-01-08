using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using OpenDefinery;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace OD_FamEditor
{
    /// <summary>
    /// Interaction logic for Window_FamEditor.xaml
    /// </summary>
    public partial class Window_FamEditor : Window
    {
        public FamEditor FamEditor { get; set; }
        public FamilyType SelectedFamType { get; set; }
        public ObservableCollection<FamEditorParameter> SelectedFamParams { get; set; }

        public Window_FamEditor(RvtConnector rvtConnector)
        {
            InitializeComponent();

            // Instatiate a main FamEditor class
            FamEditor = new FamEditor();

            FamEditor.RvtConnector = rvtConnector;
            FamEditor.Doc = FamEditor.RvtConnector.Document;

            // Retrieve data from the current Family
            if (!FamEditor.Doc.IsFamilyDocument)
            {
                TaskDialog.Show("Error: RFA Only", 
                    "This addin can only modify Revit families which are open in the family editor. " +
                    "To use this addin, open an RFA file first.");
            }
            else
            {
                FamEditor.LoadData();
            }

            if (FamEditor.FamilyTypes != null)
            {
                // Set the Family Types to the ListBox
                ListBox_FamilyTypes.ItemsSource = FamEditor.FamilyTypes;
                ListBox_FamilyTypes.SelectedItem = ListBox_FamilyTypes.Items[0];
            }
        }

        /// <summary>
        /// Update the view to show/edit Parameters in the window.
        /// </summary>
        private void RefreshParamTableView()
        {
            // Toggle UI
            ScrollViewer_ParamForm.Visibility = System.Windows.Visibility.Collapsed;
            DataGrid_Params.Visibility = System.Windows.Visibility.Visible;

            // Groups Parameters based on Property Name
            ListCollectionView collectionView = new ListCollectionView(SelectedFamParams);

            collectionView.GroupDescriptions.Add(new PropertyGroupDescription("PropGroup"));

            DataGrid_Params.ItemsSource = collectionView;
        }

        /// <summary>
        /// Update the view to show/edit Parameters in the window.
        /// </summary>
        private void RefreshParamFormView()
        {
            // Toggle UI
            DataGrid_Params.Visibility = System.Windows.Visibility.Collapsed;
            ScrollViewer_ParamForm.Visibility = System.Windows.Visibility.Visible;

            // First clear all children and content from previous Family Type selection
            foreach (UIElement c in StackPanel_Params.Children)
            {
                var element = c as FrameworkElement;
                try
                {
                    UnregisterName(element.Name);

                    if (c.GetType() == typeof(Expander))
                    {
                        var expander = c as Expander;
                        var expanderContent = expander.Content as StackPanel;

                        UnregisterName(expanderContent.Name);
                    }
                }
                catch { }
            }

            StackPanel_Params.Children.Clear();

            // Sort thhe Property Groups and add Children to the Stack Panel
            SelectedFamParams.OrderBy(x => x.PropGroup);

            SelectedFamParams.OrderBy(x => x.FamilyParameter.Definition.Name);

            var propGroups = SelectedFamParams.Select(x => x.PropGroup).Distinct();

            foreach (var g in propGroups)
            {
                var cleanName = g.Replace(' ', '_');

                // First we need to clean up any previous registered names
                //UnregisterName("expander_" + cleanName);
                //UnregisterName("expanderContent_" + cleanName);

                // Create new UI elements
                var expander = new Expander();
                expander.IsExpanded = true;
                expander.Header = g;
                expander.Name = "expander_" + cleanName;
                StackPanel_Params.Children.Add(expander);
                RegisterName(expander.Name, expander);

                var groupStackPanel = new StackPanel();
                groupStackPanel.Name = "expanderContent_" + cleanName;
                expander.Content = groupStackPanel;
                RegisterName(groupStackPanel.Name, groupStackPanel);
            }

            // Add each Parameter as children to the appropriaate Expander
            foreach (var p in SelectedFamParams)
            {
                var cleanGroupName = p.PropGroup.Replace(' ', '_');

                var groupExpander = StackPanel_Params.FindName("expander_" + cleanGroupName) as Expander;
                var groupContent = groupExpander.FindName("expanderContent_" + cleanGroupName) as StackPanel;

                var labelName = new TextBlock();
                labelName.Style = this.FindResource("TextBlock_FormLabel") as Style;

                labelName.Name = "Label_ParamName";
                labelName.Text = p.FamilyParameter.Definition.Name;

                groupContent.Children.Add(labelName);

                if (p.FamilyParameter.Definition.ParameterType == ParameterType.YesNo)
                {
                    var checkBoxValue = new System.Windows.Controls.CheckBox();

                    checkBoxValue.IsChecked = (bool)p.Value;

                    groupContent.Children.Add(checkBoxValue);
                }
                else
                {
                    var textBoxValue = new System.Windows.Controls.TextBox();
                    textBoxValue.Style = this.FindResource("TextBox_Default") as Style;

                    textBoxValue.Text = p.Value as string;

                    groupContent.Children.Add(textBoxValue);
                }
            }
        }

        /// <summary>
        /// Family Type ListBox selection changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListBox_FamilyTypes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Get current instance of window
            //var win = Window.GetWindow(System.Windows.Application.Current.MainWindow) as Window_FamEditor;

            SelectedFamType = ListBox_FamilyTypes.SelectedItem as FamilyType;

            // Update the title text
            if (ListBox_FamilyTypes.SelectedItems.Count > 0)
            {
                // Get the Parameters for the selected family
                FamEditor.GetFamParams();

                //// DEBUG: Instantiate a dictionary of Parameter values as strings
                //var paramStrings = new Dictionary<string, string>();

                //foreach (var fam in FamEditor.FamilyParams)
                //{
                //    foreach (var p in fam.Value)
                //    {
                //        paramStrings[p.Definition.Name] = FamEditor.FamilyParamValueString(
                //            SelectedFamType, p, FamEditor.Doc
                //            );
                //    }
                //}

                var famParams = new ObservableCollection<FamEditorParameter>();
                foreach (var fam in FamEditor.FamilyParams)
                {
                    foreach (var p in fam.Value)
                    {
                        var famParam = new FamEditorParameter();
                        famParam.FamilyParameter = p;
                        
                        // Retrieve the value of the Parameter
                        famParam.Value = FamEditor.FamilyParamValue(
                            SelectedFamType, p, FamEditor.Doc);

                        var paramGroup = p.Definition.ParameterGroup;
                        famParam.PropGroup = LabelUtils.GetLabelFor(paramGroup);

                        famParams.Add(famParam);
                    }
                }

                // Set the list of Parameters
                SelectedFamParams = famParams;

                // Refresh the UI to display the Parameters in the table view
                //RefreshParamTableView();

                // Refresh the UI to display the Parameters in the form view
                RefreshParamFormView();
            }
        }
    }
}
