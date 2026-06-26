using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDefinery
{
    public class User
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// Retrieve a User by their user ID.
        /// </summary>
        /// <param name="definery">The main Definery object provides the auth code</param>
        /// <param name="userId">The ID of the user</param>
        /// <returns></returns>
        public static User GetById(Definery definery, int userId)
        {
            try
            {
                var response = OdHttp.Get(Definery.BaseUrl + string.Format("rest/user/id/{0}?_format=json", userId.ToString()), definery);

                var users = OdJson.Deserialize<List<User>>(response.Content);

                if (users.Count() == 1)
                {
                    return users.FirstOrDefault();
                }
                // If there are none or more than one result, return null
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieve a User by their username.
        /// </summary>
        /// <param name="definery">The main Definery object provides the auth code</param>
        /// <param name="username">The name of the user</param>
        /// <returns></returns>
        public static User GetByUserName(Definery definery, string username)
        {
            try
            {
                var response = OdHttp.Get(Definery.BaseUrl + string.Format("rest/user/name/{0}?_format=json", username), definery);

                var users = OdJson.Deserialize<List<User>>(response.Content);

                if (users.Count() == 1)
                {
                    return users.FirstOrDefault();
                }
                // If there are none or more than one result, return null
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
