using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OpenDefinery;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace OD_ParamManager
{
    [Transaction(TransactionMode.Manual)]
    public class RvtCommand : IExternalCommand
    {
        public RvtConnector RvtConnector { get; set; }

        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {
            // Instantiate the connection to the Revit Model
            var rvtConnector = new RvtConnector(commandData);

            // Instantiate a main window
            var mw = new Window_ParamManager(rvtConnector);
            mw.ShowDialog();

            return Result.Succeeded;
        }

        /// <summary>
        /// Delete Shared Parameter elements from the current Document.
        /// </summary>
        /// <param name="mw">The Window instance to pass make active after purging</param>
        /// <param name="doc">The current Revit Document</param>
        /// <param name="paramsToDelete">The list of OpenDefinery Shared Parameters to delete</param>
        public static bool PurgeParameters(Window_ParamManager mw, Document doc, List<DefineryParameter> paramsToDelete)
        {
            // Instantiate a collection of parameters to delete from the model
            ICollection<ElementId> elementIds = new List<ElementId>();

            // Retrieve Element IDs
            foreach (var p in paramsToDelete)
            {
                ElementId id = new ElementId(p.ElementId);

                elementIds.Add(id);
            }

            // Delete all Elements by their ID
            if (elementIds.Count > 0)
            {
                // Instantiate a TaskDialog to warn the user of data loss
                TaskDialog td = new TaskDialog("WARNING: Possible Data Loss");
                td.Id = "PurgeParams";
                td.MainIcon = TaskDialogIcon.TaskDialogIconWarning;
                td.TitleAutoPrefix = false;
                td.AllowCancellation = true;

                td.MainInstruction = string.Format(
                    "Are you sure you want to purge {0} shared parameters from this model?",
                    elementIds.Count.ToString()
                    );

                td.MainContent =
                    "Purging shared parameters from this model will completely delete them from the project model and any loaded families. " +
                    "This can result in untinentional data loss.\n\n" +
                    "Would you like to continue?";

                td.ExpandedContent = "";

                foreach (var p in paramsToDelete)
                {
                    td.ExpandedContent += p.Name + " [" + p.Guid + "]\n";
                }

                td.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                TaskDialogResult result = td.Show();

                // Delete Shared Parameters from the model if the user confirms
                if (result == TaskDialogResult.Yes)
                {
                    Transaction trans = new Transaction(doc, "Purge Shared Parameters");
                    trans.Start();

                    foreach (var eId in elementIds)
                    {
                        doc.Delete(eId);
                    }

                    trans.Commit();

                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Get details about a Shared Parameter such as which elements are using it
        /// </summary>
        /// <param name="doc">The Document to search</param>
        /// <param name="odParam">An OpenDefinery Shared Parameter</param>
        /// <returns></returns>
        public static Dictionary<string, List<string>> GetParamDetails(RvtConnector rvtConnector, DefineryParameter odParam)
        {
            var output = new Dictionary<string, List<string>>();

            // Cast the int to and ElementId and retrieve the Parameter Element from the model
            ElementId paramId = new ElementId(odParam.ElementId);
            var paramElem = rvtConnector.Document.GetElement(paramId) as ParameterElement;
            SharedParameterElement sharedParameterElement = paramElem as SharedParameterElement;
            //var paramDef = paramElem.GetDefinition();

            // Iterate through all elements and add the Shared Parameter to the dictionary if found
            foreach (var e in rvtConnector.FamilyInstances)
            {
                var typeOrInstance = string.Empty;

                // Try to the Family Type to access the type parameters
                Element elemType = rvtConnector.Document.GetElement(e.GetTypeId());

                if (elemType != null)
                {
                    var paramSet = elemType.Parameters;

                    foreach (Parameter p in paramSet)
                    {
                        if (p.IsShared)
                        {
                            if (p.GUID == sharedParameterElement.GuidValue)
                            {
                                typeOrInstance = "Type";

                                var data = new List<string>();

                                // Create a unique key for the dictionary
                                var key = string.Format("{0} <{1}>", p.Definition.Name, elemType.Id.ToString());

                                data.Add("Element Name: " + e.Name + ":" + elemType.Name);
                                data.Add("Guid: " + p.GUID.ToString());
                                data.Add("Binding: " + typeOrInstance);
                                data.Add("Value: " + p.AsString());

                                // Only add the data once since the parameter is associated to a Family Type
                                if (!output.ContainsKey(key))
                                {
                                    output.Add(key, data);
                                }
                            }
                        }
                    }
                }

                // Retrieve the instance value if it wasn't retrieved from the Family Type
                if (string.IsNullOrEmpty(typeOrInstance))
                {
                    typeOrInstance = "Instance";
                    var paramSet = e.Parameters;

                    foreach (Parameter p in paramSet)
                    {
                        if (p.IsShared)
                        {
                            if (p.GUID == sharedParameterElement.GuidValue)
                            {
                                var data = new List<string>();

                                // Create a unique key for the dictionary
                                var key = string.Format("{0} <{1}>", p.Definition.Name, e.Id.ToString());

                                data.Add("Element Name: " + e.Name);
                                data.Add("Name: " + p.Definition.Name);
                                data.Add("Guid: " + p.GUID.ToString());
                                data.Add("Binding: " + typeOrInstance);
                                data.Add("Value: " + p.AsString());

                                if (!output.ContainsKey(key))
                                {
                                    output.Add(key, data);
                                }
                            }
                        }
                    }
                }
            }

            // TODO: Output a better data structure that shows:
            // Param name, guid, datatype, instance/type, element id, and value
            return output;
        }
    }
}