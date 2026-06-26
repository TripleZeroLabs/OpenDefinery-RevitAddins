using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;

namespace OpenDefinery
{
    public class Tag
    {
        public string Id { get; set; }
        public Guid Uuid { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// Creates a new Tag on OpenDefinery
        /// </summary>
        /// <param name="definery">The main Definery object provides the CSRF token</param>
        /// <param name="tagName">The name for the new Tag</param>
        /// <returns>The Tag ID of the newly created Tag as a string</returns>
        public static string Create(Definery definery, string tagName)
        {
            var body =
                "{\"vid\": \"tags\"," +
                "\"name\": [" +
                    "{\"value\": \"" + tagName + "\"}" +
                    "]}";

            var response = OdHttp.Post(Definery.BaseUrl + "taxonomy/term?_format=hal_json", body, definery);

            using (var doc = JsonDocument.Parse(response.Content))
            {
                var value = doc.RootElement.GetProperty("tid")[0].GetProperty("value");

                return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
            }
        }

        /// <summary>
        /// Retrieve the Tag ID from its name. Useful for passing into API calls where the ID is required.
        /// </summary>
        /// <param name="definery">The main Definery object provides the basic auth code</param>
        /// <param name="tagName">The name of the Tag</param>
        /// <returns>The Tag ID as a string</returns>
        public static string GetIdFromName(Definery definery, string tagName)
        {
            var response = OdHttp.Get(Definery.BaseUrl + string.Format("rest/tags/{0}?_format=json", tagName), definery);

            if (response.StatusCode == System.Net.HttpStatusCode.OK && response.Content != "[]")
            {
                var tags = OdJson.Deserialize<List<Tag>>(response.Content);

                return tags.FirstOrDefault().Id;
            }
            if (response.StatusCode == System.Net.HttpStatusCode.OK && response.Content == "[]")
            {
                return "[]";
            }
            else
            {
                Debug.WriteLine("There was an error getting the ID of " + tagName + ".");
                return null;
            }
        }

        /// <summary>
        /// Helper method to format tag names.
        /// </summary>
        /// <param name="tagName">The name of the Tag to format</param>
        /// <returns>The formatted Tag Name</returns>
        public static string FormatName(string tagName)
        {
            var newTag = tagName;

            // Remove spaces
            newTag = newTag.Replace(" ", "");

            return newTag;
        }
    }
}
