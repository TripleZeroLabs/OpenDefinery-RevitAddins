using Autodesk.Revit.UI;
using OpenDefinery;
using System.Reflection;
using System.Windows.Media.Imaging;
using System;

namespace OD_ParamManager
{
    class RvtApp : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication a)
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

            // Set icon path to bitmap
            //Uri iconUri = new Uri("pack://application:,,,/Resources/icon_32x32.png");
            Uri iconUri = new Uri("pack://application:,,,/OpenDefinery-ParamManager;component/Assets/Icons/logo_32x32.png");
            BitmapImage largeImage = new BitmapImage(iconUri);

            // Apply image to button 
            button.LargeImage = largeImage;

            a.ApplicationClosing += A_ApplicationClosing;

            //Set Application to Idling
            a.Idling += A_Idling;
            return Result.Succeeded;
        }

        /// <summary>
        /// Idling
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void A_Idling(object sender, Autodesk.Revit.UI.Events.IdlingEventArgs e)
        {

        }

        /// <summary>
        /// Closing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void A_ApplicationClosing(object sender, Autodesk.Revit.UI.Events.ApplicationClosingEventArgs e)
        {
            throw new NotImplementedException();
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }
    }
}
