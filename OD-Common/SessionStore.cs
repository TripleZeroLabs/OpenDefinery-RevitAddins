using OpenDefinery;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Path = System.IO.Path;

namespace OD_Common
{
    /// <summary>
    /// Opt-in "Remember me" credential store shared by every OpenDefinery add-in. The username +
    /// password are encrypted with Windows DPAPI (CurrentUser scope) and written to
    /// %LocalAppData%\OpenDefinery\session.dat, so a sign-in in one add-in is available to the
    /// others (and across restarts).
    ///
    /// DPAPI ties decryption to this Windows user on this machine, so a copied file is useless
    /// elsewhere. It does NOT protect against code running as the same user - the standard
    /// "remember me" tradeoff. Interim measure; when OAuth lands, a refresh token replaces the
    /// stored password here.
    /// </summary>
    public static class SessionStore
    {
        // Non-secret app entropy (the source is open, so this only scopes the blob to this app).
        // Kept stable so sessions remembered by earlier builds still decrypt.
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("OpenDefinery.ParamManager.v1");

        private static string Dir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenDefinery");

        private static string FilePath => Path.Combine(Dir, "session.dat");

        public static bool HasSaved => File.Exists(FilePath);

        public static void Save(string username, string password)
        {
            try
            {
                Directory.CreateDirectory(Dir);

                // username can't contain a newline; the password is everything after the first one.
                var plain = Encoding.UTF8.GetBytes((username ?? string.Empty) + "\n" + (password ?? string.Empty));
                var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
                Array.Clear(plain, 0, plain.Length);

                File.WriteAllBytes(FilePath, encrypted);
            }
            catch
            {
                // If we can't persist, fail closed - just don't remember.
                Clear();
            }
        }

        public static bool TryLoad(out string username, out string password)
        {
            username = null;
            password = null;

            try
            {
                if (!File.Exists(FilePath)) return false;

                var encrypted = File.ReadAllBytes(FilePath);
                var plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                var text = Encoding.UTF8.GetString(plain);
                Array.Clear(plain, 0, plain.Length);

                var split = text.IndexOf('\n');
                if (split < 0) return false;

                username = text.Substring(0, split);
                password = text.Substring(split + 1);
                return true;
            }
            catch
            {
                // Corrupt, tampered, or not decryptable by this user - drop it.
                Clear();
                return false;
            }
        }

        public static void Clear()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch { /* best effort */ }
        }
    }

    /// <summary>Restores a remembered OpenDefinery session, if one was saved.</summary>
    public static class SessionManager
    {
        /// <summary>
        /// Return an authenticated session, silently restoring a remembered one if needed.
        /// Returns null if there's no valid saved session (stale credentials are NOT wiped, so a
        /// transient network hiccup at startup doesn't permanently forget the login).
        /// </summary>
        public static Definery TryRestore()
        {
            if (Definery.Current != null && !string.IsNullOrEmpty(Definery.Current.AuthCode))
            {
                return Definery.Current;
            }

            if (!SessionStore.TryLoad(out var username, out var password))
            {
                return null;
            }

            // Retry a few times: when Revit restores this session at startup, the network stack
            // isn't always ready, so the login POST can fail transiently. The credentials are
            // valid (they were saved from a successful sign-in), so a brief retry avoids a
            // spurious login prompt. Genuinely bad credentials still fall through to the prompt.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    var session = Definery.Init(new Definery(), username, password);
                    if (session != null && !string.IsNullOrEmpty(session.CsrfToken))
                    {
                        return session;
                    }
                }
                catch
                {
                    // Swallow and retry.
                }

                if (attempt < 2) System.Threading.Thread.Sleep(600);
            }

            return null;
        }
    }

    /// <summary>
    /// Small non-secret preferences shared across the add-ins (e.g. the last-used Collection), so a
    /// selection made in one window is reflected everywhere. Stored as plain text in
    /// %LocalAppData%\OpenDefinery\settings.dat - nothing sensitive lives here.
    /// </summary>
    public static class SharedSettings
    {
        private static string Dir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenDefinery");

        private static string LastCollectionPath => Path.Combine(Dir, "last-collection.dat");

        /// <summary>The Collection id last selected by the user, or null if none/invalid.</summary>
        public static int? LastCollectionId
        {
            get
            {
                try
                {
                    if (!File.Exists(LastCollectionPath)) return null;
                    var text = File.ReadAllText(LastCollectionPath).Trim();
                    return int.TryParse(text, out var id) ? (int?)id : null;
                }
                catch { return null; }
            }
            set
            {
                try
                {
                    Directory.CreateDirectory(Dir);
                    if (value.HasValue) File.WriteAllText(LastCollectionPath, value.Value.ToString());
                    else if (File.Exists(LastCollectionPath)) File.Delete(LastCollectionPath);
                }
                catch { /* best effort */ }
            }
        }
    }
}
