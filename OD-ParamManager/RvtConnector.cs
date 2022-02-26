using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace OD_ParamManager
{
    public class RvtConnector
    {
        public FilteredElementCollector FamilyInstances { get; set; }
        public Document Document { get; set; }

        public RvtConnector(ExternalCommandData commandData)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document = uidoc.Document;

            // Retrieve all family instances from the model
            FamilyInstances = GetFamilyInstances(Document);
        }

        static FilteredElementCollector GetFamilyInstances(Document doc)
        {
            // Set all Family Instances
            FilteredElementCollector collector1 = new FilteredElementCollector(doc);
            return collector1.OfClass(typeof(FamilyInstance));
        }
    }
}
