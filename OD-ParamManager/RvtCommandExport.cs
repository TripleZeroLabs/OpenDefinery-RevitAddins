using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OpenDefinery;
using System.Windows.Interop;

namespace OD_ParamManager
{
    /// <summary>
    /// "Export Parameters" ribbon command: lists the shared parameters in the active family
    /// (or project) and exports the selected ones to an OpenDefinery Collection.
    /// ReadOnly transaction mode - this reads from the model and writes only to the cloud.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class RvtCommandExport : IExternalCommand
    {
        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {
            if (commandData.Application.ActiveUIDocument == null)
            {
                TaskDialog.Show(
                    "No Document Open",
                    "Open a Revit family (or project) before exporting shared parameters.");

                return Result.Cancelled;
            }

            var rvtConnector = new RvtConnector(commandData);

            var window = new Window_ExportParams(rvtConnector);

            // Own the window to Revit's main window. ShowDialog alone only blocks WPF windows
            // on this thread - Revit's main window is native, so without an owner it stays
            // clickable and the dialog can fall behind Revit. Setting the owner makes it
            // properly modal and keeps it in front.
            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;

            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}
