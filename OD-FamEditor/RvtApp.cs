using Autodesk.Revit.UI;
using OpenDefinery;
using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Path = System.IO.Path;

namespace OD_FamEditor
{
    /// <summary>
    /// Family Editor add-in entry point. Adds a "Family Editor" button to the shared
    /// "OpenDefinery" panel on the "Triple Zero Labs" ribbon tab (joined via the core's
    /// Ui.RibbonPanel helper, so it sits alongside the Parameter Manager buttons).
    /// </summary>
    class RvtApp : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication a)
        {
            // Resolve this add-in's dependencies (OpenDefinery.dll, TripleZeroLabs.Os2.dll, ...)
            // from its own folder before any core type is touched - Revit does not probe the
            // add-in folder automatically on .NET Framework.
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAddinDependencies;

            return Startup(a);
        }

        private Result Startup(UIControlledApplication a)
        {
            RibbonPanel panel = Ui.RibbonPanel(a, "OpenDefinery");

            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;

            PushButton button = panel.AddItem(
                new PushButtonData(
                    "Family Editor",
                    " Family\n Editor ",
                    thisAssemblyPath,
                    "OD_FamEditor.RvtCommand")
                ) as PushButton;

            button.ToolTip = "An improved way to manage family types, parameters, values, and formulas.";

            Uri iconUri = new Uri("pack://application:,,,/OpenDefinery-FamEditor;component/Assets/Icons/logo_32x32.png");
            button.LargeImage = new BitmapImage(iconUri);

            return Result.Succeeded;
        }

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
