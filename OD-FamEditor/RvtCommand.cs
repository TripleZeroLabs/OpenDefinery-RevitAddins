using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OpenDefinery;

namespace OD_FamEditor
{
    [Transaction(TransactionMode.Manual)]
    public class RvtCommand : IExternalCommand
    {
        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {
            // Instantiate the connection to the Revit Model
            var rvtConnector = new RvtConnector(commandData);

            // Instantiate the Family Editor window
            var mw = new Window_FamEditor(rvtConnector);
            mw.ShowDialog();

            return Result.Succeeded;
        }
    }
}
