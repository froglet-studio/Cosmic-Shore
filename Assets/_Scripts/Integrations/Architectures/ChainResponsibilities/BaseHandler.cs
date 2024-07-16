namespace CosmicShore.Integrations.Architectures.ChainResponsibilities
{
    public abstract class BaseHandler : IHandler
    {
        private IHandler _nextHandler;
        
        public IHandler SetNext(IHandler handler)
        {
            _nextHandler = handler;
            return handler;
        }

        public virtual object Handle(object request)
        {
            if (_nextHandler != null)
            {
                return _nextHandler.Handle(request);
            }

            return null;
        }
    }
}
