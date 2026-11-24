using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A loose crystal adrift in a Salvage cell (Docs/Voyages/MILESTONE_2.md §4; Shombith 2026-11-24: "collect loose
    /// crystals"): it wanders slowly on a seeded heading that turns a little, and stays inside its cell's
    /// <see cref="Radius"/> of <see cref="Centre"/>. It is an ordinary pickup in every other way. The motion stops for
    /// good the moment its collider is turned off (the crystal was taken and its capture flourish owns the transform).
    /// One position per frame and no physics body, like <see cref="EjectedCrystal"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoyageDriftCrystal : MonoBehaviour
    {
        /// <summary>Degrees a second the heading turns at most.</summary>
        const float TurnDegreesPerSecond = 12f;

        Vector3 _heading;
        float _speed;
        float _turn;
        Collider _collider;

        /// <summary>The cell's centre the crystal stays near.</summary>
        public Vector3 Centre { get; private set; }

        /// <summary>How far from <see cref="Centre"/> the crystal may drift before it turns back in.</summary>
        public float Radius { get; private set; }

        /// <summary>Set the drift: <paramref name="heading"/> (any length), <paramref name="speed"/> u/s, a turn of
        /// <paramref name="turn"/> (-1..1 of the most a second), kept inside <paramref name="radius"/> of
        /// <paramref name="centre"/>.</summary>
        public void Drift(Vector3 heading, float speed, float turn, Vector3 centre, float radius)
        {
            _heading = heading.sqrMagnitude > 1e-6f ? heading.normalized : Vector3.forward;
            _speed = Mathf.Max(0f, speed);
            _turn = Mathf.Clamp(turn, -1f, 1f) * TurnDegreesPerSecond;
            Centre = centre;
            Radius = Mathf.Max(1f, radius);
            _collider = GetComponent<Collider>();
            enabled = _speed > 0f;
        }

        void Update()
        {
            if (_collider && !_collider.enabled)
            {
                Destroy(this);
                return;
            }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var pos = transform.position;
            // Past the rim: steer straight back toward the centre; otherwise turn gently about the local up.
            Vector3 home = Centre - pos;
            if (home.sqrMagnitude > Radius * Radius)
                _heading = Vector3.RotateTowards(_heading, home.normalized, Mathf.Deg2Rad * 90f * dt, 0f);
            else
                _heading = Quaternion.AngleAxis(_turn * dt, Vector3.up) * _heading;
            transform.position = pos + _heading * (_speed * dt);
        }
    }
}
