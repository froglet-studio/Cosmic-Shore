// PORT Deviation — type-preserving SHELL of the URP post-processing manager
// (original: Assets/_Scripts/Controller/Managers/PostProcessingManager.cs, 37 lines:
// URP Volume profile toggling — presentation-phase concerns). Only the type exists so
// AppManager's RegisterManagerSingleton<PostProcessingManager> binding compiles; the
// real port arrives with the rendering phase. Precedent: CameraManager shell (#12).
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class PostProcessingManager : Singleton<PostProcessingManager>
    {
    }
}
