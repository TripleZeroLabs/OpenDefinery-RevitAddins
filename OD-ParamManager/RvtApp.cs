using Autodesk.Revit.UI;
using OpenDefinery;
using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Path = System.IO.Path;

namespace OD_ParamManager
{
    class RvtApp : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication a)
        {
            // Resolve this add-in's dependencies (OpenDefinery.dll, OpenDefinery.Theme.dll,
            // System.Text.Json.dll, CompareNETObjects.dll, ...) from its own folder. On .NET
            // Framework, Revit does not probe the add-in folder automatically. Registered here
            // BEFORE any OpenDefinery type is touched (the real work lives in Startup), so the
            // handler is active by the time those assemblies need to load.
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAddinDependencies;

            return Startup(a);
        }

        private Result Startup(UIControlledApplication a)
        {
            // Instantiate the ribbon panel to add the button to
            RibbonPanel panel = Ui.RibbonPanel(a, "OpenDefinery");

            // Reflection to look for this assembly path
            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;

            // Add button to panel
            PushButton button = panel.AddItem(
                new PushButtonData(
                    "Parameter Manager",
                    "  Param Manager  ",
                    thisAssemblyPath,
                    "OD_ParamManager.RvtCommand")
                ) as PushButton;

            // Add tool tip
            button.ToolTip = "Manage shared parameters using the OpenDefinery platform.";

            // Set icon
            Uri iconUri = new Uri("pack://application:,,,/OpenDefinery-ParamManager;component/Assets/Icons/logo_32x32.png");
            button.LargeImage = new BitmapImage(iconUri);

            // Export Parameters: pushes shared parameters from the open family/project up to
            // an OpenDefinery Collection.
            PushButton exportButton = panel.AddItem(
                new PushButtonData(
                    "Export Parameters",
                    "  Export Parameters  ",
                    thisAssemblyPath,
                    "OD_ParamManager.RvtCommandExport")
                ) as PushButton;

            exportButton.ToolTip = "Export shared parameters from the current family to an OpenDefinery Collection.";
            exportButton.LargeImage = new BitmapImage(iconUri);

            // Family Editor: manage family types and their data.
            PushButton famEditorButton = panel.AddItem(
                new PushButtonData(
                    "Family Editor",
                    "  Family Editor  ",
                    thisAssemblyPath,
                    "OD_ParamManager.RvtCommandFamEditor")
                ) as PushButton;

            famEditorButton.ToolTip = "An improved way to manage family types and data.";
            famEditorButton.LargeImage = new BitmapImage(iconUri);

            return Result.Succeeded;
        }

        /// <summary>
        /// Load a requested dependency from the add-in's own directory if the CLR can't find it.
        /// </summary>
        private static Assembly ResolveAddinDependencies(object sender, ResolveEventArgs args)
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(dir)) return null;

            var name = new AssemblyName(args.Name).Name;
            if (name.EndsWith(".resources")) return null;

            var candidate = Path.Combine(dir, name + ".dll");
            return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }
    }
}
