using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlueBrick.Audit.Core
{
    /// <summary>
    /// Deterministic canonical JSON serializer used by
    /// <see cref="AuditStateVersionBuilder"/> to produce a stable SHA-256
    /// state version. Per BB-M001 packet section 11, the serializer must:
    /// <list type="bullet">
    /// <item>use <see cref="CultureInfo.InvariantCulture"/> everywhere;</item>
    /// <item>emit deterministic property order (sorted keys);</item>
    /// <item>emit deterministic collection order (input must be pre-sorted by caller or by a wrapper);</item>
    /// <item>distinguish explicit <c>null</c> from empty <see cref="string"/>/collection (no silent collapse);</item>
    /// <item>exclude timestamps from state-hash inputs (handled by the caller excluding the timestamp field from the POCO snapshot);</item>
    /// <item>never emit full local paths in canonical public artifacts (handled by <see cref="AuditRedactionService"/>);</item>
    /// <item>produce byte-identical JSON for the same input across repeated runs.</item>
    /// </list>
    /// The serializer deliberately does NOT rely on default dictionary
    /// iteration order: callers must pass ordered LINQ projections or this
    /// class re-sorts string-typed dictionary keys with an ordinal
    /// comparer before emitting.
    /// </summary>
    public static class AuditCanonicalSerializer
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        /// <summary>
        /// Canonicalize an arbitrary POCO to a deterministic JSON string.
        /// <para>Rules:</para>
        /// <list type="bullet">
        /// <item>sort all object property keys by ordinal (case-sensitive) for stability across processes;</item>
        /// <item>recursively sort nested object keys;</item>
        /// <item>sort string-typed dictionary keys (input order is NOT trusted);</item>
        /// <item>lists stay in caller-supplied order (caller owns collection ordering);</item>
        /// <item><c>null</c> token emitted as JSON <c>null</c>; empty string as <c>""</c>; empty array as <c>[]</c>; explicit distinction is preserved.</item>
        /// </list>
        /// </summary>
        public static string ToCanonicalJson(object value)
        {
            if (value == null) return "null";
            var token = JToken.FromObject(value, JsonSerializer.Create(CanonicalJsonSettings));
            var ordered = OrderToken(token);
            using (var sw = new StringWriter(CultureInfo.InvariantCulture))
            using (var jw = new JsonTextWriter(sw))
            {
                jw.Formatting = Formatting.None;
                jw.Culture = CultureInfo.InvariantCulture;
                jw.StringEscapeHandling = StringEscapeHandling.EscapeNonAscii;
                ordered.WriteTo(jw);
                return sw.ToString();
            }
        }

        /// <summary>Canonicalize and UTF-8-encode without BOM for hashing.</summary>
        public static byte[] ToCanonicalBytes(object value)
        {
            return Utf8NoBom.GetBytes(ToCanonicalJson(value));
        }

        /// <summary>
        /// True if two POCOs canonicalize to byte-identical JSON. Used by the
        /// pure tests (CanonicalSerializer_SameObject_ProducesStableJson /
        /// CanonicalSerializer_UnorderedCollections_ProduceSameJson) to verify
        /// determinism without actually invoking SHA-256.
        /// </summary>
        public static bool CanonicalEquals(object a, object b)
        {
            return string.Equals(ToCanonicalJson(a), ToCanonicalJson(b), StringComparison.Ordinal);
        }

        private static JToken OrderToken(JToken token)
        {
            if (token == null) return JValue.CreateNull();
            switch (token.Type)
            {
                case JTokenType.Object:
                    var obj = (JObject)token;
                    var orderedProps = obj.Properties()
                        .OrderBy(p => p.Name, StringComparer.Ordinal)
                        .ToArray();
                    obj.RemoveAll();
                    foreach (var p in orderedProps)
                    {
                        obj.Add(p.Name, OrderToken(p.Value));
                    }
                    return obj;
                case JTokenType.Array:
                    var arr = (JArray)token;
                    for (int i = 0; i < arr.Count; i++) arr[i] = OrderToken(arr[i]);
                    return arr;
                case JTokenType.Property:
                    var prop = (JProperty)token;
                    prop.Value = OrderToken(prop.Value);
                    return prop;
                case JTokenType.Null:
                    return JValue.CreateNull();
                default:
                    return token;
            }
        }

        private static readonly JsonSerializerSettings CanonicalJsonSettings = new JsonSerializerSettings
        {
            Culture = CultureInfo.InvariantCulture,
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include,
            DefaultValueHandling = DefaultValueHandling.Include,
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
            TypeNameHandling = TypeNameHandling.None,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind,
            DateParseHandling = DateParseHandling.None,
            FloatFormatHandling = FloatFormatHandling.String,
            FloatParseHandling = FloatParseHandling.Double
        };
    }
}
