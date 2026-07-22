using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OpenDefinery;

namespace OD_ParamManager
{
    [Transaction(TransactionMode.Manual)]
    public class RvtCommandFamEditor : IExternalCommand
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

            // Own it to Revit's main window so it is genuinely modal (Revit stays blocked)
            // and never falls behind the Revit window.
            new System.Windows.Interop.WindowInteropHelper(mw).Owner =
                commandData.Application.MainWindowHandle;

            mw.ShowDialog();

            return Result.Succeeded;
        }
    }
}
