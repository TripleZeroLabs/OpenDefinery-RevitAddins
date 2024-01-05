using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OD_ParamManager;
using OpenDefinery;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace OD_FamEditor
{
    [Transaction(TransactionMode.Manual)]
    public class RvtCommand : IExternalCommand
    {
        public RvtConnector RvtConnector { get; set; }

        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {
            // Instantiate the connection to the Revit Model
            var rvtConnector = new RvtConnector(commandData);

            // (for C# 8.0)
            var loader = new AssemblyLoader();

            // Instantiate a main window
            var mw = new Window_FamEditor(rvtConnector);
            mw.ShowDialog();

            return Result.Succeeded;
        }
    }

    // IDisposable interface is implemented only to simplify the use.
    // At runtime CLR is trying to find assemlies from Revit.exe directory.
    // So if we subscribe to the AssemblyResolve event we can find and load
    // any assambly at the runtime while using addin commands.
    // Also we need to unsubscribe after, that's why i implemented IDisposible.
    public class AssemblyLoader : IDisposable
    {
        private static string ExecutingPath => Assembly.GetExecutingAssembly().Location;

        public AssemblyLoader()
        {
            AppDomain.CurrentDomain.AssemblyResolve += LoadMaterialDesign;
        }

        private static Assembly LoadMaterialDesign(object sender, ResolveEventArgs args)
        {
            if (null == ExecutingPath) return null;
            string assemlyToLoad = string.Empty;
            var requested = GetAssemblyName(args.Name);
            if (requested == null) return null;

            string GetAssemblyName(string fullName)
            {
                var name = fullName.Substring(0, fullName.IndexOf(','));
                if (name.EndsWith(".resources"))
                {
                    name = name.Substring(0, name.Length - ".resources".Length);

                    return null;
                }

                return name;
            }

            var path = ExecutingPath;
            var dir = new FileInfo(path).Directory;

            var assemblies = from file in dir.EnumerateFiles()
                             where (file.Name.EndsWith(".dll") ||
                                file.Name.EndsWith(".exe"))
                             select Assembly.LoadFrom(file.FullName);

            foreach (var assembly in assemblies)
            {
                var assemName = GetAssemblyName(assembly.FullName);

                try
                {
                    if (assemName == requested)
                    {
                        return assembly;
                    }
                }
                catch (Exception)
                {
                    continue;
                }

                //}
            }
            return null;
        }

        void IDisposable.Dispose()
        {
            AppDomain.CurrentDomain.AssemblyResolve -= LoadMaterialDesign;
        }
    }

}