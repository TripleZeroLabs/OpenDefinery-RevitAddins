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

namespace OD_FamEditor
{
    public class RvtConnector
    {
        Autodesk.Revit.ApplicationServices.Application App { get; set; }
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
