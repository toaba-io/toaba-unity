using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Toaba.Http
{
    internal static class Json
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() },
            NullValueHandling = NullValueHandling.Ignore,
            // Otherwise Newtonsoft reformats timestamps read into strings.
            DateParseHandling = DateParseHandling.None,
        };

        public static byte[] Serialize(object value) =>
            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value, Settings));

        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json ?? "", Settings);

        public static JObject TryParseObject(string json)
        {
            try
            {
                return Deserialize<JToken>(json) as JObject;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
