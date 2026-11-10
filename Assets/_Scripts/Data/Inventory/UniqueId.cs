using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// The id every unique pilot and artifact carries (Docs/Pilots/INVENTORY.md §1): its TEMPLATE id, a '#', and the
    /// 16 hex characters of its 64-bit seed, e.g. <c>pilot.callsign.volt#9f3c01d2a4b85e77</c> or
    /// <c>artifact.javelin#03aa91c0f2e6d418</c>.
    ///
    /// <para><b>Why the template rides in the id.</b> A squad slot, a network code, an Ark's hold, a wreck and a friend's
    /// public squad record all pass ids around without the record behind them. With the template inside, anyone can
    /// still answer "which callsign is this pilot?" and "which payload does this artifact make?" (the catalog strips the
    /// suffix, <see cref="TemplateOf"/>), and an artifact's rolled traits can be re-rolled from its seed. A bare template
    /// id with no '#' is a LEGACY id from before pilots and artifacts were unique (squads saved 2026-10-27..11-07); the
    /// inventory resolves it to the crew's pilot of that callsign or the hoard's first artifact of that kind.</para>
    /// </summary>
    public static class UniqueId
    {
        public const char Separator = '#';

        /// <summary><paramref name="templateId"/>#<paramref name="seed"/> in hex.</summary>
        public static string Compose(string templateId, ulong seed) =>
            (templateId ?? string.Empty) + Separator + SeededRandom.ToHex(seed);

        /// <summary>The template part of <paramref name="id"/> (the whole id when it has no '#'); empty for null.</summary>
        public static string TemplateOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;
            int cut = id.IndexOf(Separator);
            return cut < 0 ? id : id.Substring(0, cut);
        }

        /// <summary>True when <paramref name="id"/> is a unique id (template#seed) rather than a bare template id.</summary>
        public static bool IsUnique(string id) => TrySplit(id, out _, out _);

        /// <summary>Splits a unique id. False for a bare template id, an empty one or a malformed seed.</summary>
        public static bool TrySplit(string id, out string templateId, out ulong seed)
        {
            templateId = string.Empty;
            seed = 0;
            if (string.IsNullOrEmpty(id)) return false;
            int cut = id.IndexOf(Separator);
            if (cut <= 0 || cut != id.LastIndexOf(Separator)) return false;
            if (!SeededRandom.TryParseHex(id.Substring(cut + 1), out seed)) return false;
            templateId = id.Substring(0, cut);
            return true;
        }

        /// <summary>The short serial players read on a card: the first 8 hex characters of the seed, upper case, split
        /// in two ("9F3C-01D2"). Empty for a bare template id.</summary>
        public static string SerialOf(string id)
        {
            if (!TrySplit(id, out _, out ulong seed)) return string.Empty;
            string hex = SeededRandom.ToHex(seed).ToUpperInvariant();
            return hex.Substring(0, 4) + "-" + hex.Substring(4, 4);
        }

        /// <summary>Milliseconds since the Unix epoch, UTC (the cloud records' timestamp convention).</summary>
        public static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
