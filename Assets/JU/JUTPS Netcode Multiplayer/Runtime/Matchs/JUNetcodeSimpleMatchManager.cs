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
            public int MaxPlayers;

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

            /// <param name="sessionName">Name of the session.</param>
            /// <param name="playerName">Name of the local player.</param>
            /// <param name="maxPlayers">Initial value of <see cref="MaxPlayers"/>.</param>
            /// <param name="mapName">Initial value of <see cref="MapName"/>.</param>
            /// <param name="startScene">Initial value of <see cref="StartScene"/>.</param>
            public MatchSettings(string sessionName, string playerName, int maxPlayers, string mapName = null, string startScene = null) :
            base(sessionName, playerName)
            {
                MaxPlayers = maxPlayers;
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

        protected override void Awake()
        {
            base.Awake();
            SubscribeOnPlayerSpawned(OnPlayerSpawned);
        }

        /// <inheritdoc/>
        protected override async Task CreateMatch(MatchSettings matchSettings)
        {
            await CreateOrJoinRoom(
                roomName: matchSettings.RoomName,
                maxPlayers: matchSettings.MaxPlayers,
                mapName: matchSettings.MapName,
                startScene: matchSettings.StartScene,
                roomPassword: matchSettings.RoomPassword);
        }

        private void OnPlayerSpawned(JUNetcodeSimpleMatchPlayerProperties player)
        {
        }

        internal override (Vector3 position, Quaternion rotation) GenerateSpawnPosition(ulong ownerClientId)
        {
            if (SpawnPoints == null || SpawnPoints.Length == 0)
            {
                throw new System.InvalidOperationException($"Spawn points must be set on Match Manager {name}.");
            }

            // The same owner ID produces the same spawn point on every peer.
            int seed = unchecked((int)(ownerClientId ^ (ownerClientId >> 32) ^ 99UL));
            System.Random random = new System.Random(seed);
            int spawnPointIndex = random.Next(SpawnPoints.Length);
            Transform spawnPoint = SpawnPoints[spawnPointIndex];
            if (spawnPoint == null)
            {
                throw new System.InvalidOperationException($"Spawn point at index {spawnPointIndex} is null on Match Manager {name}.");
            }

            return (spawnPoint.position, spawnPoint.rotation);
        }
    }
}
