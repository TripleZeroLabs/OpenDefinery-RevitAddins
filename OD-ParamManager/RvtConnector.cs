using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OpenDefinery;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OD_ParamManager
{
    public class RvtConnector
    {
        Autodesk.Revit.ApplicationServices.Application App { get; set; }
        public FilteredElementCollector FamilyInstances { get; set; }
        public Document Document { get; set; }

        public RvtConnector(ExternalCommandData commandData)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            
            App = uiapp.Application;
            Document = uidoc.Document;

            // Retrieve all family instances from the model
            FamilyInstances = GetFamilyInstances(Document);
        }

        /// <summary>
        /// Retrieve family instances from the current Revit Document
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        static FilteredElementCollector GetFamilyInstances(Document doc)
        {
            // Set all Family Instances
            FilteredElementCollector collector1 = new FilteredElementCollector(doc);
            return collector1.OfClass(typeof(FamilyInstance));
        }

        /// <summary>
        /// Add Shared Parameters to the current model from a text file
        /// </summary>
        /// <param name="parmsTxtFilePath"></param>
        public void LoadFromTxt(string parmsTxtFilePath)
        {
            if (this.App.SharedParametersFilename != null)
            {
                // Store the current shared parameters file in a variable temporarily to reassign later
                var previousParamTxtFile = this.App.SharedParametersFilename;

                // Set the shared parameters file to the temporary file
                this.App.SharedParametersFilename = parmsTxtFilePath;

                DefinitionFile spFile = null;

                try
                {
                    spFile = this.App.OpenSharedParameterFile();
                }

                catch (Exception ex)
                {
                    MessageBox.Show(
                        "There was an error opening the shared parameter text file.\n\n" + ex.ToString(),
                        "Error opening shared parameter file");
                }

                if (spFile != null)
                {
                    // Instantiate a FamilyManager instance to modify the famile
                    FamilyManager famMan = this.Document.FamilyManager;

                    // Instantiate a list of the existing parameters
                    var existfamilyPar = famMan.GetParameters();

                    // Loop through all Groups in the shared parameter text file
                    using (Transaction t = new Transaction(this.Document))
                    {
                        t.Start("Add Shared Parameters");

                        // Instantiate lists for output later
                        var successful = new List<ExternalDefinition>();
                        var failed = new List<ExternalDefinition>();

                        // Get each group in the shared parameter file
                        foreach (DefinitionGroup dG in spFile.Groups)
                        {
                            var v = (from ExternalDefinition d in dG.Definitions select d);

                            // Get each parameter in the current group
                            foreach (ExternalDefinition extDef in v)
                            {
                                try
                                {
                                    FamilyParameter fp = famMan.AddParameter(
                                        extDef,
                                        BuiltInParameterGroup.PG_IDENTITY_DATA,
                                        false
                                    );

                                    successful.Add(extDef);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show(ex.ToString(), "Error Adding " + extDef.Name);

                                    failed.Add(extDef);
                                }
                            }

                        }
                        t.Commit();

                        // Output results in Message Box
                        var transactionStatusString = string.Empty;

                        if (successful.Count > 0)
                        {
                            transactionStatusString += "Parameters added succesfully:\n";

                            foreach (ExternalDefinition e in successful)
                            {
                                transactionStatusString += e.Name + ": " + e.GUID.ToString() + "\n";
                            }
                        }

                        if (failed.Count > 0)
                        {
                            transactionStatusString += "\nParameters failed:\n";

                            foreach (ExternalDefinition e in failed)
                            {
                                transactionStatusString += e.Name + ": " + e.GUID.ToString() + "\n";
                            }
                        }

                        MessageBox.Show(transactionStatusString, "Finished Adding Parameters");
                    }

                    // Reset the shared parameters text file back to the original
                    this.App.SharedParametersFilename = previousParamTxtFile;
                }
            }
        }

        /// <summary>
        /// Add the currently selected Shared Parameters in a DataGrid to the current model
        /// </summary>
        /// <param name="dG">DataGrid with current selection</param>
        public void AddSelectedParams(System.Windows.Controls.DataGrid dG)
        {
            var parmsToAdd = new List<SharedParameter>();

            // Add each selected row to the Collection
            foreach (var p in dG.SelectedItems)
            {
                // Get current Shared Parameter as a SharedParameter object
                var selectedParam = p as SharedParameter;

                parmsToAdd.Add(selectedParam);
            }

            // Family Manager logic
            if (this.Document.IsFamilyDocument)
            {
                // Generate a temporary Shared Parameter text file
                var paramTable = SharedParameter.CreateParamTable(parmsToAdd);
                var tempFolder = System.IO.Path.GetTempPath();
                var tempParamTextFile =
                    string.Format(
                        tempFolder + "OpenDefineryTempParameters_" + Guid.NewGuid().ToString() + ".txt"
                        );

                // Write the string the text file
                File.WriteAllText(tempParamTextFile, paramTable);

                // Load all Shared Parameters
                LoadFromTxt(tempParamTextFile);

                // Delete the temporary file
                File.Delete(tempParamTextFile);
            }
            else
            {
                MessageBox.Show(
                    "This feature only works on Revit Families at this time.",
                    "Coming Soon"
                    );
            }
        }
    }
}
