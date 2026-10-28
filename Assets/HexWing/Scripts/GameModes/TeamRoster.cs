using System.Collections.Generic;

namespace HexWing
{
    /// <summary>
    /// Who flies in a team match: one slot per ship, each the local player or a bot (in M7 also a remote
    /// player, whose ship is flown by a network <see cref="IShipPilot"/>). The match logic only deals with
    /// slots, so going online means filling slots differently, not rewriting the mode.
    /// </summary>
    public class TeamRoster
    {
        public enum Seat { LocalPlayer, Bot, RemotePlayer }

        public class Slot
        {
            public Team Team;
            public Seat Seat;
            public string Name;
            public AIShip Ship;          // bots (and later remote players) only
            public float RespawnAt;
            public int Kills, Deaths;
            public bool WingSlot;        // allied bots escort the local player when they have nothing to shoot
            public int WingIndex;
        }

        public readonly List<Slot> Slots = new();

        /// <summary>Offline: the local player plus bots on team Player, bots on team Enemy.</summary>
        public void FillOffline(int teamSize)
        {
            Slots.Clear();
            Slots.Add(new Slot { Team = Team.Player, Seat = Seat.LocalPlayer, Name = "YOU" });
            for (int i = 1; i < teamSize; i++)
                Slots.Add(new Slot { Team = Team.Player, Seat = Seat.Bot, Name = "WINGMAN " + i, WingSlot = true, WingIndex = i - 1 });
            for (int i = 0; i < teamSize; i++)
                Slots.Add(new Slot { Team = Team.Enemy, Seat = Seat.Bot, Name = "ENEMY " + (i + 1) });
        }

        public Slot LocalPlayer
        {
            get { foreach (var s in Slots) if (s.Seat == Seat.LocalPlayer) return s; return null; }
        }

        public Slot FindByShip(AIShip ship)
        {
            foreach (var s in Slots) if (s.Ship == ship) return s;
            return null;
        }

        /// <summary>The slot that owns a damageable (the local player's ship or a slot's AI ship), or null.</summary>
        public Slot FindByHealth(Damageable d)
        {
            if (d == null) return null;
            foreach (var s in Slots)
            {
                if (s.Seat == Seat.LocalPlayer && PlayerShip.Instance != null && PlayerShip.Instance.Health == d) return s;
                if (s.Ship != null && s.Ship.Health == d) return s;
            }
            return null;
        }
    }
}
