using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent
{
    /// <summary>
    /// The one way a .page document is turned into bytes for git: two-space indent, LF line endings,
    /// UTF-8 without a BOM, trailing newline.
    /// <para>
    /// Publish status is "the bytes on the work branch differ from the bytes on the production branch",
    /// so serialization has to be a function of the content alone. It is not, by default: Newtonsoft
    /// indents with <see cref="Environment.NewLine"/>, which is CRLF on a Windows host and LF on Linux —
    /// the same page would look changed or unchanged depending on where the platform happens to run.
    /// The environment is already inconsistent about this (some live pages are stored with CRLF, some
    /// with LF, and legacy ones with a four-space indent), which is the drift this class exists to stop.
    /// </para>
    /// <para>
    /// The seeding script in vc-content (tools/page-canonical.mjs) writes the same bytes. Both write
    /// into git, so they have to agree exactly, or the first save of an untouched page would rewrite
    /// the whole file.
    /// </para>
    /// </summary>
    public static class PageJson
    {
        private const int IndentSize = 2;
        private const char Bom = '\uFEFF';

        /// <summary>UTF-8 that does not emit a byte order mark.</summary>
        public static readonly Encoding Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <summary>
        /// Serializes a page document to its canonical form. Accepts whatever the save endpoint
        /// deserialized the request body into (<see cref="JToken"/> or a plain object).
        /// </summary>
        public static string Serialize(object content)
        {
            var token = content as JToken ?? (content == null ? JValue.CreateNull() : JToken.FromObject(content));

            var buffer = new StringWriter();
            using (var writer = new JsonTextWriter(buffer)
            {
                Formatting = Formatting.Indented,
                Indentation = IndentSize,
                IndentChar = ' ',
            })
            {
                token.WriteTo(writer);
            }

            // trailing newline: a text file without one shows up as "\ No newline at end of file" in
            // every diff, and the seeding script writes one
            return Canonicalize(buffer.ToString()) + "\n";
        }

        /// <summary>
        /// Brings text read back from git (or from blob) to the shape <see cref="Serialize"/> produces,
        /// so two versions of a page can be compared without re-parsing them. Used for publish status,
        /// where the only question is "are these the same bytes".
        /// </summary>
        public static string Canonicalize(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return json;
            }

            // drop a BOM the blob or the API may have prefixed, then collapse CRLF and bare CR to LF
            return json.TrimStart(Bom).Replace("\r\n", "\n").Replace('\r', '\n');
        }

        /// <summary>
        /// True when two page documents are the same bytes once canonicalized. A missing page and an
        /// empty one are different things, so a null only equals another null.
        /// </summary>
        public static bool AreSame(string left, string right)
        {
            if (left == null || right == null)
            {
                return ReferenceEquals(left, right);
            }

            return string.Equals(Canonicalize(left), Canonicalize(right), StringComparison.Ordinal);
        }
    }
}
