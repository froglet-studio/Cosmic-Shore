using System;
using System.Collections.Generic;
using System.Text;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Utility
{
    /// <summary>Why a display name was rejected. <see cref="None"/> means the name passed.</summary>
    public enum DisplayNameError
    {
        None = 0,
        Empty = 1,
        TooShort = 2,
        TooLong = 3,
        InvalidCharacters = 4,
        InvalidFormat = 5,
        Inappropriate = 6,
        Reserved = 7,
        Taken = 8,
        ServiceUnavailable = 9,
    }

    /// <summary>
    /// Outcome of a display-name validation or change attempt. On success,
    /// <see cref="SanitizedName"/> carries the cleaned name that was (or should be) saved —
    /// trimmed, with internal whitespace runs collapsed to single spaces.
    /// </summary>
    public readonly struct DisplayNameValidationResult
    {
        public DisplayNameError Error { get; }
        public string Message { get; }
        public string SanitizedName { get; }

        public bool IsValid => Error == DisplayNameError.None;

        DisplayNameValidationResult(DisplayNameError error, string message, string sanitizedName)
        {
            Error = error;
            Message = message;
            SanitizedName = sanitizedName;
        }

        public static DisplayNameValidationResult Success(string sanitizedName) =>
            new(DisplayNameError.None, string.Empty, sanitizedName);

        public static DisplayNameValidationResult Fail(DisplayNameError error, string message) =>
            new(error, message, string.Empty);
    }

    /// <summary>
    /// The one place display names are judged. Enforces length, allowed characters and
    /// format from <see cref="DisplayNameValidationConfigSO"/>, and rejects slurs and
    /// profanity via built-in deny lists that survive leetspeak (n1gg3r), separator
    /// padding (f.u.c.k), casing, and letter repetition (niiigger).
    ///
    /// Two matching tiers keep the filter strict without Scunthorpe-style collateral:
    /// unambiguous terms are blocked anywhere in the name, while short ambiguous terms
    /// ("ass", "coon", "jap") are blocked only as whole words — so "Cassandra",
    /// "Raccoon" and "Japan" stay legal.
    ///
    /// Every UI that lets a player enter a name must route through
    /// <c>PlayerDataService.TrySetDisplayNameAsync</c>, which calls this first.
    /// </summary>
    public static class DisplayNameValidator
    {
        static DisplayNameValidationConfigSO _config;

        /// <summary>Shared config (Resources asset, or in-memory defaults if missing).</summary>
        public static DisplayNameValidationConfigSO Config
        {
            get
            {
                if (_config == null)
                    _config = DisplayNameValidationConfigSO.LoadOrDefault();
                return _config;
            }
        }

        /// <summary>Test seam / hot-swap: pass null to fall back to the Resources asset.</summary>
        public static void SetConfigOverride(DisplayNameValidationConfigSO config) => _config = config;

        // ── Built-in deny lists ─────────────────────────────────────────────
        // These live in code, not the config asset, so the safety floor cannot be
        // emptied by an asset edit; the config only ADDS terms or allowlists names.

        /// <summary>
        /// High-severity or unambiguous terms blocked ANYWHERE in a name, matched as a
        /// substring after normalization (lowercase, leet-mapped, separators stripped).
        /// </summary>
        static readonly string[] BlockedAnywhere =
        {
            // racial / ethnic slurs
            "nigger", "nigga", "niggah", "negroid", "kike", "polack",
            "wetback", "raghead", "towelhead", "zipperhead", "porchmonkey",
            "chingchong", "currymuncher", "junglebunny", "halfbreed", "sandnigger",
            // hate / extremist
            "nazi", "hitler", "heilhitler", "siegheil", "swastika",
            "whitepower", "kuklux", "klansman", "kkk", "holocaust",
            // sexual / explicit / degrading
            "fuck", "cunt", "bitch", "whore", "slut", "faggot", "pussy",
            "penis", "vagina", "blowjob", "handjob", "dildo", "cocksucker",
            "dickhead", "jerkoff", "cumshot", "jizz", "nutsack", "ballsack",
            "shemale", "tranny", "molest", "pedophile", "paedophile", "childporn",
            "retard", "shit", "twat", "douche", "bastard", "asshole", "arsehole",
        };

        /// <summary>
        /// Shorter ambiguous terms blocked only as a WHOLE word/token (and as the whole
        /// name once separators are stripped), so common legitimate names that merely
        /// contain them are not rejected.
        /// </summary>
        static readonly string[] BlockedWholeWord =
        {
            "ass", "arse", "anal", "anus", "sex", "cum", "tit", "tits",
            "fag", "dyke", "homo", "dick", "cock", "prick", "hoe",
            "wank", "wanker", "boner", "semen", "rape", "rapist",
            "pedo", "paedo", "hooker",
            "spic", "coon", "chink", "gook", "jap", "paki", "negro",
            "kraut", "wop", "dago", "gyp", "gyppo", "beaner", "squaw",
            "injun", "redskin", "darkie", "darky", "honky", "mulatto", "muzzie",
        };

        // ── Public API ──────────────────────────────────────────────────────

        /// <summary>
        /// Validates a raw, user-entered name against the full local rule set:
        /// length, character set, format, reserved names, and the content filter.
        /// Does NOT check uniqueness — that is the async half in
        /// <c>PlayerDataService.TrySetDisplayNameAsync</c>.
        /// </summary>
        public static DisplayNameValidationResult Validate(string rawName)
        {
            var config = Config;
            string sanitized = Sanitize(rawName);

            if (string.IsNullOrEmpty(sanitized))
                return DisplayNameValidationResult.Fail(DisplayNameError.Empty, "Enter a name.");

            if (sanitized.Length < config.MinLength)
                return DisplayNameValidationResult.Fail(DisplayNameError.TooShort,
                    $"Names must be at least {config.MinLength} characters long.");

            if (sanitized.Length > config.MaxLength)
                return DisplayNameValidationResult.Fail(DisplayNameError.TooLong,
                    $"Names can be at most {config.MaxLength} characters long.");

            bool hasLetter = false;
            foreach (char c in sanitized)
            {
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                {
                    hasLetter = true;
                    continue;
                }

                if (c >= '0' && c <= '9')
                    continue;

                if (c == ' ' && config.AllowSpaces)
                    continue;

                if (c != ' ' && config.AllowedSpecialCharacters.IndexOf(c) >= 0)
                    continue;

                return DisplayNameValidationResult.Fail(DisplayNameError.InvalidCharacters,
                    BuildCharacterRuleMessage(config));
            }

            if (config.RequireLetter && !hasLetter)
                return DisplayNameValidationResult.Fail(DisplayNameError.InvalidFormat,
                    "Names must contain at least one letter.");

            if (!IsAsciiLetterOrDigit(sanitized[0]) || !IsAsciiLetterOrDigit(sanitized[sanitized.Length - 1]))
                return DisplayNameValidationResult.Fail(DisplayNameError.InvalidFormat,
                    "Names must start and end with a letter or number.");

            for (int i = 1; i < sanitized.Length; i++)
            {
                if (!IsAsciiLetterOrDigit(sanitized[i]) && !IsAsciiLetterOrDigit(sanitized[i - 1]))
                    return DisplayNameValidationResult.Fail(DisplayNameError.InvalidFormat,
                        "Names cannot contain back-to-back spaces or punctuation.");
            }

            if (IsAllowlisted(sanitized, config))
                return DisplayNameValidationResult.Success(sanitized);

            if (IsReserved(sanitized, config))
                return DisplayNameValidationResult.Fail(DisplayNameError.Reserved,
                    "That name is reserved. Pick another one.");

            if (config.EnableProfanityFilter && ContainsBlockedTerm(sanitized, config))
                return DisplayNameValidationResult.Fail(DisplayNameError.Inappropriate,
                    "That name isn't allowed. Pick something respectful.");

            return DisplayNameValidationResult.Success(sanitized);
        }

        /// <summary>
        /// Canonical key used for the duplicate-name check: lowercase with everything but
        /// letters and digits stripped, so "Sky Walker", "sky.walker" and "SKYWALKER" all
        /// claim the same name. Digits are kept as-is so "Pilot1234" and "Pilot5678" stay
        /// distinct.
        /// </summary>
        public static string NormalizeForUniqueness(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            var sb = new StringBuilder(name.Length);
            foreach (char raw in name)
            {
                char c = char.ToLowerInvariant(raw);
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                    sb.Append(c);
            }

            return sb.ToString();
        }

        /// <summary>Trims and collapses internal whitespace runs to a single space.</summary>
        public static string Sanitize(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return string.Empty;

            var sb = new StringBuilder(rawName.Length);
            bool lastWasSpace = false;
            foreach (char c in rawName.Trim())
            {
                bool isSpace = char.IsWhiteSpace(c);
                if (isSpace && lastWasSpace)
                    continue;

                sb.Append(isSpace ? ' ' : c);
                lastWasSpace = isSpace;
            }

            return sb.ToString();
        }

        // ── Content filter internals ────────────────────────────────────────

        static bool IsAsciiLetterOrDigit(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');

        static string BuildCharacterRuleMessage(DisplayNameValidationConfigSO config)
        {
            var sb = new StringBuilder("Names can only contain letters and numbers");
            if (config.AllowSpaces)
                sb.Append(", spaces");
            if (config.AllowedSpecialCharacters.Length > 0)
                sb.Append(" and ").Append(string.Join(" ", config.AllowedSpecialCharacters.ToCharArray()));
            sb.Append('.');
            return sb.ToString();
        }

        static bool IsAllowlisted(string sanitized, DisplayNameValidationConfigSO config)
        {
            var allowed = config.AllowedNames;
            if (allowed == null)
                return false;

            for (int i = 0; i < allowed.Count; i++)
            {
                if (string.Equals(sanitized, allowed[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static bool IsReserved(string sanitized, DisplayNameValidationConfigSO config)
        {
            var reserved = config.ReservedNames;
            if (reserved == null || reserved.Count == 0)
                return false;

            string full = NormalizeForUniqueness(sanitized);
            var tokens = TokenizeForFilter(sanitized);

            for (int i = 0; i < reserved.Count; i++)
            {
                string term = NormalizeForUniqueness(reserved[i]);
                if (term.Length == 0)
                    continue;

                if (full == term)
                    return true;

                for (int t = 0; t < tokens.Count; t++)
                {
                    if (tokens[t] == term)
                        return true;
                }
            }

            return false;
        }

        static bool ContainsBlockedTerm(string sanitized, DisplayNameValidationConfigSO config)
        {
            // Letters-only views of the name: leet-mapped ("n1gg3r" → "nigger"), with all
            // separators stripped ("f.u.c.k" → "fuck"), plus a repeat-collapsed variant
            // ("niiigger" → "niger") to defeat letter padding.
            string normalized = NormalizeForFilter(sanitized);
            string collapsed = CollapseRepeats(normalized);
            var tokens = TokenizeForFilter(sanitized);

            if (MatchesAnywhere(BlockedAnywhere, normalized, collapsed))
                return true;
            if (MatchesAnywhereList(config.AdditionalBlockedAnywhere, normalized, collapsed))
                return true;

            if (MatchesWholeWord(BlockedWholeWord, normalized, tokens))
                return true;
            if (MatchesWholeWordList(config.AdditionalBlockedWholeWord, normalized, tokens))
                return true;

            return false;
        }

        static bool MatchesAnywhere(string[] terms, string normalized, string collapsed)
        {
            for (int i = 0; i < terms.Length; i++)
            {
                if (AnywhereTermHits(terms[i], normalized, collapsed))
                    return true;
            }

            return false;
        }

        static bool MatchesAnywhereList(IReadOnlyList<string> terms, string normalized, string collapsed)
        {
            if (terms == null)
                return false;

            for (int i = 0; i < terms.Count; i++)
            {
                if (AnywhereTermHits(terms[i], normalized, collapsed))
                    return true;
            }

            return false;
        }

        static bool AnywhereTermHits(string rawTerm, string normalized, string collapsed)
        {
            string term = NormalizeForFilter(rawTerm);
            if (term.Length == 0)
                return false;

            if (normalized.Contains(term))
                return true;

            // The collapsed comparison catches letter padding, but a heavily-repeating
            // term collapses to something too short/generic to substring-match safely
            // ("kkk" → "k"), so only compare when the collapsed term keeps enough shape.
            string collapsedTerm = CollapseRepeats(term);
            return collapsedTerm.Length >= 4 && collapsed.Contains(collapsedTerm);
        }

        static bool MatchesWholeWord(string[] terms, string normalized, List<string> tokens)
        {
            for (int i = 0; i < terms.Length; i++)
            {
                if (WholeWordTermHits(terms[i], normalized, tokens))
                    return true;
            }

            return false;
        }

        static bool MatchesWholeWordList(IReadOnlyList<string> terms, string normalized, List<string> tokens)
        {
            if (terms == null)
                return false;

            for (int i = 0; i < terms.Count; i++)
            {
                if (WholeWordTermHits(terms[i], normalized, tokens))
                    return true;
            }

            return false;
        }

        static bool WholeWordTermHits(string rawTerm, string normalized, List<string> tokens)
        {
            string term = NormalizeForFilter(rawTerm);
            if (term.Length == 0)
                return false;

            // The entire name is the word once separators are stripped ("a s s" → "ass").
            if (normalized == term)
                return true;

            for (int t = 0; t < tokens.Count; t++)
            {
                if (tokens[t] == term)
                    return true;
            }

            return false;
        }

        /// <summary>Lowercases, maps leetspeak to letters, and drops every non-letter.</summary>
        static string NormalizeForFilter(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length);
            foreach (char raw in value)
            {
                char c = MapLeet(char.ToLowerInvariant(raw));
                if (c >= 'a' && c <= 'z')
                    sb.Append(c);
            }

            return sb.ToString();
        }

        /// <summary>Name split into words on any non-alphanumeric, each leet-mapped to letters.</summary>
        static List<string> TokenizeForFilter(string value)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(value))
                return tokens;

            var sb = new StringBuilder();
            foreach (char raw in value)
            {
                if (IsAsciiLetterOrDigit(raw))
                {
                    char c = MapLeet(char.ToLowerInvariant(raw));
                    if (c >= 'a' && c <= 'z')
                        sb.Append(c);
                    continue;
                }

                if (sb.Length > 0)
                {
                    tokens.Add(sb.ToString());
                    sb.Clear();
                }
            }

            if (sb.Length > 0)
                tokens.Add(sb.ToString());

            return tokens;
        }

        static string CollapseRepeats(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length);
            char last = '\0';
            foreach (char c in value)
            {
                if (c == last)
                    continue;

                sb.Append(c);
                last = c;
            }

            return sb.ToString();
        }

        static char MapLeet(char c) => c switch
        {
            '0' => 'o',
            '1' => 'i',
            '2' => 'z',
            '3' => 'e',
            '4' => 'a',
            '5' => 's',
            '6' => 'g',
            '7' => 't',
            '8' => 'b',
            '9' => 'g',
            '@' => 'a',
            '$' => 's',
            '!' => 'i',
            '+' => 't',
            '|' => 'i',
            _ => c,
        };
    }
}
