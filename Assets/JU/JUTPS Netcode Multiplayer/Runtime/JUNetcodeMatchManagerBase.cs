using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.Events;

namespace JU.TPS.Netcode
{
    /// <summary>
    /// Base serializable settings used to start a match. Extend it to add mode-specific options.
    /// </summary>
    [Serializable]
    public abstract class MatchSettingsBase
    {
        [Tooltip("Name of the multiplayer room to create or join.")]
        [SerializeField] private string _roomName;

        [Tooltip("Name of the local player.")]
        [SerializeField] private string _playerName;

        /// <summary>
        /// Room password used to join the match.
        /// </summary>
        public string RoomPassword { get; private set; }

        /// <summary>
        /// Name of the room to create or join.
        /// </summary>
        public string RoomName
        {
            get => _roomName;
        }

        /// <summary>
        /// Name of the local player.
        /// </summary>
        public string PlayerName
        {
            get => _playerName;
            internal set => _playerName = value;
        }

        /// <summary>
        /// Room maximum number of players.
        /// </summary>
        public virtual int MatchMaxPlayers
        {
            get
            {
                throw new Exception($"Missing 'get' implementation for {nameof(MatchMaxPlayers)} on {GetType().Name}");
            }
        }

        /// <summary>
        /// Name of the scene to load when the match starts.
        /// </summary>
        public virtual string StartScene
        {
            get
            {
                throw new Exception($"Missing 'get' implementation for {nameof(StartScene)} on {GetType().Name}");
            }
            internal set
            {
                throw new Exception($"Missing 'set' implementation for {nameof(StartScene)} on {GetType().Name}");
            }
        }

        /// <summary>
        /// Name of the map to be displayed in the match or main menu on room list.
        /// </summary>
        public virtual string MapName
        {
            get
            {
                throw new Exception($"Missing 'get' implementation for {nameof(MapName)} on {GetType().Name}");
            }
            internal set
            {
                throw new Exception($"Missing 'set' implementation for {nameof(MapName)} on {GetType().Name}");
            }
        }

        /// <param name="roomName">Initial value of <see cref="RoomName"/>.</param>
        /// <param name="playerName">Initial value of <see cref="PlayerName"/>.</param>
        /// <param name="sessionPassword">Initial value of <see cref="RoomPassword"/>.</param>
        protected MatchSettingsBase(string roomName, string playerName, string sessionPassword = null)
        {
            _roomName = roomName;
            _playerName = playerName;
            RoomPassword = sessionPassword;
        }
    }

    /// <summary>
    /// Base networked component that holds the properties of a player in a match.
    /// </summary>
    public abstract class JUPlayerPropertiesBase : NetworkBehaviour
    {
        private static bool _setupDone;

        private (Vector3 position, Quaternion rotation)? _spawnTransform;

        private static event UnityAction<JUPlayerPropertiesBase> _onPlayerSpawned;
        private static event UnityAction<JUPlayerPropertiesBase> _onPlayerDespawned;

        private IHealth _health;
        private NetworkVariable<FixedString64Bytes> _netPlayerName;

        /// <summary>
        /// Display name of this player.
        /// </summary>
        public string PlayerName { get; private set; }

        /// <summary>
        /// Collider attached to this GameObject, if any.
        /// </summary>
        public Collider Collider { get; private set; }

        /// <summary>
        /// The health component attached to this player, if any.
        /// </summary>
        public IHealth Health
        {
            get
            {
                if (_health == null)
                {
                    _health = GetComponent<IHealth>();
                }

                return _health;
            }
        }

        /// <summary>
        /// Maximum health, or zero if there is no <see cref="IHealth"/> component.
        /// </summary>
        public float MaxHealth
        {
            get
            {
                if (_health == null)
                {
                    return 0f;
                }

                return _health.MaxHealth;
            }
        }

        /// <summary>
        /// Current health, or zero if there is no <see cref="IHealth"/> component.
        /// </summary>
        public float HealthValue
        {
            get
            {
                if (_health == null)
                {
                    return 0f;
                }

                return _health.Health;
            }
        }

        /// <summary>
        /// Current health from 0 to 1, or zero if there is no <see cref="IHealth"/> component.
        /// </summary>
        public float NormalizedHealth
        {
            get
            {
                if (_health == null)
                {
                    return 0f;
                }

                return _health.NormalizedHealth;
            }
        }

        public bool IsDead
        {
            get
            {
                if (_health == null)
                {
                    return false;
                }

                return _health.IsDead;
            }
        }

        /// <summary>
        /// Bounds of <see cref="Collider"/>, or a small box at this position if there is none.
        /// </summary>
        public Bounds Bounds
        {
            get
            {
                if (Collider == null)
                {
                    return new Bounds(transform.position, Vector3.one * 0.1f);
                }

                return Collider.bounds;
            }
        }

        /// <summary>
        /// Top center point of <see cref="Bounds"/>.
        /// </summary>
        public Vector3 UpperCenter
        {
            get
            {
                return Bounds.center + (Vector3.up * Bounds.extents.y);
            }
        }

        /// <summary>
        /// Session of the active <see cref="JUNetcodeMatchManagerBase"/>.
        /// </summary>
        public ISession CurrentSession
        {
            get => JUNetcodeMatchManagerBase.CurrentRoom;
        }

        /// <summary>
        /// Spawn position and rotation of the player.
        /// </summary>
        public virtual (Vector3 position, Quaternion rotation) Spawn
        {
            get
            {
                if (_spawnTransform == null)
                {
                    if (IsSpawned == false)
                    {
                        throw new InvalidOperationException("The player spawn transform is unavailable before the NetworkObject is spawned.");
                    }

                    (Vector3 position, Quaternion rotation) spawn = JUNetcodeMatchManagerBase.RawInstance.GenerateSpawnPosition(OwnerClientId);
                    _spawnTransform = spawn;
                }

                return _spawnTransform.Value;
            }
        }

        /// <summary>
        /// Creates <see cref="_netPlayerName"/>.
        /// </summary>
        protected JUPlayerPropertiesBase()
        {
            _netPlayerName = new(new FixedString64Bytes(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        }

        /// <summary>
        /// Unity message. Extension point for derived classes.
        /// </summary>
        protected virtual void Awake()
        {
            if (_setupDone == false)
            {
                _setupDone = true;
                Application.quitting += Cleanup;
            }
        }

        /// <summary>
        /// Unity message. Starts listening to <see cref="_netPlayerName"/> changes.
        /// </summary>
        protected virtual void OnEnable()
        {
            _netPlayerName.OnValueChanged += OnPlayerNameChanged;
        }

        /// <summary>
        /// Unity message. Stops listening to <see cref="_netPlayerName"/> changes.
        /// </summary>
        protected virtual void OnDisable()
        {
            _netPlayerName.OnValueChanged -= OnPlayerNameChanged;
        }

        /// <summary>
        /// Unity message. Caches <see cref="Collider"/> and the <see cref="IHealth"/> component.
        /// </summary>
        protected virtual void Start()
        {
            _health = GetComponent<IHealth>();
            Collider = GetComponent<Collider>();
        }

        /// <inheritdoc/>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                _netPlayerName.Value = JUNetcodeMatchManagerBase.RawInstance.RawMatchSettings.PlayerName;
            }

            PlayerName = _netPlayerName.Value.ToString();

            StartCoroutine(WaitForSpawned(() =>
            {
                _onPlayerSpawned?.Invoke(this);
            }));
        }

