using Loxodon.Framework.ViewModels;

namespace CosmicShore.Integrations.Loxodon.MVVM
{
    public class ProgressBarViewModel : ViewModelBase
    {
        private string _tip;
        private bool _enabled;
        private float _value;
        public ProgressBarViewModel(){}

        public string Tip
        {
            get => _tip;
            set => Set(ref _tip, value);
        }

        public bool Enabled
        {
            get => _enabled;
            set => Set(ref _enabled, value);
        }

        public float Value
        {
            get => _value;
            set => Set(ref _value, value);
        }
    }
}
