using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OpenDefinery;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OD_ParamManager
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public static Document Document { get; set; }

        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document = uidoc.Document;

            // Instantiate a list to store the shared parameters from the current Revit model
            var revitParams = new List<SharedParameter>();

            // Collect shared parameters as elements
            FilteredElementCollector collector
                = new FilteredElementCollector(Document)
                .WhereElementIsNotElementType();

            collector.OfClass(typeof(SharedParameterElement));

            // Add each parameter to the list
            foreach (Element e in collector)
            {
                SharedParameterElement param = e as SharedParameterElement;
                Definition def = param.GetDefinition();

                Debug.WriteLine("[" + e.Id + "]\t" + def.Name + "\t(" + param.GuidValue + ")");

                // Cast the SharedParameterElement to a "lite" OpenDefinery SharedParameter
                // TODO: Retrieve all Revit parameter data such as the DATAGAATEGORY
                var castedParam = new SharedParameter(
                    param.GuidValue, def.Name, def.ParameterType.ToString(), string.Empty, string.Empty, string.Empty, string.Empty, string.Empty
                    );

                castedParam.ElementId = Convert.ToInt32(e.Id.IntegerValue);

                revitParams.Add(castedParam);
            }

            // Instantiate a main window
            var mw = new Window_ParamManager(revitParams);
            mw.ShowDialog();
            
            return Result.Succeeded;
        }

        /// <summary>
        /// Delete Shared Parameter elements from the current Document.
        /// </summary>
        /// <param name="mw">The Window instance to pass make active after purging</param>
        /// <param name="doc">The current Revit Document</param>
        /// <param name="paramsToDelete">The list of OpenDefinery Shared Parameters to delete</param>
        public static void PurgeParameters(Window_ParamManager mw, Document doc, List<SharedParameter> paramsToDelete)
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
                TaskDialog td = new TaskDialog("Purge Shared Parameters");
                td.Id = "PurgeParams";
                td.MainIcon = TaskDialogIcon.TaskDialogIconWarning;
                td.Title = "WARNING: Possible Data Loss";
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

                    mw.Activate();
                }
                else   // Do nothing
                {
                    mw.Activate();
                }
            }
        }
    }
}