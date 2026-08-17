using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Single source of truth for the display-name rules: length bounds, allowed
    /// characters, reserved names, designer extensions to the built-in profanity
    /// lists, and the uniqueness-check policy.
    ///
    /// The built-in deny lists live in code (<see cref="CosmicShore.Utility.DisplayNameValidator"/>)
    /// so they cannot be accidentally emptied from an asset; this config only ever
    /// ADDS terms (or allowlists exact names) on top of them.
    ///
    /// Loaded from <c>Resources/DisplayNameValidationConfig</c>; a missing asset
    /// falls back to these field-initializer defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "DisplayNameValidationConfig", menuName = "ScriptableObjects/Player/Display Name Validation Config")]
    public class DisplayNameValidationConfigSO : ScriptableObject
    {
        public const string ResourcePath = "DisplayNameValidationConfig";

        [Header("Length")]
        [Tooltip("Minimum display name length (after trimming and collapsing whitespace).")]
        [Min(1)] [SerializeField] int minLength = 3;

        [Tooltip("Maximum display name length (after trimming and collapsing whitespace).")]
        [Min(1)] [SerializeField] int maxLength = 25;

        [Header("Characters")]
        [Tooltip("Allow single spaces between words (leading/trailing/double spaces are always rejected).")]
        [SerializeField] bool allowSpaces = true;

        [Tooltip("Non-alphanumeric characters allowed in a name, e.g. \"_-.\". Names may not start or end with one, or repeat them back to back.")]
        [SerializeField] string allowedSpecialCharacters = "_-.";

        [Tooltip("Require at least one letter so a name cannot be digits/punctuation only.")]
        [SerializeField] bool requireLetter = true;

        [Header("Content Filter")]
        [Tooltip("Master switch for the slur/profanity filter. Leave ON outside of internal test builds.")]
        [SerializeField] bool enableProfanityFilter = true;

        [Tooltip("Names that impersonate the team or the system. Matched as a whole name and as a word inside a name.")]
        [SerializeField] List<string> reservedNames = new()
        {
            "admin", "administrator", "moderator", "mod", "staff", "system",
            "server", "console", "official", "support", "developer", "gamemaster",
            "owner", "root", "froglet", "frogletinc", "cosmicshore",
        };

        [Tooltip("Extra terms blocked ANYWHERE inside a name (substring match after leet/separator normalization). For unambiguous terms only.")]
        [SerializeField] List<string> additionalBlockedAnywhere = new();

        [Tooltip("Extra terms blocked as a WHOLE word/token only (avoids false positives on short ambiguous terms).")]
        [SerializeField] List<string> additionalBlockedWholeWord = new();

        [Tooltip("Exact full names exempt from the content filter (rescues legitimate names the filter would reject). Compared case-insensitively.")]
        [SerializeField] List<string> allowedNames = new();

        [Header("Uniqueness")]
        [Tooltip("Reject a name another player has already claimed (checked against the Cloud Save public name registry).")]
        [SerializeField] bool enableUniquenessCheck = true;

        [Tooltip("If the availability check cannot run (offline, index not configured), ON rejects the change, OFF allows it with a warning log. OFF keeps first-run/offline name setup working.")]
        [SerializeField] bool blockWhenUniquenessUnknown = false;

        public int MinLength => minLength;
        public int MaxLength => maxLength;
        public bool AllowSpaces => allowSpaces;
        public string AllowedSpecialCharacters => allowedSpecialCharacters ?? string.Empty;
        public bool RequireLetter => requireLetter;
        public bool EnableProfanityFilter => enableProfanityFilter;
        public IReadOnlyList<string> ReservedNames => reservedNames;
        public IReadOnlyList<string> AdditionalBlockedAnywhere => additionalBlockedAnywhere;
        public IReadOnlyList<string> AdditionalBlockedWholeWord => additionalBlockedWholeWord;
        public IReadOnlyList<string> AllowedNames => allowedNames;
        public bool EnableUniquenessCheck => enableUniquenessCheck;
        public bool BlockWhenUniquenessUnknown => blockWhenUniquenessUnknown;

        /// <summary>
        /// Resolves the shared config: the Resources asset when present, otherwise an
        /// in-memory instance carrying the field-initializer defaults, so validation
        /// works (and stays strict) even if the asset is missing from a build.
        /// </summary>
        public static DisplayNameValidationConfigSO LoadOrDefault()
        {
            var config = Resources.Load<DisplayNameValidationConfigSO>(ResourcePath);
            if (config == null)
                config = CreateInstance<DisplayNameValidationConfigSO>();
            return config;
        }
    }
}
