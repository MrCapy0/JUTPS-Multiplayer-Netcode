using System.Threading.Tasks;
using UnityEngine;

namespace JU.TPS.Netcode
{
    /// <summary>
    /// Simplest match manager: creates or joins a session with a player limit and no extra rules.
    /// </summary>
    public class JUNetcodeSimpleMatchManager : JUNetcodeMatchManagerBase<
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
            private string _mapName;
            private string _startScene;

            /// <summary>
            /// Maximum number of players in the session.
            /// </summary>
            [Space]
            [SerializeField] private int _maxPlayers;

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
                get => _maxPlayers;
            }

            /// <param name="sessionName">Name of the session.</param>
            /// <param name="playerName">Name of the local player.</param>
            /// <param name="maxPlayers">Initial value of <see cref="_maxPlayers"/>.</param>
            /// <param name="mapName">Initial value of <see cref="MapName"/>.</param>
            /// <param name="startScene">Initial value of <see cref="StartScene"/>.</param>
            public MatchSettings(string sessionName, string playerName, int maxPlayers, string mapName = null, string startScene = null) :
            base(sessionName, playerName)
            {
                _maxPlayers = maxPlayers;
                _mapName = mapName;
                _startScene = startScene;
            }
        }

        public Transform[] SpawnPoints;

        protected override void Reset()
        {
            base.Reset();
            _debugMatchSettings = new MatchSettings(
                sessionName: "Debug Session Simple Match",
                playerName: "Player 1",
                maxPlayers: 4,
                mapName: $"Debug {gameObject.scene.buildIndex}",
                startScene: gameObject.scene.name
            );
        }

        /// <inheritdoc/>
        /// <exception cref="System.Exception">No spawn points are configured, or the selected spawn point is null.</exception>
        internal override (Vector3 position, Quaternion rotation) GenerateSpawnPosition(ulong ownerClientId)
        {
            if (SpawnPoints == null || SpawnPoints.Length == 0)
            {
                throw new System.Exception($"Spawn points must be set on Match Manager {name}.");
            }

            // The same owner ID produces the same spawn point on every peer.
            int seed = unchecked((int)(ownerClientId ^ (ownerClientId >> 32) ^ 99UL));
            System.Random random = new System.Random(seed);
            int spawnPointIndex = random.Next(SpawnPoints.Length);
            Transform spawnPoint = SpawnPoints[spawnPointIndex];
            if (spawnPoint == null)
            {
                throw new System.Exception($"Spawn point at index {spawnPointIndex} is null on Match Manager {name}.");
            }

            return (spawnPoint.position, spawnPoint.rotation);
        }
    }
}
