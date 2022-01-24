﻿using KellermanSoftware.CompareNetObjects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Windows;

namespace OpenDefinery
{
    public class SharedParameter
    {
        [JsonProperty("id")]
        public int DefineryId { get; set; }

        [JsonProperty("guid")]
        public Guid Guid { get; set; }
        public int ElementId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("data_type")]
        public string DataType { get; set; }

        [JsonProperty("data_category")]
        public string DataCategoryHashcode { get; set; }  // TODO: Instantiate the hashcode as a Data Category object

        [JsonProperty("group")]
        public string Group { get; set; }

        [JsonProperty("user_modifiable")]
        public string UserModifiable { get; set; }

        [JsonProperty("visible")]
        public string Visible { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("collections")]
        public string CollectionsString { get; set; }

        public int ForkedSourceId { get; set; }
        public List<Collection> Collections { get; set; }
        public string BatchId { get; set; }
        public bool IsStandard { get; set; }

        // Standard constructor
        public SharedParameter(
            Guid guid,
            string name,
            string dataTypeName,
            string dataCatHashcode,
            string groupId,
            string isVisible,
            string description,
            string isUserModifiable)
        {
            Guid = guid;
            Name = name;
            DataType = dataTypeName;
            DataCategoryHashcode = dataCatHashcode;
            Group = groupId;
            Visible = isVisible;
            Description = description;
            UserModifiable = isUserModifiable;
        }

        // Lite constructor
        [JsonConstructor]
        public SharedParameter(Guid guid, int id)
        {
            Guid = guid;
            DefineryId = id;
        }

        /// <summary>
        /// Create a SharedParameter object from a line in a shared parameter text file (typically generated by Revit).
        /// </summary>
        /// <param name="txtLine">The line of text from the shared parmater text file.</param>
        /// <returns></returns>
        public static SharedParameter FromTxt(Definery definery, string txtLine)
        {
            if (txtLine[0] != '#')  // Ignore the comment lines
            {
                var values = txtLine.Split('\t');

                var parameter = new SharedParameter(
                    new Guid(values[1]),
                    values[2],
                    values[3],
                    values[4],
                    values[5],
                    values[6],
                    string.Empty,
                    "1"
                    );

                // Older shared parameter text files do not have the DESCRIPTION column
                if (values.Count() == 8)
                {
                    parameter.Description = string.Empty;
                    parameter.UserModifiable = values[7];
                }
                if (values.Count() == 9)
                {
                    parameter.Description = values[7];
                    parameter.UserModifiable = values[8];
                }

                return parameter;
            }

            return null;
        }

