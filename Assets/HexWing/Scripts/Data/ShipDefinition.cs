using System.Collections.Generic;
using UnityEngine;

namespace HexWing
{
    public enum ShipClass { Fighter, Interceptor, Bomber }

    public enum AbilityKind { None, TargetingComputer, PursuitMissile, HomingMissiles, Repair, BombRun, TurretDrone }

    public enum PassiveKind { None, ShieldBooster, Cooling, HeavyPlating }

    /// <summary>
    /// A ship class as data: flight numbers, hull and shields, guns, two abilities and a passive. The
    /// builder makes one asset per class in Resources/Ships (edit them in the Inspector); without the
    /// assets, <see cref="Defaults"/> gives the same values from code.
    /// - Falcon (fighter): all-rounder; pursuit missile + repair; shield booster.
    /// - Hawk (interceptor): fast, agile, fragile hunter; targeting computer + homing missiles; cooling.
    /// - Bison (bomber): slow and tough, for big targets; bomb run + turret drone; heavy plating.
    /// </summary>
    [CreateAssetMenu(fileName = "Ship", menuName = "HexWing/Ship Definition")]
    public class ShipDefinition : ScriptableObject
    {
        public string displayName = "FALCON";
        public ShipClass shipClass = ShipClass.Fighter;
        [TextArea] public string description = "";
        [Tooltip("Cockpit accent light colour for this class.")]
        public Color accent = new(0.37f, 0.83f, 1f);

        public FlightSettings flight = new();
        public float hull = 100f;
        public float shields = 60f;
        public float shieldDelay = 4f;
        public float shieldRegen = 20f;

        [Header("Guns (relative to GameConfig.combat)")]
        public float laserDamage = 1f;
        public float fireRate = 1f;
        public float heatPerShot = 1f;

        [Header("Abilities (A, B) and passive")]
        public AbilityKind ability1 = AbilityKind.PursuitMissile;
        public AbilityKind ability2 = AbilityKind.Repair;
        public PassiveKind passive = PassiveKind.ShieldBooster;

        static List<ShipDefinition> _all;

        /// <summary>The three classes, from Resources/Ships if present, else from code, in class order.</summary>
        public static IReadOnlyList<ShipDefinition> All
        {
            get
            {
                if (_all != null) return _all;
                _all = new List<ShipDefinition>(Resources.LoadAll<ShipDefinition>("Ships"));
                if (_all.Count == 0)
                {
                    Debug.LogWarning("[HexWing] No ship definitions in Resources/Ships, using the built-in ones. Run HexWing > Build Game.");
                    _all.AddRange(Defaults());
                }
                _all.Sort((a, b) => a.shipClass.CompareTo(b.shipClass));
                return _all;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _all = null;

        /// <summary>The three classes with their starting values (also what the builder writes to the assets).</summary>
        public static ShipDefinition[] Defaults()
        {
            var fighter = CreateInstance<ShipDefinition>();
            fighter.name = "Falcon";
            fighter.displayName = "FALCON";
            fighter.shipClass = ShipClass.Fighter;
            fighter.description = "Fighter: all-round.\nA: pursuit missile\nB: repair\nPassive: boosted shields";
            fighter.accent = new Color(0.37f, 0.83f, 1f);
            fighter.ability1 = AbilityKind.PursuitMissile;
            fighter.ability2 = AbilityKind.Repair;
            fighter.passive = PassiveKind.ShieldBooster;

            var interceptor = CreateInstance<ShipDefinition>();
            interceptor.name = "Hawk";
            interceptor.displayName = "HAWK";
            interceptor.shipClass = ShipClass.Interceptor;
            interceptor.description = "Interceptor: fast, agile, fragile.\nA: targeting computer\nB: homing missiles\nPassive: cooling";
            interceptor.accent = new Color(0.55f, 1f, 0.6f);
            interceptor.flight.maxSpeed = 175f;
            interceptor.flight.minSpeed = 40f;
            interceptor.flight.acceleration = 60f;
            interceptor.flight.pitchRate = 82f;
            interceptor.flight.yawRate = 58f;
            interceptor.flight.rollRate = 170f;
            interceptor.hull = 70f;
            interceptor.shields = 40f;
            interceptor.laserDamage = 0.9f;
            interceptor.fireRate = 1.2f;
            interceptor.ability1 = AbilityKind.TargetingComputer;
            interceptor.ability2 = AbilityKind.HomingMissiles;
            interceptor.passive = PassiveKind.Cooling;

            var bomber = CreateInstance<ShipDefinition>();
            bomber.name = "Bison";
            bomber.displayName = "BISON";
            bomber.shipClass = ShipClass.Bomber;
            bomber.description = "Bomber: slow, tough.\nA: bomb run\nB: turret drone\nPassive: heavy plating";
            bomber.accent = new Color(1f, 0.7f, 0.28f);
            bomber.flight.maxSpeed = 125f;
            bomber.flight.minSpeed = 30f;
            bomber.flight.acceleration = 35f;
            bomber.flight.pitchRate = 55f;
            bomber.flight.yawRate = 40f;
            bomber.flight.rollRate = 110f;
            bomber.hull = 170f;
            bomber.shields = 80f;
            bomber.laserDamage = 1.25f;
            bomber.fireRate = 0.8f;
            bomber.ability1 = AbilityKind.BombRun;
            bomber.ability2 = AbilityKind.TurretDrone;
            bomber.passive = PassiveKind.HeavyPlating;

            return new[] { fighter, interceptor, bomber };
        }

        public static string AbilityLabel(AbilityKind k) => k switch
        {
            AbilityKind.TargetingComputer => "TARGETING",
            AbilityKind.PursuitMissile => "PURSUIT",
            AbilityKind.HomingMissiles => "SALVO",
            AbilityKind.Repair => "REPAIR",
            AbilityKind.BombRun => "BOMB RUN",
            AbilityKind.TurretDrone => "TURRET",
            _ => "-",
        };

        public static string PassiveLabel(PassiveKind k) => k switch
        {
            PassiveKind.ShieldBooster => "SHIELDS+",
            PassiveKind.Cooling => "COOLING",
            PassiveKind.HeavyPlating => "PLATING",
            _ => "-",
        };
    }
}
