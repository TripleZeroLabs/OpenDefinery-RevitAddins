using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;

namespace OpenDefinery
{
    public class Collection
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("author")]
        public string Author { get; set; }

        [JsonPropertyName("public")]
        public bool IsPublic { get; set; }

        /// <summary>
        /// Retrieve Shared Parameters from a Collection using pagination
        /// </summary>
        public static ObservableCollection<DefineryParameter> GetParameters(
            Definery definery, Collection collection, int itemsPerPage, int offset, bool resetTotals)
        {
            var listOfParams = new List<DefineryParameter>();

            var response = OdHttp.Get(Definery.BaseUrl + string.Format(
                "rest/params/collection/{0}?_format=json&items_per_page={1}&offset={2}", collection.Id, itemsPerPage, offset), definery);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                if (OdJson.CountProperty(response.Content, "rows") == 0)
                {
                    Debug.WriteLine("This collection is empty.");
                }
                else
                {
                    listOfParams = OdJson.Deserialize<List<DefineryParameter>>(OdJson.GetPropertyRaw(response.Content, "rows"));
                }
            }
            else
            {
                Debug.WriteLine("There was an error getting the parameters.");
            }

            var parameters = new ObservableCollection<DefineryParameter>(listOfParams);

            return DefineryParameter.SetCollections(definery, parameters);
        }

        /// <summary>
        /// Retrieve all Shared Parameters from a Collection (no pagination)
        /// </summary>
        public static ObservableCollection<DefineryParameter> GetParameters(
            Definery definery, Collection collection)
        {
            var paramsOut = new ObservableCollection<DefineryParameter>();

            if (definery != null && collection != null)
            {
                var listOfParams = new List<DefineryParameter>();

                var response = OdHttp.Get(Definery.BaseUrl + string.Format(
                    "rest/params/collection/{0}/all?_format=json", collection.Id), definery);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    if (OdJson.CountProperty(response.Content, "rows") == 0)
                    {
                        Debug.WriteLine("This collection is empty.");
                    }
                    else
                    {
                        listOfParams = OdJson.Deserialize<List<DefineryParameter>>(OdJson.GetPropertyRaw(response.Content, "rows"));
                    }
                }
                else
                {
                    Debug.WriteLine("There was an error getting the parameters.");
                }

                var parameters = new ObservableCollection<DefineryParameter>(listOfParams);

                paramsOut = DefineryParameter.SetCollections(definery, parameters);
            }

            return paramsOut;
        }

        /// <summary>
        /// Retrieve the currently logged in user's Collections.
        /// </summary>
        public static List<Collection> ByCurrentUser(Definery definery)
        {
            // Check if the user is authenticated first since an anonymous user has no Collections
            var url = !string.IsNullOrEmpty(definery.AuthCode)
                ? Definery.BaseUrl + "rest/collections?_format=json"
                : Definery.BaseUrl + "rest/collections/published?_format=json";

            var response = OdHttp.Get(url, definery);

            try
            {
                return OdJson.Deserialize<List<Collection>>(response.Content);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());

                return null;
            }
        }

        /// <summary>
        /// Retrieve all published Collections including the current user's Collections.
        /// </summary>
        public static List<Collection> GetPublished(Definery definery)
        {
            var response = OdHttp.Get(Definery.BaseUrl + "rest/collections/published?_format=json", definery);

            // Return the data if the response was OK
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // If the user has no Collections, it returns an empty array
                if (response.Content != "[]")
                {
                    try
                    {
                        var collections = OdJson.Deserialize<List<Collection>>(response.Content);
                        var filteredCollections = new List<Collection>();

                        foreach (var collection in collections)
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
        /// Retrieve "lite" parameters (GUID + node id only) that belong to a Collection.
        /// Useful for duplicate checks during batch upload.
        /// </summary>
        public static List<DefineryParameter> GetIds(Definery definery, Collection collection)
        {
            var response = OdHttp.Get(
                Definery.BaseUrl + string.Format("rest/lite/collection/{0}?_format=json", collection.Id.ToString()),
                definery, useBearer: true);

            Debug.WriteLine(response.Content);

            return OdJson.Deserialize<List<DefineryParameter>>(response.Content);
        }

        /// <summary>
        /// Create a new Collection.
        /// </summary>
        public static Collection Create(Definery definery, string name, string description, bool? isPublic)
        {
            // Convert booleans to strings
            var publicString = (isPublic == false | isPublic == null) ? "0" : "1";

            var body =
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
                "}";

            var response = OdHttp.Post(Definery.BaseUrl + "node?_format=hal_json", body, definery);
            Debug.WriteLine(response.Content);

            // Deserialize the response to a generic Node first
            if (response.StatusCode.ToString() == "Created")
            {
                var genericNode = OdJson.Deserialize<Node>(response.Content);

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
        public static void Delete(Definery definery, int collectionId)
        {
            var body =
                "{\"type\": [" +
                "{\"target_id\": \"collection\"}" +
                "]}";

            var response = OdHttp.Delete(Definery.BaseUrl + string.Format("node/{0}?_format=hal_json", collectionId.ToString()), body, definery);
            Debug.WriteLine(response.Content);
        }

        /// <summary>
        /// Check that a Collection has duplicate GUIDs.
        /// </summary>
        public static bool HasDuplicateGuids(Collection collection, Guid guid)
        {
            var hasDuplicate = false;

            return hasDuplicate;
        }

        /// <summary>
        /// Retrieve a list of Collections from a comma separated values string (typically returned from the API).
        /// </summary>
        public static DefineryParameter GetFromString(Definery definery, DefineryParameter parameter, string collectionsString)
        {
            var collections = new List<Collection>();

            // Get multiple Collections
            if (!string.IsNullOrEmpty(collectionsString) && collectionsString.Contains(","))
            {
                var strings = collectionsString.Split(',');

                foreach (var s in strings)
                {
                    var foundCollections = definery.PublishedCollections.Where(o => o.Id.ToString() == s.Trim());

                    foreach (var foundCollection in foundCollections)
                    {
                        collections.Add(foundCollection);
                    }
                }
            }
            // Get a single Collection
            if (!string.IsNullOrEmpty(collectionsString) && !collectionsString.Contains(","))
            {
                var foundCollection = definery.PublishedCollections.Where(o => o.Id.ToString() == collectionsString.Trim()).FirstOrDefault();

                collections.Add(foundCollection);
            }

            parameter.Collections = collections;

            return parameter;
        }

        /// <summary>
        /// Retrieve minimal Shared Parameter data from OpenDefinery
        /// </summary>
        public static List<DefineryParameter> GetLiteParams(Definery definery, Collection collection)
        {
            var response = OdHttp.Get(
                Definery.BaseUrl + string.Format("rest/lite/collection/{0}?_format=json", collection.Id.ToString()),
                definery, useBearer: true);

            Debug.WriteLine(response.Content);

            try
            {
                return OdJson.Deserialize<List<DefineryParameter>>(response.Content);
            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());

                return null;
            }
        }

        /// <summary>
        /// Compares the shared parameters in the OpenDefinery Collection to the parameters extracted from current Revit model
        /// </summary>
        public static List<DefineryParameter> ValidateParameters(
            Definery definery,
            Collection collection,
            List<DefineryParameter> revitParams)
        {
            if (definery != null && collection != null)
            {
                var odParams = GetParameters(definery, collection).ToList();

                if (odParams != null)
                {
                    var validatedParams = new List<DefineryParameter>();

                    foreach (var p in revitParams)
                    {
                        var foundOdParams = odParams.Where(o => o.Guid == p.Guid);

                        if (foundOdParams.Count() == 1)
                        {
                            validatedParams.Add(DefineryParameter.SetDefineryData(foundOdParams.FirstOrDefault(), p));
                        }
                        else if (foundOdParams.Count() == 0)
                        {
                            p.IsStandard = false;

                            validatedParams.Add(p);
                        }
                        else if (foundOdParams.Count() > 1)
                        {
                            Debug.WriteLine(string.Format("Multiple parameters with GUID {0} found. Returning first for now.", p.Guid.ToString()));

                            validatedParams.Add(DefineryParameter.SetDefineryData(foundOdParams.FirstOrDefault(), p));
                        }
                    }

                    return validatedParams;
                }
                else
                {
                    Debug.Write("There was an error retrieving the shared parameters in the Collection.", "Error retrieving parameters.");

                    return null;
                }
            }
            else
            {
                Debug.Write("There was an error retrieving the Collection.", "Error retrieving collection.");

                return null;
            }
        }
    }
}
