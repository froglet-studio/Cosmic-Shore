using CosmicShore.FTUE.Drivers;
using CosmicShore.FTUE.Interfaces;
using System.Collections;
using UnityEngine;

namespace CosmicShore.FTUE.Adapters
{
    [AddComponentMenu("FTUE/Adapters/FTUEIntroAnimatorAdapter")]
    public class FTUEIntroAnimatorAdapter : MonoBehaviour, IAnimator
    {
        [SerializeField] private FTUEIntroAnimator _inner;

        public IEnumerator PlayIntro()
        {
            bool done = false;
            yield return _inner.PlayIntro(() => done = true);
            while (!done) yield return null;
        }

        public IEnumerator PlayOutro()
        {
            bool done = false;
            yield return _inner.PlayOutro(() => done = true);
            while (!done) yield return null;
        }
    }
}
