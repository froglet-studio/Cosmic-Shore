// PORT Deviation — type-preserving SHELL of the pooled prism factory
// (original: Assets/_Scripts/Controller/Prisms/PrismFactory.cs, 274 lines: the
// GenericPoolManager-backed prism instantiation pipeline the trail spawners drive
// in the Unity scene graph). The port's prism creation currently flows through the
// vessel-layer spawners and PrismSpatialIndex directly; only the type exists so
// AppManager's RegisterManagerSingleton<PrismFactory> binding compiles. The full
// port lands with the prism-pooling pass. Precedent: AudioSystem shell (#11).
using CosmicShore.Engine;

namespace CosmicShore.Gameplay
{
    public class PrismFactory : MonoBehaviour
    {
    }
}
