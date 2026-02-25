using System.Collections;

namespace CosmicShore.FTUE.Interfaces
{
    public interface IAnimator
    {
        IEnumerator PlayIntro();
        IEnumerator PlayOutro();
    }
}
