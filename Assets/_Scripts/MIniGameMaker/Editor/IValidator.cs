namespace CosmicShore.Tools.MiniGameMaker
{
    public enum Severity { Pass, Warning, Fail }
    
    public interface IValidator
    {
        string Name { get; }
        (Severity, string) Check();
        void Fix(); // optional; safe no-op if not applicable
    }
}