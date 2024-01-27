using System.Collections;
using Loxodon.Framework.Binding;
using Loxodon.Framework.Views;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Integrations.Loxodon.MVVM
{
    public class ProgressBarView : UIView
    {
        [SerializeField] GameObject progressBar;
        [SerializeField] Text progressTip;
        [SerializeField] Text progressText;
        [SerializeField] Slider progressSlider;
        protected override void Awake()
        {
            var bindingSet = this.CreateBindingSet<ProgressBarView, ProgressBarViewModel>();
            bindingSet.Bind(progressBar)
                .For(v => v.activeSelf)
                .To(vm => vm.Enabled)
                .OneWay();
            bindingSet.Bind(progressTip)
                .For(v => v.text)
                .To(vm => vm.Tip)
                .OneWay();
            bindingSet.Bind(progressText)
                .For(v => v.text)
                .ToExpression(vm => string.Format("{0:0.00}%", vm.Value * 100))
                .OneWay();
            bindingSet.Bind(progressSlider)
                .For(v => v.value)
                .To(vm => vm.Value)
                .OneWay();
            
            bindingSet.Build();
        }

        IEnumerator Unzip(ProgressBarViewModel progressBarVM)
        {
            progressBarVM.Tip = "Unziping";
            progressBarVM.Enabled = true;

            for (var i = 0; i < 30; i++)
            {
                progressBarVM.Value = (i / (float)30);
                yield return null;
            }

            progressBarVM.Enabled = false;
            progressBarVM.Tip = "";
        }
    }
}