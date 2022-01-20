# OpenDefinery for Revitᵀᴹ
OpenDefinery is reinventing how shared parameters are managed in Revitᵀᴹ. By ditching the antiquated plain text file and implementing an online database, additional meta data can be stored which makes BIM data within Revitᵀᴹ more scalable and easier to manage.

For more information on the OpenDefinery platform, visit [OpenDefinery.com](http://opendefinery.com).

#### This add-in requires an OpenDefinery account. 
Currently, the OpenDefinery platform is in private beta, which means all accounts need approval. [Reach out via email](mailto:i@opendefinery.com) to create a new account or gain approval for an account requested via the [registration page](https://app.opendefinery.com/user/register).

## OpenDefinery Shared Parameter Manager
This is an extremely early version of the OpenDefinery Revitᵀᴹ add-in which means it doesn't do much and is not very pretty, but don't forget to star this repository because there are many more features coming in the near term.

![](https://github.com/TripleZeroLabs/OpenDefinery-RevitAddins/blob/master/OD-ParamManager/images/screenshot.png)

### Current Features (v0.1)
- **Parameter Validation:** Iterate through each shared parameter in a project or family and check if it exist in a particular [OpenDefinery Collection](https://app.opendefinery.com/browse/collections). In other words, this tool validates if a project is in compliance with any given standard that is hosted on OpenDefinery.

---
# How does it work?

## Introducing the industry's first REST API for shared parameters.
One of the main benefits of this suite of add-ins is that they leverage the [OpenDefinery API](https://documenter.getpostman.com/view/5483074/T1LHGpQo). This API can be used to create and retrieve shared parameters, as well as retrieve entire "sets" of shared paramaters (aka `Collections`) which is how to run a validation against any given standard.

Ultimately, a software development kit (SDK) will be released, but until then this open source project can serve as a reference for how to use the API using C#.

#### Example Request
This example demonstrates how the `SharedParameter` class is used to retrieve a shared parameter from the OpenDefinery database by simply passing the GUID as an argument.

For more complete API docs and more sample code, visit the [OpenDefinery API Documentation](https://documenter.getpostman.com/view/5483074/T1LHGpQo).
```
public static ObservableCollection<SharedParameter> FromGuid(Definery definery, Guid guid)
{
    var client = new RestClient(Definery.BaseUrl + string.Format("rest/params/guid/{0}?_format=json", guid.ToString()));
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