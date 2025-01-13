namespace CosmicShore.Core
{
    using Unity.Netcode;
    using UnityEngine;

    public class NetworkInputStatus : NetworkBehaviour, IInputStatus
    {
        // Floats
        private readonly NetworkVariable<float> n_xSum = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<float> n_ySum = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<float> n_xDiff = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<float> n_yDiff = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);

        public float XSum
        {
            get => n_xSum.Value;
            set => n_xSum.Value = value;
        }

        public float YSum
        {
            get => n_ySum.Value;
            set => n_ySum.Value = value;
        }

        public float XDiff
        {
            get => n_xDiff.Value;
            set => n_xDiff.Value = value;
        }

        public float YDiff
        {
            get => n_yDiff.Value;
            set => n_yDiff.Value = value;
        }

        // Booleans
        private readonly NetworkVariable<bool> n_idle = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> n_paused = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> n_isGyroEnabled = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> n_invertYEnabled = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> n_invertThrottleEnabled = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> n_oneTouchLeft = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);

        public bool Idle
        {
            get => n_idle.Value;
            set => n_idle.Value = value;
        }

        public bool Paused
        {
            get => n_paused.Value;
            set => n_paused.Value = value;
        }

        public bool IsGyroEnabled
        {
            get => n_isGyroEnabled.Value;
            set => n_isGyroEnabled.Value = value;
        }

        public bool InvertYEnabled
        {
            get => n_invertYEnabled.Value;
            set => n_invertYEnabled.Value = value;
        }

        public bool InvertThrottleEnabled
        {
            get => n_invertThrottleEnabled.Value;
            set => n_invertThrottleEnabled.Value = value;
        }

        public bool OneTouchLeft
        {
            get => n_oneTouchLeft.Value;
            set => n_oneTouchLeft.Value = value;
        }

        // Vectors
        private readonly NetworkVariable<Vector2> n_rightJoystickHome = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Vector2> n_leftJoystickHome = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Vector2> n_rightClampedPosition = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Vector2> n_leftClampedPosition = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Vector2> n_rightJoystickStart = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Vector2> n_leftJoystickStart = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Vector2> n_rightNormalizedJoystickPosition = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Vector2> n_leftNormalizedJoystickPosition = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Vector2> n_easedRightJoystickPosition = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Vector2> n_easedLeftJoystickPosition = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);

        public Vector2 RightJoystickHome
        {
            get => n_rightJoystickHome.Value;
            set => n_rightJoystickHome.Value = value;
        }

        public Vector2 LeftJoystickHome
        {
            get => n_leftJoystickHome.Value;
            set => n_leftJoystickHome.Value = value;
        }

        public Vector2 RightClampedPosition
        {
            get => n_rightClampedPosition.Value;
            set => n_rightClampedPosition.Value = value;
        }

        public Vector2 LeftClampedPosition
        {
            get => n_leftClampedPosition.Value;
            set => n_leftClampedPosition.Value = value;
        }

        public Vector2 RightJoystickStart
        {
            get => n_rightJoystickStart.Value;
            set => n_rightJoystickStart.Value = value;
        }

        public Vector2 LeftJoystickStart
        {
            get => n_leftJoystickStart.Value;
            set => n_leftJoystickStart.Value = value;
        }

        public Vector2 RightNormalizedJoystickPosition
        {
            get => n_rightNormalizedJoystickPosition.Value;
            set => n_rightNormalizedJoystickPosition.Value = value;
        }

        public Vector2 LeftNormalizedJoystickPosition
        {
            get => n_leftNormalizedJoystickPosition.Value;
            set => n_leftNormalizedJoystickPosition.Value = value;
        }

        public Vector2 EasedRightJoystickPosition
        {
            get => n_easedRightJoystickPosition.Value;
            set => n_easedRightJoystickPosition.Value = value;
        }

        public Vector2 EasedLeftJoystickPosition
        {
            get => n_easedLeftJoystickPosition.Value;
            set => n_easedLeftJoystickPosition.Value = value;
        }
    }
}
