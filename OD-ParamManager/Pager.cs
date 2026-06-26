using OpenDefinery_DesktopApp;
using System.Text.Json.Serialization;

namespace OpenDefinery
{
    public class Pager
    {
        // Ignoring the current_page property from Drupal response until it reports the correct value
        public int CurrentPage { get; set; }

        [JsonPropertyName("total_items")]
        public int TotalItems { get; set; }

        [JsonPropertyName("total_pages")]
        public int TotalPages { get; set; }

        [JsonPropertyName("items_per_page")]
        public int ItemsPerPage { get; set; }

        // The Offset is not included in the Drupal response, so it must be set elsewhere
        public int Offset { get; set; }
        public bool IsFirstPage { get; set; }
        public bool IsLastPage { get; set; }

        /// <summary>
        /// Helper method to update the Pager object based on a response body.
        /// </summary>
        /// <param name="responseContent">The raw JSON response body as a string</param>
        /// <returns>The new Pager object</returns>
        public static Pager SetFromParamReponse(string responseContent, bool resetTotals)
        {
            // Instantiate a new pager and populate from the Drupal "pager" object
            var pager = new Pager();

            var pagerRaw = OdJson.GetPropertyRaw(responseContent, "pager");
            if (pagerRaw != null)
            {
                pager = OdJson.Deserialize<Pager>(pagerRaw);
            }

            // Always reassign values for total pages and items because the pager property from Drupal is relative to the current request,
            // however we always want to report the absolute totals if they are greater than zero.
            if (!resetTotals)
            {
                // Add the MainWindow data to the Pager object
                pager.TotalPages = MainWindow.Pager.TotalPages;
                pager.TotalItems = MainWindow.Pager.TotalItems;
                pager.CurrentPage = MainWindow.Pager.CurrentPage;
            }
            else
            {
                pager.CurrentPage = 0;
            }

            return pager;
        }

        public static Pager Reset()
        {
            var pager = new Pager();
            pager.TotalItems = 0;
            pager.TotalPages = 0;
            pager.CurrentPage = 0;
            pager.ItemsPerPage = MainWindow.Pager.ItemsPerPage;

            return pager;
        }
    }
}
