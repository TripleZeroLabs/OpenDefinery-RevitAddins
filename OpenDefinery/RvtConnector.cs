using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using OpenDefinery;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenDefinery
{
    public class RvtConnector
    {
        public Autodesk.Revit.ApplicationServices.Application App { get; set; }
        public FilteredElementCollector FamilyInstances { get; set; }
        public Document Document { get; set; }

        public RvtConnector(ExternalCommandData commandData)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;

            App = uiapp.Application;
            Document = uidoc.Document;
        }

        /// <summary>
        /// Create a Shared Parameter from OpenDefinery and copy the values for all Family Types.
        /// Note that this also replaces any instances of the existing Parameter in existing formulas
        /// throughout the Family.
        /// </summary>
        /// <param name="fm"></param>
        /// <param name="sourceParam"></param>
        /// <param name="defineryParam"></param>
        /// <returns></returns>
        public bool CopyParamValues(
            FamilyManager fm,
            SharedParameterElement sourceParam,
            DefineryParameter defineryParam)
        {
            bool success = false;

            // Instantiate Parameter data
            var singleItemList = new List<DefineryParameter>
                {
                    defineryParam
                };

            var newParamElementId = CreateParams(singleItemList).FirstOrDefault();
            var newParam = Document.GetElement(newParamElementId) as SharedParameterElement;
            var existingParamDef = sourceParam.GetDefinition();


            // Loop through all Family Types to update the value for each type
            foreach (FamilyType ft in fm.Types)
            {
                if (ft.Name != " ")
                {
                    var newFamilyParameter = fm.get_Parameter(newParam.GuidValue);

                    // Retrieve all Parameters in the Family
                    IList<FamilyParameter> allParams = fm.GetParameters();

                    var paranValues = new Dictionary<FamilyParameter, string>();
                    foreach (var p in allParams)
                    {
                        // Copy the value to the new Parameter
                        if (p.Id == sourceParam.Id)
                        {
                            var currentVal = GetValue(
                                ft, p, this.Document);

                            var currentValString = new StringParameterValue(currentVal.ToString());

                            Transaction transSetValue = new Transaction(Document, "Set Parameter Value");

                            transSetValue.Start();

                            try
                            {
                                // Set the current type first since the Set() method only allows to set
                                // the Parameter of the current FamilylType
                                fm.CurrentType = ft;

                                // Set the Parameter value
                                switch (p.StorageType)
                                {
                                    case StorageType.Double:
                                        var doubleValue =
                                        (double)ft.AsDouble(p);

                                        fm.Set(newFamilyParameter, doubleValue);

                                        break;

                                    case StorageType.ElementId:
                                        ElementId id = ft.AsElementId(p);
                                        fm.Set(newFamilyParameter, id);

                                        break;

                                    case StorageType.Integer:
                                        int integerValue = (int)ft.AsInteger(p);
                                        fm.Set(newFamilyParameter, integerValue);

                                        break;

                                    case StorageType.String:
                                        fm.Set(newFamilyParameter, ft.AsString(p));

                                        break;
                                }

                                success = true;
                            }

                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.ToString());

                                success = false;
                            }

                            transSetValue.Commit();

                        }

                        // Replace Parameter in formulas
                        if (!string.IsNullOrEmpty(p.Formula))
                        {

                            // Set the formula for names that are the entire formula
                            if (p.Formula == existingParamDef.Name)
                            {
                                // Start the transaction
                                Transaction transSetFormula = new Transaction(Document, "Replace Parameter");
                                transSetFormula.Start();
                                fm.SetFormula(p, defineryParam.Name);
                                transSetFormula.Commit();

                                success = true;
                            }
                            // TODO: Use regex to find if the parameter name is in the formula
                            //else if (Regex.IsMatch(
                            //    p.Formula.Replace(" ", ""),
                            //    string.Format("[+<>*//^[-]|({0})|[+<>*//^)-]]", sourceParam.Name)))
                            else if (p.Formula.Contains(sourceParam.Name))
                            {
                                //TaskDialog.Show("Param Found", "Found in formula: " + sourceParam.Name);

                                var currentFormula = p.Formula;
                                var newFormula = p.Formula.Replace(sourceParam.Name, defineryParam.Name);
                                    
                                try
                                {
                                    // Start the transaction
                                    Transaction transSetFormula = new Transaction(Document, "Replace Parameter");
                                    transSetFormula.Start();
                                    fm.SetFormula(p, newFormula);
                                    transSetFormula.Commit();

                                    success = true;
                                }
                                catch (Exception e)
                                {
                                    TaskDialog.Show(
                                        "Error while setting the formula", e.ToString() + "\n\n" +
                                        e.Message + "\n\nFormula: " + newFormula);

                                    success = false;
                                }
                            }
                        }
                    }
                }
            }

            return success;
        }

        /// <summary>
        /// Replace an existing Revit Shared Parameter with and OpenDefinery DefineryParameter
        /// </summary>
        /// <param name="elementId">The Revit Element ID of the existing Parameter to be replaced</param>
        /// <param name="defineryParam">The new OpenDefinery DefineryParameter</param>
        /// <returns>True if the replacement was successful.</returns>
        public bool ReplaceParameterInFamilly(
            RvtConnector rvtConnector,
            long elementId,
            DefineryParameter defineryParam)
        {
            var success = false;

            // Retrieve the existing Parameter Element from the Revit DB by its Element ID
            var elemId = RvtCompat.NewElementId(elementId);
            var existingSharedParam = this.Document.GetElement(elemId) as SharedParameterElement;
            Definition existingDef = null;

            // If the Element cast returns null, assume it is not a Shared Parameter
            if (existingSharedParam == null)
            {
                var existingFamParam = this.Document.GetElement(elemId) as ParameterElement;
                existingDef = existingFamParam.GetDefinition();
            }
            else
            {
                existingDef = existingSharedParam.GetDefinition();
            }

            // Check if the parameter data types match
            var existingDataType = RvtCompat.GetDataTypeToken(existingDef);

            if (existingDataType == defineryParam.DataType)
            {
                FamilyManager fm = Document.FamilyManager;

                // Check if the Parameter already exists
                if (fm.get_Parameter(defineryParam.Guid) == null)
                {
                    // Copy the Parameter values from existing to new
                    success = CopyParamValues(fm, existingSharedParam, defineryParam);

                    //// Loop through all Family Types to update the value for each type
                    //foreach (FamilyType ft in fm.Types)
                    //{
                    //    if (ft.Name != " ")
                    //    {
                    //        var newFamilyParameter = fm.get_Parameter(destinationParam.GuidValue);

                    //        // Retrieve all Parameters in the Family
                    //        IList<FamilyParameter> allParams = fm.GetParameters();

                    //        var paranValues = new Dictionary<FamilyParameter, string>();
                    //        foreach (var p in allParams)
                    //        {
                    //            // Copy the value to the new Parameter
                    //            if (p.Id == sourceParam.Id)
                    //            {
                    //                var currentVal = GetValue(
                    //                    ft, p, this.Document);

                    //                var currentValString = new StringParameterValue(currentVal.ToString());

                    //                Transaction transSetValue = new Transaction(Document, "Set Parameter Value");

                    //                transSetValue.Start();

                    //                try
                    //                {
                    //                    // Set the current type first since the Set() method only allows to set
                    //                    // the Parameter of the current FamilylType
                    //                    fm.CurrentType = ft;

                    //                    // Set the Parameter value
                    //                    switch (p.StorageType)
                    //                    {
                    //                        case StorageType.Double:
                    //                            var doubleValue =
                    //                            (double)ft.AsDouble(p);

                    //                            fm.Set(newFamilyParameter, doubleValue);

                    //                            break;

                    //                        case StorageType.ElementId:
                    //                            ElementId id = ft.AsElementId(p);
                    //                            fm.Set(newFamilyParameter, id);

                    //                            break;

                    //                        case StorageType.Integer:
                    //                            int integerValue = (int)ft.AsInteger(p);
                    //                            fm.Set(newFamilyParameter, integerValue);

                    //                            break;

                    //                        case StorageType.String:
                    //                            fm.Set(newFamilyParameter, ft.AsString(p));

                    //                            break;
                    //                    }

                    //                    success = true;
                    //                    //fm.Set(newFamilyParameter, value);
                    //                }

                    //                catch (Exception ex)
                    //                {
                    //                    Debug.WriteLine(ex.ToString());
                    //                }

                    //                transSetValue.Commit();
                    //            }

                    //            // Replace Parameter in formulas
                    //            if (!string.IsNullOrEmpty(p.Formula))
                    //            {

                    //                // Set the formula for names that are the entire formula
                    //                if (p.Formula == existingParamDef.Name)
                    //                {
                    //                    // Start the transaction
                    //                    Transaction transSetValue = new Transaction(Document, "Replace Parameter");
                    //                    transSetValue.Start();
                    //                    fm.SetFormula(p, defineryParam.Name);
                    //                    transSetValue.Commit();

                    //                    success = true;
                    //                }
                    //                // TODO: Use regex to find if the parameter name is in the formula
                    //                else if (Regex.IsMatch(
                    //                    p.Formula.Replace(" ", ""),
                    //                    string.Format("[\\[+<>-]{0}[+<>-\\]]", sourceParam.Name)))
                    //                {
                    //                    //TaskDialog.Show("Param Found", "Found in formula: " + sourceParam.Name);

                    //                    var currentFormula = p.Formula;
                    //                    var newFormula = p.Formula.Replace(sourceParam.Name, defineryParam.Name);
                    //                    try
                    //                    {
                    //                        // Start the transaction
                    //                        Transaction transSetValue = new Transaction(Document, "Replace Parameter");
                    //                        transSetValue.Start();
                    //                        fm.SetFormula(p, newFormula);
                    //                        transSetValue.Commit();

                    //                        success = true;
                    //                    }
                    //                    catch (Exception e)
                    //                    {
                    //                        TaskDialog.Show(
                    //                            "Error while setting the formula", e.ToString() + "\n\n" +
                    //                            e.Message + "\n\nFormula: " + newFormula);

                    //                        success = false;
                    //                    }
                    //                }
                    //            }
                    //        }
                    //    }
                    //}
                }
                else
                {
                    TaskDialog.Show(
                    "Shared Parameter Exists",
                    string.Format(
                        "The shared parameter \"{0}\" already exists in this family. Please select another parameter.",
                        defineryParam.Name)
                    );

                    success = false;
                }
            }

            // Show the user an error if the datatypes do not match
            else
            {
                TaskDialog.Show(
                    "DataType Error", "The DataType of the selected parameter does not match the parameter to be replaced. " +
                    "Please select another parameter."
                    );

                success = false;
            }

            return success;
        }

        /// <summary>
        /// Per-parameter outcome of the most recent <see cref="CreateParams"/> call.
        /// </summary>
        public List<ParamLoadResult> LastLoadResults { get; private set; }

        /// <summary>
        /// Add OpenDefinery parameters to the active FAMILY document, each under its own
        /// parameter group and instance/type binding. Records a per-parameter outcome in
        /// <see cref="LastLoadResults"/>. Family documents only - a project document reports
        /// every request as failed (binding + group are family-parameter concepts).
        /// </summary>
        public List<ElementId> CreateParamsWithBinding(List<FamilyParamRequest> requests)
        {
            var output = new List<ElementId>();
            LastLoadResults = new List<ParamLoadResult>();

            if (requests == null || requests.Count == 0) return output;

            if (Document == null || !Document.IsFamilyDocument)
            {
                foreach (var r in requests)
                {
                    LastLoadResults.Add(new ParamLoadResult
                    {
                        Name = r.Param.Name,
                        Guid = r.Param.Guid.ToString(),
                        Outcome = ParamLoadOutcome.Failed,
                        Message = "Binding and parameter group require an open family document."
                    });
                }

                return output;
            }

            // Write a temp shared-parameter file for the whole batch, then add each definition
            // individually so it can take its own group + binding.
            var paramTable = DefineryParameter.CreateParamTable(requests.Select(r => r.Param).ToList());
            var tempFile = System.IO.Path.GetTempPath() +
                "OpenDefineryTempParameters_" + Guid.NewGuid().ToString() + ".txt";
            File.WriteAllText(tempFile, paramTable);

            // Map by GUID so each ExternalDefinition finds its requested group/binding.
            var byGuid = requests
                .GroupBy(r => r.Param.Guid)
                .ToDictionary(g => g.Key, g => g.First());

            // Setting SharedParametersFilename to null afterward is valid, so no null-guard here
            // (unlike the older LoadAllParams, which silently no-ops when none was configured).
            var previousParamTxtFile = this.App.SharedParametersFilename;
            this.App.SharedParametersFilename = tempFile;

            try
            {
                DefinitionFile spFile = null;
                try
                {
                    spFile = this.App.OpenSharedParameterFile();
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Error opening shared parameter file",
                        "There was an error opening the shared parameter text file.\n\n" + ex);
                }

                if (spFile != null)
                {
                    FamilyManager famMan = this.Document.FamilyManager;

                    // Revit rejects a parameter whose GUID or name is already in use, so index both.
                    var existingGuids = new HashSet<Guid>();
                    var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (FamilyParameter ep in famMan.GetParameters())
                    {
                        if (ep.Definition != null) existingNames.Add(ep.Definition.Name);
                        if (ep.IsShared) existingGuids.Add(ep.GUID);
                    }

                    using (Transaction t = new Transaction(this.Document))
                    {
                        t.Start("Add Shared Parameters");

                        foreach (DefinitionGroup dg in spFile.Groups)
                        {
                            foreach (ExternalDefinition extDef in dg.Definitions.Cast<ExternalDefinition>())
                            {
                                FamilyParamRequest req;
                                if (!byGuid.TryGetValue(extDef.GUID, out req)) continue;

                                if (existingGuids.Contains(extDef.GUID) || existingNames.Contains(extDef.Name))
                                {
                                    LastLoadResults.Add(new ParamLoadResult
                                    {
                                        Name = extDef.Name,
                                        Guid = extDef.GUID.ToString(),
                                        Outcome = ParamLoadOutcome.AlreadyExists,
                                        Message = "Already in this family."
                                    });
                                    continue;
                                }

                                try
                                {
                                    var group = req.Group ?? RvtCompat.IdentityGroup;
                                    FamilyParameter fp = RvtCompat.AddParameter(famMan, extDef, group, req.IsInstance);

                                    output.Add(fp.Id);
                                    existingGuids.Add(extDef.GUID);
                                    existingNames.Add(extDef.Name);

                                    LastLoadResults.Add(new ParamLoadResult
                                    {
                                        Name = extDef.Name,
                                        Guid = extDef.GUID.ToString(),
                                        Outcome = ParamLoadOutcome.Added
                                    });
                                }
                                catch (Exception ex)
                                {
                                    LastLoadResults.Add(new ParamLoadResult
                                    {
                                        Name = extDef.Name,
                                        Guid = extDef.GUID.ToString(),
                                        Outcome = ParamLoadOutcome.Failed,
                                        Message = ex.Message
                                    });
                                }
                            }
                        }

                        t.Commit();
                    }
                }

                this.App.SharedParametersFilename = previousParamTxtFile;
            }
            finally
            {
                try { File.Delete(tempFile); } catch { /* temp file cleanup is best-effort */ }
            }

            return output;
        }

        /// <summary>
        /// Delete shared parameters from the active document by GUID. In a family this removes
        /// the family parameter; in a project it deletes the SharedParameterElement (removing it
        /// from the project and every loaded family/instance). Wrapped in a single named
        /// transaction so the whole operation is a single Revit undo step.
        /// </summary>
        /// <param name="guids">GUIDs of the shared parameters to delete.</param>
        /// <param name="failures">Per-parameter failure messages, if any.</param>
        /// <returns>The number of parameters actually deleted.</returns>
        public int DeleteParams(List<Guid> guids, out List<string> failures)
        {
            failures = new List<string>();
            var deleted = 0;

            if (Document == null || guids == null || guids.Count == 0) return 0;

            var guidSet = new HashSet<Guid>(guids);

            using (Transaction t = new Transaction(Document, "Delete Shared Parameters"))
            {
                t.Start();

                if (Document.IsFamilyDocument)
                {
                    var fm = Document.FamilyManager;

                    // Collect first, then remove: mutating while enumerating GetParameters() is unsafe.
                    var toRemove = fm.GetParameters()
                        .Where(fp => fp.IsShared && guidSet.Contains(fp.GUID))
                        .ToList();

                    foreach (var fp in toRemove)
                    {
                        var name = fp.Definition != null ? fp.Definition.Name : fp.GUID.ToString();
                        try
                        {
                            fm.RemoveParameter(fp);
                            deleted++;
                        }
                        catch (Exception ex)
                        {
                            failures.Add(name + ": " + ex.Message);
                        }
                    }
                }
                else
                {
                    foreach (var guid in guidSet)
                    {
                        var spe = SharedParameterElement.Lookup(Document, guid);
                        if (spe == null)
                        {
                            failures.Add(guid.ToString() + ": not found in this project.");
                            continue;
                        }

                        var name = spe.GetDefinition() != null ? spe.GetDefinition().Name : guid.ToString();
                        try
                        {
                            Document.Delete(spe.Id);
                            deleted++;
                        }
                        catch (Exception ex)
                        {
                            failures.Add(name + ": " + ex.Message);
                        }
                    }
                }

                t.Commit();
            }

            return deleted;
        }

        /// <summary>
        /// Add OpenDefinery Parameters to the current Document
        /// </summary>
        /// <param name="paramsToAdd"></param>
        public List<ElementId> CreateParams(List<DefineryParameter> paramsToAdd)
        {
            // Generate a temporary Shared Parameter text file
            var paramTable = DefineryParameter.CreateParamTable(paramsToAdd);
            var tempFolder = System.IO.Path.GetTempPath();
            var tempParamTextFile =
                string.Format(
                    tempFolder + "OpenDefineryTempParameters_" + Guid.NewGuid().ToString() + ".txt"
                    );

            // Write the string the text file
            File.WriteAllText(tempParamTextFile, paramTable);

            // Load all Shared Parameters
            var addedParams = LoadAllParams(tempParamTextFile);

            // Delete the temporary file
            File.Delete(tempParamTextFile);

            return addedParams;
        }

        /// <summary>
        /// Add Shared Parameters to the current Revit Family from a text file
        /// </summary>
        /// <param name="parmsTxtFilePath"></param>
        public List<ElementId> LoadAllParams(string parmsTxtFilePath)
        {
            // Instantiate lists for output later
            var successful = new List<ExternalDefinition>();
            var failed = new List<ExternalDefinition>();
            var skipped = new List<ExternalDefinition>();
            var failureReasons = new Dictionary<string, string>();
            var output = new List<ElementId>();

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
                    TaskDialog.Show("Error opening shared parameter file",
                        "There was an error opening the shared parameter text file.\n\n" + ex.ToString()
                        );
                }

                // Logic to run if the current Document is a Revit Family
                if (spFile != null && this.Document.IsFamilyDocument)
                {
                    // Instantiate a FamilyManager instance to modify the famile
                    FamilyManager famMan = this.Document.FamilyManager;

                    // Index the parameters already in the family. Revit rejects a parameter
                    // whose GUID *or* name is already in use, so both are checked up front -
                    // AddParameter throws ArgumentException rather than returning a result.
                    var existingGuids = new HashSet<Guid>();
                    var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (FamilyParameter existingParam in famMan.GetParameters())
                    {
                        if (existingParam.Definition != null)
                        {
                            existingNames.Add(existingParam.Definition.Name);
                        }

                        // GUID is only meaningful on shared parameters.
                        if (existingParam.IsShared)
                        {
                            existingGuids.Add(existingParam.GUID);
                        }
                    }

                    // Loop through all Groups in the shared parameter text file
                    using (Transaction t = new Transaction(this.Document))
                    {
                        t.Start("Add Shared Parameters");

                        // Get each group in the shared parameter file
                        foreach (DefinitionGroup dG in spFile.Groups)
                        {
                            var v = (from ExternalDefinition d in dG.Definitions select d);

                            // Get each parameter in the current group
                            foreach (ExternalDefinition extDef in v)
                            {
                                // Already in this family - skip quietly rather than failing the batch.
                                if (existingGuids.Contains(extDef.GUID) ||
                                    existingNames.Contains(extDef.Name))
                                {
                                    skipped.Add(extDef);
                                    continue;
                                }

                                try
                                {
                                    FamilyParameter fp = RvtCompat.AddIdentityParameter(famMan, extDef);

                                    successful.Add(extDef);
                                    output.Add(fp.Id);

                                    // Keep the index current so duplicates within this same
                                    // batch are caught too.
                                    existingGuids.Add(extDef.GUID);
                                    existingNames.Add(extDef.Name);
                                }
                                catch (Exception ex)
                                {
                                    failed.Add(extDef);
                                    failureReasons[extDef.Name] = ex.Message;
                                }
                            }

                        }
                        t.Commit();

                    }

                }
                // Logic to execute when the current Document is a project
                else if (spFile != null && !this.Document.IsFamilyDocument)
                {
                    // Loop through all Groups in the shared parameter text file
                    using (Transaction t = new Transaction(this.Document))
                    {
                        t.Start("Add Shared Parameters");

                        // Get each group in the shared parameter file
                        foreach (DefinitionGroup dG in spFile.Groups)
                        {
                            var v = (from ExternalDefinition d in dG.Definitions select d);

                            // Get each parameter in the current group
                            foreach (ExternalDefinition extDef in v)
                            {
                                // Already bound in this project - skip quietly.
                                if (SharedParameterElement.Lookup(this.Document, extDef.GUID) != null)
                                {
                                    skipped.Add(extDef);
                                    continue;
                                }

                                try
                                {
                                    var newParam = SharedParameterElement.Create(
                                        this.Document,
                                        extDef
                                    );

                                    successful.Add(extDef);
                                    output.Add(newParam.Id);
                                }
                                catch (Exception ex)
                                {
                                    failed.Add(extDef);
                                    failureReasons[extDef.Name] = ex.Message;
                                }
                            }

                        }
                        t.Commit();

                    }

                }

                // Reset the shared parameters text file back to the original
                this.App.SharedParametersFilename = previousParamTxtFile;
            }

            // Record a per-parameter outcome so the caller can report what actually happened
            // instead of just a count of the ones that succeeded.
            LastLoadResults = new List<ParamLoadResult>();

            foreach (ExternalDefinition d in successful)
            {
                LastLoadResults.Add(new ParamLoadResult
                {
                    Name = d.Name,
                    Guid = d.GUID.ToString(),
                    Outcome = ParamLoadOutcome.Added
                });
            }

            foreach (ExternalDefinition d in skipped)
            {
                LastLoadResults.Add(new ParamLoadResult
                {
                    Name = d.Name,
                    Guid = d.GUID.ToString(),
                    Outcome = ParamLoadOutcome.AlreadyExists,
                    Message = "Already in this document."
                });
            }

            foreach (ExternalDefinition d in failed)
            {
                string reason;

                LastLoadResults.Add(new ParamLoadResult
                {
                    Name = d.Name,
                    Guid = d.GUID.ToString(),
                    Outcome = ParamLoadOutcome.Failed,
                    Message = failureReasons.TryGetValue(d.Name, out reason) ? reason : "Unknown error."
                });
            }

            return output;
        }

        /// <summary>
        /// Get the value of a FamilyParameter
        /// </summary>
        /// <param name="t"></param>
        /// <param name="fp"></param>
        /// <param name="doc"></param>
        /// <returns></returns>
        public static object GetValue(
          FamilyType t,
          FamilyParameter fp,
          Document doc)
        {
            object value = t.AsValueString(fp);

            switch (fp.StorageType)
            {
                case StorageType.Double:
                    var doubleValue =
                      (double)t.AsDouble(fp);

                    value = doubleValue;

                    break;

                case StorageType.ElementId:
                    //if (fp.Definition.ParameterType == ParameterType.Image)
                    //{
                    //    value = "{{ Image }}";
                    //}
                    //else if (fp.Definition.ParameterType == ParameterType.Material)
                    //{
                    //    value = "{{ Material }}";
                    //}
                    //else
                    //{
                    //    ElementId id = t.AsElementId(fp);
                    //    Element e = doc.GetElement(id);
                    //    value = id.ToString();
                    //}

                    ElementId id = t.AsElementId(fp);
                    value = id.ToString();

                    break;

                case StorageType.Integer:
                    if (RvtCompat.IsYesNo(fp.Definition))
                    {
                        var intValue = t.AsInteger(fp).ToString();

                        if (intValue == "1")
                        {
                            value = true;
                        }
                        else if (intValue == "0")
                        {
                            value = false;
                        }
                        else
                        {
                            value = null;
                        }
                    }
                    else
                    {
                        value = t.AsInteger(fp).ToString();
                    }

                    break;

                case StorageType.String:
                    value = t.AsString(fp);

                    break;
            }
            return value;
        }

        /// <summary>
        /// Helper method to cast any Parameter datatype to a string
        /// </summary>
        /// <param name="t"></param>
        /// <param name="fp"></param>
        /// <param name="doc"></param>
        /// <returns></returns>
        public static string GetValueString(
          FamilyType t,
          FamilyParameter fp,
          Document doc)
        {
            string value = t.AsValueString(fp);

            switch (fp.StorageType)
            {
                case StorageType.Double:
                    var doubleValue =
                      (double)t.AsDouble(fp);

                    value = doubleValue.ToString();

                    break;

                case StorageType.ElementId:
                    //if (fp.Definition.ParameterType == ParameterType.Image)
                    //{
                    //    value = "Image";
                    //}
                    //else if (fp.Definition.ParameterType == ParameterType.Material)
                    //{
                    //    value = "Material";
                    //}
                    //else
                    //{
                    //    ElementId id = t.AsElementId(fp);
                    //    Element e = doc.GetElement(id);
                    //    value = id.ToString();
                    //}

                    ElementId id = t.AsElementId(fp);
                    value = id.ToString();

                    break;

                case StorageType.Integer:
                    if (RvtCompat.IsYesNo(fp.Definition))
                    {
                        var intValue = t.AsInteger(fp).ToString();

                        if (intValue == "1")
                        {
                            value = "YES";
                        }
                        else if (intValue == "0")
                        {
                            value = "NO";
                        }
                        else
                        {
                            value = "NULL";
                        }
                    }
                    else
                    {
                        value = t.AsInteger(fp).ToString();
                    }

                    break;

                case StorageType.String:
                    value = t.AsString(fp);

                    break;
            }
            return value;
        }

        /// <summary>
        /// Set the value of a Family Parameter
        /// </summary>
        /// <param name="fm"></param>
        /// <param name="fp"></param>
        /// <param name="value"></param>
        public static void SetValue(
          FamilyManager fm,
          FamilyParameter fp,
          object value)
        {
            switch (fp.StorageType)
            {
                case StorageType.Double:
                    Double doubleVal = (Double)value;
                    fm.Set(fp, doubleVal);

                    break;

                case StorageType.ElementId:
                    ElementId elemIdVal = (ElementId)value;
                    fm.Set(fp, elemIdVal);

                    break;

                case StorageType.Integer:
                    int intVal = (int)value;
                    fm.Set(fp, intVal);

                    break;

                case StorageType.String:
                    string stringVal = (string)value;
                    fm.Set(fp, stringVal);

                    break;
            }
        }
    }
}