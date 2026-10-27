using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// A capital ship's gun turret: turns toward the nearest player-side ship within range on its side of
    /// the hull and fires twin bolts at its lead point with some aim error.
    /// </summary>
    public class CapitalTurret : MonoBehaviour
    {
        const float Range = 1000f, FireRate = 1.6f, Damage = 7f, AimError = 3f;

        public Damageable Health { get; private set; }

        CapitalShip _ship;
        Vector3 _side;
        Damageable _target;
        float _nextShot, _nextPick;
        int _barrel;
        System.Random _rng;

        public void Init(CapitalShip ship, Damageable health, Vector3 side)
        {
            _ship = ship;
            Health = health;
            _side = side;
            _rng = new System.Random(GetInstanceID());
        }

        public void ResetTurret()
        {
            _target = null;
            transform.localRotation = Quaternion.identity;
        }

        void Update()
        {
            if (Health == null || !Health.Alive || _ship.Current == CapitalShip.Phase.Destroyed) return;
            if (Time.time >= _nextPick)
            {
                _nextPick = Time.time + 0.5f;
                _target = Pick();
            }
            if (_target == null || !_target.Alive) return;
            float dist = Vector3.Distance(transform.position, _target.transform.position);
            var lead = _target.transform.position + _target.Velocity * (dist / GameConfig.Instance.combat.laserSpeed);
            var dir = (lead - transform.position).normalized;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir, _ship.transform.up), 60f * Time.deltaTime);
            if (Time.time < _nextShot || Vector3.Angle(transform.forward, dir) > 4f) return;
            _nextShot = Time.time + 1f / FireRate;
            var shot = Quaternion.Euler(Rand() * AimError, Rand() * AimError, 0f) * transform.forward;
            var muzzle = transform.TransformPoint(new Vector3(_barrel == 0 ? -1.4f : 1.4f, 3f, 12.5f));
            _barrel = 1 - _barrel;
            LaserSystem.Instance?.Fire(muzzle, shot, Vector3.zero, Health, Team.Enemy, Damage);
            Effects.Instance?.SpawnFlash(muzzle, 2f, 0.08f, Effects.FlashKind.Impact);
        }

        float Rand() => (float)_rng.NextDouble() * 2f - 1f;

        Damageable Pick()
        {
            Damageable best = null;
            float bestSq = Range * Range;
            var all = Damageable.All;
            var sideWorld = _ship.transform.TransformDirection(_side);
            for (int i = 0; i < all.Count; i++)
            {
                var d = all[i];
                if (!d.Alive || d.Team != Team.Player) continue;
                var to = d.transform.position - transform.position;
                // only above its side of the hull, so it never shoots through the ship
                if (Vector3.Dot(to, sideWorld) < -20f && Vector3.Dot(to, _ship.transform.up) < 0f) continue;
                float sq = to.sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = d; }
            }
            return best;
        }
    }
}
