using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// The fixed set of callsigns (Docs/Pilots/CALLSIGNS.md §2): each callsign is ONE vision power, and every pilot
    /// carries exactly one. A callsign is shared by every crew in the game (two crews can each have a VOLT); the person
    /// who carries it is unique (<see cref="PilotRecord"/>).
    ///
    /// <para>The callsign's id is the <c>PilotSO.Id</c> that <c>Tools/Build/author_pilot_assets.py</c> writes,
    /// <c>pilot.callsign.&lt;word&gt;</c>, so the pure data layer can name a callsign without loading an asset.</para>
    /// </summary>
    public static class Callsigns
    {
        public const string IdPrefix = "pilot.callsign.";

        static PilotVision[] _all;

        /// <summary>Every callsign, in the enum's order (None excluded).</summary>
        public static IReadOnlyList<PilotVision> All
        {
            get
            {
                if (_all != null) return _all;
                var list = new List<PilotVision>();
                foreach (PilotVision v in Enum.GetValues(typeof(PilotVision)))
                    if (v != PilotVision.None) list.Add(v);
                list.Sort((a, b) => ((int)a).CompareTo((int)b));
                _all = list.ToArray();
                return _all;
            }
        }

        /// <summary>The callsign's id, e.g. <c>pilot.callsign.volt</c>; empty for None.</summary>
        public static string IdOf(PilotVision vision) =>
            vision == PilotVision.None ? string.Empty : IdPrefix + vision.ToString().ToLowerInvariant();

        /// <summary>The callsign's word as players read it, e.g. VOLT.</summary>
        public static string WordOf(PilotVision vision) =>
            vision == PilotVision.None ? string.Empty : vision.ToString().ToUpperInvariant();

        /// <summary>True when <paramref name="id"/> names a callsign (a legacy squad slot saved one directly).</summary>
        public static bool IsCallsignId(string id) =>
            !string.IsNullOrEmpty(id) && id.StartsWith(IdPrefix, StringComparison.Ordinal);

        /// <summary>The callsign <paramref name="id"/> names. False for anything that is not a callsign id.</summary>
        public static bool TryParse(string id, out PilotVision vision)
        {
            vision = PilotVision.None;
            if (!IsCallsignId(id)) return false;
            string word = id.Substring(IdPrefix.Length);
            foreach (var v in All)
                if (string.Equals(v.ToString(), word, StringComparison.OrdinalIgnoreCase))
                {
                    vision = v;
                    return true;
                }
            return false;
        }
    }
}