        /// <inheritdoc/>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _onPlayerDespawned?.Invoke(this);
        }

        private void OnPlayerNameChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
        {
            if (IsSpawned == false)
            {
                return;
            }

            PlayerName = newValue.ToString();
        }

        protected IEnumerator WaitForSpawned(UnityAction onSpawned)
        {
            while (IsSpawned == false)
            {
                yield return null;
            }

            onSpawned?.Invoke();
        }

        private void Cleanup()
        {
            _onPlayerSpawned = null;
            _onPlayerDespawned = null;
        }

        /// <summary>
        /// Subscribes to the event raised when a player is spawned in the network.
        /// </summary>
        /// <param name="callback"></param>
        public static void SubscribeOnPlayerSpawned(UnityAction<JUPlayerPropertiesBase> callback)
        {
            _onPlayerSpawned += callback;
        }

        /// <summary>
        /// Unsubscribes from the event raised when a player is spawned in the network.
        /// </summary>
        /// <param name="callback"></param>
        public static void UnsubscribeOnPlayerSpawned(UnityAction<JUPlayerPropertiesBase> callback)
        {
            _onPlayerSpawned -= callback;
        }

        /// <summary>
        /// Subscribes to the event raised when a player is despawned in the network.
        /// </summary>
        /// <param name="callback"></param>
        public static void SubscribeOnPlayerDespawned(UnityAction<JUPlayerPropertiesBase> callback)
        {
            _onPlayerDespawned += callback;
        }

