using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;

namespace OpenDefinery
{
    public class Definery
    {
        public static string BaseUrl = "http://app.opendefinery.com/";
        public string CsrfToken { get; set; }
        public string AuthCode { get; set; }
        public List<Collection> MyCollections { get; set; }
        public List<Collection> PublishedCollections { get; set; }
        public Collection SelectedCollection { get; set; }
        public List<SharedParameter> DefineryParameters { get; set; }
        public List<SharedParameter> RevitParameters { get; set; }
        public List<SharedParameter> ValidatedParams { get; set; }
        public ObservableCollection<SharedParameter> Parameters { get; set; }
        public List<DataType> DataTypes { get; set; }
        public List<DataCategory> DataCategories { get; set; }
        public List<Group> Groups { get; set; }
        public User CurrentUser { get; set; }

        /// <summary>
        /// Login to OpenDefinery using a username and password.
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <param name="username">The OpenDefinery username to login</param>
        /// <param name="password">The password of the OpenDefinery user</param>
        public static Definery Init(Definery definery, string username, string password)
        {
            var client = new RestClient(BaseUrl + "user/login?_format=json");
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "application/json");
            request.AddParameter("application/json", "{\r\n    \"name\": \"" + username + "\",\r\n    \"pass\": \"" + password + "\"\r\n}", ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);

            // Return the CSRF token if the response was OK
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                try
                {
                    JObject json = JObject.Parse(response.Content);

                    // Assign tokens to Definery members
                    definery.CsrfToken = json.SelectToken("csrf_token").ToString();

                    // Add logged in user data
                    definery.CurrentUser = new User();
                    definery.CurrentUser.Id = json.SelectToken("current_user.uid").ToString();
                    definery.CurrentUser.Name = json.SelectToken("current_user.name").ToString();

                    // Store the auth code for GET requests
                    definery.AuthCode = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));

                    return definery;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[" + ex.ToString() + "]" + response.Content, "Error Logging In");

                    // Return the original Definery object to maintain previously set properties
                    return definery;
                }
            }
            else
            {
                Debug.WriteLine(response.Content, "Error Logging In");

                // Return the original Definery object to maintain previously set properties
                return definery;
            }
        }

        /// <summary>
        /// Main method to load all the data from OpenDefinery
        /// </summary>
        public static Definery LoadData(Definery definery)
        {
            // Load the data from OpenDefinery
            definery.Groups = Group.GetAll(definery);
            definery.DataTypes = DataType.GetAll(definery);
            definery.DataCategories = DataCategory.GetAll(definery);
            //definery.MyCollections = Collection.ByCurrentUser(definery);
            //definery.PublishedCollections = Collection.GetPublished(definery);

            // Clean up Data Category names
            foreach (var cat in definery.DataCategories)
            {
                var splitName = cat.Name.Split('_');
                cat.Name = splitName[1];
            }

            // Sort the lists for future use by UI
            definery.DataTypes.Sort(delegate (DataType x, DataType y)
            {
                if (x.Name == null && y.Name == null) return 0;
                else if (x.Name == null) return -1;
                else if (y.Name == null) return 1;
                else return x.Name.CompareTo(y.Name);
            });

            definery.DataCategories.Sort(delegate (DataCategory x, DataCategory y)
            {
                if (x.Name == null && y.Name == null) return 0;
                else if (x.Name == null) return -1;
                else if (y.Name == null) return 1;
                else return x.Name.CompareTo(y.Name);
            });

            return definery;
        }
    }
}
