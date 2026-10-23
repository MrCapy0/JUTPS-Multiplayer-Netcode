using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
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
    [System.Serializable]
    public abstract class MatchSettingsBase
    {
        [Tooltip("Name of the multiplayer session to create or join.")]
        [SerializeField] private string _sessionName;

        [Tooltip("Name of the local player.")]
        [SerializeField] private string _playerName;

        /// <summary>
        /// Name of the session to create or join.
        /// </summary>
        public string SessionName
        {
            get => _sessionName;
        }

        /// <summary>
        /// Name of the local player.
        /// </summary>
        public string PlayerName
        {
            get => _playerName;
            internal set => _playerName = value;
        }

        /// <param name="sessionName">Initial value of <see cref="SessionName"/>.</param>
        /// <param name="playerName">Initial value of <see cref="PlayerName"/>.</param>
        protected MatchSettingsBase(string sessionName, string playerName)
        {
            _sessionName = sessionName;
            _playerName = playerName;
        }
    }

    /// <summary>
    /// Base networked component that holds the properties of a player in a match.
    /// </summary>
    public abstract class JUPlayerPropertiesBase : NetworkBehaviour
    {
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
        public float Health
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
            get => JUNetcodeMatchManagerBase.RawInstance.CurrentSession;
        }

        /// <summary>
        /// Creates <see cref="_netPlayerName"/>.
        /// </summary>
        protected JUPlayerPropertiesBase()
        {
            _netPlayerName = new NetworkVariable<FixedString64Bytes>(new FixedString64Bytes(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        }

        /// <summary>
        /// Unity message. Extension point for derived classes.
        /// </summary>
        protected virtual void Awake()
        {
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
        }

        private void OnPlayerNameChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
        {
            if (IsSpawned == false)
            {
                return;
            }

            PlayerName = newValue.ToString();
        }
    }

    /// <summary>
    /// <see cref="JUPlayerPropertiesBase"/> bound to a match manager and a concrete player type.
    /// Raises <see cref="OnPlayerSpawned"/> and <see cref="OnPlayerDespawned"/> for the manager.
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
        /// <summary>
        /// Raised when a player is spawned on the network.
        /// </summary>
        public static event UnityAction<TPlayerProperties> OnPlayerSpawned;

        /// <summary>
        /// Raised when a player is despawned from the network.
        /// </summary>
        public static event UnityAction<TPlayerProperties> OnPlayerDespawned;

        /// <summary>
        /// Active match manager.
        /// </summary>
        public TManager MatchManager
        {
            get => (TManager)JUNetcodeMatchManagerBase.RawInstance;
        }

        /// <inheritdoc/>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            StartCoroutine(WaitForSpawned(() =>
            {
                OnPlayerSpawned?.Invoke((TPlayerProperties)this);
                Debug.Log("player spawned");
            }));
        }

        /// <inheritdoc/>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            StartCoroutine(WaitForSpawned(() =>
            {
                OnPlayerDespawned?.Invoke((TPlayerProperties)this);
            }));
        }

        private IEnumerator WaitForSpawned(UnityAction onSpawned)
        {
            while (IsSpawned == false)
            {
                yield return null;
            }

            onSpawned?.Invoke();
        }
    }

    /// <summary>
    /// Non-generic base of every match manager. Handles the session lifecycle and exposes player events.
    /// </summary>
    public abstract class JUNetcodeMatchManagerBase : NetworkBehaviour
    {
        private static bool _setupDone;

        private static JUNetcodeMatchManagerBase _rawInstance;

        /// <summary>
        /// Raised when the local player creates or joins a session.
        /// </summary>
        public event UnityAction<ISession> JoinSession;

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
        /// Session the local player is in, or null.
        /// </summary>
        public ISession CurrentSession { get; private set; }

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

        protected virtual void Awake()
        {
            if (_setupDone == false)
            {
                _setupDone = true;

#if UNITY_EDITOR
                UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
            }

            if (_rawInstance == null)
            {
                _rawInstance = this;
            }

            Debug.Assert(_rawInstance == this, $"Another Match Manager ({_rawInstance.name} of type {_rawInstance.GetType().Name}) already exists. Multiple instances are not allowed.");
        }

        protected virtual void OnEnable()
        {
        }

        protected virtual void OnDisable()
        {
        }

        protected virtual void Start()
        {
            ThrowIfNetworkManagerMissing();
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        protected virtual void Update()
        {
        }

        public override void OnDestroy()
        {
            _rawInstance = null;
            base.OnDestroy();

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
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
        /// Called when a client connects to the network.
        /// </summary>
        protected virtual void OnClientConnected(ulong clientId)
        {
        }

        /// <summary>
        /// Called when a client disconnects from the network.
        /// </summary>
        protected virtual void OnClientDisconnected(ulong clientId)
        {
        }

        protected virtual void OnEditorExitPlayMode()
        {
        }

        /// <summary>
        /// Leaves <see cref="CurrentSession"/> if one exists.
        /// </summary>
        protected async void LeaveCurrentSession()
        {
            ISession session = CurrentSession;
            if (session == null)
            {
                return;
            }

            CurrentSession = null;
            try
            {
                await session.LeaveAsync();
            }
            catch (SessionException e)
            {
                Debug.LogWarning($"Failed to leave session: {e.Message}");
            }
        }

        /// <summary>
        /// Creates or joins a session, then raises <see cref="JoinSession"/>.
        /// </summary>
        /// <param name="sessionName">Name of the session. Invalid characters are replaced.</param>
        /// <param name="maxPlayers">Maximum number of players in the session.</param>
        protected async void CreateOrJoinSession(string sessionName, int maxPlayers)
        {
            // Session IDs must not contain spaces or special characters.
            sessionName = System.Text.RegularExpressions.Regex.Replace(sessionName ?? string.Empty, "[^a-zA-Z0-9_-]", "-");

            ThrowIfNetworkManagerMissing();

            SessionOptions sessionOptions = new SessionOptions
            {
                MaxPlayers = maxPlayers,
                IsLocked = false,
                IsPrivate = false,
            }.WithRelayNetwork();

            try
            {
                CurrentSession = await MultiplayerService.Instance.CreateOrJoinSessionAsync(sessionName, sessionOptions);
                JoinSession?.Invoke(CurrentSession);
            }
            catch (SessionException e)
            {
                Debug.LogError($"Failed to create or join session: {e.Message}");
            }
        }

#if UNITY_EDITOR
        private void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
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
    public abstract class JUNetcodeMatchManagerBaseT<
        // Concrete manager type (the deriving class itself).
        TManager,
        // Settings type used to start the match.
        TMatchSettings,
        // Player properties type spawned for each player.
        TPlayerProperties
    > : JUNetcodeMatchManagerBase
    where TManager : JUNetcodeMatchManagerBaseT<
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
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    return false;
                }

                if (AuthenticationService.Instance.IsSignedIn == false)
                {
                    return false;
                }

                if (Instance != null && Instance.CurrentSession != null)
                {
                    return false;
                }

                return true;
            }
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

        protected JUNetcodeMatchManagerBaseT()
        {
            _playerProperties = new Dictionary<ulong, TPlayerProperties>();
            _netPlayers = new NetworkList<NetworkBehaviourReference>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        }

        /// <inheritdoc/>
        protected override void Awake()
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

            JUPlayerPropertiesBase<TManager, TPlayerProperties>.OnPlayerSpawned += OnPlayerPropertiesSpawned;
            JUPlayerPropertiesBase<TManager, TPlayerProperties>.OnPlayerDespawned += OnPlayerPropertiesDespawned;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (StartMatchSettings == null)
            {
                DebugStartMatch();
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

                Debug.Log("Starting real game match.");
                CreateMatch(StartMatchSettings);
            }
            else
            {
                Debug.LogError($"The multiplayer game started but the {nameof(TManager)}.{nameof(StartMatchSettings)} was not set. The match cannot proceed without it.");
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            JUPlayerPropertiesBase<TManager, TPlayerProperties>.OnPlayerSpawned -= OnPlayerPropertiesSpawned;
            JUPlayerPropertiesBase<TManager, TPlayerProperties>.OnPlayerDespawned -= OnPlayerPropertiesDespawned;
            LeaveCurrentSession();
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

        /// <summary>
        /// Starts the match. Implement it to create or join the session for the mode.
        /// </summary>
        /// <param name="matchSettings">Settings of the match to start.</param>
        protected virtual void CreateMatch(TMatchSettings matchSettings)
        {
            throw new NotImplementedException($"Missing implementation for {nameof(CreateMatch)} on {GetType().Name}");
        }

        /// <summary>
        /// Called when a player has spawned.
        /// </summary>
        protected virtual void OnPlayerSpawned(TPlayerProperties playerProperties)
        {
            if (IsOwner == true)
            {
                _netPlayers.Add(playerProperties);
            }

            _playerProperties[playerProperties.NetworkObjectId] = playerProperties;

            _playerSpawnedT?.Invoke(playerProperties);
            _playerSpawned?.Invoke(playerProperties);
        }

        /// <summary>
        /// Called when a player has left.
        /// </summary>
        protected virtual void OnPlayerLeft(TPlayerProperties playerProperties)
        {
            if (IsOwner == true)
            {
                _netPlayers.Remove(playerProperties);
            }

            _playerProperties.Remove(playerProperties.NetworkObjectId);

            _playerLeftT?.Invoke(playerProperties);
            _playerLeft?.Invoke(playerProperties);
        }

        protected override void OnEditorExitPlayMode()
        {
            base.OnEditorExitPlayMode();

            StartMatchSettings = null;
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

        private async void DebugStartMatch()
        {
            if (CurrentSession != null)
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
                    InitializationOptions options = new InitializationOptions().SetProfile(System.Guid.NewGuid().ToString("N").Substring(0, 30));
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

            _debugMatchSettings.PlayerName = $"Anonymous_{System.Guid.NewGuid().ToString("N").Substring(0, 4)}";

            StartMatchSettings = _debugMatchSettings;
            CreateMatch(StartMatchSettings);
        }

        private static void DebugMatchLog(string message)
        {
            Debug.Log($"[DebugMatch] {message}");
        }
    }
}
