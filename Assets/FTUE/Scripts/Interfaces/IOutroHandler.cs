using System.Collections;

namespace CosmicShore.Core
{
    public interface IOutroHandler : ITutorialStepHandler
    {
        IEnumerator PlayOutro();
    }
}
