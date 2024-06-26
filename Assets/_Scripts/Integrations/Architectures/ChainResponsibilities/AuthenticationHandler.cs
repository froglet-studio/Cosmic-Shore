namespace CosmicShore.Integrations.Architectures.ChainResponsibilities
{
    public class AuthenticationHandler : BaseHandler{
        public override object Handle(object request)
        {
            if (request is LoginInfo)
            {
                return request.ToString();
            }

            return base.Handle(request);
        }
    }
}