#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A Unity fileID is a SIGNED 64-bit integer. A file containing one that exceeds
    /// <c>long.MaxValue</c> does not degrade gracefully — Unity's
    /// <c>SerializedFile::IndexTextFile</c> fails the WHOLE file with
    /// <c>Could not extract 'FileID' … This number overflows internal type</c>, and every
    /// reference inside it then resolves as <c>Broken text PPtr</c>. The prefab still sits
    /// in the project looking fine; nothing reports it until something tries to load it.
    ///
    /// This is a hand-authoring hazard, not an editor one: the editor cannot produce an
    /// out-of-range id, but a script that mints one (asset surgery, a generator, a merge
    /// fixup) can, and did — <c>Manta.prefab</c> and <c>Serpent.prefab</c> each carried one
    /// for weeks (<c>9678703874602163012</c> / <c>9900976137657699045</c>), written by two
    /// separate tool passes, and only surfaced when a new auditor tried to open them.
    ///
    /// So the gate is a text scan, deliberately cheap and deliberately whole-project: it
    /// reads every serialized text asset once and fails naming the file, the id, and the
    /// line. Anything that hand-writes a fileID is covered by construction, forever,
    /// without that tool having to remember the rule.
    /// </summary>
    public class SerializedFileIdIntegrityTests
    {
        static readonly string[] SerializedExtensions =
        {
            ".prefab", ".unity", ".asset", ".mat", ".controller", ".playable", ".shadergraph",
        };

        // Anchors (`--- !u!114 &<id>`) and references (`fileID: <id>`) alike — both sides
        // of the reference have to fit, and an id long enough to be at risk is always ≥ 19
        // digits, so the cheap length pre-filter keeps this to one pass over the text.
        static readonly Regex IdPattern = new Regex(
            @"(?:^--- !u!\d+ &(\d{19,})|fileID: (\d{19,}))", RegexOptions.Multiline | RegexOptions.Compiled);

        [Test]
        public void NoSerializedAsset_CarriesAFileIdThatOverflowsInt64()
        {
            var failures = new List<string>();
            string assets = UnityEngine.Application.dataPath;

            foreach (var path in Directory.EnumerateFiles(assets, "*.*", SearchOption.AllDirectories))
            {
                bool serialized = false;
                foreach (var ext in SerializedExtensions)
                {
                    if (path.EndsWith(ext, System.StringComparison.OrdinalIgnoreCase)) { serialized = true; break; }
                }
                if (!serialized) continue;

                string text;
                try { text = File.ReadAllText(path); }
                catch { continue; }

                foreach (Match m in IdPattern.Matches(text))
                {
                    string raw = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                    // ulong.TryParse rather than BigInteger: a value too large even for
                    // ulong fails the parse, which is itself a failure — so both the
                    // "overflows long" and "absurdly long" cases fall out of one check.
                    if (ulong.TryParse(raw, out ulong value) && value <= long.MaxValue) continue;
                    int line = 1;
                    for (int i = 0; i < m.Index && i < text.Length; i++)
                        if (text[i] == '\n') line++;
                    failures.Add($"{path.Substring(assets.Length - "Assets".Length)}:{line} — fileID {raw}");
                }
            }

            Assert.IsEmpty(failures,
                "Serialized asset(s) carry a fileID larger than long.MaxValue (9223372036854775807). Unity " +
                "cannot parse the containing file AT ALL — it reports \"Could not extract 'FileID' … overflows " +
                "internal type\" and every reference inside becomes a broken PPtr, with no symptom until " +
                "something loads it. Fix: rewrite the id (anchor AND every reference, whole-word) to an " +
                "in-range value, then re-check that the file's dangling-reference set is unchanged.\n" +
                string.Join("\n", failures));
        }
    }
}
#endif
