namespace CosmicShore
{
    public abstract class Fauna : LifeForm
    {
        [SerializeField] GameObject healthBlockContainer;
        public float aggression; 

        protected abstract void Spawn();

        protected override void Start()
        {
            base.Start();
            if (healthBlockContainer) healthBlockContainer.GetComponentsInChildren<HealthBlock>(healthBlocks);
        }

    }
}
