using Autodesk.Revit.UI;
using OpenDefinery;
using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Path = System.IO.Path;

namespace OD_FamEditor
{
    class RvtApp : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication a)
        {
            // Resolve this add-in's dependencies from its own folder (see OD-ParamManager RvtApp).
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
                    "Family Editor",
                    "  Family Editor  ",
                    thisAssemblyPath,
                    "OD_FamEditor.RvtCommand")
                ) as PushButton;

            // Add tool tip
            button.ToolTip = "An improved way to manage family types and data.";

            // Set icon
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
