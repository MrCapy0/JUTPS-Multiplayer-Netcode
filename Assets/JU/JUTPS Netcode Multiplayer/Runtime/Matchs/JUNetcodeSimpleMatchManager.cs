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

        public Transform[] SpawnPoints;

        private void Reset()
        {
            _debugMatchSettings = new MatchSettings(
                "Debug Session Simple Match",
                "Player 1",
                4
            );
        }

        protected override void Awake()
        {
            base.Awake();
            SubscribeOnPlayerSpawned(OnPlayerSpawned);
        }

        /// <inheritdoc/>
        protected override void CreateMatch(MatchSettings matchSettings)
        {
            CreateOrJoinSession(matchSettings.SessionName, matchSettings.MaxPlayers);
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
