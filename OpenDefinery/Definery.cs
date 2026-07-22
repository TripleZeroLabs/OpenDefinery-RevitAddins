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
        public static string BaseUrl = "https://app.opendefinery.com/";
        public string CsrfToken { get; set; }
        public string AuthCode { get; set; }
        public List<Collection> MyCollections { get; set; }
        public List<Collection> PublishedCollections { get; set; }
        public List<Collection> AllCollections { get; set; }
        public Collection SelectedCollection { get; set; }
        public List<DefineryParameter> DefineryParameters { get; set; }
        public List<DefineryParameter> RevitParameters { get; set; }
        public List<DefineryParameter> ValidatedParams { get; set; }
        public ObservableCollection<DefineryParameter> Parameters { get; set; }
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
        /// <summary>
        /// The authenticated session for this Revit process, set by <see cref="Init"/> on
        /// success. Lets a second window (e.g. Export Parameters) reuse the sign-in instead
        /// of prompting again.
        /// </summary>
        public static Definery Current { get; private set; }

        public static Definery Init(Definery definery, string username, string password)
        {
            // Drupal's /user/login only accepts ANONYMOUS requests. The HttpClient (and its
            // cookies) live for the whole Revit process, so a session left over from a
            // previous sign-in makes this fail with "This route can only be accessed by
            // anonymous users." Clear it first so login is always anonymous.
            OdHttp.ResetSession();

            var body = "{\"name\": " + OdJson.ToJsonString(username) +
                       ", \"pass\": " + OdJson.ToJsonString(password) + "}";
            var response = OdHttp.Post(BaseUrl + "user/login?_format=json", body, definery);

            // Return the CSRF token if the response was OK
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                try
                {
                    // Assign tokens to Definery members
                    definery.CsrfToken = OdJson.GetString(response.Content, "csrf_token");

                    // Add logged in user data
                    definery.CurrentUser = new User();
                    definery.CurrentUser.Id = OdJson.GetString(response.Content, "current_user", "uid");
                    definery.CurrentUser.Name = OdJson.GetString(response.Content, "current_user", "name");

                    // Store the auth code for GET requests
                    definery.AuthCode = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));

                    // Publish the session so other windows can reuse this sign-in.
                    Current = definery;

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
