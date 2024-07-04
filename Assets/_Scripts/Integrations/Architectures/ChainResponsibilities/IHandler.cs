namespace CosmicShore.Integrations.Architectures.ChainResponsibilities
{
    public interface IHandler
    {
        IHandler SetNext(IHandler handler);
        object Handle(object request);
    }
}