using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using VirtoCommerce.ProductSubscription.Core.Models;

namespace VirtoCommerce.ProductSubscription.Data.Models;

/// <summary>Reads and writes the configuration column: camelCase JSON without empty values.</summary>
public static class SubscriptionConfigurationJson
{
    private static readonly JsonSerializerSettings _settings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.None,
    };

    public static string Serialize(SubscriptionConfiguration configuration)
    {
        return configuration == null ? null : JsonConvert.SerializeObject(configuration, _settings);
    }

    public static SubscriptionConfiguration Deserialize(string json)
    {
        return string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<SubscriptionConfiguration>(json, _settings);
    }
}
