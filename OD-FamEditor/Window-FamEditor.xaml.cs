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
            // Groups Parameters based on Property Name
            ListCollectionView collectionView = new ListCollectionView(SelectedFamParams);

            collectionView.GroupDescriptions.Add(new PropertyGroupDescription("PropGroup"));

            DataGrid_Params.ItemsSource = collectionView;
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
                RefreshParamTableView();
            }
        }
    }
}
