namespace CosmicShore.Core
{
    /// <summary>
    /// The ways a player can sign in (Docs/MENU_REDESIGN_B.md §54). Guest is UGS anonymous sign-in, which
    /// works today; the others are filled in by the login branch through <see cref="IPlatformSignIn"/>.
    /// </summary>
    public enum SignInProvider
    {
        None = 0,
        Guest = 1,
        Steam = 2,
        Google = 3,
        Apple = 4,
    }

    public enum SignInStatus
    {
        Succeeded = 0,
        /// <summary>The player closed the provider's page or sheet.</summary>
        Cancelled = 1,
        /// <summary>The provider or UGS refused; <see cref="SignInOutcome.Reason"/> says why, for the log.</summary>
        Failed = 2,
        /// <summary>This build cannot use the provider (no SDK yet, wrong platform).</summary>
        Unavailable = 3,
        /// <summary>Linking only: the provider's account already belongs to another pilot. Pilots never merge.</summary>
        AlreadyHasPilot = 4,
    }

    /// <summary>What the link-conflict window shows of a pilot.</summary>
    public readonly struct PilotSummary
    {
        public readonly string Name;
        public readonly int Crystals;
        public readonly int Vessels;
        public readonly int QuestsDone;

        public PilotSummary(string name, int crystals, int vessels, int questsDone)
        {
            Name = name;
            Crystals = crystals;
            Vessels = vessels;
            QuestsDone = questsDone;
        }
    }

    public readonly struct SignInOutcome
    {
        public readonly SignInStatus Status;
        /// <summary>A diagnostic reason for the log. Never shown to the player: the UI's lines are authored.</summary>
        public readonly string Reason;
        /// <summary><see cref="SignInStatus.AlreadyHasPilot"/> only: the pilot signed in now.</summary>
        public readonly PilotSummary ThisPilot;
        /// <summary><see cref="SignInStatus.AlreadyHasPilot"/> only: the pilot the provider's account owns.</summary>
        public readonly PilotSummary OtherPilot;

        public SignInOutcome(SignInStatus status, string reason = null, PilotSummary thisPilot = default, PilotSummary otherPilot = default)
        {
            Status = status;
            Reason = reason;
            ThisPilot = thisPilot;
            OtherPilot = otherPilot;
        }

        public bool Succeeded => Status == SignInStatus.Succeeded;
        public static SignInOutcome Unavailable(SignInProvider provider) => new(SignInStatus.Unavailable, $"{provider} sign-in is not in this build.");
    }
}
