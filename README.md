# OpenDefinery for Revitᵀᴹ
![](https://github.com/TripleZeroLabs/OpenDefinery-RevitAddins/blob/master/OpenDefinery/images/OpenDefinery-Banner-1.png)
OpenDefinery is reinventing how shared parameters are managed in Revitᵀᴹ. By ditching the antiquated plain text file and implementing an online database, additional meta data can be stored which makes BIM data within Revitᵀᴹ more scalable and easier to manage.

For more information on the OpenDefinery platform, visit [OpenDefinery.com](http://opendefinery.com).

#### This add-in requires an OpenDefinery account. 
Currently, the OpenDefinery platform is in private beta, which means all accounts need approval. [Reach out via email](mailto:i@opendefinery.com) to create a new account or gain approval for an account requested via the [registration page](https://app.opendefinery.com/user/register).

## OpenDefinery Shared Parameter Manager
This is an extremely early version of the OpenDefinery Revitᵀᴹ add-in and is only intended for testing purposes only. Do not use this software on live production Revit models.

![](https://github.com/TripleZeroLabs/OpenDefinery-RevitAddins/blob/master/OD-ParamManager/images/screenshot.png)

### Current Features (v0.2)
- **Parameter Validation:** Iterate through each shared parameter in a project or family and check if it exist in a particular [OpenDefinery Collection](https://app.opendefinery.com/browse/collections). In other words, this tool validates if a project is in compliance with any given standard that is hosted on OpenDefinery.
- **Add Parameters to Current Family:** Add multiple shared parameters from an [OpenDefinery Collection](https://app.opendefinery.com/browse/collections) to the current family.
- **Purge Shared Parameters:** Identify which shared parameters are being used and completely purge them from the Revit database.
- **Manage Collections:** Create new OpenDefinery Collections and add new Shared Parameters to Collections.

---
# How does it work?

## Introducing the industry's first REST API for shared parameters.
One of the main benefits of this suite of add-ins is that they leverage the [OpenDefinery API](https://documenter.getpostman.com/view/5483074/T1LHGpQo). This API can be used to create and retrieve shared parameters, as well as retrieve entire "sets" of shared paramaters (aka `Collections`) which is how to run a validation against any given standard.

#### Example Request
The below examples demonstrate how to retrieve a list of `SharedParameters` from OpenDefinery by passing a GUID to the API.

**cURL**

`curl --location --request GET 'http://app.opendefinery.com/rest/params/guid/a4d4a6bb-1eca-43ed-a52b-376bff2d9a76?_format=json'`

**JSON Reponse**
```
{
  "rows": [
    {
      "id": "28156",
      "name": "AirFlowAirShaft",
      "data_category": "",
      "data_type": "HVAC_AIR_FLOW",
      "description": "The air flow of an air shaft.",
      "group": "Default Group",
      "guid": "a4d4a6bb-1eca-43ed-a52b-376bff2d9a76",
      "user_modifiable": "1",
      "visible": "1",
      "author": "6",
      "collections": "10794"
    },
    {
      "id": "29584",
      "name": "AirFlow_AirShaft",
      "data_category": "",
      "data_type": "HVAC_AIR_FLOW",
      "description": "The air flow of an air shaft.",
      "group": "Default Group",
      "guid": "a4d4a6bb-1eca-43ed-a52b-376bff2d9a76",
      "user_modifiable": "1",
      "visible": "1",
      "author": "1",
      "collections": ""
    }
  ],
  "pager": {
    "current_page": 0,
    "total_items": 2,
    "total_pages": 1,
    "items_per_page": 100
  }
}
```

Note that an array of `rows` is returned because OpenDefinery can store multiple shared parameters with the same GUID. The `collections` property helps identify where each instance of the `SharedParameter` is assigned.

**C# Method**

This example demonstrates how the above endpoint is implemented within the `SharedParameter` class retrieve a shared parameter from the OpenDefinery database by simply passing the GUID as an argument.

For more complete API docs and more sample code, visit the [OpenDefinery API Documentation](https://documenter.getpostman.com/view/5483074/T1LHGpQo).

```cs
public static ObservableCollection<SharedParameter> FromGuid(Definery definery, Guid guid)
{
    var client = new RestClient(definery.BaseUrl + 
        string.Format("rest/params/guid/{0}?_format=json", guid.ToString()));
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
```

# Contributors Wanted
You don't need a software developer to contribute to this growing project. [Reach out](mailto:i@opendefinery.com) and let's connect!