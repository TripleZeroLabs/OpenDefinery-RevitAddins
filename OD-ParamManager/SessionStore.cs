using OpenDefinery;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Path = System.IO.Path;

namespace OD_ParamManager
{
    /// <summary>
    /// Opt-in "Remember me" credential store. The username + password are encrypted with
    /// Windows DPAPI (CurrentUser scope) and written to %LocalAppData%\OpenDefinery\session.dat.
    ///
    /// DPAPI ties decryption to this Windows user on this machine, so a copied file is useless
    /// elsewhere. It does NOT protect against code running as the same user - the standard
    /// "remember me" tradeoff. This is an interim measure; when OAuth lands, a refresh token
    /// replaces the stored password here.
    /// </summary>
    internal static class SessionStore
    {
        // Non-secret app entropy (the source is open, so this only scopes the blob to this app).
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

    /// <summary>Restores a remembered OpenDefinery session at startup, if one was saved.</summary>
    internal static class SessionManager
    {
        /// <summary>
        /// Return an authenticated session, silently restoring a remembered one if needed.
        /// Returns null if there's no valid saved session (stale credentials are wiped).
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
                // Network error or rejected credentials fall through to the wipe below.
            }

            // Stale/invalid - don't keep retrying on every launch.
            SessionStore.Clear();
            return null;
        }
    }
}
