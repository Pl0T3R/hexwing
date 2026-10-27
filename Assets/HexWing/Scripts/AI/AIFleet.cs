using System.Collections.Generic;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Pools of AI fighters for a mode (ships are created on first need and reused): spawn by kind and
    /// team, count who is alive, hear about kills. Ships live under the mode's object, so switching the
    /// mode off hides them.
    /// </summary>
    public class AIFleet
    {
        readonly Transform _parent;
        readonly Dictionary<ShipMeshes.Kind, List<AIShip>> _pools = new();

        /// <summary>A ship of this fleet was destroyed: the ship, who did it.</summary>
        public event System.Action<AIShip, Damageable> Killed;

        public AIFleet(Transform parent) => _parent = parent;

        public AIShip Spawn(ShipMeshes.Kind kind, Pose pose, Team team, float skill, float hull, float shields, bool wingman = false, int slot = 0)
        {
            if (!_pools.TryGetValue(kind, out var pool)) _pools[kind] = pool = new List<AIShip>();
            AIShip ship = null;
            foreach (var s in pool) if (!s.gameObject.activeSelf) { ship = s; break; }
            if (ship == null)
            {
                ship = AIShip.Create(_parent, kind, pool.Count);
                ship.Killed += (s, by) => Killed?.Invoke(s, by);
                pool.Add(ship);
            }
            ship.Spawn(pose, team, skill, hull, shields, wingman, slot);
            return ship;
        }

        public int Alive(Team team)
        {
            int n = 0;
            foreach (var pool in _pools.Values)
                foreach (var s in pool)
                    if (s.Active && s.Health.Team == team) n++;
            return n;
        }

        public int AliveWingmen()
        {
            int n = 0;
            foreach (var pool in _pools.Values)
                foreach (var s in pool)
                    if (s.Active && s.Pilot.IsWingman) n++;
            return n;
        }

        public bool IsWingmanAlive(int slot)
        {
            foreach (var pool in _pools.Values)
                foreach (var s in pool)
                    if (s.Active && s.Pilot.IsWingman && s.Pilot.WingSlot == slot) return true;
            return false;
        }

        public void DespawnAll()
        {
            foreach (var pool in _pools.Values)
                foreach (var s in pool) s.Despawn();
        }
    }
}
