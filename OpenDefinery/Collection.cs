using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OpenDefinery
{
    public class Collection
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        /// <summary>
        /// Retrieve Shared Parameters from a Collection using pagination
        /// </summary>
        /// <param name="definery">The main Definery object provides the basic auth code.</param>
        /// <param name="collection">The Collection to retrieve Shared Parameters from</param>
        /// <param name="itemsPerPage">The number of items to return per page. Acceptable values are 5, 10, 25, 50, 100.</param>
        /// <param name="offset">The offset of items to skip pages</param>
        /// <param name="resetTotals">Clear the total pages and items from the pager to start over?</param>
        /// <returns>A list of SharedParameter objects</returns>
        public static ObservableCollection<SharedParameter> GetParameters(
            Definery definery, Collection collection, int itemsPerPage, int offset, bool resetTotals)
        {
            var listOfParams = new List<SharedParameter>();

            var client = new RestClient(Definery.BaseUrl + string.Format(
                "rest/params/collection/{0}?_format=json&items_per_page={1}&offset={2}", collection.Id, itemsPerPage, offset)
                );
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            IRestResponse response = client.Execute(request);

            // Set the pager
            //MainWindow.Pager = Pager.SetFromParamReponse(response, resetTotals);

            // Logic if the response was OK
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // Get the SharedParameters as JToken
                JObject json = JObject.Parse(response.Content);
                var paramResponse = json.SelectToken("rows");

                if (paramResponse.Count() == 0)
                {
                    Debug.WriteLine("This collection is empty.");
                }
                else
                {
                    // Cast the rows from the reponse to a List of Shared Parameters
                    listOfParams = JsonConvert.DeserializeObject<List<SharedParameter>>(paramResponse.ToString());
                }
            }
            else
            {
                Debug.WriteLine("There was an error getting the parameters.");
            }

            var parameters = new ObservableCollection<SharedParameter>(listOfParams);

            // Set the Collections
            var updatedParams = SharedParameter.SetCollections(definery, parameters);

            return updatedParams;
        }

        /// <summary>
        /// Retrieve all Shared Parameters from a Collection (no pagination)
        /// </summary>
        /// <param name="definery">The main Definery object provides the basic auth code.</param>
        /// <param name="collection">The Collection to retrieve Shared Parameters from</param>
        /// <param name="itemsPerPage">The number of items to return per page. Acceptable values are 5, 10, 25, 50, 100.</param>
        /// <param name="offset">The offset of items to skip pages</param>
        /// <param name="resetTotals">Clear the total pages and items from the pager to start over?</param>
        /// <returns>A list of SharedParameter objects</returns>
        public static ObservableCollection<SharedParameter> GetParameters(
            Definery definery, Collection collection)
        {
            var listOfParams = new List<SharedParameter>();

            var client = new RestClient(Definery.BaseUrl + string.Format(
                "rest/params/collection/{0}?_format=json", collection.Id)
                );
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            IRestResponse response = client.Execute(request);

            // Set the pager
            //MainWindow.Pager = Pager.SetFromParamReponse(response, resetTotals);

            // Logic if the response was OK
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // Get the SharedParameters as JToken
                JObject json = JObject.Parse(response.Content);
                var paramResponse = json.SelectToken("rows");

                if (paramResponse.Count() == 0)
                {
                    Debug.WriteLine("This collection is empty.");
                }
                else
                {
                    // Cast the rows from the reponse to a List of Shared Parameters
                    listOfParams = JsonConvert.DeserializeObject<List<SharedParameter>>(paramResponse.ToString());
                }
            }
            else
            {
                Debug.WriteLine("There was an error getting the parameters.");
            }

            var parameters = new ObservableCollection<SharedParameter>(listOfParams);

            // Set the Collections
            var updatedParams = SharedParameter.SetCollections(definery, parameters);

            return updatedParams;
        }

        /// <summary>
        /// Retrieve the currently logged in user's Collections.
        /// </summary>
        /// <param name="definery">The main Definery object provides the CSRF token.</param>
        /// <returns>A list of Collection objects</returns>
        public static List<Collection> ByCurrentUser(Definery definery)
        {
            var client = new RestClient(Definery.BaseUrl + "rest/collections?_format=json");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            IRestResponse response = client.Execute(request);

            // Return the data if the response was OK
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // If the user has no Collections, it returns an empty array
                // Only process the response if it is not an empty array
                if (response.Content != "[]")
                {
                    try
                    {
                        var collections = JsonConvert.DeserializeObject<List<Collection>>(response.Content);

                        return collections;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());

                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            else
            {
                Debug.WriteLine(response.StatusCode.ToString());

                return null;
            }
        }

        /// <summary>
        /// Retrieve all published Collections including the current user's Collections.
        /// </summary>
        /// <param name="definery"></param>
        /// <returns></returns>
        public static List<Collection> GetAll(Definery definery)
        {
            var client = new RestClient(Definery.BaseUrl + "rest/collections/published?_format=json");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            IRestResponse response = client.Execute(request);

            // Return the data if the response was OK
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // If the user has no Collections, it returns an empty array
                // Only process the response if it is not an empty array
                if (response.Content != "[]")
                {
                    try
                    {
                        var collections = JsonConvert.DeserializeObject<List<Collection>>(response.Content);
                        var filteredCollections = new List<Collection>();

                        // Add Collection to filtered list only if it isn't authored by the current user
                        foreach(var collection in collections)
                        {
                            filteredCollections.Add(collection);
                        }

                        return filteredCollections;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());

                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            else
            {
                Debug.WriteLine(response.StatusCode.ToString());

                return null;
            }
        }

        /// <summary>
        /// Create a new Collction.
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <param name="name">The name of the Colllection</param>
        /// <param name="description">The description of the Collection</param>
        /// <returns></returns>
        public static Collection Create(Definery definery, string name, string description, bool? isPublic)
        {
            // Convert booleans to strings
            var publicString = string.Empty;
            
            if (isPublic == false | isPublic == null)
            {
                publicString = "0";
            }
            else
            {
                publicString = "1";
            }

            var client = new RestClient(Definery.BaseUrl + "node?_format=hal_json");
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("X-CSRF-Token", definery.CsrfToken);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            request.AddParameter("application/json", 
                "{" +
                    "\"type\":" +
                    "[{" +
                        "\"target_id\": \"collection\"" +
                    "}]," +
                    "\"title\":" +
                    "[{" +
                        "\"value\": \"" + name + "\"" +
                    "}]," +
                    "\"body\":" +
                    "[{" +
                        "\"value\": \"" + description + "\"" +
                    "}]," +
                    "\"field_public\":" +
                    "[{" +
                        "\"value\": \"" + publicString + "\"" +
                    "}]" +
                "}", 

                ParameterType.RequestBody);

            IRestResponse response = client.Execute(request);
            Debug.WriteLine(response.Content);

            // Deserialize the response to a generic Node first
            if (response.StatusCode.ToString() == "Created")
            {
                var genericNode = JsonConvert.DeserializeObject<Node>(response.Content);

                // Instantiate the collection
                var newCollection = new Collection();
                newCollection.Id = genericNode.Nid[0].Value;
                newCollection.Name = genericNode.Title[0].Value;
                newCollection.Author = definery.CurrentUser.Id.ToString();

                return newCollection;
            }
            else
            {
                Debug.WriteLine("There was an error creating the Collection.");

                return null;
            }
        }

        /// <summary>
        /// Delete a Collection
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <param name="collectionId">The ID of the Collection to delete</param>
        public static void Delete(Definery definery, int collectionId)
        {
            var client = new RestClient(Definery.BaseUrl + string.Format("node/{0}?_format=hal_json", collectionId.ToString()));
            var request = new RestRequest(Method.DELETE);
            request.AddHeader("X-CSRF-Token", definery.CsrfToken);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            request.AddParameter("application/json",
                "{\"type\": [" +
                "{\"target_id\": \"collection\"}" +
                "]}", 
                ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);
            Console.WriteLine(response.Content);
        }

        /// <summary>
        /// Check that a Collection has duplicate GUIDs.
        /// </summary>
        /// <param name="collection">The Collection to check</param>
        /// <param name="guid">The GUID to check for</param>
        /// <returns></returns>
        public static bool HasDuplicateGuids(Collection collection, Guid guid)
        {
            var hasDuplicate = false;

            

            return hasDuplicate;
        }

        /// <summary>
        /// Retrieve a list of Collections from a comma separated values string (typically returned from the API).
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <param name="collectionsString">A comma separated values string of Collection IDs</param>
        /// <returns></returns>
        public static SharedParameter GetFromString(Definery definery, SharedParameter parameter, string collectionsString)
        {
            var collections = new List<Collection>();

            // Get multiple Collections
            if (!string.IsNullOrEmpty(collectionsString) && collectionsString.Contains(","))
            {
                var strings = collectionsString.Split(',');

                foreach (var s in strings)
                {
                    // Get Collection from ID
                    var foundCollections = definery.AllCollections.Where(o => o.Id.ToString() == s.Trim());

                    foreach (var foundCollection in foundCollections)
                    {
                        // Add Collection to list
                        collections.Add(foundCollection);
                    }
                }
            }
            // Get a single Collection
            if (!string.IsNullOrEmpty(collectionsString) && !collectionsString.Contains(","))
            {
                // Get Collection from ID
                var foundCollection = definery.AllCollections.Where(o => o.Id.ToString() == collectionsString.Trim()).FirstOrDefault();

                // Add Collection to list
                collections.Add(foundCollection);
            }
            
            // Set the new list to the SharedParameter property and return
            parameter.Collections = collections;

            return parameter;
        }

        /// <summary>
        /// Retrieve minimal for Shared Parameter data from OpenDefinery
        /// </summary>
        /// <param name="definery"></param>
        /// <param name="collection"></param>
        /// <returns></returns>
        public static List<SharedParameter> GetLiteParams(Definery definery, Collection collection)
        {
            // Make the API call
            var client = new RestClient(Definery.BaseUrl + string.Format("rest/lite/collection/{0}?_format=json", collection.Id.ToString()));
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", "Bearer " + definery.AuthCode);
            IRestResponse response = client.Execute(request);
            Debug.WriteLine(response.Content);

            try
            {
                var parameters = JsonConvert.DeserializeObject<List<SharedParameter>>(response.Content);

                return parameters;
            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());

                return null;
            }

        }
    }
}
