using CosmicShore.Engine;

namespace CosmicShore.Gameplay.MultiMouse
{
    public interface IMultiMouseDevice
    {
        string Id { get; }
        string DisplayName { get; }

        Vector2 ConsumeDelta();

        bool LeftButton { get; }
        bool RightButton { get; }
        bool MiddleButton { get; }

        bool LeftButtonDownThisFrame { get; }
        bool LeftButtonUpThisFrame { get; }
        bool RightButtonDownThisFrame { get; }
        bool RightButtonUpThisFrame { get; }
        bool MiddleButtonDownThisFrame { get; }
        bool MiddleButtonUpThisFrame { get; }

        void Tick();
    }

    public interface IMultiMouseProvider
    {
        bool IsAvailable { get; }
        int DeviceCount { get; }
        IMultiMouseDevice GetDevice(int index);
        void Tick();
        void Refresh();
        void Shutdown();
    }
}
