using System;
using System.Collections.Generic;
using System.Text;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A control a prompt can name that is not an ability: the flight inputs every hull has.
    /// Its label is authored per flight scheme and device in <c>DrillLibrarySO.Strings</c>.
    /// </summary>
    public enum DrillControl
    {
        Steer = 0,
        Throttle = 1,
        Drift = 2,
    }

    /// <summary>The facts a prompt's tokens are filled from - supplied by the composer's caller.</summary>
    public interface IDrillTokenSource
    {
        /// <summary>The hull's player-facing name.</summary>
        string VesselName { get; }

        /// <summary>The card's player-facing name.</summary>
        string ModeName { get; }

        /// <summary>The hull's ability for <paramref name="element"/>, from its ability map. False
        /// for an open design slot (no label authored).</summary>
        bool TryAbility(Element element, out string label, out string description);

        /// <summary>The label of the control the <paramref name="element"/> ability is bound to on
        /// the player's device. False for a passive ability or a control with no label.</summary>
        bool TryAbilityControlLabel(Element element, out string label);

        /// <summary>The authored label of a flight control for this hull's scheme and the player's device.</summary>
        bool TryControlLabel(DrillControl control, out string label);
    }

    /// <summary>How a template resolved.</summary>
    public enum DrillTokenOutcome
    {
        /// <summary>Every token was filled.</summary>
        Complete = 0,
        /// <summary>A token named a fact this hull does not have (an open ability slot, a passive
        /// ability's control). The line cannot be said honestly; the composer drops it.</summary>
        MissingFact = 1,
        /// <summary>A token the resolver does not know - a typo. Rendered as itself so it is seen.</summary>
        UnknownToken = 2,
    }

    /// <summary>
    /// Fills a prompt template's tokens. The SENTENCE is always authored; this only puts facts
    /// into it (TRAINING_PLAN §4.2.1).
    ///
    /// <para>Tokens: <c>{vessel}</c>, <c>{mode}</c>, <c>{ability:E}</c>, <c>{abilityDescription:E}</c>,
    /// <c>{glyph:E}</c> for an element E (Charge, Mass, Space, Time), and <c>{glyph:C}</c> for a
    /// flight control C (<see cref="DrillControl"/>: Steer, Throttle, Drift).</para>
    ///
    /// <para>Two failures, kept apart on purpose: a MISSING FACT (the hull has no such ability)
    /// is an honest absence and drops the line; an UNKNOWN TOKEN is an authoring mistake and
    /// renders visibly as itself, logged once, so a typo is seen rather than silently eaten.
    /// A missing fact outranks an unknown token when a line has both.</para>
    /// </summary>
    public static class DrillTokens
    {
        static readonly HashSet<string> Reported = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Reported.Clear();

        public static string Resolve(string template, IDrillTokenSource source, out DrillTokenOutcome outcome)
        {
            outcome = DrillTokenOutcome.Complete;
            if (string.IsNullOrEmpty(template)) return string.Empty;
            if (template.IndexOf('{') < 0) return template;

            var sb = new StringBuilder(template.Length + 16);
            int i = 0;
            while (i < template.Length)
            {
                char c = template[i];
                int close = c == '{' ? template.IndexOf('}', i + 1) : -1;
                if (close < 0)
                {
                    sb.Append(c);
                    i++;
                    continue;
                }

                string token = template.Substring(i + 1, close - i - 1);
                switch (TryFill(token, source, out string value))
                {
                    case DrillTokenOutcome.Complete:
                        sb.Append(value);
                        break;
                    case DrillTokenOutcome.MissingFact:
                        outcome = DrillTokenOutcome.MissingFact;
                        break;
                    default:
                        sb.Append('{').Append(token).Append('}');
                        if (outcome == DrillTokenOutcome.Complete) outcome = DrillTokenOutcome.UnknownToken;
                        ReportUnknown(token, template);
                        break;
                }
                i = close + 1;
            }
            return sb.ToString();
        }

        /// <summary>True when <paramref name="token"/> (without braces) names something the
        /// resolver understands. The authoring tool's validator uses this.</summary>
        public static bool IsKnownToken(string token) => Parse(token, out _, out _, out _) != TokenKind.Unknown;

        enum TokenKind { Unknown, Vessel, Mode, Ability, AbilityDescription, AbilityGlyph, ControlGlyph }

        static DrillTokenOutcome TryFill(string token, IDrillTokenSource source, out string value)
        {
            value = null;
            var kind = Parse(token, out var element, out var control, out _);
            if (kind == TokenKind.Unknown) return DrillTokenOutcome.UnknownToken;
            if (source == null) return DrillTokenOutcome.MissingFact;

            bool ok;
            switch (kind)
            {
                case TokenKind.Vessel: value = source.VesselName; ok = !string.IsNullOrEmpty(value); break;
                case TokenKind.Mode: value = source.ModeName; ok = !string.IsNullOrEmpty(value); break;
                case TokenKind.Ability:
                    ok = source.TryAbility(element, out value, out _) && !string.IsNullOrEmpty(value);
                    break;
                case TokenKind.AbilityDescription:
                    ok = source.TryAbility(element, out _, out value) && !string.IsNullOrEmpty(value);
                    break;
                case TokenKind.AbilityGlyph:
                    ok = source.TryAbilityControlLabel(element, out value) && !string.IsNullOrEmpty(value);
                    break;
                default:
                    ok = source.TryControlLabel(control, out value) && !string.IsNullOrEmpty(value);
                    break;
            }
            return ok ? DrillTokenOutcome.Complete : DrillTokenOutcome.MissingFact;
        }

        static TokenKind Parse(string token, out Element element, out DrillControl control, out string arg)
        {
            element = Element.None;
            control = default;
            arg = null;
            if (string.IsNullOrEmpty(token)) return TokenKind.Unknown;

            int colon = token.IndexOf(':');
            string name = colon < 0 ? token : token.Substring(0, colon);
            arg = colon < 0 ? null : token.Substring(colon + 1);

            switch (name)
            {
                case "vessel": return arg == null ? TokenKind.Vessel : TokenKind.Unknown;
                case "mode": return arg == null ? TokenKind.Mode : TokenKind.Unknown;
                case "ability":
                    return TryElement(arg, out element) ? TokenKind.Ability : TokenKind.Unknown;
                case "abilityDescription":
                    return TryElement(arg, out element) ? TokenKind.AbilityDescription : TokenKind.Unknown;
                case "glyph":
                    if (TryElement(arg, out element)) return TokenKind.AbilityGlyph;
                    if (arg != null && !IsNumeric(arg) && Enum.TryParse(arg, false, out control) &&
                        Enum.IsDefined(typeof(DrillControl), control))
                        return TokenKind.ControlGlyph;
                    return TokenKind.Unknown;
                default:
                    return TokenKind.Unknown;
            }
        }

        // Enum.TryParse accepts "4" as Time; a numeric argument is a typo, never an element.
        static bool TryElement(string arg, out Element element)
        {
            element = Element.None;
            if (string.IsNullOrEmpty(arg) || IsNumeric(arg)) return false;
            if (!Enum.TryParse(arg, false, out element)) return false;
            return element is Element.Charge or Element.Mass or Element.Space or Element.Time;
        }

        static bool IsNumeric(string s) => s.Length > 0 && (char.IsDigit(s[0]) || s[0] == '-');

        static void ReportUnknown(string token, string template)
        {
            if (!Reported.Add(token)) return;
            CSDebug.LogWarning($"[Drill] Unknown token '{{{token}}}' in \"{template}\" - shown as written. " +
                               "Known: {vessel} {mode} {ability:E} {abilityDescription:E} {glyph:E} {glyph:Steer|Throttle|Drift}.");
        }
    }
}
