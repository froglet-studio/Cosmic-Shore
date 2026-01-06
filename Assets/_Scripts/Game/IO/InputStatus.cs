using System;
using CosmicShore.Game.IO;
using CosmicShore.Soap;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game
{
    public class InputStatus : NetworkBehaviour, IInputStatus
    {
        public event Action<bool> OnToggleInputPaused;
        
        //–––––––––––––––––––––––––––––––––––––––––
        // Inspector-driven events & controller (unchanged)
        [SerializeField] ScriptableEventInputEvents _onButtonPressed;
        public ScriptableEventInputEvents OnButtonPressed => _onButtonPressed;

        [SerializeField] ScriptableEventInputEvents _onButtonReleased;
        public ScriptableEventInputEvents OnButtonReleased => _onButtonReleased;

        public InputController InputController { get; set; }

        //–––––––––––––––––––––––––––––––––––––––––
        // Local fallbacks
        float   _xSumLocal,   _ySumLocal,   _xDiffLocal,   _yDiffLocal,   _throttleLocal;
        bool    _idleLocal,   _pausedLocal, _gyroLocal,    _invertYLocal, _invertThrotLocal,
                _oneTouchLocal, _cmdStickLocal;
        Vector2 _rHomeLocal,  _lHomeLocal,  _rClampLocal,  _lClampLocal,
                _rStartLocal, _lStartLocal, _rNormLocal,   _lNormLocal,
                _rEasedLocal, _lEasedLocal, _singleTouchLocal;
        Vector3 _threeDLocal;

        //–––––––––––––––––––––––––––––––––––––––––
        // NetworkVariables
        readonly NetworkVariable<float>   n_xSum   = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<float>   n_ySum   = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<float>   n_xDiff  = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<float>   n_yDiff  = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<float>   n_throt  = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);

        readonly NetworkVariable<bool>    n_idle   = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool>    n_paused = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool>    n_gyro   = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool>    n_invY   = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool>    n_invT   = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool>    n_one    = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool>    n_cmd    = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);

        readonly NetworkVariable<Vector2> n_rHome  = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector2> n_lHome  = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector2> n_rClamp = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector2> n_lClamp = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector2> n_rStart = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector2> n_lStart = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector2> n_rNorm  = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector2> n_lNorm  = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector2> n_rEased = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector2> n_lEased = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector2> n_single = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector3> n_3dPos  = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);

        //–––––––––––––––––––––––––––––––––––––––––
        // Properties switch on IsSpawned
        public float XSum
        {
            get => IsSpawned ? n_xSum.Value   : _xSumLocal;
            set { if (IsSpawned && IsOwner) n_xSum.Value = value; else _xSumLocal = value; }
        }

        public float YSum
        {
            get => IsSpawned ? n_ySum.Value   : _ySumLocal;
            set { if (IsSpawned && IsOwner) n_ySum.Value = value; else _ySumLocal = value; }
        }

        public float XDiff
        {
            get => IsSpawned ? n_xDiff.Value  : _xDiffLocal;
            set { if (IsSpawned && IsOwner) n_xDiff.Value= value; else _xDiffLocal = value; }
        }

        public float YDiff
        {
            get => IsSpawned ? n_yDiff.Value  : _yDiffLocal;
            set { if (IsSpawned && IsOwner) n_yDiff.Value= value; else _yDiffLocal = value; }
        }

        public float Throttle
        {
            get => IsSpawned ? n_throt.Value  : _throttleLocal;
            set { if (IsSpawned && IsOwner) n_throt.Value= value; else _throttleLocal = value; }
        }

        public bool Idle
        {
            get => IsSpawned ? n_idle.Value   : _idleLocal;
            set { if (IsSpawned && IsOwner) n_idle.Value = value; else _idleLocal = value; }
        }

        public bool Paused
        {
            get => IsSpawned ? n_paused.Value : _pausedLocal;
            set
            {
                if (IsSpawned && IsOwner) 
                    n_paused.Value = value;
                else
                {
                    _pausedLocal = value;
                    OnToggleInputPaused?.Invoke(value);
                }
            }
        }

        public bool IsGyroEnabled
        {
            get => IsSpawned ? n_gyro.Value   : _gyroLocal;
            set { if (IsSpawned && IsOwner) n_gyro.Value   = value; else _gyroLocal = value; }
        }

        public bool InvertYEnabled
        {
            get => IsSpawned ? n_invY.Value   : _invertYLocal;
            set { if (IsSpawned && IsOwner) n_invY.Value   = value; else _invertYLocal = value; }
        }

        public bool InvertThrottleEnabled
        {
            get => IsSpawned ? n_invT.Value   : _invertThrotLocal;
            set { if (IsSpawned && IsOwner) n_invT.Value   = value; else _invertThrotLocal = value; }
        }

        public bool OneTouchLeft
        {
            get => IsSpawned ? n_one.Value    : _oneTouchLocal;
            set { if (IsSpawned && IsOwner) n_one.Value    = value; else _oneTouchLocal = value; }
        }

        public bool CommandStickControls
        {
            get => IsSpawned ? n_cmd.Value    : _cmdStickLocal;
            set { if (IsSpawned && IsOwner) n_cmd.Value    = value; else _cmdStickLocal = value; }
        }

        public Vector2 RightJoystickHome
        {
            get => IsSpawned ? n_rHome.Value  : _rHomeLocal;
            set { if (IsSpawned && IsOwner) n_rHome.Value  = value; else _rHomeLocal = value; }
        }

        public Vector2 LeftJoystickHome
        {
            get => IsSpawned ? n_lHome.Value  : _lHomeLocal;
            set { if (IsSpawned && IsOwner) n_lHome.Value  = value; else _lHomeLocal = value; }
        }

        public Vector2 RightClampedPosition
        {
            get => IsSpawned ? n_rClamp.Value : _rClampLocal;
            set { if (IsSpawned && IsOwner) n_rClamp.Value = value; else _rClampLocal = value; }
        }

        public Vector2 LeftClampedPosition
        {
            get => IsSpawned ? n_lClamp.Value : _lClampLocal;
            set { if (IsSpawned && IsOwner) n_lClamp.Value = value; else _lClampLocal = value; }
        }

        public Vector2 RightJoystickStart
        {
            get => IsSpawned ? n_rStart.Value : _rStartLocal;
            set { if (IsSpawned && IsOwner) n_rStart.Value = value; else _rStartLocal = value; }
        }

        public Vector2 LeftJoystickStart
        {
            get => IsSpawned ? n_lStart.Value : _lStartLocal;
            set { if (IsSpawned && IsOwner) n_lStart.Value = value; else _lStartLocal = value; }
        }

        public Vector2 RightNormalizedJoystickPosition
        {
            get => IsSpawned ? n_rNorm.Value  : _rNormLocal;
            set { if (IsSpawned && IsOwner) n_rNorm.Value  = value; else _rNormLocal = value; }
        }

        public Vector2 LeftNormalizedJoystickPosition
        {
            get => IsSpawned ? n_lNorm.Value  : _lNormLocal;
            set { if (IsSpawned && IsOwner) n_lNorm.Value  = value; else _lNormLocal = value; }
        }

        public Vector2 EasedRightJoystickPosition
        {
            get => IsSpawned ? n_rEased.Value : _rEasedLocal;
            set { if (IsSpawned && IsOwner) n_rEased.Value = value; else _rEasedLocal = value; }
        }

        public Vector2 EasedLeftJoystickPosition
        {
            get => IsSpawned ? n_lEased.Value : _lEasedLocal;
            set { if (IsSpawned && IsOwner) n_lEased.Value = value; else _lEasedLocal = value; }
        }

        public Vector2 SingleTouchValue
        {
            get => IsSpawned ? n_single.Value: _singleTouchLocal;
            set { if (IsSpawned && IsOwner) n_single.Value= value; else _singleTouchLocal = value; }
        }

        public Vector3 ThreeDPosition
        {
            get => IsSpawned ? n_3dPos.Value  : _threeDLocal;
            set { if (IsSpawned && IsOwner) n_3dPos.Value  = value; else _threeDLocal = value; }
        }

        public Quaternion GetGyroRotation() => InputController.GetGyroRotation();
        
        public override void OnNetworkSpawn()
        {
            n_paused.OnValueChanged += OnNetworkPausedValueChanged;
        }

        public override void OnNetworkDespawn()
        {
            n_paused.OnValueChanged -= OnNetworkPausedValueChanged;
        }

        public void ResetForReplay()
        {
            // Non-owners shouldn't modify replicated state
            if (IsSpawned && !IsOwner)
                return;

            // Reset scalar inputs
            XSum = 0f;
            YSum = 0f;
            XDiff = 0f;
            YDiff = 0f;
            Throttle = 0f;

            // Reset booleans
            Idle = true;
            Paused = true;
            /*IsGyroEnabled = false;
            InvertYEnabled = false;
            InvertThrottleEnabled = false;
            OneTouchLeft = false;
            CommandStickControls = false;*/

            // Reset joystick / touch vectors
            RightJoystickHome = Vector2.zero;
            LeftJoystickHome = Vector2.zero;
            RightClampedPosition = Vector2.zero;
            LeftClampedPosition = Vector2.zero;
            RightJoystickStart = Vector2.zero;
            LeftJoystickStart = Vector2.zero;
            RightNormalizedJoystickPosition = Vector2.zero;
            LeftNormalizedJoystickPosition = Vector2.zero;
            EasedRightJoystickPosition = Vector2.zero;
            EasedLeftJoystickPosition = Vector2.zero;
            SingleTouchValue = Vector2.zero;
            ThreeDPosition = Vector3.zero;
        }
        
        void OnNetworkPausedValueChanged(bool oldValue, bool newValue) => OnToggleInputPaused?.Invoke(newValue);
    }
}
