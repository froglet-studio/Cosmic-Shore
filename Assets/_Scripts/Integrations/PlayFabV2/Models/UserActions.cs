using System;
using PlayFab;
using PlayFab.SharedModels;

namespace CosmicShore
{
    public class UserRequests<TRequest, TResult> 
        where TRequest: PlayFabRequestCommon
        where TResult: PlayFabResultCommon
    {
        public TRequest Request { get; set; }
        public Action<TResult> OnSuccess { get; set; }
        public Action<PlayFabError> OnFailed { get; set; }
    }
}
