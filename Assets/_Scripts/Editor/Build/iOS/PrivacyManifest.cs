using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Reads, merges and writes an Apple privacy manifest (<c>PrivacyInfo.xcprivacy</c>, an XML plist), for
    /// <see cref="IosPrivacyManifestPostprocess"/>. Pure .NET (System.Xml.Linq), so it compiles and is tested without iOS
    /// Build Support. Only the four manifest keys are interpreted; any other key in a manifest being merged into is kept
    /// as it was.
    ///
    /// <para>The merge is a union: tracking if either says so, every tracking domain, every collected data type (linked
    /// or tracking if either says so, every purpose), every accessed API category with every reason. A merge never
    /// drops a declaration, so merging into a manifest Unity wrote can only add ours to it.</para>
    /// </summary>
    public sealed class PrivacyManifest
    {
        public const string FileName = "PrivacyInfo.xcprivacy";

        const string KTracking = "NSPrivacyTracking";
        const string KDomains = "NSPrivacyTrackingDomains";
        const string KCollected = "NSPrivacyCollectedDataTypes";
        const string KCollectedType = "NSPrivacyCollectedDataType";
        const string KLinked = "NSPrivacyCollectedDataTypeLinked";
        const string KTrackingType = "NSPrivacyCollectedDataTypeTracking";
        const string KPurposes = "NSPrivacyCollectedDataTypePurposes";
        const string KAccessed = "NSPrivacyAccessedAPITypes";
        const string KApiType = "NSPrivacyAccessedAPIType";
        const string KReasons = "NSPrivacyAccessedAPITypeReasons";
        static readonly string[] ManifestKeys = { KTracking, KDomains, KCollected, KAccessed };

        public sealed class CollectedType
        {
            public string Type;
            public bool Linked;
            public bool Tracking;
            public readonly SortedSet<string> Purposes = new(StringComparer.Ordinal);
        }

        public bool Tracking;
        public readonly SortedSet<string> TrackingDomains = new(StringComparer.Ordinal);
        /// <summary>Keyed by the data type, in the order first seen.</summary>
        public readonly List<CollectedType> Collected = new();
        /// <summary>API category -> reasons, in the order first seen.</summary>
        public readonly List<KeyValuePair<string, SortedSet<string>>> AccessedApis = new();

        public IReadOnlyCollection<string> ReasonsFor(string category) =>
            AccessedApis.FirstOrDefault(p => p.Key == category).Value ?? new SortedSet<string>();

        public CollectedType CollectedOf(string type) => Collected.FirstOrDefault(c => c.Type == type);

        // ── Reading ─────────────────────────────────────────────────────────────

        public static PrivacyManifest Parse(string xml) => FromDict(RootDict(Load(xml)));

        /// <summary>Plists carry a DOCTYPE: it is read (and kept on write) but never fetched.</summary>
        static XDocument Load(string xml)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null, IgnoreWhitespace = true };
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            return XDocument.Load(reader);
        }

        static XElement RootDict(XDocument doc) =>
            doc.Root?.Element("dict") ?? throw new FormatException("Not a plist with a root <dict>.");

        static PrivacyManifest FromDict(XElement dict)
        {
            var m = new PrivacyManifest();
            foreach (var (key, value) in Entries(dict))
            {
                switch (key)
                {
                    case KTracking: m.Tracking = value.Name.LocalName == "true"; break;
                    case KDomains: foreach (var s in Strings(value)) m.TrackingDomains.Add(s); break;
                    case KCollected:
                        foreach (var d in value.Elements("dict"))
                        {
                            var c = new CollectedType();
                            foreach (var (k, v) in Entries(d))
                            {
                                if (k == KCollectedType) c.Type = v.Value.Trim();
                                else if (k == KLinked) c.Linked = v.Name.LocalName == "true";
                                else if (k == KTrackingType) c.Tracking = v.Name.LocalName == "true";
                                else if (k == KPurposes) foreach (var s in Strings(v)) c.Purposes.Add(s);
                            }
                            if (!string.IsNullOrEmpty(c.Type)) m.AddCollected(c);
                        }
                        break;
                    case KAccessed:
                        foreach (var d in value.Elements("dict"))
                        {
                            string type = null;
                            var reasons = new List<string>();
                            foreach (var (k, v) in Entries(d))
                            {
                                if (k == KApiType) type = v.Value.Trim();
                                else if (k == KReasons) reasons.AddRange(Strings(v));
                            }
                            if (!string.IsNullOrEmpty(type)) m.AddApi(type, reasons);
                        }
                        break;
                }
            }
            return m;
        }

        static IEnumerable<(string key, XElement value)> Entries(XElement dict)
        {
            var children = dict.Elements().ToList();
            for (int i = 0; i + 1 < children.Count; i++)
            {
                if (children[i].Name.LocalName != "key") continue;
                yield return (children[i].Value.Trim(), children[i + 1]);
                i++;
            }
        }

        static IEnumerable<string> Strings(XElement array) =>
            array.Elements("string").Select(e => e.Value.Trim()).Where(s => s.Length > 0);

        // ── Merging ─────────────────────────────────────────────────────────────

        void AddCollected(CollectedType add)
        {
            var have = CollectedOf(add.Type);
            if (have == null)
            {
                have = new CollectedType { Type = add.Type };
                Collected.Add(have);
            }
            have.Linked |= add.Linked;
            have.Tracking |= add.Tracking;
            foreach (var p in add.Purposes) have.Purposes.Add(p);
        }

        void AddApi(string category, IEnumerable<string> reasons)
        {
            int i = AccessedApis.FindIndex(p => p.Key == category);
            if (i < 0)
            {
                AccessedApis.Add(new KeyValuePair<string, SortedSet<string>>(category, new SortedSet<string>(StringComparer.Ordinal)));
                i = AccessedApis.Count - 1;
            }
            foreach (var r in reasons) AccessedApis[i].Value.Add(r);
        }

        /// <summary>A new manifest holding everything <paramref name="a"/> and <paramref name="b"/> declare.</summary>
        public static PrivacyManifest Union(PrivacyManifest a, PrivacyManifest b)
        {
            var m = new PrivacyManifest { Tracking = a.Tracking || b.Tracking };
            foreach (var src in new[] { a, b })
            {
                foreach (var d in src.TrackingDomains) m.TrackingDomains.Add(d);
                foreach (var c in src.Collected) m.AddCollected(c);
                foreach (var p in src.AccessedApis) m.AddApi(p.Key, p.Value);
            }
            return m;
        }

        /// <summary>
        /// <paramref name="addXml"/>'s declarations merged into <paramref name="baseXml"/>, as plist XML. Keys of the
        /// base other than the four manifest keys are kept untouched; a null or empty base gives <paramref name="addXml"/>
        /// re-written.
        /// </summary>
        public static string Merge(string baseXml, string addXml)
        {
            var add = Parse(addXml);
            if (string.IsNullOrWhiteSpace(baseXml)) return add.ToXml(null);

            var doc = Load(baseXml);
            var merged = Union(FromDict(RootDict(doc)), add);
            return merged.ToXml(doc);
        }

        // ── Writing ─────────────────────────────────────────────────────────────

        /// <summary>This manifest as plist XML; with <paramref name="into"/>, its four keys replace that document's and its
        /// other keys stay.</summary>
        public string ToXml(XDocument into)
        {
            var doc = into ?? new XDocument(
                new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
                new XElement("plist", new XAttribute("version", "1.0"), new XElement("dict")));
            var dict = RootDict(doc);

            // A DOCTYPE read back from a file carries an empty internal subset, written as "[]": drop it so a re-run
            // writes the same bytes.
            if (doc.DocumentType is { } doctype && string.IsNullOrEmpty(doctype.InternalSubset))
                doctype.InternalSubset = null;

            foreach (var (key, value) in Entries(dict).ToList())
                if (ManifestKeys.Contains(key))
                {
                    value.Remove();
                    dict.Elements("key").First(k => k.Value.Trim() == key).Remove();
                }

            dict.Add(Key(KTracking), Bool(Tracking));
            dict.Add(Key(KDomains), new XElement("array", TrackingDomains.Select(Str)));
            dict.Add(Key(KCollected), new XElement("array", Collected.Select(c => new XElement("dict",
                Key(KCollectedType), Str(c.Type),
                Key(KLinked), Bool(c.Linked),
                Key(KTrackingType), Bool(c.Tracking),
                Key(KPurposes), new XElement("array", c.Purposes.Select(Str))))));
            dict.Add(Key(KAccessed), new XElement("array", AccessedApis.Select(p => new XElement("dict",
                Key(KApiType), Str(p.Key),
                Key(KReasons), new XElement("array", p.Value.Select(Str))))));

            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                IndentChars = "\t",
                NewLineChars = "\n",
            };
            using var stream = new MemoryStream();
            using (var writer = XmlWriter.Create(stream, settings)) doc.Save(writer);
            return new UTF8Encoding(false).GetString(stream.ToArray()) + "\n";
        }

        static XElement Key(string k) => new("key", k);
        static XElement Str(string s) => new("string", s);
        static XElement Bool(bool b) => new(b ? "true" : "false");
    }
}
