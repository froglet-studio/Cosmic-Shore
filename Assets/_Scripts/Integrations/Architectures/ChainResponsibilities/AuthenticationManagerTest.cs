using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Integrations.Architectures.ChainResponsibilities
{
    public class AuthenticationManagerTest
    {
        public static void DoRequests(BaseHandler handler)
        {
            var requests = new List<object>();
            requests.Add(new LoginInfo { Id = "iewqprewi", Username = "Jade" });
            requests.Add(new ErrorInfo { ErrorCode = 404, Error = "Not found." });

            foreach (var request in requests)
            {
                var result = handler.Handle(request);

                if (request != null)
                {
                    Debug.Log($"{result}");
                }
                else
                {
                    Debug.Log("The request is not handled.");
                }
            }
        }
    }
}