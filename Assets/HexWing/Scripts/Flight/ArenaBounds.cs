using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// The playable area is a sphere around the arena centre. Outside it a countdown runs (shown on the
    /// cockpit display, with a haptic tick each second); at zero the ship is out (M2: back to the start,
    /// from M3 on: destroyed). Flying back in stops the countdown.
    /// </summary>
    public class ArenaBounds : MonoBehaviour
    {
        public Vector3 Center = Vector3.zero;

        public bool Outside { get; private set; }
        /// <summary>Seconds left before the ship is out (only while outside).</summary>
        public float SecondsLeft { get; private set; }

        public event System.Action OutOfBounds;

        int _lastTick;

        void Update()
        {
            var cfg = GameConfig.Instance.arena;
            bool outside = (transform.position - Center).sqrMagnitude > cfg.radius * cfg.radius;
            if (outside && !Outside)
            {
                SecondsLeft = cfg.outsideSeconds;
                _lastTick = Mathf.CeilToInt(SecondsLeft) + 1;
                Debug.Log("[HexWing] Left the arena: countdown started.");
            }
            Outside = outside;
            if (!outside) return;

            SecondsLeft -= Time.deltaTime;
            int whole = Mathf.CeilToInt(SecondsLeft);
            if (whole < _lastTick)
            {
                _lastTick = whole;
                Haptics.PlayBoth(GameConfig.Instance.haptics.boundsTick);
            }
            if (SecondsLeft <= 0f)
            {
                Outside = false;
                OutOfBounds?.Invoke();
            }
        }

        /// <summary>Direction from the ship back to the arena centre (for the guide arrow).</summary>
        public Vector3 HomeDirection => (Center - transform.position).normalized;
    }
}
