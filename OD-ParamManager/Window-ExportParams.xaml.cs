using OpenDefinery;
using System;
using System.Windows;
using System.Windows.Input;

namespace OD_ParamManager
{
    /// <summary>
    /// Standalone Export Parameters window. Thin shell around <see cref="Control_CurrentFamily"/>
    /// (shared with the Parameter Manager's "Current Family" tab): this window adds sign-in and
    /// an explicit Close button.
    /// </summary>
    public partial class Window_ExportParams : Window
    {
        private RvtConnector RvtConnector { get; set; }

        public Window_ExportParams(RvtConnector rvtConnector)
        {
            InitializeComponent();

            RvtConnector = rvtConnector;

            Loaded += Window_ExportParams_Loaded;

            // Read the document's parameters immediately; sign-in gates only the export side.
            CurrentFamily.SetConnector(RvtConnector);
        }

        private void Window_ExportParams_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateTitle();

            // Reuse this Revit session's sign-in, or restore a remembered one, otherwise prompt.
            var session = SessionManager.TryRestore();
            if (session != null)
            {
                RememberMeCheck.IsChecked = true;
                CurrentFamily.SetSession(session);
            }
            else
            {
                ShowLogin();
            }
        }

        private void UpdateTitle()
        {
            Title = string.Format("Export Shared Parameters  —  {0} in {1}",
                CurrentFamily.ParameterCount,
                CurrentFamily.IsFamilyDocument ? "this family" : "this project");
        }

        /// <summary>
        /// Escape must not close this window. It only dismisses the New Collection overlay (via
        /// the control) or the login card when one is open.
        /// </summary>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (OverlayGrid.Visibility == Visibility.Visible)
                {
                    // Login is required to proceed; don't let Escape dismiss it.
                }
                else
                {
                    CurrentFamily.HandleEscape();
                }

                e.Handled = true;
                return;
            }

            base.OnPreviewKeyDown(e);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ---------------------------------------------------------------- login

        private void ShowLogin()
        {
            OverlayGrid.Visibility = Visibility.Visible;
            UsernameTextBox.Focus();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            LoginStatusText.Text = string.Empty;

            var username = UsernameTextBox.Text;
            var password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                LoginStatusText.Text = "Enter a username and password.";
                return;
            }

            Definery session;
            try
            {
                session = Definery.Init(new Definery(), username, password);
            }
            catch (Exception ex)
            {
                LoginStatusText.Text = "Login error: " + ex.Message;
                return;
            }

            if (session == null || string.IsNullOrEmpty(session.CsrfToken))
            {
                LoginStatusText.Text = "Login failed. Check your credentials and try again.";
                return;
            }

            if (RememberMeCheck.IsChecked == true)
            {
                SessionStore.Save(username, password);
            }
            else
            {
                SessionStore.Clear();
            }

            OverlayGrid.Visibility = Visibility.Collapsed;
            CurrentFamily.SetSession(session);
        }
    }
}
