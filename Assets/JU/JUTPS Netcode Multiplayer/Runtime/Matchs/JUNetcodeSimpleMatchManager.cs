using UnityEngine;

namespace JU.TPS.Netcode
{
    /// <summary>
    /// Simplest match manager: creates or joins a session with a player limit and no extra rules.
    /// </summary>
    public class JUNetcodeSimpleMatchManager : JUNetcodeMatchManagerBaseT<
        // This manager.
        JUNetcodeSimpleMatchManager,
        // Settings of the match.
        JUNetcodeSimpleMatchManager.MatchSettings,
        // Player properties of this manager.
        JUNetcodeSimpleMatchPlayerProperties
    >
    {
        /// <summary>
        /// Settings of the match.
        /// </summary>
        [System.Serializable]
        public class MatchSettings : MatchSettingsBase
        {
            /// <summary>
            /// Maximum number of players in the session.
            /// </summary>
            [Space]
            public int MaxPlayers;

            /// <param name="sessionName">Name of the session.</param>
            /// <param name="playerName">Name of the local player.</param>
            /// <param name="maxPlayers">Initial value of <see cref="MaxPlayers"/>.</param>
            public MatchSettings(string sessionName, string playerName, int maxPlayers) :
            base(sessionName, playerName)
            {
                MaxPlayers = maxPlayers;
            }
        }

        private void Reset()
        {
            _debugMatchSettings = new MatchSettings(
                "Debug Session Simple Match",
                "Player 1",
                4
            );
        }

        /// <inheritdoc/>
        protected override void CreateMatch(MatchSettings matchSettings)
        {
            CreateOrJoinSession(matchSettings.SessionName, matchSettings.MaxPlayers);
        }
    }
}