        /// <summary>
        /// Retrieve SharedParameter objects which have a specific GUID.
        /// </summary>
        /// <param name="definery">The main Definery object provides the basic authorization code</param>
        /// <param name="guid">The GUID of the SharedParameters to retrieve</param>
        /// <returns>A list of SharedParameter objects</returns>
        public static ObservableCollection<SharedParameter> FromGuid(Definery definery, Guid guid)
        {
            var client = new RestClient(Definery.BaseUrl + string.Format("rest/params/guid/{0}?_format=json", guid.ToString()));
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            IRestResponse response = client.Execute(request);

            JObject json = JObject.Parse(response.Content);

            var paramResponse = json.SelectToken("rows");

            if (paramResponse != null)
            {
                var parameters = JsonConvert.DeserializeObject<List<SharedParameter>>(paramResponse.ToString());

                return new ObservableCollection<SharedParameter>(parameters);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Checks that an exact match of a SharedParameter already exists in OpenDefinery. Useful for mitigating duplicates.
        /// </summary>
        /// <param name="definery">The main Definery object provides the basic auth token</param>
        /// <param name="newParameter">The SharedParameter to validate</param>
        /// <returns>True if a match is found, false if not</returns>
        public static bool HasExactMatch(Definery definery, SharedParameter newParameter)
        {
            var foundMatch = false;

            // Retrieve all Parameters from the GUID
            var foundParams = FromGuid(definery, newParameter.Guid);

            // Logic when one ore more SharedParameter is found in OpenDefinery
            if (foundParams != null && foundParams.Count() > 1)
            {
                foreach (var p in foundParams)
                {
                    // Only consider exact match if the current user is the author
                    if (p.Author == definery.CurrentUser.Id)
                    {
                        // Compare the two parameters
                        CompareLogic compareLogic = new CompareLogic();

                        compareLogic.Config.MembersToInclude.Add("Guid");
                        compareLogic.Config.MembersToInclude.Add("Name");
                        compareLogic.Config.MembersToInclude.Add("DataType");
                        compareLogic.Config.MembersToInclude.Add("DataCategory");
                        compareLogic.Config.MembersToInclude.Add("Visible");
                        compareLogic.Config.MembersToInclude.Add("Description");
                        compareLogic.Config.MembersToInclude.Add("UserModifiable");

                        ComparisonResult result = compareLogic.Compare(newParameter, p);

                        if (result.AreEqual)
                        {
                            // Break the loop if there is any Parameter that is equal
                            foundMatch = true;

                            break;
                        }
                        else
                        {
                            foundMatch = false;
                        }
                    }
                    else
                    {
                        foundMatch = false;
                    }
                }
            }
            if (foundParams != null && foundParams.Count() == 0)
            {
                foundMatch = false;
            }

            return foundMatch;
        }

        /// <summary>
        /// Retrieve a page of ShareParameters from OpenDefinery.
        /// </summary>
        /// <param name="definery">The main Definery object provides the basic auth code.</param>
        /// <param name="itemsPerPage">The number of items per page (only increments of 5, 10, 25, 50, and 100 are allowed)</param>
        /// <param name="offset">The offset of items from zero (i.e., to start page two at 50 items per page, this should be set to 50).</param>
        /// <param name="resetTotals">Clear the total pages and items from the pager to start over?</param>
        /// <returns>A list of SharedParameters</returns>
        public static ObservableCollection<SharedParameter> GetPage(Definery definery, int itemsPerPage, int offset, bool resetTotals)
        {
            var client = new RestClient(Definery.BaseUrl + 
                string.Format("rest/params?_format=json&items_per_page={0}&offset={1}", itemsPerPage, offset));
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            IRestResponse response = client.Execute(request);

            // Return the CSRF token if the response was OK
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {

                // Set the Pager object on the Main Window
                //MainWindow.Pager = Pager.SetFromParamReponse(response, resetTotals);
                //MainWindow.Pager.ItemsPerPage = MainWindow.Pager.ItemsPerPage;

                // Get the SharedParameters as JToken
                JObject json = JObject.Parse(response.Content);
                var paramResponse = json.SelectToken("rows");

                var parameters = JsonConvert.DeserializeObject<List<SharedParameter>>(paramResponse.ToString());

                return new ObservableCollection<SharedParameter>(parameters);
            }
            else
            {
                Debug.WriteLine("There was an error getting the parameters.");

                return null;
            }
        }

        /// <summary>
        /// Retrieve all parameters from a specific User on OpenDefinery.
        /// </summary>
        /// <param name="definery">The main Definery object provides the basic auth token</param>
        /// <param name="userName">The OpenDefinery username of the user to check for</param>
        /// <param name="itemsPerPage">The number of items to return per page. Acceptable values are 5, 10, 25, 50, 100.</param>
        /// <param name="offset">The offset of items to skip pages</param>
        /// <param name="resetTotals">Clear the total pages and items from the pager to start over?</param>
        /// <returns>A list of SharedParameter objects</returns>
        public static ObservableCollection<SharedParameter> ByUser(Definery definery, string userName, int itemsPerPage, int offset, bool resetTotals)
        {
            var client = new RestClient(Definery.BaseUrl + string.Format(
                "rest/params/user/{0}?_format=json&items_per_page={1}&offset={2}", userName, itemsPerPage, offset)
                );
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            IRestResponse response = client.Execute(request);

            // Logic if the response was OK
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // Set the Pager object on the Main Window
                //MainWindow.Pager = Pager.SetFromParamReponse(response, resetTotals);

                // Get the SharedParameters as JToken
                JObject json = JObject.Parse(response.Content);
                var paramResponse = json.SelectToken("rows");

                var parameters = JsonConvert.DeserializeObject<List<SharedParameter>>(paramResponse.ToString());

                return new ObservableCollection<SharedParameter>(parameters);
            }
            else
            {
                Debug.WriteLine("There was an error getting the parameters.");

                return null;
            }
        }

        /// <summary>
        /// Set the Collections on a list of SharedParameters.
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <param name="parameters">The list of SharedParameters to process</param>
        /// <returns></returns>
        public static ObservableCollection<SharedParameter> SetCollections(Definery definery, ObservableCollection<SharedParameter> parameters)
        {
            // Set the Collections
            var updatedParams = new ObservableCollection<SharedParameter>();

            foreach (var p in parameters)
            {
                var newParam = Collection.GetFromString(definery, p, p.CollectionsString);

                updatedParams.Add(newParam);
            }

            return updatedParams;
        }

        /// <summary>
        /// Retrieve the Shared Parameters that don't belong to any Collections
        /// </summary>
        /// <param name="definery"></param>
        /// <param name="collection"></param>
        /// <returns></returns>
        public static ObservableCollection<SharedParameter> GetOrphaned(
            Definery definery, int itemsPerPage, int offset, bool resetTotals)
        {
            var listOfParams = new List<SharedParameter>();

            var client = new RestClient(Definery.BaseUrl + string.Format(
                "rest/params/orphaned?_format=json&items_per_page={0}&offset={1}", itemsPerPage, offset)
                );
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            IRestResponse response = client.Execute(request);

            // Set the pager
            //MainWindow.Pager = Pager.SetFromParamReponse(response, resetTotals);
            //MainWindow.Pager.ItemsPerPage = MainWindow.Pager.ItemsPerPage;

            // Logic if the response was OK
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // Get the SharedParameters as JToken
                JObject json = JObject.Parse(response.Content);
                var paramResponse = json.SelectToken("rows");

                if (paramResponse.Count() == 0)
                {
                    Debug.WriteLine("There are no orphaned Shared Parameters.");
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
            var updatedParams = SetCollections(definery, parameters);

            return updatedParams;
        }

        /// <summary>
        /// Search for Shared Parmeters by keyword, GUID, or data type in a single query.
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <param name="searchQuery">The term(s) to search for</param>
        /// <returns></returns>
        public static ObservableCollection<SharedParameter> Search(Definery definery, string searchQuery, int itemsPerPage, int offset, bool resetTotals)
        {
            var listOfParams = new List<SharedParameter>();

            var client = new RestClient(Definery.BaseUrl + 
                string.Format("rest/params/search?_format=json&keys={0}&items_per_page={1}&offset={2}", searchQuery, itemsPerPage, offset));
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            IRestResponse response = client.Execute(request);

            // Set the pager
            //MainWindow.Pager = Pager.SetFromParamReponse(response, resetTotals);
            //MainWindow.Pager.ItemsPerPage = MainWindow.Pager.ItemsPerPage;

            // Logic if the response was OK
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // Get the SharedParameters as JToken
                JObject json = JObject.Parse(response.Content);
                var paramResponse = json.SelectToken("rows");

                if (paramResponse.Count() == 0)
                {
                    Debug.WriteLine("There were no Shared Parameters found that match that search query.");
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
            var updatedParams = SetCollections(definery, parameters);

            return updatedParams;
        }

        /// <summary>
        /// Search for Shared Parmeters by keyword or GUID and filter by Data Type
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <param name="searchQuery">The term(s) to search for</param>
        /// <param name="dataTypeName">The name of the Data Type to filter</param>
        /// <returns></returns>
        public static ObservableCollection<SharedParameter> Search(
            Definery definery, 
            string searchQuery, 
            string dataTypeName, 
            int itemsPerPage, 
            int offset, 
            bool resetTotals
            )
        {
            var listOfParams = new List<SharedParameter>();

            var client = new RestClient(Definery.BaseUrl +
                string.Format("rest/params/search?_format=json&keys={0}&data_type={1}&items_per_page={2}&offset={3}", searchQuery, dataTypeName, itemsPerPage, offset));
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            IRestResponse response = client.Execute(request);

            // Set the pager
            //MainWindow.Pager = Pager.SetFromParamReponse(response, resetTotals);
            //MainWindow.Pager.ItemsPerPage = MainWindow.Pager.ItemsPerPage;

            // Logic if the response was OK
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // Get the SharedParameters as JToken
                JObject json = JObject.Parse(response.Content);
                var paramResponse = json.SelectToken("rows");

                if (paramResponse.Count() == 0)
                {
                    Debug.WriteLine("There were no Shared Parameters found that match that search query.");
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
            var updatedParams = SetCollections(definery, parameters);

            return updatedParams;
        }

        /// <summary>
        /// Creates a new Shared Parameter on OpenDefinery
        /// Response codes:
        ///     201: Created
        ///     422: Unprocessable entity (possibly missing a required field)
        /// </summary>
        /// <param name="definery"></param>
        /// <param name="param"></param>
        /// <param name="collectionId"></param>
        /// <returns></returns>
        public static SharedParameter Create(Definery definery, SharedParameter param, int? collectionId = null, int? forkedId = null)
        {
            var client = new RestClient(Definery.BaseUrl + "node?_format=json");
            client.Timeout = -1;

            // Assign the datatype value by the Term ID defined by OpenDefinery to pass to the API call (we cannot pass the name)
            var dataType = definery.DataTypes.Find(d => d.Name.ToString() == param.DataType);
            var dataCategory = new DataCategory();

            // Format values before assigning
            if (dataType != null)
            {
                param.DataType = dataType.Id.ToString();
            }

            if (!string.IsNullOrEmpty(param.DataCategoryHashcode))
            {
                dataCategory = DataCategory.GetByHashcode(definery, param.DataCategoryHashcode);
            }

            // Get the tag ID from OpenDefinery. If the tag does not exist, create a tag and assign the ID to request body
            // First format the tag name accordingly...
            var tagName = string.Empty;
            var tagId = string.Empty;

            if (!string.IsNullOrEmpty(param.Group))
            {
                tagName = Tag.FormatName(param.Group);

                // ... Then attempt to retrieve the tag from OpenDefinery
                tagId = Tag.GetIdFromName(definery, tagName);

                // ... If the tag does not exist, an empty array is returned. Create the tag if neccessary.
                if (tagId == "[]")
                {
                    Debug.WriteLine(string.Format("The tag \"{0}\" does not exist. Creating...", tagName));

                    // Create the tag
                    var newTagId = Tag.Create(definery, tagName);
                    Debug.WriteLine(string.Format("New tag created for {0} with ID: {1}", tagName, newTagId));

                    // Get the ID of the newly created tag
                    tagId = newTagId;
                }
            }
            // If the there is not a value for the Group, assign the Default Group
            else
            {
                tagId = null;
            }

            // Add default values
            if (param.UserModifiable == null)
            {
                param.UserModifiable = "1";
            }
            if (param.Visible == null)
            {
                param.Visible = "1";
            }

            //TODO: Clean up this mess some day.
            var requestBody = "{" +
                "\"type\": [{" +
                    "\"target_id\": \"shared_parameter\"" +
                "}]," +
                "\"title\": [{" +
                    "\"value\": \"" + param.Name + "\"" +
                "}]," +
                "\"field_guid\": [{" +
                    "\"value\": \"" + param.Guid.ToString() + "\"" +
                "}]," +
                "\"field_description\": [{" +
                    "\"value\": \"" + param.Description + "\"" +
                "}]," +
                 "\"field_batch_id\": [{" +
                    "\"value\": \"" + param.BatchId + "\"" +
                "}]," +
                // OpenDefinery ignores the out-of-the-box Revit parameter groups as they are not as robust as Collections
                "\"field_group\": {" +
                    "\"und\": \"41\"" +
                "}," +
                "\"field_data_type\": {" +
                "\"und\": \"" + param.DataType + "\"" +
                "}," +
                "\"field_data_category\": {" +
                "\"und\": \"" + dataCategory.Id + "\"" +
                "}," +
                //"\"field_collections\": {" +
                //"\"und\": \"" + collectionId + "\"" +
                //"}," +
                "\"field_visible\": {" +
                "\"und\": \"" + param.Visible + "\"" +
                "}," +
                "\"field_user_modifiable\": {" +
                "\"und\": \"" + param.UserModifiable + "\"" +
                "}";

            if (collectionId != null)
            {
                requestBody += "," +
                    "\"field_collections\": {" +
                "\"und\": \"" + collectionId + "\"" +
                "}";
            }

            if (!string.IsNullOrEmpty(forkedId.ToString()))
            {
                requestBody += ",\"field_forked_source\": {" +
                "\"und\": \"" + forkedId.ToString() + "\"" +
                "}";
            }

            // Here we pass the existing parameter "group" from the text file and assign this as a tag instead of the group to maintain the data point
            // This property is only added to the request if the tagId is not null.
            if (!string.IsNullOrEmpty(tagId))
            {
                requestBody += ",\"field_tags\": {" +
                    "\"und\": \"" + tagId + "\"" +
                    "}";
            }

            requestBody += "}";

            var request = new RestRequest(Method.POST);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            request.AddHeader("X-CSRF-Token", definery.CsrfToken);
            request.AddHeader("Content-Type", "application/json");
            request.AddParameter("application/json", requestBody, ParameterType.RequestBody);

            IRestResponse response = client.Execute(request);

            Debug.WriteLine(response);

            // Get Shared Parameter if successful
            if (response.StatusCode.ToString() == "Created")
            {
                // Instantiate a generic node from the response first
                var node = JsonConvert.DeserializeObject<Node>(response.Content);

                var newParam = FromId(definery, node.Nid[0].Value);
                
                return newParam;
            }
            else
            {
                Debug.WriteLine("There was an error creating the Shared Parameter.\n\n" + response.Content);

               return null;
            }
        }

        /// <summary>
        /// Retrieve a specific Shared Parameter by its unique ID
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <param name="nodeId">The node ID of the Shared Parameter (this is not the GUID)</param>
        /// <returns></returns>
        public static SharedParameter FromId(Definery definery, int nodeId)
        {
            var client = new RestClient(Definery.BaseUrl + string.Format("rest/params/id/{0}?_format=json", nodeId.ToString()));
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            IRestResponse response = client.Execute(request);

            if (response.StatusCode.ToString() == "OK")
            {
                var parameters = JsonConvert.DeserializeObject<List<SharedParameter>>(response.Content);

                return parameters[0];
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Add a Shared Parameter to a Collection.
        /// </summary>
        /// <param name="definery">The main Definery object provides the CSRF token</param>
        /// <param name="param">The SharedParameter object to add</param>
        /// <param name="collection">The Collection object to add the Shared Parameter to</param>
        public static IRestResponse AddToCollection(Definery definery, SharedParameter param, int newCollectionId)
        {
            // Instantiate a list of Collection IDs as strings
            var collectionIds = new List<string>();

            // Add any Collections that already exist
            if (!string.IsNullOrEmpty(param.CollectionsString))
            {
                collectionIds = param.CollectionsString.Split(',').ToList();
            }

            // Add the new Collection to the list to be added
            if (!string.IsNullOrEmpty(param.CollectionsString))
            {
                // Only add the parameter to the Collection if it isn't already in it
                if (!param.CollectionsString.Contains(newCollectionId.ToString()))
                {
                    collectionIds.Add(newCollectionId.ToString());
                }
                else
                {
                    Debug.WriteLine(param.Name + " already belongs to " + newCollectionId.ToString());
                }
            }
            else
            {
                param.CollectionsString = string.Empty;
                collectionIds.Add(newCollectionId.ToString());
            }
            // Instantiate a string to pass Collections to body of API call
            var bodyFieldCollections = ", \"field_collections\": [";
            foreach (var id in collectionIds)
            {
                // Remove leading spaces if they exist from the string split
                if (id.First() == ' ')
                {
                    id.Remove(0);
                }

                // Add the target to the string which will eventually pass to the API call
                bodyFieldCollections += "{\"target_id\":" + id + ", \"target_type\": \"node\"},";
            }

            // Remove trailing comma
            if (bodyFieldCollections.Last() == ',')
            {
                bodyFieldCollections = bodyFieldCollections.Remove(bodyFieldCollections.Length - 1, 1);
            }

            // Add trailing bracket
            bodyFieldCollections += "]";

            var client = new RestClient(string.Format(Definery.BaseUrl + "node/{0}?_format=json", param.DefineryId));
            client.Timeout = -1;
            var request = new RestRequest(Method.PATCH);
            request.AddHeader("X-CSRF-Token", definery.CsrfToken);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            request.AddParameter("application/json", "{" +
                "\"type\": [{" +
                        "\"target_id\": \"shared_parameter\"" +
                    "}]" +
                    bodyFieldCollections +
                "}", 
                ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);

            Debug.WriteLine(response.Content);

            return response;
        }

        /// <summary>
        /// Removes a Shared Parameter from a Collection.
        /// </summary>
        /// <param name="definery">The main Definery object provides the CSRF token</param>
        /// <param name="param">The SharedParameter object to add</param>
        /// <param name="collection">The Collection object to remove the Shared Parameter from</param>
        public static bool RemoveCollection(Definery definery, SharedParameter param, int removedCollectionId)
        {
            // Instantiate a list of Collection IDs as strings
            var collectionIds = new List<string>();

            // Add any Collections that already exist
            if (!string.IsNullOrEmpty(param.CollectionsString))
            {
                collectionIds = param.CollectionsString.Split(',').ToList();
            }

            // Clean up strings
            for (int i = 0; i < collectionIds.Count; i++)
            {
                collectionIds[i] = collectionIds[i].Replace(" ", "");
            }

            // Remove the new Collection frome the list to be removed
            collectionIds.Remove(removedCollectionId.ToString());

            // Instantiate a string to pass Collections to body of API call
            var field_collections = ", \"field_collections\": [";
            foreach (var id in collectionIds)
            {
                // Remove leading spaces if they exist from the string split
                if (id.First() == ' ')
                {
                    id.Remove(0);
                }

                // Add the target to the string which will eventually pass to the API call
                field_collections += "{\"target_id\":" + id + ", \"target_type\": \"node\"},";
            }

            // Remove trailing comma
            if (field_collections.Last() == ',')
            {
                field_collections = field_collections.Remove(field_collections.Length - 1, 1);
            }

            // Add trailing bracket
            field_collections += "]";

            var client = new RestClient(string.Format(Definery.BaseUrl + "node/{0}?_format=json", param.DefineryId));
            client.Timeout = -1;
            var request = new RestRequest(Method.PATCH);
            request.AddHeader("X-CSRF-Token", definery.CsrfToken);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            request.AddParameter("application/json", "{" +
                "\"type\": [{" +
                        "\"target_id\": \"shared_parameter\"" +
                    "}]" +
                    field_collections +
                "}",
                ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);

            Debug.WriteLine(response.Content);

            // Remove the selected DataGrid Items if the reponse was OK
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Modify an existing Shared Parameter, but only allow modifications to the name and description for now.
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <param name="param">The existing Shared Parameter to modify</param>
        /// <param name="name">The updated Parameter name</param>
        /// <param name="description">The updated description</param>
        /// <returns></returns>
        public static void Modify(Definery definery, SharedParameter param, string name, string description)
        {
            var client = new RestClient(string.Format(Definery.BaseUrl + "node/{0}?_format=json", param.DefineryId));
            var request = new RestRequest(Method.PATCH);
            request.AddHeader("X-CSRF-Token", definery.CsrfToken);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            request.AddParameter("application/json", "{\"type\": [{" +
                "\"target_id\": \"shared_parameter\"}]," +
                "\"title\": {\"value\": \"" + name + "\"}," +
                "\"field_description\": {\"value\": \"" + description + "\"}}", 
                ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);

            Debug.WriteLine(response.Content);
        }

        /// <summary>
        /// Generate a tab delimited string of shared parameters (including header)
        /// </summary>
        /// <param name="paramList">The list of parameters to convert</param>
        /// <returns></returns>
        public static string CreateParamTable(List<SharedParameter> paramList)
        {
            // Instatiate string with header row of TSV
            var output = "*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\n";

            // Add a line of text for each SharedParameter from the list
            foreach (var p in paramList)
            {
                output += "PARAM\t";
                output += p.Guid + "\t";
                output += p.Name + "\t";
                output += p.DataType + "\t";
                output += p.DataCategoryHashcode + "\t";
                //output += p.Group + "\t";
                output += "1\t";  // Assign the "Default Group" until more robust group system is in place
                output += p.Visible + "\t";
                output += p.Description + "\t";
                output += p.UserModifiable + "\t";

                // Finally, add a new line
                output += '\n';
            }

            return output;
        }
    }
}
