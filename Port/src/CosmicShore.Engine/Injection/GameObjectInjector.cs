namespace CosmicShore.Engine.Injection
{
    /// <summary>
    /// Reflex façade compat (E15): ported spawners call
    /// <c>GameObjectInjector.InjectRecursive(go, container)</c> (the
    /// <c>Reflex.Injectors</c> static surface); this forwards to the container's
    /// own hierarchy injection so the call sites port verbatim.
    /// </summary>
    public static class GameObjectInjector
    {
        public static void InjectRecursive(GameObject gameObject, Container container)
            => container.InjectGameObject(gameObject, recursive: true);
    }
}
