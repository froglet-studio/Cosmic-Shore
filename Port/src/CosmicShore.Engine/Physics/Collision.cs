using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>One contact between two colliders (UnityEngine.ContactPoint).</summary>
    public struct ContactPoint
    {
        public Vector3 point { get; set; }
        public Vector3 normal { get; set; }
        public float separation { get; set; }
        public Collider thisCollider { get; set; }
        public Collider otherCollider { get; set; }
        public Vector3 impulse { get; set; }
    }

    /// <summary>A solid-collision report (UnityEngine.Collision).</summary>
    public class Collision
    {
        readonly List<ContactPoint> _contacts = new();
        public Collider collider { get; set; }
        public Rigidbody rigidbody { get; set; }
        public Transform transform => collider != null ? collider.transform : null;
        public GameObject gameObject => collider != null ? collider.gameObject : null;
        public Vector3 relativeVelocity { get; set; }
        public Vector3 impulse { get; set; }
        public int contactCount => _contacts.Count;
        public ContactPoint[] contacts => _contacts.ToArray();
        public ContactPoint GetContact(int index) => _contacts[index];
        public int GetContacts(ContactPoint[] contacts) { int n = System.Math.Min(contacts.Length, _contacts.Count); for (int i = 0; i < n; i++) contacts[i] = _contacts[i]; return n; }
        public int GetContacts(List<ContactPoint> contacts) { contacts.Clear(); contacts.AddRange(_contacts); return _contacts.Count; }
        public void AddContact(ContactPoint c) => _contacts.Add(c);
    }

    // ── 2D physics (the game carries a handful of 2D colliders on UI/debug objects) ──

    public class Collider2D : Behaviour
    {
        public bool isTrigger { get; set; }
        public Vector2 offset { get; set; }
        public Rigidbody2D attachedRigidbody => GetComponent<Rigidbody2D>();
        public Bounds bounds => new(transform.position, Vector3.zero);
        public bool OverlapPoint(Vector2 point) => false;
    }

    public class BoxCollider2D : Collider2D
    {
        public Vector2 size { get; set; } = Vector2.one;
        public float edgeRadius { get; set; }
    }

    public class CircleCollider2D : Collider2D
    {
        public float radius { get; set; } = 0.5f;
    }

    public enum RigidbodyType2D { Dynamic = 0, Kinematic = 1, Static = 2 }

    public class Rigidbody2D : Component
    {
        public Vector2 linearVelocity { get; set; }
        public Vector2 velocity { get => linearVelocity; set => linearVelocity = value; }
        public float angularVelocity { get; set; }
        public float gravityScale { get; set; } = 1f;
        public RigidbodyType2D bodyType { get; set; }
        public bool isKinematic { get => bodyType == RigidbodyType2D.Kinematic; set => bodyType = value ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic; }
        public bool simulated { get; set; } = true;
        public Vector2 position { get; set; }
        public void MovePosition(Vector2 p) => position = p;
    }

    public class Collision2D
    {
        public Collider2D collider { get; set; }
        public Collider2D otherCollider { get; set; }
        public GameObject gameObject => collider != null ? collider.gameObject : null;
        public Transform transform => collider != null ? collider.transform : null;
        public Vector2 relativeVelocity { get; set; }
    }
}