        /// <summary>
        /// Unsubscribes from the event raised when a player is despawned in the network.
        /// </summary>
        /// <param name="callback"></param>
        public static void UnsubscribeOnPlayerDespawned(UnityAction<JUPlayerPropertiesBase> callback)
        {
            _onPlayerDespawned -= callback;
        }
    }

    /// <summary>
    /// <see cref="JUPlayerPropertiesBase"/> bound to a match manager and a concrete player type.
    /// Raises <see cref="_onPlayerSpawned"/> and <see cref="_onPlayerDespawned"/> for the manager.
    /// </summary>
    /// <typeparam name="TManager">Match manager type that owns these players.</typeparam>
    /// <typeparam name="TPlayerProperties">Concrete player properties type (the deriving class itself).</typeparam>
    public abstract class JUPlayerPropertiesBase<
        // Match manager type that owns these players.
        TManager,
        // Concrete player properties type (the deriving class itself).
        TPlayerProperties
    > : JUPlayerPropertiesBase
    where TManager : JUNetcodeMatchManagerBase
    where TPlayerProperties : JUPlayerPropertiesBase<TManager, TPlayerProperties>
    {
        private static event UnityAction<TPlayerProperties> _onPlayerSpawned;
        private static event UnityAction<TPlayerProperties> _onPlayerDespawned;

        /// <summary>
        /// Active match manager.
        /// </summary>
        public TManager MatchManager
        {
            get => (TManager)JUNetcodeMatchManagerBase.RawInstance;
        }

        protected override void Awake()
        {
            base.Awake();
        }

        /// <inheritdoc/>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            StartCoroutine(WaitForSpawned(() =>
            {
                _onPlayerSpawned?.Invoke((TPlayerProperties)this);
            }));
        }

        /// <inheritdoc/>
        public override void OnNetworkDespawn()
        {
            _onPlayerDespawned?.Invoke((TPlayerProperties)this);
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Subscribes to the event raised when a player is spawned on the network.
        /// </summary>
        /// <param name="callback"></param>
        public static void SubscribeOnPlayerSpawned(UnityAction<TPlayerProperties> callback)
        {
            _onPlayerSpawned += callback;
        }

        /// <summary>
        /// Unsubscribes from the event raised when a player is spawned on the network.
        /// </summary>
        /// <param name="callback"></param>
        public static void UnsubscribeOnPlayerSpawned(UnityAction<TPlayerProperties> callback)
        {
            _onPlayerSpawned -= callback;
        }

        /// <summary>
        /// Subscribes to the event raised when a player is despawned from the network.
        /// </summary>
        /// <param name="callback"></param>
        public static void SubscribeOnPlayerDespawned(UnityAction<TPlayerProperties> callback)
        {
            _onPlayerDespawned += callback;
        }

        /// <summary>
        /// Unsubscribes from the event raised when a player is despawned from the network.
        /// </summary>
        /// <param name="callback"></param>
        public static void UnsubscribeOnPlayerDespawned(UnityAction<TPlayerProperties> callback)
        {
            _onPlayerDespawned -= callback;
        }
    }

    /// <summary>
    /// Non-generic base of every match manager. Handles the session lifecycle and exposes player events.
    /// </summary>
    public abstract class JUNetcodeMatchManagerBase : NetworkBehaviour
    {
        /// <summary>
        /// The filter options for rooms based on password requirement.
        /// </summary>
        public enum PasswordFilter
        {
            /// <summary>
            /// All rooms, regardless of password requirement.
            /// </summary>
            All,
            /// <summary>
            /// Rooms that do not require a password.
            /// </summary>
            WithoutPassword,
            /// <summary>
            /// Rooms that require a password.
            /// </summary>
            WithPassword,
        }

        [Serializable]
        public struct RespawnSettings
        {
            [Tooltip("If true, the player will respawn after being eliminated.")]
            [SerializeField]
            private bool _enabled;

            [Tooltip("Time it takes for the player to respawn.")]
            [SerializeField]
            [Min(0f)]
            private float _respawnTime;

            [Tooltip("Time it takes for the player to be able to respawn.")]
            [SerializeField]
            [Min(0f)]
            private float _enableRespawnTime;

            /// <summary>
            /// If true, the player will respawn after being eliminated.
            /// </summary>
            public bool Enabled
            {
                get => _enabled;
            }

            /// <summary>
            /// Time it takes for the player to respawn.
            /// </summary>
            public float RespawnTime
            {
                get => _respawnTime;
            }

            /// <summary>
            /// Time it takes for the player to be able to respawn.
            /// </summary>
            public float EnableRespawnTime
            {
                get => _enableRespawnTime;
            }

            public RespawnSettings(bool enabled, float respawnTime, float enableRespawnTime)
            {
                _enabled = enabled;
                _respawnTime = respawnTime;
                _enableRespawnTime = enableRespawnTime;
            }
        }

        private static bool _setupQuitApplicationDone;
        private static bool _setupExitPlayModeDone;

        private static JUNetcodeMatchManagerBase _rawInstance;
        private bool _isPlayerSpawnConfigured;
        private bool _isLocalPlayerSpawned;
        private static bool _isLeavingRoom;
        private static float _lastRoomSearchTime;

        private readonly static string _roomPropertyStartScene = "startScene";
        private readonly static string _roomPropertyMapName = "mapName";

        [SerializeField] private RespawnSettings _respawnSettings;

        /// <summary>
        /// Raised when the local player creates or joins a room.
        /// </summary>
        public static event UnityAction<ISession> JoinRoom;

        /// <summary>
        /// Raised when exiting playmode in the editor.
        /// </summary>
        public static event UnityAction ExitingPlayMode;

        /// <summary>
        /// Active match manager in the scene.
        /// </summary>
        public static JUNetcodeMatchManagerBase RawInstance
        {
            get
            {
                if (_rawInstance == null)
                {
                    _rawInstance = FindAnyObjectByType<JUNetcodeMatchManagerBase>(FindObjectsInactive.Include);
                }

                return _rawInstance;
            }
        }

        /// <summary>
        /// Room the local player is in, or null.
        /// </summary>
        public static ISession CurrentRoom { get; private set; }

        /// <summary>
        /// Local player properties.
        /// </summary>
        public JUPlayerPropertiesBase RawLocalPlayer { get; private set; }

        /// <summary>
        /// If true, the player will respawn after <see cref="LocalPlayerEnableRespawnTimer"/> reaches zero.
        /// </summary>
        public bool LocalPlayerRespawnTriggered { get; protected set; }

        /// <summary>
        /// Time remaining until the local player respawns.
        /// </summary>
        public float LocalPlayerRespawnTimer { get; protected set; }

        /// <summary>
        /// Time remaining until the local player *can* respawn.
        /// It does not respawn the player automatically. Just allow the player to respawn when the timer reaches zero.
        /// </summary>
        public float LocalPlayerEnableRespawnTimer { get; protected set; }

        /// <summary>
        /// Respawn settings for the local player.
        /// </summary>
        public RespawnSettings Respawn
        {
            get => _respawnSettings;
            protected set => _respawnSettings = value;
        }


        /// <summary>
        /// Number of players in the match.
        /// </summary>
        public virtual int PlayersCount
        {
            get
            {
                throw new NotImplementedException($"Missing implementation for {nameof(PlayersCount)} property in {GetType().Name}");
            }
        }

        /// <summary>
        /// Settings used to start the match.
        /// </summary>
        public virtual MatchSettingsBase RawMatchSettings
        {
            get
            {
                throw new NotImplementedException($"Missing implementation for {nameof(RawMatchSettings)} property in {GetType().Name}");
            }
        }

        /// <summary>
        /// Checks if the player is connected to Unity Services and authenticated.
        /// </summary>
        public static bool IsConnectedAndAuthenticated
        {
            get
            {
                if (UnityServices.Instance == null)
                {
                    return false;
                }

                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    return false;
                }

                if (AuthenticationService.Instance == null || AuthenticationService.Instance.IsSignedIn == false)
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Checks if the player can search for rooms.
        /// </summary>
        public static bool CanSearchRooms
        {
            get
            {
                if (IsConnectedAndAuthenticated == false)
                {
                    return false;
                }

                return Time.unscaledTime - _lastRoomSearchTime >= 1;
            }
        }

        protected virtual void Reset()
        {
            Respawn = new RespawnSettings(
                enabled: true,
                respawnTime: 3f,
                enableRespawnTime: 3f
            );
        }

        protected virtual void Awake()
        {
            SetupIfNeeded();

            if (_rawInstance == null)
            {
                _rawInstance = this;
            }

            Debug.Assert(_rawInstance == this, $"Another Match Manager ({_rawInstance.name} of type {_rawInstance.GetType().Name}) already exists. Multiple instances are not allowed.");

            ConfigureConnectionApproval();

            JUPlayerPropertiesBase.SubscribeOnPlayerSpawned(OnPlayerSpawned);
            JUPlayerPropertiesBase.SubscribeOnPlayerDespawned(OnPlayerDespawned);
        }

        /// <inheritdoc/>
        public override void OnDestroy()
        {
            _rawInstance = null;
            Application.wantsToQuit -= TryQuit;

            base.OnDestroy();

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= SpawnPlayerOnConnected;
            }

            JUPlayerPropertiesBase.UnsubscribeOnPlayerSpawned(OnPlayerSpawned);
            JUPlayerPropertiesBase.UnsubscribeOnPlayerDespawned(OnPlayerDespawned);
        }

        protected virtual void OnEnable()
        {
        }

        protected virtual void OnDisable()
        {
        }

        protected virtual void OnApplicationQuit()
        {
        }

        protected virtual void Start()
        {
            ThrowIfNetworkManagerMissing();
            if (_isPlayerSpawnConfigured == false)
            {
                ConfigureConnectionApproval();
            }

            NetworkManager networkManager = NetworkManager.Singleton;
            networkManager.OnClientConnectedCallback += SpawnPlayerOnConnected;

            if (networkManager.IsConnectedClient)
            {
                SpawnPlayerOnConnected(networkManager.LocalClientId);
            }
        }

        protected virtual void Update()
        {
            if (IsSpawned == false)
            {
                return;
            }

            if (RawLocalPlayer != null && RawLocalPlayer.Health != null && RawLocalPlayer.Health.IsDead == true)
            {
                LocalPlayerEnableRespawnTimer -= Time.deltaTime;
                LocalPlayerEnableRespawnTimer = Mathf.Max(LocalPlayerEnableRespawnTimer, 0f);

                if (LocalPlayerRespawnTriggered)
                {
                    LocalPlayerRespawnTimer -= Time.deltaTime;
                    LocalPlayerRespawnTimer = Mathf.Max(LocalPlayerRespawnTimer, 0f);

                    if (LocalPlayerRespawnTimer == 0f)
                    {
                        RespawnLocalPlayerNow();
                    }
                }
            }
        }

        private static void SetupIfNeeded()
        {
            if (_setupQuitApplicationDone == false)
            {
                _setupQuitApplicationDone = true;
                Application.wantsToQuit += TryQuit;
            }

            if (_setupExitPlayModeDone == false)
            {
                _setupExitPlayModeDone = true;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
            }
        }

        private void ConfigureConnectionApproval()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                return;
            }

            if (networkManager.IsListening)
            {
                throw new InvalidOperationException("The match manager must configure player spawning before the NetworkManager starts.");
            }

            if (networkManager.NetworkConfig.NetworkTopology == NetworkTopologyTypes.DistributedAuthority)
            {
                networkManager.NetworkConfig.AutoSpawnPlayerPrefabClientSide = false;
                _isPlayerSpawnConfigured = true;
                return;
            }

            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = OnServerConnectionApproval;
            _isPlayerSpawnConfigured = true;
        }

        private void OnServerConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response
        )
        {
            // If network topology is distributed authority, the server does not approve connections here.
            // In that case, we simply return without approving the connection.
            // The player is spawned by the client on SpawnPlayerOnConnected if authority is distributed.
            if (NetworkManager.Singleton.NetworkConfig.NetworkTopology == NetworkTopologyTypes.DistributedAuthority)
            {
                return;
            }

            response.Approved = true;
            response.CreatePlayerObject = true;

            (Vector3 position, Quaternion rotation) spawn = GenerateSpawnPosition(request.ClientNetworkId);
            response.Position = spawn.position;
            response.Rotation = spawn.rotation;
        }

        private void SpawnPlayerOnConnected(ulong clientId)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || networkManager.LocalClient == null)
            {
                return;
            }

            // Client-server spawns the player during connection approval. Only distributed authority spawns it here.
            if (networkManager.NetworkConfig.NetworkTopology != NetworkTopologyTypes.DistributedAuthority)
            {
                return;
            }

            // The callback also fires for other clients, and Start() may replay it for the local client.
            if (clientId != networkManager.LocalClientId || _isLocalPlayerSpawned || networkManager.LocalClient.PlayerObject != null)
            {
                return;
            }

            GameObject playerPrefab = networkManager.NetworkConfig.PlayerPrefab;
            if (playerPrefab == null || playerPrefab.TryGetComponent(out NetworkObject _) == false)
            {
                throw new Exception($"A player prefab with a {nameof(NetworkObject)} component must be assigned in the {nameof(NetworkManager)}.");
            }

            if (playerPrefab.TryGetComponent(out JUPlayerPropertiesBase _) == false)
            {
                throw new Exception($"A player prefab with a {nameof(JUPlayerPropertiesBase)} component must be assigned in the {nameof(NetworkManager)}.");
            }

            (Vector3 position, Quaternion rotation) spawn = GenerateSpawnPosition(clientId);
            GameObject player = Instantiate(playerPrefab, spawn.position, spawn.rotation);
            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
            _isLocalPlayerSpawned = true;
        }

        private void OnPlayerSpawned(JUPlayerPropertiesBase playerProperties)
        {
            if (playerProperties.IsLocalPlayer == true)
            {
                RawLocalPlayer = playerProperties;
            }
        }

        private void OnPlayerDespawned(JUPlayerPropertiesBase playerProperties)
        {
            if (playerProperties.IsLocalPlayer == true)
            {
                RawLocalPlayer = null;
            }
        }

        /// <summary>
        /// Subscribes to the event raised when a player spawns.
        /// </summary>
        public virtual void SubscribeOnPlayerSpawned(UnityAction<JUPlayerPropertiesBase> callback)
        {
            throw new Exception($"Missing implementation for {nameof(SubscribeOnPlayerSpawned)} on {GetType().Name}");
        }

        /// <summary>
        /// Unsubscribes from the event raised when a player spawns.
        /// </summary>
        public virtual void UnsubscribeOnPlayerSpawned(UnityAction<JUPlayerPropertiesBase> callback)
        {
            throw new Exception($"Missing implementation for {nameof(UnsubscribeOnPlayerSpawned)} on {GetType().Name}");
        }

        /// <summary>
        /// Subscribes to the event raised when a player leaves.
        /// </summary>
        public virtual void SubscribeOnPlayerLeft(UnityAction<JUPlayerPropertiesBase> callback)
        {
            throw new Exception($"Missing implementation for {nameof(SubscribeOnPlayerLeft)} on {GetType().Name}");
        }

        /// <summary>
        /// Unsubscribes from the event raised when a player leaves.
        /// </summary>
        public virtual void UnsubscribeOnPlayerLeft(UnityAction<JUPlayerPropertiesBase> callback)
        {
            throw new Exception($"Missing implementation for {nameof(UnsubscribeOnPlayerLeft)} on {GetType().Name}");
        }

        /// <summary>
        /// Subscribes to the event raised when a player takes damage.
        /// </summary>
        /// <param name="callback">The callback to invoke when a player takes damage.</param>
        public virtual void SubscribeOnPlayerTakeDamage(UnityAction<(JUPlayerPropertiesBase player, IHealth.DamageResultInfo damageInfo)> callback)
        {
            throw new Exception($"Missing implementation for {nameof(SubscribeOnPlayerTakeDamage)} on {GetType().Name}");
        }

        /// <summary>
        /// Unsubscribes from the event raised when a player takes damage.
        /// </summary>
        /// <param name="callback"></param>
        /// <exception cref="Exception"></exception>
        public virtual void UnsubscribeOnPlayerTakeDamage(UnityAction<(JUPlayerPropertiesBase player, IHealth.DamageResultInfo damageInfo)> callback)
        {
            throw new Exception($"Missing implementation for {nameof(UnsubscribeOnPlayerTakeDamage)} on {GetType().Name}");
        }

        /// <summary>
        /// Subscribes to the event raised when a player dies.
        /// </summary>
        /// <param name="callback">The callback to invoke when a player dies.</param>
        public virtual void SubscribeOnPlayerDie(UnityAction<JUPlayerPropertiesBase> callback)
        {
            throw new Exception($"Missing implementation for {nameof(SubscribeOnPlayerDie)} on {GetType().Name}");
        }

        /// <summary>
        /// Unsubscribes from the event raised when a player dies.
        /// </summary>
        /// <param name="callback">The callback to invoke when a player dies.</param>
        public virtual void UnsubscribeOnPlayerDie(UnityAction<JUPlayerPropertiesBase> callback)
        {
            throw new Exception($"Missing implementation for {nameof(UnsubscribeOnPlayerDie)} on {GetType().Name}");
        }

        /// <summary>
        /// Subscribes to the event raised when a player respawns.
        /// </summary>
        /// <param name="callback">The callback to invoke when a player respawns.</param>
        public virtual void SubscribeOnPlayerRespawn(UnityAction<JUPlayerPropertiesBase> callback)
        {
            throw new Exception($"Missing implementation for {nameof(SubscribeOnPlayerRespawn)} on {GetType().Name}");
        }

        /// <summary>
        /// Unsubscribes from the event raised when a player respawns.
        /// </summary>
        /// <param name="callback">The callback to invoke when a player respawns.</param>
        public virtual void UnsubscribeOnPlayerRespawn(UnityAction<JUPlayerPropertiesBase> callback)
        {
            throw new Exception($"Missing implementation for {nameof(UnsubscribeOnPlayerRespawn)} on {GetType().Name}");
        }

        /// <summary>
        /// Finds available rooms with pagination.
        /// </summary>
        /// <param name="page">The page number to retrieve.</param>
        /// <param name="pageSize">The number of rooms per page.</param>
        /// <param name="passwordFilter">The password filter to apply when searching for rooms.</param>
        /// <param name="excludeFullRooms">Whether to exclude rooms that are already full.</param>
        /// <returns>A collection of rooms found. Null if the search fails or is not allowed.</returns>
        public async static Task<IEnumerable<ISessionInfo>> FindRooms(int page, int pageSize, PasswordFilter passwordFilter, bool excludeFullRooms)
        {
            if (CanSearchRooms == false)
            {
#if UNITY_EDITOR
                Debug.LogError($"Check {nameof(JUNetcodeMatchManagerBase)}.{nameof(CanSearchRooms)} before calling {nameof(FindRooms)}.");
#endif

                return null;
            }

            List<FilterOption> filterOptions = new();

            if (passwordFilter != PasswordFilter.All)
            {
                filterOptions.Add(new FilterOption(
                    field: FilterField.HasPassword,
                    operation: FilterOperation.Equal,
                    value: passwordFilter == PasswordFilter.WithoutPassword ? "false" : "true"
                ));
            }

            if (excludeFullRooms)
            {
                filterOptions.Add(new FilterOption(
                    field: FilterField.AvailableSlots,
                    operation: FilterOperation.Equal,
                    value: "0"
                ));
            }

            QuerySessionsOptions queryOptions = new()
            {
                Skip = page * pageSize,
                Count = pageSize,
                FilterOptions = filterOptions
            };

            try
            {
                QuerySessionsResults result = await MultiplayerService.Instance.QuerySessionsAsync(queryOptions);
                _lastRoomSearchTime = Time.unscaledTime;
                return result.Sessions;
            }
            catch (SessionException e)
            {
                Debug.LogError($"Failed to query sessions: {e.Message} {e.Error}");
                return null;
            }
        }

        /// <summary>
        /// Checks if a room with the specified room name exists.
        /// </summary>
        /// <param name="roomName">The name of the room to check for existence.</param>
        /// <returns>True if a room with the specified room name exists, otherwise false.</returns>
        public async static Task<bool> RoomExist(string roomName)
        {
            if (CanSearchRooms == false)
            {
#if UNITY_EDITOR
                Debug.LogError($"Check {nameof(JUNetcodeMatchManagerBase)}.{nameof(CanSearchRooms)} before calling {nameof(RoomExist)}.");
#endif

                return false;
            }

            QuerySessionsOptions queryOptions = new()
            {
                FilterOptions = new()
                {
                    new(
                        field: FilterField.Name,
                        operation: FilterOperation.Equal,
                        value: roomName
                    )
                },
                Count = 1
            };

            try
            {
                QuerySessionsResults result = await MultiplayerService.Instance.QuerySessionsAsync(queryOptions);
                _lastRoomSearchTime = Time.unscaledTime;
                return result.Sessions.Count > 0;
            }
            catch (SessionException e)
            {
                if (e.Error != SessionError.RateLimitExceeded)
                {
                    Debug.LogError($"Failed to query sessions: {e.Message} {e.Error}");
                }

                return false;
            }
        }

        private static async void OnEditorExitPlayMode()
        {
            if (CurrentRoom != null)
            {
                await LeaveCurrentRoom();
            }

            _setupQuitApplicationDone = false;
            _lastRoomSearchTime = 0f;
            _isLeavingRoom = false;
            CurrentRoom = null;
            JoinRoom = null;
            ExitingPlayMode?.Invoke();
        }

        /// <summary>
        /// Leaves <see cref="CurrentRoom"/> if one exists.
        /// parameter <paramref name="onLeft"/> is invoked after leaving the room.
        /// </summary>
        protected async static Task LeaveCurrentRoom(UnityAction<bool> onLeft = null)
        {
            if (_isLeavingRoom)
            {
                onLeft?.Invoke(false);
                return;
            }

            ISession session = CurrentRoom;
            CurrentRoom = null;

            // The room may have been joined by a request still in flight, so fall back to the service's own list.
            if (session == null)
            {
                try
                {
                    foreach (ISession activeSession in MultiplayerService.Instance.Sessions.Values)
                    {
                        session = activeSession;
                        break;
                    }
                }
                catch (Exception)
                {
                    // Services are already shut down, nothing left to leave.
                }
            }

            if (session == null)
            {
                onLeft?.Invoke(false);
                return;
            }

            _isLeavingRoom = true;
            try
            {
                await session.LeaveAsync();
                onLeft?.Invoke(true);
            }
            catch (ObjectDisposedException)
            {
                // The multiplayer service already disposed the session, can happen when exiting play mode.
                onLeft?.Invoke(false);
            }
            catch (SessionException e)
            {
                Debug.LogError($"Failed to leave room: {e.Message}");
                onLeft?.Invoke(false);
            }
            finally
            {
                _isLeavingRoom = false;
            }

            if (NetworkManager.Singleton)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        /// <summary>
        /// Respawns the local player immediately by resetting their health.
        /// </summary>
        public void RespawnLocalPlayerNow()
        {
            IHealth playerHealth = RawLocalPlayer.Health;
            Debug.Assert(playerHealth != null, $"Player {RawLocalPlayer.name} health component is missing.");
            Debug.Assert(playerHealth.IsDead, $"Player {RawLocalPlayer.name} is not dead and cannot respawn.");

            playerHealth.ResetHealth();
        }

        /// <summary>
        /// Return the room name.
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public static string GetRoomName(ISessionInfo session)
        {
            if (session == null)
            {
                return "No Room";
            }

            return session.Id;
        }

        /// <summary>
        /// Return the room name.
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public static string GetRoomName(ISession session)
        {
            if (session == null)
            {
                return "No Room";
            }

            return session.Id;
        }

        /// <summary>
        /// Return the map name of the room.
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public static string GetMapName(ISessionInfo session)
        {
            if (session == null)
            {
                return "Unknown";
            }

            if (session.Properties == null)
            {
                return "Unknown";
            }

            if (session.Properties.TryGetValue(_roomPropertyMapName, out SessionProperty mapNameProperty))
            {
                string mapName = mapNameProperty.Value.ToString();
                mapName = string.IsNullOrEmpty(mapName) ? "Unknown" : mapName;
                return mapName;
            }
            return "Unknown";
        }

        /// <summary>
        /// Return the map name of the room.
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public static string GetMapName(ISession session)
        {
            if (session == null)
            {
                return "Unknown";
            }

            if (session.Properties.TryGetValue(_roomPropertyMapName, out SessionProperty mapNameProperty))
            {
                string mapName = mapNameProperty.Value;
                mapName = string.IsNullOrEmpty(mapName) ? "Unknown" : mapName;
                return mapName;
            }

            return "Unknown";
        }

        /// <summary>
        /// Creates or joins a room, then raises <see cref="JoinRoom"/>. Does not start the match, just confirm player presence.
        /// </summary>
        /// <param name="roomName">Name of the room. Invalid characters are replaced.</param>
        /// <param name="maxPlayers">Maximum number of players in the room.</param>
        /// <param name="mapName">Name of the map for the room.</param>
        /// <param name="startScene">Name of the scene to start in the room.</param>
        /// <param name="roomPassword">Password for the room, if any.</param>
        protected async static Task CreateOrJoinRoom(string roomName, int maxPlayers, string mapName = null, string startScene = null, string roomPassword = null)
        {
            if (CurrentRoom != null)
            {
                Debug.LogError("Already in a room.");
                return;
            }

            SetupIfNeeded();

            // Session IDs must not contain spaces or special characters.
            roomName = System.Text.RegularExpressions.Regex.Replace(roomName ?? string.Empty, "[^a-zA-Z0-9_-]", "-");

            if (roomPassword != null)
            {
                roomPassword = roomPassword.Trim();
            }
            if (string.IsNullOrWhiteSpace(roomPassword) == true || string.IsNullOrEmpty(roomPassword) == true)
            {
                roomPassword = null;
            }

            if (roomPassword != null)
            {
                if (roomPassword.Length < 8 || roomPassword.Length > 64)
                {
                    throw new ArgumentException($"[{nameof(CreateOrJoinRoom)}] Room password must be between 8 and 64 characters.");
                }
            }

            ThrowIfNetworkManagerMissing();

            Dictionary<string, SessionProperty> _roomProperties = new();

            if (startScene != null)
            {
                if (string.IsNullOrWhiteSpace(startScene) == false && string.IsNullOrEmpty(startScene) == false)
                {
                    _roomProperties[_roomPropertyStartScene] = new(startScene);
                }
            }

            if (mapName != null)
            {
                if (string.IsNullOrWhiteSpace(mapName) == false && string.IsNullOrEmpty(mapName) == false)
                {
                    _roomProperties[_roomPropertyMapName] = new(mapName);
                }
            }

            SessionOptions sessionOptions = new SessionOptions
            {
                MaxPlayers = maxPlayers,
                IsLocked = false,
                IsPrivate = false,
                Password = roomPassword,
                SessionProperties = _roomProperties
            }.WithRelayNetwork();

            try
            {
                CurrentRoom = await MultiplayerService.Instance.CreateOrJoinSessionAsync(roomName, sessionOptions);
                JoinRoom?.Invoke(CurrentRoom);
            }
            catch (SessionException e)
            {
                Debug.LogError($"Failed to create or join session: {e.Message}");
            }
        }

        /// <summary>
        /// Tries to trigger the respawn for the local player. Returns true if the respawn was successfully triggered, false otherwise.
        /// After triggered, a timer will start and the player will be respawned once the timer completes.
        /// </summary>
        /// <returns></returns>
        public bool TryTriggerRespawn()
        {
            if (LocalPlayerEnableRespawnTimer > 0)
            {
                return false;
            }

            LocalPlayerRespawnTriggered = true;
            return true;
        }

        /// <summary>
        /// Generates a spawn position for the given player properties.
        /// </summary>
        /// <param name="ownerClientId">The client ID that owns the player object.</param>
        /// <returns>A tuple containing the spawn position and rotation.</returns>
        internal virtual (Vector3 position, Quaternion rotation) GenerateSpawnPosition(ulong ownerClientId)
        {
            throw new NotImplementedException($"Missing implementation for {nameof(GenerateSpawnPosition)} in {GetType().Name}");
        }

        private static bool TryQuit()
        {
            if (_isLeavingRoom)
            {
                return false;
            }

            _ = LeaveCurrentRoom(success =>
            {
                if (success)
                {
                    Application.Quit();
                }
            });

            return CurrentRoom == null && _isLeavingRoom == false;
        }

#if UNITY_EDITOR
        private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                OnEditorExitPlayMode();
            }
        }
