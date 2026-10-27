namespace HexWing
{
    /// <summary>
    /// What a pilot asks the ship to do this frame. The player's input schemes, the AI (M4) and the
    /// network (M7) all fill this, so the flight model never knows who is flying.
    /// </summary>
    public struct ShipControls
    {
        /// <summary>-1..1: nose down (-) / up (+).</summary>
        public float Pitch;
        /// <summary>-1..1: nose left (-) / right (+).</summary>
        public float Yaw;
        /// <summary>-1..1: roll left (-) / right (+).</summary>
        public float Roll;
        /// <summary>0..1: minimum to full speed.</summary>
        public float Throttle;
        /// <summary>True while the pilot gives roll input (auto-level waits for it to stop).</summary>
        public bool Rolling => Roll > 0.02f || Roll < -0.02f;

        // M3 on: weapons and abilities
        public bool Fire;
        public bool Lock;
        public bool Ability1;
        public bool Ability2;
        public bool Evasive;
    }

    /// <summary>Anything that can fly a ship.</summary>
    public interface IShipPilot
    {
        void ReadControls(ref ShipControls controls);
    }
}
