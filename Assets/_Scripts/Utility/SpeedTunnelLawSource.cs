#if UNITY_EDITOR
using System.Text.RegularExpressions;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The predicates that decide whether the speed-tunnel PLATFORM LAW is still enforced by the
    /// SOURCE and the ASSETS (Docs/SPEED_TUNNEL.md §2 layer 4).
    ///
    /// They live here — in the runtime assembly, editor-only — for one reason: the two gates that
    /// need them cannot see each other. The FrogletTools validator compiles into
    /// Assembly-CSharp-Editor and the edit-mode tests compile into Assembly-CSharp, which cannot
    /// reference it. Writing the rule twice is how the asset gate and the test gate drift apart,
    /// so the rule is written once, here, and both call it (the
    /// <c>PrismOcclusionDiagnostics.IsCorridorCapable</c> pattern).
    ///
    /// Pure string analysis, no UnityEditor and no UnityEngine — the whole file is guarded rather
    /// than living under an Editor/ folder so the runtime tests can reach it
    /// (Docs/CONDITIONAL_COMPILATION.md pattern 2).
    /// </summary>
    public static class SpeedTunnelLawSource
    {
        /// <summary>
        /// Script GUID of the retired per-vessel driver (<c>SpeedTunnelEffectController</c>),
        /// taken from its deleted .cs.meta.
        ///
        /// This, not the class name, is what a prefab still carrying that component looks like.
        /// Unity identifies a script in prefab YAML ONLY by GUID —
        /// <c>m_Script: {fileID: 11500000, guid: …, type: 3}</c> with an empty
        /// <c>m_EditorClassIdentifier</c> — so the type name never appears in the file, and with
        /// the class deleted the component also deserializes to a NULL entry that a
        /// <c>GetComponentsInChildren</c> sweep skips. A gate that searches for the type name is
        /// therefore vacuous: it can never fire, on exactly the state it was written to catch.
        /// </summary>
        public const string RetiredDriverScriptGuid = "111cfe0b6e1549e4be45e6edb1bf695e";

        /// <summary>The one bind call the law is allowed to have.</summary>
        public const string BindCall = "VesselSpeedTunnel.SetTarget";

        /// <summary>The gate every bind call must sit under.</summary>
        public const string LocalPilotGate = "IsLocalPilot";

        /// <summary>
        /// Characters of source allowed between a bind call and the <c>IsLocalPilot</c> guard
        /// above it. Generous enough for a guard with a comment block and a brace between it and
        /// the call, far too small to reach out of the enclosing method.
        /// </summary>
        const int GateProximityChars = 400;

        /// <summary>True if the prefab/scene text still references the retired driver.</summary>
        public static bool ReferencesRetiredDriver(string assetText) =>
            !string.IsNullOrEmpty(assetText) && assetText.Contains(RetiredDriverScriptGuid);

        /// <summary>
        /// EVERY <c>VesselSpeedTunnel.SetTarget</c> call site in <paramref name="controllerSource"/>
        /// sits under an <c>IsLocalPilot</c> guard.
        ///
        /// A whole-file <c>Contains("IsLocalPilot")</c> cannot express this and silently stopped
        /// being a gate the moment <c>ChangePlayer</c> grew a second occurrence: deleting the
        /// guard around the <c>Initialize</c> binding would bind the tunnel to every remote and
        /// AI vessel — letting someone else's boost drive your camera — while still satisfying
        /// the substring.
        /// </summary>
        public static bool EveryBindIsGatedOnLocalPilot(string controllerSource, out string reason)
        {
            if (string.IsNullOrEmpty(controllerSource))
            {
                reason = "VesselController source is empty or unreadable.";
                return false;
            }

            int found = 0;
            for (int i = controllerSource.IndexOf(BindCall, System.StringComparison.Ordinal);
                 i >= 0;
                 i = controllerSource.IndexOf(BindCall, i + 1, System.StringComparison.Ordinal))
            {
                found++;
                int windowStart = i - GateProximityChars;
                if (windowStart < 0) windowStart = 0;
                string window = controllerSource.Substring(windowStart, i - windowStart);
                if (window.IndexOf(LocalPilotGate, System.StringComparison.Ordinal) < 0)
                {
                    reason = $"a {BindCall} call site has no {LocalPilotGate} guard within " +
                             $"{GateProximityChars} characters above it — the law would bind to " +
                             "remote and AI vessels.";
                    return false;
                }
            }

            if (found == 0)
            {
                reason = $"no {BindCall} call site at all — the law is bound nowhere, so no " +
                         "vessel tunnels.";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>The drive call with raw measured speed and nothing else.</summary>
        public const string RawDrive = "config.Effect01(_target.Speed)";

        /// <summary>
        /// The drive call inside a warp field (Docs/WARP_FIELD.md): measured speed with the field's
        /// scale at the hull divided back out. A field multiplies a vessel's WORLD speed by
        /// <c>s</c> while shrinking its hull, camera and trail by the same <c>s</c>, so this is
        /// the speed in the pilot's own frame, and exactly the raw speed with no field
        /// (<c>ScaleAt</c> returns 1). It is the one divisor the law admits: <c>s</c> is a function
        /// of POSITION, the same for every vessel at that point, so "the same speed on any vessel
        /// looks the same" still holds.
        /// </summary>
        public const string FeltDrive = "config.Effect01(_target.Speed / warp)";

        // The only sanctioned definition of `warp`: a read of the field at a position, optionally
        // null-guarded on the hull (`hull ? WarpFieldRuntime.ScaleAt(hull.position) : 1f`). Tight
        // on purpose, so a vessel term cannot ride in on the divisor instead of the call
        // (`... ScaleAt(hull.position) * topSpeed`, `... : vesselTopSpeed`).
        static readonly Regex WarpDeclaration = new(
            @"\bfloat\s+warp\s*=\s*(?:\w+\s*\?\s*)?WarpFieldRuntime\.ScaleAt\(\s*[\w.]+\s*\)\s*(?::\s*1f\s*)?;");

        // Any write to `warp` — its declaration included, so exactly one is allowed.
        static readonly Regex WarpWrite = new(@"\bwarp\s*[-+*/]?=(?!=)");

        static readonly Regex Effect01Call = new(@"\bEffect01\s*\(");

        /// <summary>
        /// EVERY <c>Effect01</c> drive call passes measured speed: raw (<see cref="RawDrive"/>), or
        /// with the warp field's scale divided back out (<see cref="FeltDrive"/>) where
        /// <c>warp</c> is defined once, as a read of the field and nothing else.
        ///
        /// This is what actually keeps the mapping absolute. Asserting the shape of
        /// <c>SpeedTunnelConfigSO.Effect01</c> is not enough: a normalization can be introduced
        /// at the CALL SITE (<c>Effect01(_target.Speed / vesselTopSpeed * …)</c>) with every
        /// signature test still green, and that would make the same speed look different on
        /// different vessels — the one property the law exists to guarantee.
        ///
        /// Comments are stripped first, so a commented-out raw call cannot stand in for a live
        /// one, and every call is checked rather than "the file contains one good call", so a
        /// second, normalized drive cannot sit beside a sanctioned one.
        /// </summary>
        public static bool DriveSiteUsesRawSpeed(string driverSource, out string reason)
        {
            if (string.IsNullOrEmpty(driverSource))
            {
                reason = "VesselSpeedTunnel source is empty or unreadable.";
                return false;
            }

            string code = StripComments(driverSource);

            int calls = Effect01Call.Matches(code).Count;
            int raw = CountOrdinal(code, RawDrive);
            int felt = CountOrdinal(code, FeltDrive);

            if (calls == 0 || raw + felt != calls)
            {
                reason = $"a drive site is neither {RawDrive} nor {FeltDrive} ({calls} Effect01 " +
                         $"call(s), {raw + felt} sanctioned). The mapping must be a pure function " +
                         "of measured speed — anything per-vessel folded in here breaks 'the same " +
                         "speed on any vessel looks the same'.";
                return false;
            }

            if (felt > 0 &&
                (WarpDeclaration.Matches(code).Count != 1 || WarpWrite.Matches(code).Count != 1))
            {
                reason = $"{FeltDrive} divides by a `warp` that is not defined exactly once as " +
                         "`float warp = WarpFieldRuntime.ScaleAt(<position>);` (optionally " +
                         "`hull ? ... : 1f`) and never re-assigned. The warp field's scale is the " +
                         "only divisor the law admits — it is a function of position, the same " +
                         "for every vessel; any other term there is a per-vessel normalization.";
                return false;
            }

            reason = null;
            return true;
        }

        static string StripComments(string source)
        {
            string text = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(text, @"//.*?$", string.Empty, RegexOptions.Multiline);
        }

        static int CountOrdinal(string text, string needle)
        {
            int count = 0;
            for (int i = text.IndexOf(needle, System.StringComparison.Ordinal);
                 i >= 0;
                 i = text.IndexOf(needle, i + needle.Length, System.StringComparison.Ordinal))
                count++;
            return count;
        }
    }
}
#endif
