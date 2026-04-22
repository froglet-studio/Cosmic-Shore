using System.Collections;

namespace CosmicShore.Core
{
    public interface IAnimator
    {
        IEnumerator PlayIntro();
        IEnumerator PlayOutro();
    }
}
