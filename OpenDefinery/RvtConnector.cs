using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OpenDefinery;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
    }
}
