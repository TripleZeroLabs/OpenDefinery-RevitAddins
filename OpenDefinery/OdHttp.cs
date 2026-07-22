using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace OpenDefinery
{
    /// <summary>
    /// Result of an HTTP call. Mirrors the members the old RestSharp IRestResponse exposed
    /// (<see cref="StatusCode"/> / <see cref="Content"/>) so call sites stay stable.
    /// </summary>
    public sealed class OdResponse
    {
        public HttpStatusCode StatusCode { get; set; }
        public string Content { get; set; }
        public bool IsSuccessStatusCode { get; set; }
    }

    /// <summary>
    /// Minimal HTTP transport built on a single shared <see cref="HttpClient"/>, replacing
    /// RestSharp (a known DLL-conflict source inside Revit/Dynamo). All requests flow through
    /// here, so authentication, headers, and (future) API versioning are configured in ONE
    /// place — when OpenDefinery API v2 lands, point <see cref="Definery.BaseUrl"/>/auth here
    /// rather than editing every call site.
    /// </summary>
    public static class OdHttp
    {
        // One HttpClient for the process lifetime (avoids socket exhaustion).
        private static HttpClient _client = CreateClient();

        private static HttpClient CreateClient()
        {
            // Explicit cookie container so the session can be reset (see ResetSession).
            return new HttpClient(new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer()
            });
        }

        /// <summary>
        /// Drop the current session (cookies) and start a fresh client.
        ///
        /// Drupal's /user/login route only accepts ANONYMOUS requests. Because the
        /// HttpClient lives for the whole Revit process, a session cookie from a previous
        /// sign-in survives closing the add-in window, and the next login attempt fails with
        /// "This route can only be accessed by anonymous users." Call this immediately before
        /// authenticating so the login request is always anonymous.
        /// </summary>
        public static void ResetSession()
        {
            // Not disposing the previous client: any in-flight request keeps using it and
            // the connection pool is reclaimed by the GC. Login happens rarely.
            _client = CreateClient();
        }

        public static OdResponse Get(string url, Definery definery, bool useBearer = false)
            => Send(HttpMethod.Get, url, definery, null, useBearer);

        public static OdResponse Post(string url, string jsonBody, Definery definery)
            => Send(HttpMethod.Post, url, definery, jsonBody, false);

        public static OdResponse Patch(string url, string jsonBody, Definery definery)
            => Send(new HttpMethod("PATCH"), url, definery, jsonBody, false);

        public static OdResponse Delete(string url, Definery definery)
            => Send(HttpMethod.Delete, url, definery, null, false);

        public static OdResponse Delete(string url, string jsonBody, Definery definery)
            => Send(HttpMethod.Delete, url, definery, jsonBody, false);

        public static OdResponse Send(HttpMethod method, string url, Definery definery, string jsonBody, bool useBearer)
            => SendAsync(method, url, definery, jsonBody, useBearer).GetAwaiter().GetResult();

        public static async Task<OdResponse> PostAsync(string url, string jsonBody, Definery definery)
            => await SendAsync(HttpMethod.Post, url, definery, jsonBody, false).ConfigureAwait(false);

        public static async Task<OdResponse> SendAsync(HttpMethod method, string url, Definery definery, string jsonBody, bool useBearer)
        {
            using (var request = new HttpRequestMessage(method, url))
            {
                ApplyAuth(request, definery, useBearer);

                if (jsonBody != null)
                {
                    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                }

                using (var response = await _client.SendAsync(request).ConfigureAwait(false))
                {
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    return new OdResponse
                    {
                        StatusCode = response.StatusCode,
                        Content = content,
                        IsSuccessStatusCode = response.IsSuccessStatusCode
                    };
                }
            }
        }

        /// <summary>
        /// Central place to attach credentials. Basic auth (base64 user:pass) + CSRF token mirror
        /// the v1 Drupal REST scheme; swap to bearer/OAuth here for API v2 without touching callers.
        /// </summary>
        private static void ApplyAuth(HttpRequestMessage request, Definery definery, bool useBearer)
        {
            if (definery == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(definery.AuthCode))
            {
                var scheme = useBearer ? "Bearer " : "Basic ";
                request.Headers.TryAddWithoutValidation("Authorization", scheme + definery.AuthCode);
            }

            if (!string.IsNullOrEmpty(definery.CsrfToken))
            {
                request.Headers.TryAddWithoutValidation("X-CSRF-Token", definery.CsrfToken);
            }
        }
    }
}
