using OpenDefinery;
using System.Windows;
using System.Windows.Input;

namespace OD_Common
{
    /// <summary>
    /// Shared OpenDefinery sign-in window used by every add-in. On success it authenticates,
    /// publishes the session (Definery.Current), optionally remembers it, and exposes it via
    /// <see cref="Session"/>.
    /// </summary>
    public partial class LoginWindow : Window
    {
        /// <summary>The authenticated session, set once sign-in succeeds; null until then.</summary>
        public Definery Session { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();

            // Pre-fill a remembered username so a re-auth is one field away.
            if (SessionStore.TryLoad(out var username, out _))
            {
                UsernameTextBox.Text = username;
                RememberMeCheck.IsChecked = true;
                Loaded += (s, e) => PasswordBox.Focus();
            }
            else
            {
                Loaded += (s, e) => UsernameTextBox.Focus();
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            LoginStatus.Text = string.Empty;

            var username = UsernameTextBox.Text;
            var password = PasswordBox.Password;
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                LoginStatus.Text = "Enter a username and password.";
                return;
            }

            Definery session;
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            try { session = Definery.Init(new Definery(), username, password); }
            catch (System.Exception ex) { LoginStatus.Text = "Login error: " + ex.Message; return; }
            finally { Mouse.OverrideCursor = null; }

            if (session == null || string.IsNullOrEmpty(session.CsrfToken))
            {
                LoginStatus.Text = "Login failed. Check your credentials and try again.";
                return;
            }

            // Load the reference data the Collection features depend on (DataTypes, published
            // Collections). Definery.Init only sets the auth tokens.
            try
            {
                if (session.DataTypes == null || session.DataTypes.Count == 0)
                    Definery.LoadData(session);
                if (session.PublishedCollections == null)
                    session.PublishedCollections = Collection.GetPublished(session);
            }
            catch { /* non-fatal; windows re-hydrate on demand */ }

            if (RememberMeCheck.IsChecked == true) SessionStore.Save(username, password);
            else SessionStore.Clear();

            Session = session;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
