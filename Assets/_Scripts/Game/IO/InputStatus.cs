namespace CosmicShore.Core
{
    using UnityEngine;

    public class InputStatus : MonoBehaviour, IInputStatus
    {
        // Floats
        private float xSum;
        private float ySum;
        private float xDiff;
        private float yDiff;

        public float XSum
        {
            get => xSum;
            set => xSum = value;
        }

        public float YSum
        {
            get => ySum;
            set => ySum = value;
        }

        public float XDiff
        {
            get => xDiff;
            set => xDiff = value;
        }

        public float YDiff
        {
            get => yDiff;
            set => yDiff = value;
        }

        // Booleans
        private bool idle;
        private bool paused;
        private bool isGyroEnabled;
        private bool invertYEnabled;
        private bool invertThrottleEnabled;
        private bool oneTouchLeft;

        public bool Idle
        {
            get => idle;
            set => idle = value;
        }

        public bool Paused
        {
            get => paused;
            set => paused = value;
        }

        public bool IsGyroEnabled
        {
            get => isGyroEnabled;
            set => isGyroEnabled = value;
        }

        public bool InvertYEnabled
        {
            get => invertYEnabled;
            set => invertYEnabled = value;
        }

        public bool InvertThrottleEnabled
        {
            get => invertThrottleEnabled;
            set => invertThrottleEnabled = value;
        }

        public bool OneTouchLeft
        {
            get => oneTouchLeft;
            set => oneTouchLeft = value;
        }

        // Vectors
        private Vector2 rightJoystickHome;
        private Vector2 leftJoystickHome;
        private Vector2 rightClampedPosition;
        private Vector2 leftClampedPosition;
        private Vector2 rightJoystickStart;
        private Vector2 leftJoystickStart;
        private Vector2 rightNormalizedJoystickPosition;
        private Vector2 leftNormalizedJoystickPosition;
        private Vector2 easedRightJoystickPosition;
        private Vector2 easedLeftJoystickPosition;

        public Vector2 RightJoystickHome
        {
            get => rightJoystickHome;
            set => rightJoystickHome = value;
        }

        public Vector2 LeftJoystickHome
        {
            get => leftJoystickHome;
            set => leftJoystickHome = value;
        }

        public Vector2 RightClampedPosition
        {
            get => rightClampedPosition;
            set => rightClampedPosition = value;
        }

        public Vector2 LeftClampedPosition
        {
            get => leftClampedPosition;
            set => leftClampedPosition = value;
        }

        public Vector2 RightJoystickStart
        {
            get => rightJoystickStart;
            set => rightJoystickStart = value;
        }

        public Vector2 LeftJoystickStart
        {
            get => leftJoystickStart;
            set => leftJoystickStart = value;
        }

        public Vector2 RightNormalizedJoystickPosition
        {
            get => rightNormalizedJoystickPosition;
            set => rightNormalizedJoystickPosition = value;
        }

        public Vector2 LeftNormalizedJoystickPosition
        {
            get => leftNormalizedJoystickPosition;
            set => leftNormalizedJoystickPosition = value;
        }

        public Vector2 EasedRightJoystickPosition
        {
            get => easedRightJoystickPosition;
            set => easedRightJoystickPosition = value;
        }

        public Vector2 EasedLeftJoystickPosition
        {
            get => easedLeftJoystickPosition;
            set => easedLeftJoystickPosition = value;
        }
    }

}