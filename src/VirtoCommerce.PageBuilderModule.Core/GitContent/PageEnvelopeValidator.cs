using Newtonsoft.Json.Linq;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent
{
    /// <summary>
    /// Structural validation of a .page document before it is committed to git — the C# port of
    /// the ERROR-level checks of vc-content tools/validate-page.mjs that need no theme schemas
    /// (envelope shape, settings.type, sections are typed objects, unique ids). Schema-aware
    /// checks (unknown section type etc.) stay in the PR CI gate, which runs the full validator.
    /// </summary>
    public static class PageEnvelopeValidator
    {
        /// <summary>
        /// Returns the list of errors; empty means the document is committable.
        /// Reads both the {settings, content} envelope and the legacy flat array.
        /// </summary>
        public static IList<string> Validate(JToken page)
        {
            var errors = new List<string>();

            JToken settings;
            JToken content;
            switch (page)
            {
                // legacy flat array: [0] = settings, the rest = sections
                case JArray array:
                    settings = array.Count > 0 ? array[0] : null;
                    content = new JArray(array.Skip(1));
                    break;
                case JObject envelope:
                    settings = envelope["settings"];
                    content = envelope["content"];
                    break;
                default:
                    errors.Add("Page is neither a {settings, content} envelope nor a legacy array.");
                    return errors;
            }

            if (settings is not JObject settingsObject)
            {
                errors.Add("Missing settings object (page-level settings).");
            }
            else if (settingsObject["type"]?.Value<string>() != "settings")
            {
                errors.Add($"settings.type must be \"settings\" (got: {settingsObject["type"]?.ToString() ?? "null"}).");
            }

            if (content is not JArray sections)
            {
                errors.Add("Missing content array (sections).");
                return errors;
            }

            var seenIds = new Dictionary<string, int>();
            for (var i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                if (section is not JObject sectionObject)
                {
                    errors.Add($"content[{i}]: section is not an object.");
                    continue;
                }

                var type = sectionObject["type"]?.Value<string>();
                var label = $"content[{i}]{(string.IsNullOrEmpty(type) ? string.Empty : $" ({type})")}";

                if (string.IsNullOrEmpty(type))
                {
                    errors.Add($"{label}: missing type field.");
                }

                var id = sectionObject["id"]?.Value<string>();
                if (!string.IsNullOrEmpty(id))
                {
                    if (seenIds.TryGetValue(id, out var firstIndex))
                    {
                        errors.Add($"{label}: duplicate id \"{id}\" (already in content[{firstIndex}]).");
                    }
                    else
                    {
                        seenIds.Add(id, i);
                    }
                }
            }

            return errors;
        }
    }
}