#endif

        private static void ThrowIfNetworkManagerMissing()
        {
            if (NetworkManager.Singleton == null)
            {
                throw new InvalidOperationException($"{nameof(NetworkManager)} is missing on this scene.");
            }
        }
    }

    /// <summary>
    /// Typed base for match managers. Can be used to implement game types, like pvp, coop, battle royale or custom modes.
    /// </summary>
    /// <typeparam name="TManager">Concrete manager type (the deriving class itself).</typeparam>
    /// <typeparam name="TMatchSettings">Settings type used to start the match.</typeparam>
    /// <typeparam name="TPlayerProperties">Player properties type spawned for each player.</typeparam>
    public abstract class JUNetcodeMatchManagerBase<
        // Concrete manager type (the deriving class itself).
        TManager,
        // Settings type used to start the match.
        TMatchSettings,
        // Player properties type spawned for each player.
        TPlayerProperties
    > : JUNetcodeMatchManagerBase
    where TManager : JUNetcodeMatchManagerBase<
        TManager,
        TMatchSettings,
        TPlayerProperties
    >
    where TMatchSettings : MatchSettingsBase
    where TPlayerProperties : JUPlayerPropertiesBase<TManager, TPlayerProperties>
    {
        private static TManager _instance;

        private Dictionary<ulong, TPlayerProperties> _playerProperties;

        private NetworkList<NetworkBehaviourReference> _netPlayers;

        private event UnityAction<TPlayerProperties> _playerSpawnedT;
        private event UnityAction<TPlayerProperties> _playerLeftT;

        private event UnityAction<JUPlayerPropertiesBase> _playerSpawned;
        private event UnityAction<JUPlayerPropertiesBase> _playerLeft;

        private event UnityAction<(JUPlayerPropertiesBase player, IHealth.DamageResultInfo damageInfo)> _playerTakeDamage;
        private event UnityAction<JUPlayerPropertiesBase> _playerDie;

        private event UnityAction<JUPlayerPropertiesBase> _playerRespawn;
        private event UnityAction<TPlayerProperties> _playerRespawnT;

        /// <summary>
        /// Settings used when no match was configured before the scene loaded. Only used for debugging.
        /// </summary>
        [SerializeField] protected TMatchSettings _debugMatchSettings;

        /// <summary>
        /// Settings of the match to start. Set it before the scene with the manager loads.
        /// </summary>
        public static TMatchSettings StartMatchSettings { get; set; }

        /// <summary>
        /// Active manager of type <typeparamref name="TManager"/>.
        /// </summary>
        public static TManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<TManager>(FindObjectsInactive.Include);
                }

                return _instance;
            }
        }

        /// <summary>
        /// Whether services are ready and no session is active.
        /// </summary>
        public static bool CanCreateOrJoinMatch
        {
            get
            {
                if (IsConnectedAndAuthenticated == false)
                {
                    return false;
                }

                if (Instance != null && CurrentRoom != null)
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Local player properties casted to <typeparamref name="TPlayerProperties"/>.
        /// </summary>
        public TPlayerProperties PlayerProperties
        {
            get => (TPlayerProperties)RawLocalPlayer;
        }

        /// <summary>
        /// Players spawned in the match.
        /// </summary>
        public IEnumerable<TPlayerProperties> Players
        {
            get => _playerProperties.Values;
        }

        /// <inheritdoc/>
        public override int PlayersCount
        {
            get => _netPlayers.Count;
        }

        /// <inheritdoc/>
        public override MatchSettingsBase RawMatchSettings
        {
            get => StartMatchSettings;
        }

        protected JUNetcodeMatchManagerBase()
        {
            _playerProperties = new();
            _netPlayers = new(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        }

        /// <inheritdoc/>
        protected override async void Awake()
        {
            base.Awake();

            if (_instance == null)
            {
                _instance = (TManager)this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }

            _playerRespawnT += player =>
            {
                _playerRespawn?.Invoke(player);
            };

            ExitingPlayMode += () =>
            {
                StartMatchSettings = null;
            };

            JUPlayerPropertiesBase<TManager, TPlayerProperties>.SubscribeOnPlayerSpawned(OnPlayerPropertiesSpawned);
            JUPlayerPropertiesBase<TManager, TPlayerProperties>.SubscribeOnPlayerDespawned(OnPlayerPropertiesDespawned);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (StartMatchSettings == null)
            {
                await DebugStartMatch();
                return;
            }
#endif
            // Game start
            if (StartMatchSettings != null)
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    Debug.LogError("Unity Services must be initialized and authenticated before starting the match.");
                    return;
                }

                if (AuthenticationService.Instance.IsSignedIn == false)
                {
                    Debug.LogError($"{nameof(AuthenticationService)} must be signed in before starting the match.");
                    return;
                }

                await CreateMatch(StartMatchSettings);
            }
            else
            {
                Debug.LogError($"The multiplayer game started but the {nameof(TManager)}.{nameof(StartMatchSettings)} was not set. The match cannot proceed without it.");
            }
        }

        protected override void OnApplicationQuit()
        {
            base.OnApplicationQuit();
            if (CurrentRoom != null)
            {
                _ = LeaveCurrentRoom();
                return;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _ = LeaveCurrentRoom();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            JUPlayerPropertiesBase<TManager, TPlayerProperties>.UnsubscribeOnPlayerSpawned(OnPlayerPropertiesSpawned);
            JUPlayerPropertiesBase<TManager, TPlayerProperties>.UnsubscribeOnPlayerDespawned(OnPlayerPropertiesDespawned);
        }

        /// <summary>
        /// Subscribes to the event raised when a player spawns.
        /// </summary>
        public void SubscribeOnPlayerSpawned(UnityAction<TPlayerProperties> callback)
        {
            _playerSpawnedT += callback;
        }

        /// <inheritdoc/>
        public override void SubscribeOnPlayerSpawned(UnityAction<JUPlayerPropertiesBase> callback)
        {
            _playerSpawned += callback;
        }

        /// <summary>
        /// Unsubscribes from the event raised when a player spawns.
        /// </summary>
        public virtual void UnsubscribeOnPlayerSpawned(UnityAction<TPlayerProperties> callback)
        {
            _playerSpawnedT -= callback;
        }

        /// <inheritdoc/>
        public override void UnsubscribeOnPlayerSpawned(UnityAction<JUPlayerPropertiesBase> callback)
        {
            _playerSpawned -= callback;
        }

        /// <summary>
        /// Subscribes to the event raised when a player leaves.
        /// </summary>
        public void SubscribeOnPlayerLeft(UnityAction<TPlayerProperties> callback)
        {
            _playerLeftT += callback;
        }

        /// <inheritdoc/>
        public override void SubscribeOnPlayerLeft(UnityAction<JUPlayerPropertiesBase> callback)
        {
            _playerLeft += callback;
        }

        /// <summary>
        /// Unsubscribes from the event raised when a player leaves.
        /// </summary>
        public virtual void UnsubscribeOnPlayerLeft(UnityAction<TPlayerProperties> callback)
        {
            _playerLeftT -= callback;
        }

        /// <inheritdoc/>
        public override void UnsubscribeOnPlayerLeft(UnityAction<JUPlayerPropertiesBase> callback)
        {
            _playerLeft -= callback;
        }

        /// <inheritdoc/>
        public override void SubscribeOnPlayerTakeDamage(UnityAction<(JUPlayerPropertiesBase player, IHealth.DamageResultInfo damageInfo)> callback)
        {
            _playerTakeDamage += callback;
        }

        /// <inheritdoc/>
        public override void UnsubscribeOnPlayerTakeDamage(UnityAction<(JUPlayerPropertiesBase player, IHealth.DamageResultInfo damageInfo)> callback)
        {
            _playerTakeDamage -= callback;
        }

        /// <inheritdoc/>
        public override void SubscribeOnPlayerDie(UnityAction<JUPlayerPropertiesBase> callback)
        {
            _playerDie += callback;
        }

        /// <inheritdoc/>
        public override void UnsubscribeOnPlayerDie(UnityAction<JUPlayerPropertiesBase> callback)
        {
            _playerDie -= callback;
        }

        /// <inheritdoc/>
        public override void SubscribeOnPlayerRespawn(UnityAction<JUPlayerPropertiesBase> callback)
        {
            _playerRespawn += callback;
        }

        /// <inheritdoc/>
        public override void UnsubscribeOnPlayerRespawn(UnityAction<JUPlayerPropertiesBase> callback)
        {
            _playerRespawn -= callback;
        }

        /// <summary>
        /// Subscribes to the event raised when a player respawns.
        /// </summary>
        /// <param name="callback"></param>
        public void SubscribeOnPlayerRespawn(UnityAction<TPlayerProperties> callback)
        {
            _playerRespawnT += callback;
        }

        /// <summary>
        /// Unsubscribes from the event raised when a player respawns.
        /// </summary>
        /// <param name="callback"></param>
        public void UnsubscribeOnPlayerRespawn(UnityAction<TPlayerProperties> callback)
        {
            _playerRespawnT -= callback;
        }

        /// <summary>
        /// Starts the match. Implement it to create or join the session for the mode.
        /// </summary>
        /// <param name="matchSettings">Settings of the match to start.</param>
        protected virtual async Task CreateMatch(TMatchSettings matchSettings)
        {
            throw new NotImplementedException($"Missing implementation for {nameof(CreateMatch)} on {GetType().Name}");
        }

        /// <summary>
        /// Creates or joins a match room based on the current <see cref="StartMatchSettings"/>. Does not start the match, just confirm player presence.
        /// Usefull to enter on a room from main menu and then load game scene (that contains the match manager, it's made manually by the developer).
        /// </summary>
        /// <returns></returns>
        public async static Task CreateOrJoinOnMatchRoom()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Assert(StartMatchSettings != null, $"Before calling {nameof(CreateOrJoinOnMatchRoom)}, you need to set {nameof(StartMatchSettings)} on {typeof(TManager).Name}");
            Debug.Assert(string.IsNullOrEmpty(StartMatchSettings.PlayerName) == false, $"Before calling {nameof(CreateOrJoinOnMatchRoom)}, you need to set {nameof(StartMatchSettings)}.{nameof(StartMatchSettings.PlayerName)} on {typeof(TManager).Name}");
            Debug.Assert(string.IsNullOrEmpty(StartMatchSettings.RoomName) == false, $"Before calling {nameof(CreateOrJoinOnMatchRoom)}, you need to set {nameof(StartMatchSettings)}.{nameof(StartMatchSettings.RoomName)} on {typeof(TManager).Name}");
            Debug.Assert(CurrentRoom == null, "The player is already on a room.");
#endif

            await CreateOrJoinRoom(
                roomName: StartMatchSettings.RoomName,
                maxPlayers: StartMatchSettings.MatchMaxPlayers,
                roomPassword: StartMatchSettings.RoomPassword,
                mapName: StartMatchSettings.MapName,
                startScene: StartMatchSettings.StartScene
            );
        }

        private void OnPlayerSpawned(TPlayerProperties playerProperties)
        {
            if (IsOwner == true)
            {
                _netPlayers.Add(playerProperties);
            }

            _playerProperties[playerProperties.NetworkObjectId] = playerProperties;

            if (playerProperties.gameObject.TryGetComponent(out IHealth health))
            {
                health.OnDamaged += (damageInfo) =>
                {
                    _playerTakeDamage?.Invoke((playerProperties, damageInfo));
                };

                health.OnDeath += () =>
                {
                    LocalPlayerEnableRespawnTimer = Respawn.RespawnTime;
                    LocalPlayerRespawnTimer = Respawn.RespawnTime;
                    LocalPlayerRespawnTriggered = false;
                    _playerDie?.Invoke(playerProperties);
                };

                // Respawn.
                health.OnResetHealth += () =>
                {
                    NetworkTransform playerNetTransform = playerProperties.gameObject.GetComponent<NetworkTransform>();
                    Debug.Assert(playerNetTransform != null, $"Player {name} {nameof(NetworkTransform)} component is missing.");
                    (Vector3 position, Quaternion rotation) spawn = playerProperties.Spawn;

                    if (playerProperties.IsOwner == true)
                    {
                        // Reset the physics pose before committing the teleport to NetworkTransform.
                        if (playerProperties.TryGetComponent(out Rigidbody playerRigidbody))
                        {
                            playerRigidbody.position = spawn.position;
                            playerRigidbody.rotation = spawn.rotation;
                            playerRigidbody.linearVelocity = Vector3.zero;
                            playerRigidbody.angularVelocity = Vector3.zero;
                        }

                        playerNetTransform.SetState(
                            posIn: spawn.position,
                            rotIn: spawn.rotation,
                            teleportDisabled: false
                        );
                    }

                    _playerRespawnT?.Invoke(playerProperties);
                };
            }

            _playerSpawnedT?.Invoke(playerProperties);
            _playerSpawned?.Invoke(playerProperties);
        }

        private void OnPlayerLeft(TPlayerProperties playerProperties)
        {
            // The list is unusable once the manager itself is despawned (e.g. on shutdown).
            if (IsSpawned == true && IsOwner == true)
            {
                _netPlayers.Remove(playerProperties);
            }

            _playerProperties.Remove(playerProperties.NetworkObjectId);

            _playerLeftT?.Invoke(playerProperties);
            _playerLeft?.Invoke(playerProperties);
        }

        private void OnPlayerPropertiesSpawned(TPlayerProperties playerProperties)
        {
            IEnumerator WaitForSpawn()
            {
                yield return new WaitUntil(() => IsSpawned == true && playerProperties.IsSpawned == true);
                OnPlayerSpawned(playerProperties);
            }

            StartCoroutine(WaitForSpawn());
        }

        private void OnPlayerPropertiesDespawned(TPlayerProperties playerProperties)
        {
            OnPlayerLeft(playerProperties);
        }

        private async Task DebugStartMatch()
        {
            if (CurrentRoom != null)
            {
                Debug.LogWarning("A match is already in progress.");
                return;
            }

            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    // A unique profile gives each running instance its own anonymous player ID,
                    // otherwise instances on the same machine share the cached identity.
                    InitializationOptions options = new InitializationOptions().SetProfile(Guid.NewGuid().ToString("N").Substring(0, 30));
                    await UnityServices.InitializeAsync(options);
                    DebugMatchLog("Unity Services initialized.");
                }
            }
            catch (ServicesCreationException e)
            {
                Debug.LogError($"Failed to initialize Unity Services: {e.Message}");
                return;
            }

            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                return;
            }

            if (AuthenticationService.Instance.IsSignedIn == false)
            {
                try
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    DebugMatchLog("Signed in anonymously.");
                }
                catch (AuthenticationException e)
                {
                    Debug.LogError($"Failed to sign in anonymously: {e.Message}");
                    return;
                }
            }

            if (AuthenticationService.Instance.IsSignedIn == false)
            {
                return;
            }

            _debugMatchSettings.PlayerName = $"Anonymous_{Guid.NewGuid().ToString("N").Substring(0, 4)}";
            _debugMatchSettings.MapName = $"DEBUG {gameObject.scene.buildIndex}";
            _debugMatchSettings.StartScene = gameObject.scene.name;

            StartMatchSettings = _debugMatchSettings;
            await CreateMatch(StartMatchSettings);
        }

        private static void DebugMatchLog(string message)
        {
            Debug.Log($"[DebugMatch] {message}");
        }
    }
}
