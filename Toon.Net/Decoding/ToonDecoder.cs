using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ToonNet.Options;

namespace ToonNet.Decoding
{
    internal static class ToonDecoder
    {
        private sealed record Line(int Indent, string Content);

        public static object? ParseToon(string toon, ToonDecodeOptions options)
        {
            var linesRaw = toon
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);

            var lines = new List<Line>();
            foreach (var raw in linesRaw)
            {
                int indentCount = 0;
                while (indentCount < raw.Length && raw[indentCount] == ' ')
                    indentCount++;

                string content = raw[indentCount..].TrimEnd();
                if (string.IsNullOrWhiteSpace(content))
                    continue;

                lines.Add(new Line(indentCount, content));
            }

            int index = 0;
            if (lines.Count == 0)
                return new Dictionary<string, object?>();

            // Asumimos root objeto
            var rootObj = ParseObject(lines, ref index, lines[0].Indent, options);
            return rootObj;
        }

        private static Dictionary<string, object?> ParseObject(
            IReadOnlyList<Line> lines,
            ref int index,
            int currentIndent,
            ToonDecodeOptions options)
        {
            var obj = new Dictionary<string, object?>(StringComparer.Ordinal);

            while (index < lines.Count)
            {
                var line = lines[index];

                if (line.Indent < currentIndent)
                    break;
                if (line.Indent > currentIndent)
                {
                    break;
                }

                int colonPos = line.Content.IndexOf(':');
                if (colonPos < 0)
                    throw new FormatException($"Invalid line (missing ':'): '{line.Content}'");

                string header = line.Content[..colonPos].Trim();
                string valuePart = line.Content[(colonPos + 1)..].Trim();

                ParseHeader(header, out string name, out int? length, out string[]? columns);

                index++;

                if (length != null)
                {
                    if (columns != null)
                    {
                        var arr = ParseTabularArray(lines, ref index, currentIndent + 2, length.Value, columns, options);
                        obj[name] = arr;
                    }
                    else if (valuePart.Length > 0)
                    {
                        var arr = ParsePrimitiveArrayInline(valuePart, length.Value, options);
                        obj[name] = arr;
                    }
                    else
                    {
                        throw new NotSupportedException(
                            $"Array '{name}' with a declared length but without columns or inline values, this is not supported in this simplified parser.");
                    }
                }
                else
                {
                    if (valuePart.Length == 0)
                    {
                        var nested = ParseObject(lines, ref index, currentIndent + 2, options);
                        obj[name] = nested;
                    }
                    else
                    {
                        obj[name] = ParseScalar(valuePart);
                    }
                }
            }

            return obj;
        }

        private static void ParseHeader(
            string header,
            out string name,
            out int? length,
            out string[]? columns)
        {
            length = null;
            columns = null;

            int bracketPos = header.IndexOf('[');
            if (bracketPos < 0)
            {
                name = header;
                return;
            }

            name = header[..bracketPos].Trim();

            int closeBracket = header.IndexOf(']', bracketPos + 1);
            if (closeBracket < 0)
                throw new FormatException($"Invalid header (missing ']'): '{header}'");

            string lengthPart = header[(bracketPos + 1)..closeBracket];
            int i = 0;
            while (i < lengthPart.Length && char.IsDigit(lengthPart[i])) i++;
            if (i == 0)
                throw new FormatException($"Invalid array length in header: '{header}'");

            length = int.Parse(lengthPart[..i], CultureInfo.InvariantCulture);

            int bracePos = header.IndexOf('{', closeBracket + 1);
            if (bracePos >= 0)
            {
                int closeBrace = header.IndexOf('}', bracePos + 1);
                if (closeBrace < 0)
                    throw new FormatException($"Invalid header (missing '}}'): '{header}'");

                string colsPart = header[(bracePos + 1)..closeBrace];
                columns = colsPart.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
        }

        private static List<object?> ParsePrimitiveArrayInline(
            string valuePart,
            int length,
            ToonDecodeOptions options)
        {
            var values = ParseRow(valuePart, options.Delimiter);
            var list = new List<object?>(values.Count);
            foreach (var v in values)
                list.Add(ParseScalar(v));

            return list;
        }

        private static List<Dictionary<string, object?>> ParseTabularArray(
            IReadOnlyList<Line> lines,
            ref int index,
            int rowIndent,
            int length,
            string[] columns,
            ToonDecodeOptions options)
        {
            var rows = new List<Dictionary<string, object?>>(length);

            while (index < lines.Count && rows.Count < length)
            {
                var line = lines[index];
                if (line.Indent < rowIndent) break;
                if (line.Indent > rowIndent)
                    throw new FormatException($"Unexpected indentation in tabular row: '{line.Content}'");

                index++;

                var fields = ParseRow(line.Content.Trim(), options.Delimiter);
                if (fields.Count != columns.Length)
                {
                    while (fields.Count < columns.Length) fields.Add("null");
                }

                var obj = new Dictionary<string, object?>(columns.Length, StringComparer.Ordinal);
                for (int i = 0; i < columns.Length; i++)
                {
                    string fieldText = i < fields.Count ? fields[i] : "null";
                    obj[columns[i]] = ParseScalar(fieldText);
                }

                rows.Add(obj);
            }

            return rows;
        }

        private static List<string> ParseRow(string text, char delimiter)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            bool escape = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (escape)
                {
                    sb.Append(c switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        '"' => '"',
                        '\\' => '\\',
                        _ => c
                    });
                    escape = false;
                    continue;
                }

                if (c == '\\' && inQuotes)
                {
                    escape = true;
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (c == delimiter && !inQuotes)
                {
                    result.Add(sb.ToString().Trim());
                    sb.Clear();
                    continue;
                }

                sb.Append(c);
            }

            if (sb.Length > 0 || text.EndsWith(delimiter))
                result.Add(sb.ToString().Trim());

            return result;
        }

        private static object? ParseScalar(string token)
        {
            token = token.Trim();
            if (token.Length == 0) return null;

            if (token == "null") return null;
            if (token == "true") return true;
            if (token == "false") return false;

            if (token.Length >= 2 && token[0] == '"' && token[^1] == '"')
            {
                string inner = token[1..^1];
                var sb = new StringBuilder();
                bool escape = false;
                foreach (char c in inner)
                {
                    if (escape)
                    {
                        sb.Append(c switch
                        {
                            'n' => '\n',
                            'r' => '\r',
                            't' => '\t',
                            '"' => '"',
                            '\\' => '\\',
                            _ => c
                        });
                        escape = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escape = true;
                        continue;
                    }

                    sb.Append(c);
                }

                return sb.ToString();
            }

            if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                return l;
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                return d;

            return token;
        }
    }
}
