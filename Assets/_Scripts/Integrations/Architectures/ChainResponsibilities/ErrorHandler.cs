namespace CosmicShore.Integrations.Architectures.ChainResponsibilities
{
    public class ErrorHandler : BaseHandler
    {
        public override object Handle(object request)
        {
            if (request is ErrorInfo)
            {
                return request.ToString();
            }
            return base.Handle(request);
        }
    }
}