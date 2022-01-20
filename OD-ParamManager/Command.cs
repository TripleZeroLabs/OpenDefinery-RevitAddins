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
        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            // Instantiate a list to store the shared parameters from the current Revit model
            var revitParams = new List<SharedParameter>();

            // Collect shared parameters as elements
            FilteredElementCollector collector
                = new FilteredElementCollector(doc)
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
    }
}
