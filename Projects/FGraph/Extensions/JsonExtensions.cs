using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace FGraph
{
    public static class JsonExtensions
    {
        public static IEnumerable<String> GetStrings(this JToken t)
        {
            switch (t)
            {
                case JArray jArray:
                    foreach (JToken item in jArray)
                    {
                        String? value = item.Value<String>();
                        if (value != null)
                            yield return value;
                    }

                    break;
                case JToken jToken:
                {
                    String? value = jToken.Value<String>();
                    if (value != null)
                        yield return value;
                }
                    break;
                default:
                    throw new Exception($"Unexpected string array type {t.GetType()}");
            }
        }

        public static IEnumerable<Tuple<String, String>> GetTuples(this JToken t)
        {
            switch (t)
            {
                case JObject jObject:
                    foreach (KeyValuePair<String, JToken?> option in jObject)
                    {
                        String? value = option.Value?.Value<String>();
                        if (value == null)
                            throw new Exception($"Null string found in json");
                        yield return new Tuple<string, string>(option.Key, value);
                    }

                    break;

                default:
                    throw new Exception($"Unexpected string array type {t.GetType()}");
            }
        }
    }
}
