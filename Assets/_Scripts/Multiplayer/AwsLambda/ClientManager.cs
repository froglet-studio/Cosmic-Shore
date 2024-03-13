using System;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using CosmicShore.Utility.ClassExtensions;
using CosmicShore.Utility.Singleton;

namespace CosmicShore
{
    public class ClientManager : SingletonPersistent<ClientManager>
    {
        private HttpClient _client;
        // Start is called before the first frame update
        private async void Start()
        {
            var fullUrl = $"{SharedData.HttpUrl}/calculator/add/1/2";
            await RunOperations(fullUrl);
            
            fullUrl = $"{SharedData.HttpUrl}/calculator/subtract/1/2";
            await RunOperations(fullUrl);

        }

        public async Task RunOperations(string url)
        {
            var response = await GetResponse(url);
            this.LogWithClassMethod(MethodBase.GetCurrentMethod()?.ToString(), $"Add result: {response}");
        }

        // Update is called once per frame
        public async Task<string> GetResponse(string url)
        {
            
            using (_client = new())
            {
                try
                {
                    var response = await _client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        var errorMessage = $"Error getting response: {response.StatusCode}";
                        this.LogErrorWithClassMethod(MethodBase.GetCurrentMethod()?.ToString(), errorMessage);
                        return errorMessage;
                    }
                
                }
                catch (Exception e)
                {
                    this.LogErrorWithClassMethod(MethodBase.GetCurrentMethod()?.ToString(), e.Message);
                    return e.Message;
                }
            }
           
        }
    }
}
