// FTUEIntroAnimatorAdapter.cs
using CosmicShore.FTUE;
using System.Collections;
using UnityEngine;

[AddComponentMenu("FTUE/Adapters/FTUEIntroAnimatorAdapter")]
public class FTUEIntroAnimatorAdapter : MonoBehaviour, IAnimator
{
    [SerializeField] private FTUEIntroAnimator _inner;

    public IEnumerator PlayIntro()
    {
        bool done = false;
        // inner.PlayIntro expects an Action callback, so we capture it:
        yield return _inner.PlayIntro(() => done = true);
        // wait until that callback really fired
        while (!done) yield return null;
    }

    public IEnumerator PlayOutro()
    {
        bool done = false;
        yield return _inner.PlayOutro(() => done = true);
        while (!done) yield return null;
    }
}
