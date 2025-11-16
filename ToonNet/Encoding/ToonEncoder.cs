using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ToonNet.Options;

namespace ToonNet.Encoding
{
    internal static class ToonEncoder
    {
        public static string SerializeJson(string json, ToonEncodeOptions options)
        {
            using var doc = JsonDocument.Parse(json);
            var sb = new StringBuilder();

            WriteElement(null, doc.RootElement, sb, 0, options);

            return sb.ToString();
        }

        private static void WriteElement(
            string? name,
            JsonElement element,
            StringBuilder sb,
            int indentLevel,
            ToonEncodeOptions options)
        {
            string indent = new string(' ', indentLevel * options.Indent.Length);

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    if (name != null)
                    {
                        sb.Append(indent);
                        sb.Append(name);
                        sb.Append(':');
                        sb.AppendLine();
                        indentLevel++;
                        indent = new string(' ', indentLevel * options.Indent.Length);
                    }

                    foreach (var prop in element.EnumerateObject())
                    {
                        WriteElement(prop.Name, prop.Value, sb, indentLevel, options);
                    }
                    break;

                case JsonValueKind.Array:
                    if (name == null)
                        throw new NotSupportedException(
                            "This encoder assumes a root object or a named primitive/tabular root array. " +
                            "Use a root object for greater compatibility.");

                    WriteArray(name, element, sb, indentLevel, options);
                    break;

                default:
                    if (name == null)
                        throw new NotSupportedException(
                            "Primitive root is not supported in this simplified encoder.");

                    sb.Append(indent);
                    sb.Append(name);
                    sb.Append(": ");
                    sb.Append(FormatScalar(element, options));
                    sb.AppendLine();
                    break;
            }
        }

        private static void WriteArray(
            string name,
            JsonElement array,
            StringBuilder sb,
            int indentLevel,
            ToonEncodeOptions options)
        {
            int count = array.GetArrayLength();
            string indent = new string(' ', indentLevel * options.Indent.Length);

            if (count == 0)
            {
                sb.Append(indent);
                sb.Append(name);
                sb.Append('[').Append(count).Append("]:");
                sb.AppendLine();
                return;
            }

            // 1) Uniform array of objects with only primitive fields -> tabular
            if (IsUniformObjectArray(array, out var fieldNames))
            {
                sb.Append(indent);
                sb.Append(name);
                sb.Append('[').Append(count).Append(']');
                sb.Append('{');
                sb.Append(string.Join(options.Delimiter, fieldNames));
                sb.Append("}:");
                sb.AppendLine();

                string rowIndent = new string(' ', (indentLevel + 1) * options.Indent.Length);

                foreach (var item in array.EnumerateArray())
                {
                    var values = new List<string>(fieldNames.Count);

                    foreach (var field in fieldNames)
                    {
                        var value = item.GetProperty(field);
                        values.Add(FormatScalar(value, options));
                    }

                    sb.Append(rowIndent);
                    sb.Append(string.Join(options.Delimiter, values));
                    sb.AppendLine();
                }

                return;
            }

            // 2) Primitive array -> inline
            if (IsPrimitiveArray(array))
            {
                var values = array.EnumerateArray()
                                  .Select(e => FormatScalar(e, options));

                sb.Append(indent);
                sb.Append(name);
                sb.Append('[').Append(count).Append("]: ");
                sb.Append(string.Join(options.Delimiter, values));
                sb.AppendLine();
                return;
            }

            // 3) Other arrays (not implemented yet)
            throw new NotSupportedException(
                $"Array '{name}' not a primitive nor a uniform array of objects with primitive fields.   " +
                "This simplified encoder only supports those two cases (as in the main examples from the TOON README).");
        }

        private static bool IsUniformObjectArray(JsonElement array, out List<string> fieldNames)
        {
            fieldNames = new List<string>();

            if (array.ValueKind != JsonValueKind.Array)
                return false;

            bool first = true;
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    return false;

                var props = item.EnumerateObject().ToArray();

                if (first)
                {
                    fieldNames = props.Select(p => p.Name).ToList();
                    if (props.Any(p => p.Value.ValueKind == JsonValueKind.Object ||
                                       p.Value.ValueKind == JsonValueKind.Array))
                    {
                        fieldNames.Clear();
                        return false;
                    }

                    first = false;
                }
                else
                {
                    if (props.Length != fieldNames.Count)
                        return false;

                    for (int i = 0; i < props.Length; i++)
                    {
                        if (!string.Equals(props[i].Name, fieldNames[i], StringComparison.Ordinal))
                            return false;

                        var kind = props[i].Value.ValueKind;
                        if (kind == JsonValueKind.Object || kind == JsonValueKind.Array)
                            return false;
                    }
                }
            }

            return !first;
        }

        private static bool IsPrimitiveArray(JsonElement array)
        {
            if (array.ValueKind != JsonValueKind.Array) return false;

            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    return false;
            }

            return true;
        }

        private static string FormatScalar(JsonElement value, ToonEncodeOptions options)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    {
                        string s = value.GetString() ?? string.Empty;

                        bool needsQuotes =
                            s.IndexOfAny(new[] { options.Delimiter, '"', '\n', '\r', '\t' }) >= 0 ||
                            s.Length == 0 ||
                            s.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                            s.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                            s.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                            double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _);

                        if (!needsQuotes)
                            return s;

                        var sb = new StringBuilder();
                        sb.Append('"');
                        foreach (char c in s)
                        {
                            sb.Append(c switch
                            {
                                '"' => "\\\"",
                                '\\' => "\\\\",
                                '\n' => "\\n",
                                '\r' => "\\r",
                                '\t' => "\\t",
                                _ => c.ToString()
                            });
                        }
                        sb.Append('"');
                        return sb.ToString();
                    }

                case JsonValueKind.Number:
                    return value.GetRawText();

                case JsonValueKind.True:
                    return "true";
                case JsonValueKind.False:
                    return "false";
                case JsonValueKind.Null:
                    return "null";

                default:
                    return value.GetRawText();
            }
        }
    }
}
