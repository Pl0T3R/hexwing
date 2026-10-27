using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Moving parts of the cockpit: the stick tilts with pitch and yaw and twists with roll, the throttle
    /// lever follows the throttle, and a small arrow above the console points at the next course ring,
    /// the current target or the nearest enemy (or back to the arena when outside). They show what the ship is being told to do, whatever the scheme.
    /// </summary>
    public class CockpitInstruments : MonoBehaviour
    {
        const float StickTilt = 18f, StickTwist = 14f, LeverSwing = 28f;
        static readonly Vector3 ArrowOffset = new(0f, -0.2f, 0.5f);   // from the eye point: just above the centre display

        PlayerCockpit _cockpit;
        ShipFlight _ship;
        ArenaBounds _bounds;
        ShipWeapons _weapons;
        Transform _arrow;
        Quaternion _stickRest, _leverRest;

        void Start()
        {
            _cockpit = GetComponent<PlayerCockpit>();
            _ship = GetComponent<ShipFlight>();
            _bounds = GetComponent<ArenaBounds>();
            _weapons = GetComponent<ShipWeapons>();
            if (_cockpit == null) return;
            if (_cockpit.Displays.Stick != null) _stickRest = _cockpit.Displays.Stick.localRotation;
            if (_cockpit.Displays.Throttle != null) _leverRest = _cockpit.Displays.Throttle.localRotation;

            var b = new MeshKit.Builder();
            b.Prism(new Vector3(0f, 0f, -0.035f), new Vector3(0f, 0f, 0.035f), 0.014f, 0.0015f, 3);   // a slim three-sided dart
            b.Box(new Vector3(0f, 0f, -0.04f), new Vector3(0.02f, 0.003f, 0.012f), Quaternion.identity);
            var go = MeshKit.Part("Guide Arrow", transform, b.ToMesh("Guide Arrow"), Mats.Get(Mats.Accent));
            _arrow = go.transform;
            _arrow.localPosition = GameConfig.Instance.cockpit.eyePoint + ArrowOffset;
        }

        void LateUpdate()
        {
            if (_cockpit == null || _ship == null) return;
            var c = _ship.Controls;
            if (_cockpit.Displays.Stick != null)
            {
                // nose down = stick forward (+x tilts the top forward); yaw right = top to the right (-z)
                _cockpit.Displays.Stick.localRotation = _stickRest *
                    Quaternion.Euler(-c.Pitch * StickTilt, c.Roll * StickTwist, -c.Yaw * StickTilt);
            }
            if (_cockpit.Displays.Throttle != null)
                _cockpit.Displays.Throttle.localRotation = _leverRest * Quaternion.Euler(Mathf.Lerp(-LeverSwing, LeverSwing, c.Throttle), 0f, 0f);

            if (_arrow == null) return;
            Vector3? target = null;
            if (_bounds != null && _bounds.Outside) target = transform.position + _bounds.HomeDirection * 1000f;
            else if (Training.Instance != null && Training.Instance.isActiveAndEnabled)
                target = Training.Instance.Pointer != null ? Training.Instance.Pointer.position : (Vector3?)null;
            else if (RingsCourse.Instance != null && RingsCourse.Instance.NextRing != null) target = RingsCourse.Instance.NextRing.position;
            else if (_weapons != null && _weapons.Target != null) target = _weapons.Target.transform.position;
            else target = NearestEnemy();
            _arrow.gameObject.SetActive(target.HasValue);
            if (!target.HasValue) return;
            var dir = target.Value - _arrow.position;
            if (dir.sqrMagnitude > 1e-4f) _arrow.rotation = Quaternion.LookRotation(dir, transform.up);
        }

        Vector3? NearestEnemy()
        {
            Vector3? best = null;
            float bestSq = float.MaxValue;
            var all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                var d = all[i];
                if (!d.Alive || !d.Targetable || d.Team != Team.Enemy) continue;
                float sq = (d.transform.position - transform.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = d.transform.position; }
            }
            return best;
        }
    }
}
