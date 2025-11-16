using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using ToonNet.Options;

namespace ToonNet.Converter
{
    /// <summary>
    /// JSON &lt;-&gt; TOON converter based on the official spec (practical subset).
    /// </summary>
    public static class ToonConverter
    {
        private static readonly JsonSerializerOptions DefaultJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // --------------------------------------------------------------------
        //  PUBLIC API
        // --------------------------------------------------------------------

        /// <summary>Serializes a .NET object to TOON.</summary>
        public static string SerializeObject(
            object? value,
            ToonEncodeOptions? options = null,
            JsonSerializerOptions? jsonOptions = null)
        {
            string json = JsonSerializer.Serialize(value, jsonOptions ?? DefaultJsonOptions);
            return SerializeJson(json, options);
        }

        /// <summary>Serializes a JSON string to TOON.</summary>
        public static string SerializeJson(string json, ToonEncodeOptions? options = null)
        {
            options ??= new ToonEncodeOptions();
            return Encoding.ToonEncoder.SerializeJson(json, options);
        }

        /// <summary>Deserializes TOON to JSON.</summary>
        public static string DeserializeToJson(
            string toon,
            ToonDecodeOptions? options = null,
            JsonSerializerOptions? jsonOptions = null)
        {
            options ??= new ToonDecodeOptions();
            var root = Decoding.ToonDecoder.ParseToon(toon, options);

            return JsonSerializer.Serialize(root, jsonOptions ?? DefaultJsonOptions);
        }

        /// <summary>Deserializes TOON directly to T using JSON underneath.</summary>
        public static T? DeserializeObject<T>(
            string toon,
            ToonDecodeOptions? options = null,
            JsonSerializerOptions? jsonOptions = null)
        {
            string json = DeserializeToJson(toon, options, jsonOptions);
            return JsonSerializer.Deserialize<T>(json, jsonOptions ?? DefaultJsonOptions);
        }
    }
}
