#nullable enable

using System;
using System.Collections.Generic;
using CadentCable.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace CadentCable.Json.Newtonsoft
{
    public sealed class NewtonsoftJsonSerializer : IJsonSerializer
    {
        private readonly JsonSerializerSettings _settings;
        private readonly JsonSerializer _serializer;

        public NewtonsoftJsonSerializer()
        {
            _settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Include,
                MissingMemberHandling = MissingMemberHandling.Ignore,
            };
            _settings.Converters.Add(
                new StringEnumConverter(new CamelCaseNamingStrategy(), allowIntegerValues: false));
            _serializer = JsonSerializer.Create(_settings);
        }

        public string Serialize<T>(T value)
        {
            try
            {
                return JsonConvert.SerializeObject(value, _settings);
            }
            catch (JsonException ex)
            {
                throw new CCJsonException("invalid_message", "JSON serialization failed.", ex);
            }
        }

        public ICCJsonObject ParseObject(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            JToken token;
            try
            {
                token = JToken.Parse(json);
            }
            catch (JsonReaderException ex)
            {
                throw new CCJsonException("invalid_json", "Received invalid JSON.", ex);
            }
            catch (JsonException ex)
            {
                throw new CCJsonException("invalid_json", "Received invalid JSON.", ex);
            }

            if (!(token is JObject obj))
            {
                throw new CCJsonException("invalid_message", "JSON message must be an object.");
            }

            return new NewtonsoftJsonObject(obj, _serializer);
        }

        private sealed class NewtonsoftJsonObject : ICCJsonObject
        {
            private readonly JObject _object;
            private readonly JsonSerializer _serializer;

            public NewtonsoftJsonObject(JObject obj, JsonSerializer serializer)
            {
                _object = obj;
                _serializer = serializer;
            }

            public string RawJson => _object.ToString(Formatting.None);

            public bool HasProperty(string propertyName)
            {
                return _object.Property(propertyName, StringComparison.Ordinal) != null;
            }

            public CCJsonValueKind GetValueKind(string propertyName)
            {
                JToken? token = _object.GetValue(propertyName, StringComparison.Ordinal);
                if (token == null)
                {
                    return CCJsonValueKind.Undefined;
                }

                switch (token.Type)
                {
                    case JTokenType.Null:
                    case JTokenType.Undefined:
                        return CCJsonValueKind.Null;
                    case JTokenType.Boolean:
                        return CCJsonValueKind.Boolean;
                    case JTokenType.Integer:
                    case JTokenType.Float:
                        return CCJsonValueKind.Number;
                    case JTokenType.String:
                        return CCJsonValueKind.String;
                    case JTokenType.Object:
                        return CCJsonValueKind.Object;
                    case JTokenType.Array:
                        return CCJsonValueKind.Array;
                    default:
                        return CCJsonValueKind.Undefined;
                }
            }

            public bool TryGetString(string propertyName, out string value)
            {
                JToken? token = _object.GetValue(propertyName, StringComparison.Ordinal);
                if (token != null && token.Type == JTokenType.String)
                {
                    value = token.Value<string>() ?? string.Empty;
                    return true;
                }

                value = string.Empty;
                return false;
            }

            public IReadOnlyList<ICCJsonObject> GetObjectArray(string propertyName)
            {
                JToken? token = _object.GetValue(propertyName, StringComparison.Ordinal);
                if (!(token is JArray array))
                {
                    throw new CCJsonException(
                        "invalid_message",
                        "Property '" + propertyName + "' must be an array.");
                }

                List<ICCJsonObject> result = new List<ICCJsonObject>(array.Count);
                foreach (JToken item in array)
                {
                    if (!(item is JObject itemObject))
                    {
                        throw new CCJsonException(
                            "invalid_message",
                            "Every item in '" + propertyName + "' must be an object.");
                    }

                    result.Add(new NewtonsoftJsonObject(itemObject, _serializer));
                }

                return result;
            }

            public T ToObject<T>()
            {
                try
                {
                    object? result = _object.ToObject(typeof(T), _serializer);
                    if (result == null)
                    {
                        throw new CCJsonException(
                            "invalid_message",
                            "JSON object could not be converted to " + typeof(T).Name + ".");
                    }

                    return (T)result;
                }
                catch (CCJsonException)
                {
                    throw;
                }
                catch (JsonException ex)
                {
                    throw new CCJsonException(
                        "invalid_message",
                        "JSON object could not be converted to " + typeof(T).Name + ".",
                        ex);
                }
                catch (Exception ex)
                {
                    throw new CCJsonException(
                        "invalid_message",
                        "JSON object could not be converted to " + typeof(T).Name + ".",
                        ex);
                }
            }
        }
    }
}
