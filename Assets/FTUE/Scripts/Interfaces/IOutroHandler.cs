using System.Collections;

namespace CosmicShore.FTUE.Interfaces
{
    public interface IOutroHandler : ITutorialStepHandler
    {
        IEnumerator PlayOutro();
    }
}
