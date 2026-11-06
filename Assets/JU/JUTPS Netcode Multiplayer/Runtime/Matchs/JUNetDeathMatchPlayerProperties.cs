using UnityEngine;

namespace JU.TPS.Netcode.Matchs
{
    /// <summary>
    /// Player properties for the <see cref="JUNetDeathMatchManager"/>.
    /// </summary>
    public class JUNetDeathMatchPlayerProperties : JUPlayerPropertiesBase<
        // The match manager for this player.
        JUNetDeathMatchManager,
        // This player's properties.
        JUNetDeathMatchPlayerProperties
    >
    {
    }
}