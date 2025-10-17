namespace CosmicShore.Tools.MiniGameMaker
{
    sealed class DependencySpawnerValidator : IValidator
    {
        public string Name => "DependencySpawner";
        public (Severity,string) Check() =>
            SceneUtil.Find("DependencySpawner") ? (Severity.Pass,"Found") : (Severity.Fail,"Missing");
        public void Fix() { /* fix from library happens from UI, see View */ }
    }
}