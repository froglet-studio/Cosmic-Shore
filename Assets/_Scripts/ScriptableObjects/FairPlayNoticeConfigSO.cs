using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Every word of the fair-play notice and appeal screen (phase B; Docs/AntiCheat/README.md §4.6 and §4.7). Loaded
    /// from <c>Resources/FairPlayNoticeConfig</c>. Templates take tokens: <c>{rule}</c> (the Code of Conduct rule
    /// number) and <c>{until}</c> (the date a restriction ends). An empty field shows nothing.
    ///
    /// <para>What the notice must say (LEGAL.md, DSA Art. 17): what kind of behaviour, that automated detection was
    /// involved (or that a person decided), which rule, how to appeal and how long it lasts. It must not reveal how the
    /// detection works, and must not claim more than was found ("we scanned your computer" when nothing was).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "FairPlayNoticeConfig", menuName = "ScriptableObjects/Fair Play/Fair Play Notice Config")]
    public class FairPlayNoticeConfigSO : ScriptableObject
    {
        public const string ResourcePath = "FairPlayNoticeConfig";

        [Header("Every notice")]
        [SerializeField] string eyebrow = "Fair play";
        [Tooltip("Shown when the measure was automated (the Undertow or the quitters' pool).")]
        [SerializeField, TextArea] string automatedLine = "This was found by our automated checks.";
        [Tooltip("Shown when a person decided the restriction. {until} is the date it ends.")]
        [SerializeField, TextArea] string restrictedLine = "A member of our team reviewed your account and restricted ranked and public play until {until}.";
        [Tooltip("Always shown: what is never affected.")]
        [SerializeField, TextArea] string paidContentLine = "Everything you paid for stays playable on your own and with AI teammates.";

        [Header("Per kind ({rule} = the Code of Conduct rule)")]
        [SerializeField] string cheatingTitle = "Your matches are being played separately";
        [SerializeField, TextArea] string cheatingBody = "Our checks found play that breaks rule {rule} of the Code of Conduct (play the game as it was made). For now you are matched with other accounts flagged the same way.";
        [SerializeField] string automatedTitle = "Your matches are being played separately";
        [SerializeField, TextArea] string automatedBody = "Our checks found play that looks automated, which breaks rule {rule} of the Code of Conduct (play yourself). For now you are matched with other accounts flagged the same way.";
        [SerializeField] string quittingTitle = "Your matches are being played separately";
        [SerializeField, TextArea] string quittingBody = "You have left many matches early, which breaks rule {rule} of the Code of Conduct (finish what you start). For now you are matched with other players who do the same; finishing matches brings you back.";

        [Header("Appeal")]
        [SerializeField, TextArea] string appealPrompt = "If you think we got this wrong, tell us in a few words and a person will look at it.";
        [SerializeField] string appealPlaceholder = "What happened? (optional)";
        [SerializeField] string appealButton = "Appeal";
        [SerializeField] string appealOpenLine = "Your appeal is with our team. We will look at it as soon as we can.";
        [SerializeField] string appealRefusedLine = "We looked at your appeal and the measure stays in place.";
        [SerializeField] string appealFailedLine = "The appeal could not be sent. Please try again later.";
        [SerializeField] string closeButton = "OK";
        [SerializeField] string primaryKeycap = "A";
        [SerializeField] string secondaryKeycap = "B";

        [Header("Links")]
        [Tooltip("The Code of Conduct the notice names. Empty hides the link.")]
        [SerializeField] string codeOfConductUrl = "";
        [SerializeField] string codeOfConductLabel = "Read the Code of Conduct";

        public string Eyebrow => eyebrow ?? string.Empty;
        public string AutomatedLine => automatedLine ?? string.Empty;
        public string RestrictedLine => restrictedLine ?? string.Empty;
        public string PaidContentLine => paidContentLine ?? string.Empty;
        public string AppealPrompt => appealPrompt ?? string.Empty;
        public string AppealPlaceholder => appealPlaceholder ?? string.Empty;
        public string AppealButton => appealButton ?? string.Empty;
        public string AppealOpenLine => appealOpenLine ?? string.Empty;
        public string AppealRefusedLine => appealRefusedLine ?? string.Empty;
        public string AppealFailedLine => appealFailedLine ?? string.Empty;
        public string CloseButton => closeButton ?? string.Empty;
        public string PrimaryKeycap => primaryKeycap ?? string.Empty;
        public string SecondaryKeycap => secondaryKeycap ?? string.Empty;
        public string CodeOfConductUrl => codeOfConductUrl ?? string.Empty;
        public string CodeOfConductLabel => codeOfConductLabel ?? string.Empty;

        public string TitleFor(string kind) => (kind switch
        {
            "automated" => automatedTitle,
            "quitting" => quittingTitle,
            _ => cheatingTitle,
        }) ?? string.Empty;

        public string BodyFor(string kind) => (kind switch
        {
            "automated" => automatedBody,
            "quitting" => quittingBody,
            _ => cheatingBody,
        }) ?? string.Empty;

        static FairPlayNoticeConfigSO s_loaded;

        public static FairPlayNoticeConfigSO LoadOrDefault()
        {
            if (s_loaded) return s_loaded;
            s_loaded = Resources.Load<FairPlayNoticeConfigSO>(ResourcePath);
            if (!s_loaded) s_loaded = CreateInstance<FairPlayNoticeConfigSO>();
            return s_loaded;
        }
    }
}
