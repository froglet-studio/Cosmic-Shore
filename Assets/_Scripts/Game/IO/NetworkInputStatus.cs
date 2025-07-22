using CosmicShore.Game.IO;
using CosmicShore.SOAP;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game
{
    public class NetworkInputStatus : NetworkBehaviour, IInputStatus
    {
        //–––––––––––––––––––––––––––––––––––––––––
        // Inspector-driven events & controller (unchanged)
        [SerializeField] ScriptableEventInputEvents _onButtonPressed;
        public ScriptableEventInputEvents OnButtonPressed => _onButtonPressed;

        [SerializeField] ScriptableEventInputEvents _onButtonReleased;
        public ScriptableEventInputEvents OnButtonReleased => _onButtonReleased;

        public InputController InputController { get; set; }

        //–––––––––––––––––––––––––––––––––––––––––
        // “Are we currently using the network vars?”
        bool _isNetwork = false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _isNetwork = true;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _isNetwork = false;
        }

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
        // Properties switch on _isNetwork
        public float XSum
        {
            get => _isNetwork ? n_xSum.Value   : _xSumLocal;
            set { if (_isNetwork) n_xSum.Value = value; else _xSumLocal = value; }
        }

        public float YSum
        {
            get => _isNetwork ? n_ySum.Value   : _ySumLocal;
            set { if (_isNetwork) n_ySum.Value = value; else _ySumLocal = value; }
        }

        public float XDiff
        {
            get => _isNetwork ? n_xDiff.Value  : _xDiffLocal;
            set { if (_isNetwork) n_xDiff.Value= value; else _xDiffLocal = value; }
        }

        public float YDiff
        {
            get => _isNetwork ? n_yDiff.Value  : _yDiffLocal;
            set { if (_isNetwork) n_yDiff.Value= value; else _yDiffLocal = value; }
        }

        public float Throttle
        {
            get => _isNetwork ? n_throt.Value  : _throttleLocal;
            set { if (_isNetwork) n_throt.Value= value; else _throttleLocal = value; }
        }

        public bool Idle
        {
            get => _isNetwork ? n_idle.Value   : _idleLocal;
            set { if (_isNetwork) n_idle.Value = value; else _idleLocal = value; }
        }

        public bool Paused
        {
            get => _isNetwork ? n_paused.Value : _pausedLocal;
            set { if (_isNetwork) n_paused.Value = value; else _pausedLocal = value; }
        }

        public bool IsGyroEnabled
        {
            get => _isNetwork ? n_gyro.Value   : _gyroLocal;
            set { if (_isNetwork) n_gyro.Value   = value; else _gyroLocal = value; }
        }

        public bool InvertYEnabled
        {
            get => _isNetwork ? n_invY.Value   : _invertYLocal;
            set { if (_isNetwork) n_invY.Value   = value; else _invertYLocal = value; }
        }

        public bool InvertThrottleEnabled
        {
            get => _isNetwork ? n_invT.Value   : _invertThrotLocal;
            set { if (_isNetwork) n_invT.Value   = value; else _invertThrotLocal = value; }
        }

        public bool OneTouchLeft
        {
            get => _isNetwork ? n_one.Value    : _oneTouchLocal;
            set { if (_isNetwork) n_one.Value    = value; else _oneTouchLocal = value; }
        }

        public bool CommandStickControls
        {
            get => _isNetwork ? n_cmd.Value    : _cmdStickLocal;
            set { if (_isNetwork) n_cmd.Value    = value; else _cmdStickLocal = value; }
        }

        public Vector2 RightJoystickHome
        {
            get => _isNetwork ? n_rHome.Value  : _rHomeLocal;
            set { if (_isNetwork) n_rHome.Value  = value; else _rHomeLocal = value; }
        }

        public Vector2 LeftJoystickHome
        {
            get => _isNetwork ? n_lHome.Value  : _lHomeLocal;
            set { if (_isNetwork) n_lHome.Value  = value; else _lHomeLocal = value; }
        }

        public Vector2 RightClampedPosition
        {
            get => _isNetwork ? n_rClamp.Value : _rClampLocal;
            set { if (_isNetwork) n_rClamp.Value = value; else _rClampLocal = value; }
        }

        public Vector2 LeftClampedPosition
        {
            get => _isNetwork ? n_lClamp.Value : _lClampLocal;
            set { if (_isNetwork) n_lClamp.Value = value; else _lClampLocal = value; }
        }

        public Vector2 RightJoystickStart
        {
            get => _isNetwork ? n_rStart.Value : _rStartLocal;
            set { if (_isNetwork) n_rStart.Value = value; else _rStartLocal = value; }
        }

        public Vector2 LeftJoystickStart
        {
            get => _isNetwork ? n_lStart.Value : _lStartLocal;
            set { if (_isNetwork) n_lStart.Value = value; else _lStartLocal = value; }
        }

        public Vector2 RightNormalizedJoystickPosition
        {
            get => _isNetwork ? n_rNorm.Value  : _rNormLocal;
            set { if (_isNetwork) n_rNorm.Value  = value; else _rNormLocal = value; }
        }

        public Vector2 LeftNormalizedJoystickPosition
        {
            get => _isNetwork ? n_lNorm.Value  : _lNormLocal;
            set { if (_isNetwork) n_lNorm.Value  = value; else _lNormLocal = value; }
        }

        public Vector2 EasedRightJoystickPosition
        {
            get => _isNetwork ? n_rEased.Value : _rEasedLocal;
            set { if (_isNetwork) n_rEased.Value = value; else _rEasedLocal = value; }
        }

        public Vector2 EasedLeftJoystickPosition
        {
            get => _isNetwork ? n_lEased.Value : _lEasedLocal;
            set { if (_isNetwork) n_lEased.Value = value; else _lEasedLocal = value; }
        }

        public Vector2 SingleTouchValue
        {
            get => _isNetwork ? n_single.Value: _singleTouchLocal;
            set { if (_isNetwork) n_single.Value= value; else _singleTouchLocal = value; }
        }

        public Vector3 ThreeDPosition
        {
            get => _isNetwork ? n_3dPos.Value  : _threeDLocal;
            set { if (_isNetwork) n_3dPos.Value  = value; else _threeDLocal = value; }
        }

        public Quaternion GetGyroRotation() => InputController.GetGyroRotation();
    }
}
