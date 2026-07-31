using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OpenDefinery;
using System.Windows.Interop;

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
            var rvtConnector = new RvtConnector(commandData);

            var window = new Window_FamEditor(rvtConnector);

            // Own the window to Revit's main window so it is modal and stays in front.
            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;

            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}
