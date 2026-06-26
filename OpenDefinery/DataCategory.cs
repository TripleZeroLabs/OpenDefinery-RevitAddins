using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OpenDefinery
{
    public class DataCategory
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("hashcode")]
        public string Hashcode { get; set; }

        /// <summary>
        /// Retrieve all DataCategoreis from OpenDefinery.
        /// </summary>
        /// <param name="definery">The main Definery object provides the CSRF token.</param>
        /// <returns>A list of DataType objects.</returns>
        public static List<DataCategory> GetAll(Definery definery)
        {
            var response = OdHttp.Get(Definery.BaseUrl + "rest/datacategories?_format=json", definery);

            return OdJson.Deserialize<List<DataCategory>>(response.Content);
        }

        /// <summary>
        /// Retrieve a DataCategory using its hascode from the Revit API.
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <param name="hashcode">The hascode provided by the Revit API</param>
        /// <returns></returns>
        public static DataCategory GetByHashcode(Definery definery, string hashcode)
        {
            // Get DataCategory using the hashcode
            var dataCats = definery.DataCategories.Where(o => o.Hashcode == hashcode);

            // Only return one DataCategory
            if (dataCats.Count() == 1)
            {
                return dataCats.FirstOrDefault();
            }
            else
            {
                Debug.WriteLine("Error retrieving Data Categories (duplicate hashcodes).");

                return null;
            }
        }
    }
}
