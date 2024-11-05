using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.IO
{
    public interface IInputProvider
    {
        float XSum { get; }
        float YSum { get; }
        float XDiff { get; }
        float YDiff { get; }
        Vector2 EasedLeftJoystickPosition { get; }
        Vector2 EasedRightJoystickPosition { get; }
        bool OneTouchLeft { get; }
        Vector2 SingleTouchValue { get; }
    }
}
