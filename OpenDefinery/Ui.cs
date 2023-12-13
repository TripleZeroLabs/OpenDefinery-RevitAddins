using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDefinery
{
    public class Ui
    {
        public static RibbonPanel RibbonPanel(UIControlledApplication a, string panelName)
        {
            // Tab name 
            string tab = "Triple Zero Labs";

            // Empty ribbon panel 
            RibbonPanel ribbonPanel = null;

            // Try to create ribbon tab
            try
            {
                a.CreateRibbonTab(tab);
            }
            catch { }

            // Search existing tab for your panel
            List<RibbonPanel> panels = a.GetRibbonPanels(tab);

            foreach (RibbonPanel p in panels)
            {
                if (p.Name == panelName)
                {
                    ribbonPanel = p;
                }
            }

            // If the panel wasn't found, create it instead
            if (ribbonPanel == null)
            {
                // Try to create ribbon panel
                try
                {
                    ribbonPanel = a.CreateRibbonPanel(tab, panelName);
                }
                catch { }
            }

            // Return panel 
            return ribbonPanel;
        }
    }
}
