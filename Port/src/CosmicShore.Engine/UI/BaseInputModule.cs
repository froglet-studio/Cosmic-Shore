namespace CosmicShore.Engine.UI
{
    /// <summary>Base of the event-system input modules (original: BaseInputModule).</summary>
    public abstract class BaseInputModule : UIBehaviour
    {
        public virtual bool IsModuleSupported() => true;
        public virtual bool ShouldActivateModule() => enabled;
        public virtual void ActivateModule() { }
        public virtual void DeactivateModule() { }
        public virtual void UpdateModule() { }
        public virtual void Process() { }
    }
}
