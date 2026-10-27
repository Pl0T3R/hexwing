using System.Collections;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// The ship's two abilities (A and B on the flying hand) and its passive, from its
    /// <see cref="ShipDefinition"/>. Each ability has a cooldown; some stay active for a while (targeting
    /// computer, repair) or need a moment of aiming (homing missiles). Numbers in GameConfig.abilities.
    /// </summary>
    public class AbilitySystem : MonoBehaviour
    {
        public class Slot
        {
            public AbilityKind Kind;
            public float ReadyAt;
            public float ActiveUntil;
            /// <summary>0..1 while aiming (homing missiles), else 0.</summary>
            public float Progress;
            public bool Ready => Time.time >= ReadyAt && !Busy;
            public bool Busy;
            public float CooldownLeft => Mathf.Max(0f, ReadyAt - Time.time);
            public bool Active => Time.time < ActiveUntil;
        }

        public readonly Slot A = new(), B = new();
        public PassiveKind Passive { get; private set; }
        /// <summary>Times each ability was used (for the training mode).</summary>
        public int Uses { get; private set; }

        public event System.Action<AbilityKind> Used;

        ShipFlight _flight;
        ShipWeapons _weapons;
        Damageable _self;
        bool _heldA, _heldB;
        TurretDrone _turret;

        void Awake()
        {
            _flight = GetComponent<ShipFlight>();
            _weapons = GetComponent<ShipWeapons>();
            _self = GetComponent<Damageable>();
        }

        public void Configure(ShipDefinition def)
        {
            StopAllCoroutines();
            A.Kind = def.ability1;
            B.Kind = def.ability2;
            Passive = def.passive;
            ResetAbilities();
        }

        public void ResetAbilities()
        {
            StopAllCoroutines();
            foreach (var s in new[] { A, B }) { s.ReadyAt = 0f; s.ActiveUntil = 0f; s.Progress = 0f; s.Busy = false; }
            if (_weapons != null) _weapons.DamageBoost = 1f;
            if (_turret != null) _turret.Remove();
            _turret = null;
        }

        void Update()
        {
            if (_flight == null || _flight.Frozen || (_self != null && !_self.Alive)) return;
            var c = _flight.Controls;
            if (c.Ability1 && !_heldA) TryUse(A);
            if (c.Ability2 && !_heldB) TryUse(B);
            _heldA = c.Ability1;
            _heldB = c.Ability2;
        }

        void TryUse(Slot slot)
        {
            if (slot.Kind == AbilityKind.None) return;
            if (!slot.Ready)
            {
                ShipAudio.Instance?.PlayDenied();
                return;
            }
            var cfg = GameConfig.Instance.abilities;
            switch (slot.Kind)
            {
                case AbilityKind.TargetingComputer:
                    StartCoroutine(TargetingComputer(slot, cfg));
                    break;
                case AbilityKind.PursuitMissile:
                    if (_weapons == null || _weapons.Target == null) { ShipAudio.Instance?.PlayDenied(); return; }
                    MissileSystem.Instance?.Launch(transform.TransformPoint(new Vector3(0f, -1.4f, 2f)), transform.forward, _weapons.Target, _self, OwnTeam(),
                                                   cfg.pursuitDamage, cfg.pursuitTurnScale);
                    slot.ReadyAt = Time.time + cfg.pursuitCooldown;
                    break;
                case AbilityKind.HomingMissiles:
                    if (_weapons == null || _weapons.Target == null) { ShipAudio.Instance?.PlayDenied(); return; }
                    StartCoroutine(HomingMissiles(slot, cfg));
                    break;
                case AbilityKind.Repair:
                    StartCoroutine(Repair(slot, cfg));
                    break;
                case AbilityKind.BombRun:
                    StartCoroutine(BombRun(slot, cfg));
                    break;
                case AbilityKind.TurretDrone:
                    if (_turret != null) _turret.Remove();
                    _turret = TurretDrone.Deploy(transform.position - transform.forward * 25f, transform.rotation, _self);
                    slot.ReadyAt = Time.time + cfg.turretCooldown;
                    break;
            }
            Uses++;
            Used?.Invoke(slot.Kind);
            Haptics.Play(TryGetComponent(out PlayerInput input) ? input.FlyHand : OVRInput.Controller.RTouch, GameConfig.Instance.haptics.missile);
            ShipAudio.Instance?.PlayAbility();
        }

        Team OwnTeam() => _self != null ? _self.Team : Team.Player;

        IEnumerator TargetingComputer(Slot slot, AbilitySettings cfg)
        {
            slot.ActiveUntil = Time.time + cfg.targetingSeconds;
            slot.ReadyAt = Time.time + cfg.targetingCooldown;
            float heat = _weapons != null ? _weapons.HeatScale : 1f;
            if (_weapons != null) { _weapons.HeatScale = 0f; _weapons.DamageBoost = cfg.targetingDamage; }
            yield return new WaitForSeconds(cfg.targetingSeconds);
            if (_weapons != null) { _weapons.HeatScale = heat; _weapons.DamageBoost = 1f; }
        }

        IEnumerator HomingMissiles(Slot slot, AbilitySettings cfg)
        {
            slot.Busy = true;
            var target = _weapons.Target;
            float lockCone = GameConfig.Instance.combat.lockCone;
            float t = 0f;
            int ticks = 0;
            while (t < cfg.homingLockSeconds)
            {
                if (target == null || !target.Alive || Vector3.Angle(transform.forward, target.transform.position - transform.position) > lockCone * 1.5f)
                {
                    // lost it: a short cooldown so the button isn't dead for long
                    slot.Progress = 0f;
                    slot.Busy = false;
                    slot.ReadyAt = Time.time + 2f;
                    ShipAudio.Instance?.PlayDenied();
                    yield break;
                }
                t += Time.deltaTime;
                slot.Progress = t / cfg.homingLockSeconds;
                if (Mathf.FloorToInt(slot.Progress * 5f) > ticks) { ticks++; ShipAudio.Instance?.PlayLockTick(slot.Progress); }
                yield return null;
            }
            slot.Progress = 0f;
            ShipAudio.Instance?.PlayLocked();
            for (int i = 0; i < cfg.homingCount; i++)
            {
                float side = i % 2 == 0 ? 1.4f : -1.4f;
                MissileSystem.Instance?.Launch(transform.TransformPoint(new Vector3(side, -1.2f, 2f)), transform.forward + transform.right * side * 0.08f,
                                               target, _self, OwnTeam(), cfg.homingDamage);
                ShipAudio.Instance?.PlayMissile();
                yield return new WaitForSeconds(0.15f);
            }
            slot.Busy = false;
            slot.ReadyAt = Time.time + cfg.homingCooldown;
        }

        IEnumerator Repair(Slot slot, AbilitySettings cfg)
        {
            slot.ActiveUntil = Time.time + cfg.repairSeconds;
            slot.ReadyAt = Time.time + cfg.repairCooldown;
            float healed = 0f;
            while (healed < cfg.repairAmount && _self != null && _self.Alive)
            {
                float step = cfg.repairAmount / cfg.repairSeconds * Time.deltaTime;
                step = Mathf.Min(step, cfg.repairAmount - healed);
                _self.Heal(step);
                healed += step;
                yield return null;
            }
        }

        IEnumerator BombRun(Slot slot, AbilitySettings cfg)
        {
            slot.ActiveUntil = Time.time + cfg.bombCount * cfg.bombInterval;
            slot.ReadyAt = Time.time + cfg.bombCooldown;
            for (int i = 0; i < cfg.bombCount; i++)
            {
                // bombs drop below the ship and keep some of its speed, so they land along the flight path
                var speed = (_flight != null ? _flight.Speed : 80f) * 0.6f + cfg.bombSpeed * 0.4f;
                MissileSystem.Instance?.Launch(transform.TransformPoint(new Vector3(0f, -2.2f, 0f)), (transform.forward - transform.up * 0.15f).normalized,
                                               null, _self, OwnTeam(), cfg.bombDamage, 0f, speed, cfg.bombFuse, cfg.bombBlastRadius);
                yield return new WaitForSeconds(cfg.bombInterval);
            }
        }
    }
}
