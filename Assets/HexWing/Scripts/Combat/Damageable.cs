using System.Collections.Generic;
using UnityEngine;

namespace HexWing
{
    public enum Team { Player, Enemy, Neutral }

    /// <summary>
    /// Hull and shields of anything that can be shot: shields take damage first and recharge after a
    /// quiet moment; at zero hull it is destroyed. Lasers and missiles find it from the collider they hit
    /// (looked up once per collider and cached). <see cref="All"/> lists every live one for targeting.
    /// </summary>
    public class Damageable : MonoBehaviour
    {
        public static readonly List<Damageable> All = new();
        static readonly Dictionary<Collider, Damageable> ByCollider = new();

        public Team Team = Team.Enemy;
        public float MaxHull = 100f;
        public float MaxShields;
        public float ShieldDelay = 4f;
        public float ShieldRegen = 20f;
        [Tooltip("Shown on the target display.")]
        public string DisplayName = "CEL";
        [Tooltip("Can be locked and gets HUD brackets.")]
        public bool Targetable = true;

        public float Hull { get; private set; }
        public float Shields { get; private set; }
        public bool Alive => Hull > 0f;
        /// <summary>World velocity (m/s), for lead markers and missiles. Set by whatever moves it.</summary>
        public Vector3 Velocity { get; set; }

        /// <summary>Damage taken: amount, hit point, who shot (may be null).</summary>
        public event System.Action<float, Vector3, Damageable> Damaged;
        public event System.Action<Damageable> Destroyed;

        float _lastHit = -99f;

        void Awake() => Restore();

        void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        void OnDisable() => All.Remove(this);

        public void Restore()
        {
            Hull = MaxHull;
            Shields = MaxShields;
            _lastHit = -99f;
        }

        void Update()
        {
            if (!Alive || Shields >= MaxShields) return;
            if (Time.time - _lastHit >= ShieldDelay) Shields = Mathf.Min(MaxShields, Shields + ShieldRegen * Time.deltaTime);
        }

        public void Damage(float amount, Vector3 point, Damageable source)
        {
            if (!Alive || amount <= 0f) return;
            _lastHit = Time.time;
            float toShields = Mathf.Min(Shields, amount);
            Shields -= toShields;
            Hull = Mathf.Max(0f, Hull - (amount - toShields));
            Damaged?.Invoke(amount, point, source);
            if (Hull <= 0f) Destroyed?.Invoke(source);
        }

        /// <summary>The damageable a collider belongs to (null if none). Cached per collider.</summary>
        public static Damageable Of(Collider c)
        {
            if (c == null) return null;
            if (ByCollider.TryGetValue(c, out var d) && d != null) return d;
            d = c.GetComponentInParent<Damageable>();
            ByCollider[c] = d;
            return d;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            All.Clear();
            ByCollider.Clear();
        }
    }
}
