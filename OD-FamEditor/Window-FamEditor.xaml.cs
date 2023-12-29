using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OpenDefinery;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
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
        public FamilyType SelectedFamType { get; set; }
        public FamEditor FamEditor { get; set; }

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

                RefreshUi(this);
            }
        }

        /// <summary>
        /// Refreshes all UI elements
        /// </summary>
        /// <param name="win"></param>
        static void RefreshUi(Window_FamEditor win)
        {

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
                TextBlock_RightPaneTitle.Text = SelectedFamType.Name;

                // Get the Parameters for the selected family
                FamEditor.GetFamParams();

                // Instantiate a dictionary of Parameter values as strings
                var paramStrings = new Dictionary<string, string>();

                foreach (var fam in FamEditor.FamilyParams)
                {
                    foreach (var p in fam.Value)
                    {
                        paramStrings[p.Definition.Name] = FamEditor.FamilyParamValueString(
                            SelectedFamType, p, FamEditor.Doc
                            );
                    }
                }

                DataGrid_Params.ItemsSource = paramStrings;
            }
        }
    }
}
