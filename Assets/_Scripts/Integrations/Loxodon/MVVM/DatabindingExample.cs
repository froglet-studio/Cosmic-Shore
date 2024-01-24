using Loxodon.Framework.Binding;
using Loxodon.Framework.Contexts;
using Loxodon.Framework.Views;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Integrations.Loxodon.MVVM
{
    public class DatabindingExample : UIView
    {
        [Header("Labels")]
        [SerializeField] private Text title;
        [SerializeField] private Text username;
        [SerializeField] private Text password;
        [SerializeField] private Text email;
        [SerializeField] private Text birthday;
        [SerializeField] private Text address;
        [SerializeField] private Text remember;
        
        [Header("Error Message")]
        [SerializeField] private Text errorMessage;
        
        [Header("Input Fields")]
        [SerializeField] private InputField usernameEdit;
        [SerializeField] private InputField emailEdit;
        [SerializeField] private Toggle rememberEdit;
        [SerializeField] private Button submitButton;

        protected override void Awake()
        {
            var context = Context.GetApplicationContext();
            var bindingService = new BindingServiceBundle(context.GetContainer());
            bindingService.Start();
        }
    }
}