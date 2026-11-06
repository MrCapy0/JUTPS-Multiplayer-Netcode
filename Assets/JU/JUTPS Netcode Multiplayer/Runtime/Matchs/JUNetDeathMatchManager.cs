using UnityEngine;

namespace JU.TPS.Netcode.Matchs
{
    /// <summary>
    /// Deathmatch match manager. Handles team-based deathmatch sessions.
    /// </summary>
    public class JUNetDeathMatchManager : JUNetcodeMatchManagerBase<

        // This manager.
        JUNetDeathMatchManager,
        // Settings of the match.
        JUNetDeathMatchManager.MatchSettings,
        // Player properties of this manager.
        JUNetDeathMatchPlayerProperties
    >
    {
        /// <summary>
        /// Deathmatch match settings.
        /// </summary>
        [System.Serializable]
        public class MatchSettings : MatchSettingsBase
        {
            private string _startScene;
            private string _mapName;

            [SerializeField]
            private int _teamMaxPlayers;

            /// <summary>
            /// Maximum number of players per team in the session.
            /// </summary>
            public int TeamMaxPlayers
            {
                get => _teamMaxPlayers;
            }

            /// <inheritdoc/>
            public override string MapName
            {
                get => _mapName;
                internal set => _mapName = value;
            }

            /// <inheritdoc/>
            public override string StartScene
            {
                get => _startScene;
                internal set => _startScene = value;
            }

            /// <inheritdoc/>
            public override int MatchMaxPlayers
            {
                get => TeamMaxPlayers * 2;
            }

            public MatchSettings(
                string sessionName,
                string playerName,
                int teamMaxPlayers,
                string sessionPassword = null,
                string mapName = null,
                string startScene = null
            ) :
                base(sessionName, playerName, sessionPassword)
            {
                _teamMaxPlayers = teamMaxPlayers;

                _mapName = mapName;
                _startScene = startScene;
            }
        }

        protected override void Reset()
        {
            base.Reset();

            Debug.Assert(gameObject.scene != null, "The game object must be part of a valid scene. Cannot be a prefab.");

            _debugMatchSettings = new(
                sessionName: "DebugSession",
                playerName: "DebugPlayer",
                teamMaxPlayers: 2,
                sessionPassword: null,
                mapName: $"Debug {gameObject.scene.buildIndex}",
                startScene: gameObject.scene.name
            );
        }
    }
}
