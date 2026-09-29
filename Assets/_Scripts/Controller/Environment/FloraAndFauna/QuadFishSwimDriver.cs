using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The QuadFish's swim rig — the CPU half of "a quadfish is a creature, not a prop".
    ///
    /// <para>WHY THIS EXISTS. Every other creature in the game gets its motion from
    /// something this one does not have. The shark and the brittlestar carry an FBX
    /// ARMATURE driven by an Animator plus Animation Rigging (DampedTransform chains for
    /// the brittlestar's dangling arms, MultiParentConstraints binding the shark's prism
    /// clusters to bones, MultiAimConstraints for its jaw); the boids get theirs from
    /// flocking, which is whole-body travel rather than deformation. The QuadFish's
    /// model, <c>mediumfish.fbx</c>, contains exactly one Model node and NO bones, so
    /// none of that was available to it: its four fin prisms were parented rigidly to a
    /// static body transform and nothing on the creature moved relative to anything
    /// else. It read as stiff because it was.</para>
    ///
    /// <para>Its sibling <see cref="SharkJawDriver"/> is the shape this follows — a small
    /// presentation component that reads creature state and writes transforms, owned by
    /// nothing else, replicated by nothing. The body's own undulation is NOT here: that
    /// is a GPU vertex shear in SpindleSway.hlsl, costing zero CPU. This file does the
    /// part a vertex shader cannot, which is move the fins, because a fin is a separate
    /// rigid object (a HealthPrism — conserved mass with its own collider) rather than
    /// vertices of the body mesh.</para>
    ///
    /// <para>TWO PROPERTIES MAKE THIS FREE, and both are worth stating because they are
    /// what would otherwise make animating conserved mass expensive:</para>
    /// <list type="number">
    /// <item>The flap writes <c>localRotation</c> and never <c>localPosition</c>, so a
    /// fin prism's POSITION is untouched and <see cref="PrismSpatialIndex"/> sees no
    /// change at all. PhysX re-orients the collider on its own.</item>
    /// <item>The bank writes the BODY transform, which does move the fins — and
    /// <c>LightFauna.Update</c> already calls <c>NotifyBodyPrismsMoved()</c> every frame
    /// for every creature (the movers contract), so that sync is paid for whether this
    /// component exists or not. <see cref="DefaultExecutionOrder"/> -1 is what makes it
    /// see THIS frame's pose rather than last frame's: script order between two
    /// components on different GameObjects is otherwise undefined.</item>
    /// </list>
    ///
    /// <para>Nothing here is authoritative and nothing here replicates. Every peer runs
    /// it against its own replica of the creature's transform, exactly as the shark's
    /// jaw does — the fish looks alive on every machine without a byte on the wire.</para>
    /// </summary>
    [DefaultExecutionOrder(-1)]
    public class QuadFishSwimDriver : MonoBehaviour
    {
        [Header("Fin stroke")]
        [Tooltip("Fin sweep, in degrees either side of the authored rest pose, while the " +
                 "creature is drifting. A fish at rest still trims with its fins.")]
        [Min(0f)] [SerializeField] float idleStrokeDegrees = 6f;

        [Tooltip("Fin sweep at and above 'Speed For Full Stroke'. This is the fleeing/" +
                 "chasing pose.")]
        [Min(0f)] [SerializeField] float fastStrokeDegrees = 26f;

        [Tooltip("Stroke cadence in beats per second while drifting.")]
        [Min(0f)] [SerializeField] float idleStrokeHz = 0.5f;

        [Tooltip("Stroke cadence in beats per second at and above 'Speed For Full Stroke'.")]
        [Min(0f)] [SerializeField] float fastStrokeHz = 2.2f;

        [Tooltip("The speed at which the stroke reaches its fast amplitude and cadence. " +
                 "LightFaunaDataSO authors maxSpeed per species — the shipped QuadFish " +
                 "data sits well under the shark's 35, so this is deliberately low.")]
        [Min(0.01f)] [SerializeField] float speedForFullStroke = 14f;

        [Header("Bank")]
        [Tooltip("Degrees of roll per 100 deg/s of measured turn rate. NEGATE this if the " +
                 "fish banks out of its turns instead of into them — the sign depends on " +
                 "which way the authored body mesh faces, which no code here can know.")]
        [SerializeField] float bankDegreesPerHundredTurnRate = 34f;

        [Tooltip("Hard cap on roll, so a tight goal change cannot barrel-roll the fish.")]
        [Min(0f)] [SerializeField] float maxBankDegrees = 28f;

        [Tooltip("How quickly roll chases the turn rate. Low reads as a heavy fish.")]
        [Min(0.01f)] [SerializeField] float bankResponse = 3.5f;

        Fauna _fauna;
        Transform _body;
        Quaternion _bodyRest;

        Transform[] _fins;
        Quaternion[] _finRest;
        Vector3[] _finAxis;
        float[] _finPhase;

        // Integrated, NOT `Time.time * hz`. Cadence changes with speed, and a phase
        // computed as time*frequency TELEPORTS the wave whenever the frequency moves —
        // a fish that accelerates would visibly skip mid-stroke. Integrating the phase
        // makes a cadence change continuous by construction.
        float _stroke;

        float _bank;
        Vector3 _previousForward;

        void Awake()
        {
            _fauna = GetComponentInParent<Fauna>();

            // The body is the transform the fin prisms hang off — which is also the
            // spindle, so banking it banks the mesh and its fins as one piece.
            var spindle = GetComponentInChildren<Spindle>(true);
            _body = spindle ? spindle.transform : transform;
            _bodyRest = _body.localRotation;

            CacheFins();
            _previousForward = transform.forward;
        }

        /// <summary>
        /// Finds the fins the same way <c>Fauna.CacheBodyPrisms</c> finds the body — by
        /// component, never by a serialized list — so a fifth fin, or a re-authored
        /// prefab, needs no wiring here. The elemental heart is not a HealthPrism and is
        /// correctly excluded.
        /// </summary>
        void CacheFins()
        {
            var prisms = GetComponentsInChildren<HealthPrism>(true);
            _fins = new Transform[prisms.Length];
            _finRest = new Quaternion[prisms.Length];
            _finAxis = new Vector3[prisms.Length];
            _finPhase = new float[prisms.Length];

            for (int i = 0; i < prisms.Length; i++)
            {
                var fin = prisms[i].transform;
                _fins[i] = fin;
                _finRest[i] = fin.localRotation;
                _finAxis[i] = ResolveStrokeAxis(fin.localRotation);

                // Diagonal pairs beat together, like a sea turtle's gait — derived from
                // the authored fin positions rather than assigned by index, so the
                // pairing survives the prefab being re-laid out or the fins being
                // returned in a different order by GetComponentsInChildren.
                var p = fin.localPosition;
                _finPhase[i] = (p.x * p.y) >= 0f ? 0f : Mathf.PI;
            }
        }

        /// <summary>
        /// The axis a fin sweeps about, in that fin's own local space: perpendicular to
        /// both its span (local +Y — a HealthBlock is a box whose long axis is Y) and the
        /// body's forward. Rotating about it carries the fin's tip fore-and-aft, which is
        /// an oar stroke; rotating about either of the other two axes would twist the fin
        /// about its own length or flap it against the body.
        /// </summary>
        static Vector3 ResolveStrokeAxis(Quaternion restRotation)
        {
            Vector3 forwardInFinSpace = Quaternion.Inverse(restRotation) * Vector3.forward;
            Vector3 axis = Vector3.Cross(Vector3.up, forwardInFinSpace);

            // Degenerate only if the fin points straight down the body axis, which no
            // authored QuadFish fin does — fall back to the fin's own X, which is what
            // the cross product resolves to for an unrotated fin anyway.
            return axis.sqrMagnitude < 1e-6f ? Vector3.right : axis.normalized;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // A withering creature stops swimming. This is not just taste: LightFauna's
            // death leaves the body prisms standing as a SKELETON (ordinary cell mass,
            // re-parented away from the fish), so continuing to drive the cached fin
            // transforms would be animating mass that is no longer ours.
            bool alive = _fauna && !_fauna.IsDying;

            float speed01 = alive && _fauna
                ? Mathf.Clamp01(_fauna.CurrentSpeed / speedForFullStroke)
                : 0f;

            StrokeFins(dt, speed01, alive);
            BankBody(dt, alive);
        }

        void StrokeFins(float dt, float speed01, bool alive)
        {
            if (_fins == null) return;

            float hz = Mathf.Lerp(idleStrokeHz, fastStrokeHz, speed01);
            _stroke += hz * 2f * Mathf.PI * dt;
            if (_stroke > 2f * Mathf.PI) _stroke -= 2f * Mathf.PI;

            // Dying fins ease to their rest pose rather than freezing mid-stroke — the
            // continuity law applies to a limb exactly as it applies to a prism.
            float sweep = alive ? Mathf.Lerp(idleStrokeDegrees, fastStrokeDegrees, speed01) : 0f;

            for (int i = 0; i < _fins.Length; i++)
            {
                var fin = _fins[i];
                if (!fin) continue;

                float angle = sweep * Mathf.Sin(_stroke + _finPhase[i]);
                Quaternion target = _finRest[i] * Quaternion.AngleAxis(angle, _finAxis[i]);
                fin.localRotation = alive
                    ? target
                    : Quaternion.RotateTowards(fin.localRotation, _finRest[i], 90f * dt);
            }
        }

        void BankBody(float dt, bool alive)
        {
            if (!_body) return;

            Vector3 forward = transform.forward;

            // Measured off the creature's own transform, so it works identically on a
            // simulating host and on a peer whose fish is being driven by
            // NetworkTransform — there is nothing here to replicate.
            float turnRate = Vector3.SignedAngle(_previousForward, forward, transform.up) / dt;
            _previousForward = forward;

            float target = alive
                ? Mathf.Clamp(turnRate * bankDegreesPerHundredTurnRate * 0.01f,
                              -maxBankDegrees, maxBankDegrees)
                : 0f;

            _bank = Mathf.Lerp(_bank, target, 1f - Mathf.Exp(-bankResponse * dt));
            _body.localRotation = _bodyRest * Quaternion.AngleAxis(_bank, Vector3.forward);
        }

        void OnDisable()
        {
            // Pooled reuse: hand the rig back at rest so a recycled fish never blooms in
            // mid-stroke or mid-bank.
            _stroke = 0f;
            _bank = 0f;
            if (_body) _body.localRotation = _bodyRest;
            if (_fins == null) return;
            for (int i = 0; i < _fins.Length; i++)
                if (_fins[i]) _fins[i].localRotation = _finRest[i];
        }
    }
}
