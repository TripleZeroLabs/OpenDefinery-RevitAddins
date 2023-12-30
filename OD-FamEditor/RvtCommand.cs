using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OD_ParamManager;
using OpenDefinery;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace OD_FamEditor
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
            var mw = new Window_FamEditor(rvtConnector);
            mw.ShowDialog();

            return Result.Succeeded;
        }
    }
}