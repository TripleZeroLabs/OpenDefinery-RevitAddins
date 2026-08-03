using OpenDefinery;
using System;
using System.Windows.Interop;

namespace OD_Common
{
    /// <summary>Gates add-in launch behind a required OpenDefinery sign-in.</summary>
    public static class SessionGate
    {
        /// <summary>
        /// Ensure the user is signed in before an add-in opens its window. Restores a remembered /
        /// already-active session silently; otherwise shows the shared login modally, owned to
        /// Revit's main window. Returns the authenticated session, or null if the user cancelled
        /// (the caller should abort without opening its window).
        /// </summary>
        public static Definery EnsureSignedIn(IntPtr revitMainWindow)
        {
            var restored = SessionManager.TryRestore();
            if (restored != null) return restored;

            var login = new LoginWindow();
            new WindowInteropHelper(login).Owner = revitMainWindow;
            var ok = login.ShowDialog();
            return ok == true ? login.Session : null;
        }
    }
}
