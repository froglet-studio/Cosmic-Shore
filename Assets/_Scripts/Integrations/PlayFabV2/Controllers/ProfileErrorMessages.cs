namespace CosmicShore
{
    public class ProfileErrorMessages : IErrorMessages
    {
        public const string AccountNotFound = "Cannot find associated account.";
        public const string InvalidPartner = "Invalid third-party provider.";
        public const string NameNotAvailable = "The provided display name is not available.";
        public const string ProfaneDisplayName = "The provided display name is not appropriate.";
    }
}