using System.Collections.Generic;
using System.Globalization;

namespace Raven.Server.Integrations.PostgreSQL.VirtualCatalog
{
    internal static class PgArrayText
    {
        public static bool TryParse(string text, out List<string> elements)
        {
            elements = null;
            if (text == null)
                return false;

            var body = text.Trim();
            char separator;
            if (body.StartsWith('{') && body.EndsWith('}'))
            {
                body = body[1..^1];
                separator = ',';
            }
            else
            {
                separator = ' ';
            }

            if (body.Contains('"') || body.Contains('{'))
                return false;

            elements = new List<string>();
            foreach (var element in body.Split(separator))
            {
                var trimmed = element.Trim();
                if (trimmed.Length > 0)
                    elements.Add(trimmed);
            }

            return true;
        }

        public static bool TryGetSingleInteger(object value, out long element)
        {
            element = 0;
            return value is string text &&
                   TryParse(text, out var elements) &&
                   elements.Count == 1 &&
                   long.TryParse(elements[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out element);
        }
    }
}
