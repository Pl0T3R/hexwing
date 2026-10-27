using UnityEngine;

namespace HexWing
{
    /// <summary>A mode that tells the cockpit guide arrow where to point (next ring, objective, convoy).</summary>
    public interface IGuidedMode
    {
        /// <summary>What the arrow should point at now, or null for the default (target, nearest enemy).</summary>
        Transform Pointer { get; }
    }
}
