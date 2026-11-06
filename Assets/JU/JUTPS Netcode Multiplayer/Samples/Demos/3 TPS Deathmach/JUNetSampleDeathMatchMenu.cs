using System;
using System.Collections;
using System.Collections.Generic;
using JU.TPS.Netcode.Matchs;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JU.TPS.Netcode
{
    public class JUNetSampleDeathMatchMenu : MonoBehaviour
    {
        [Serializable]
        public struct PlayerSettings
        {
            [SerializeField] private string _name;
            [SerializeField] private NetworkObject _prefab;

            public string Name
            {
                get => _name;
            }

            public NetworkObject Prefab
            {
                get => _prefab;
            }
        }

        [Serializable]
        public struct SceneSettings
        {
            [SerializeField] private string _name;
            [SerializeField] private string _sceneName;

            public string Name
            {
                get => _name;
            }

            public string SceneName
            {
                get => _sceneName;
            }
        }

        [Serializable]
        public struct UiConnectionScreenSetting
        {
            [SerializeField] private GameObject _connectingScreen;
            [SerializeField] private Text _failedToConnectText;

            public GameObject ConnectingScreen
            {
                get => _connectingScreen;
            }

            public Text FailedToConnectText
            {
                get => _failedToConnectText;
            }
        }

        [Serializable]
        public struct UiPlayerSettings
        {
            [SerializeField] private InputField _playerNameInputField;
            [SerializeField] private Text _selectedPlayerTypeText;
            [SerializeField] private Button _nextPlayerTypeButton;
            [SerializeField] private Button _previousPlayerTypeButton;
            [SerializeField] private Button _startRandomMatchButton;

            public InputField PlayerNameInputField
            {
                get => _playerNameInputField;
            }

            public Text SelectedPlayerTypeText
            {
                get => _selectedPlayerTypeText;
            }

            public Button NextPlayerTypeButton
            {
                get => _nextPlayerTypeButton;
            }

            public Button PreviousPlayerTypeButton
            {
                get => _previousPlayerTypeButton;
            }

            public Button StartRandomMatchButton
            {
                get => _startRandomMatchButton;
            }

            public string PlayerName
            {
                get
                {
                    InputField playerNameInputField = PlayerNameInputField;
                    return playerNameInputField != null ? playerNameInputField.text : string.Empty;
                }
            }

            public bool HasValidName
            {
                get
                {
                    return string.IsNullOrWhiteSpace(PlayerName) == false;
                }
            }
        }

        [Serializable]
        public struct UiRoomSettings
        {
            [SerializeField] private Button _startSelectedMatchButton;

            public Button StartSelectedMatchButton
            {
                get => _startSelectedMatchButton;
            }
        }

        [Serializable]
        public struct UiCreateRoomSettings
        {
            [SerializeField] private InputField _roomNameInputField;
            [SerializeField] private InputField _roomPasswordInputField;
            [SerializeField] private InputField _maxPlayersInputField;
            [SerializeField] private Dropdown _sceneDropdown;
            [SerializeField] private Button _createRoomButton;
            [SerializeField] private Text _roomCreationErrorText;

            public InputField RoomNameInputField
            {
                get => _roomNameInputField;
            }

            public InputField RoomPasswordInputField
            {
                get => _roomPasswordInputField;
            }

            public InputField MaxPlayersInputField
            {
                get => _maxPlayersInputField;
            }

            public Dropdown SceneDropdown
            {
                get => _sceneDropdown;
            }

            public Button CreateRoomButton
            {
                get => _createRoomButton;
            }

            public Text RoomCreationErrorText
            {
                get => _roomCreationErrorText;
            }

            public string RoomName
            {
                get
                {
                    InputField roomNameInputField = RoomNameInputField;
                    return roomNameInputField != null ? roomNameInputField.text : string.Empty;
                }
            }

            public bool HasValidName
            {
                get
                {
                    return string.IsNullOrWhiteSpace(RoomName) == false;
                }
            }

            public int MaxPlayers
            {
                get
                {
                    InputField maxPlayersInputField = MaxPlayersInputField;
                    return maxPlayersInputField != null && int.TryParse(maxPlayersInputField.text, out int result) ? result : 0;
                }
            }

            public bool HasValidMaxPlayers
            {
                get
                {
                    return MaxPlayers > 0;
                }
            }

            public string RoomPassword
            {
                get
                {
                    InputField roomPasswordInputField = RoomPasswordInputField;
                    string password = roomPasswordInputField != null ? roomPasswordInputField.text : string.Empty;
                    if (string.IsNullOrWhiteSpace(password))
                    {
                        password = null;
                    }
                    return password;
                }
            }

            public bool HasValidRoomPassword
            {
                get
                {
                    return string.IsNullOrWhiteSpace(RoomPassword) == false;
                }
            }

            public int SelectedScene
            {
                get
                {
                    Dropdown sceneDropdown = SceneDropdown;
                    return sceneDropdown != null ? sceneDropdown.value : 0;
                }
            }

            public bool CanCreateRoom
            {
                get
                {
                    if (JUNetcodeMatchManagerBase.IsConnectedAndAuthenticated == false)
                    {
                        return false;
                    }

                    if (HasValidName == false)
                    {
                        return false;
                    }

                    if (HasValidMaxPlayers == false)
                    {
                        return false;
                    }

                    return true;
                }
            }
        }

        [SerializeField] private CanvasGroup _screenInteraction;
        [SerializeField] private GameObject _loadingScreen;

        [SerializeField] private PlayerSettings[] _players;
        [SerializeField] private SceneSettings[] _scenes;
        [SerializeField] private UiPlayerSettings _uiPlayerSettings;
        [SerializeField] private UiRoomSettings _uiRoomSettings;
        [SerializeField] private UiConnectionScreenSetting _uiConnectionScreen;
        [SerializeField] private UiCreateRoomSettings _uiCreateRoomSettings;

        public CanvasGroup ScreenInteraction
        {
            get => _screenInteraction;
        }

        public GameObject LoadingScreen
        {
            get => _loadingScreen;
        }

        public IEnumerable<PlayerSettings> Players
        {
            get => _players;
        }

        public IEnumerable<SceneSettings> Scene
        {
            get => _scenes;
        }

        public UiPlayerSettings UiPlayer
        {
            get => _uiPlayerSettings;
        }

        public UiRoomSettings UiRoom
        {
            get => _uiRoomSettings;
        }

        public UiCreateRoomSettings UiCreateRoom
        {
            get => _uiCreateRoomSettings;
        }

        public UiConnectionScreenSetting UiConnectionScreen
        {
            get => _uiConnectionScreen;
        }

        private void Awake()
        {
            if (UiConnectionScreen.FailedToConnectText != null)
            {
                UiConnectionScreen.FailedToConnectText.gameObject.SetActive(false);
            }
        }

        private async void Start()
        {
            GameObject connectingScreen = UiConnectionScreen.ConnectingScreen;
            Text failedToConnectText = UiConnectionScreen.FailedToConnectText;

            if (UiConnectionScreen.ConnectingScreen != null)
            {
                connectingScreen.SetActive(true);
            }

            if (LoadingScreen != null)
            {
                LoadingScreen.SetActive(false);
            }

            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                try
                {
                    // A unique profile gives each running instance its own anonymous player ID,
                    // otherwise instances on the same machine share the cached identity.
                    InitializationOptions options = new InitializationOptions().SetProfile(Guid.NewGuid().ToString("N").Substring(0, 30));
                    await UnityServices.InitializeAsync(options);
                }
                catch (Exception e)
                {
                    if (failedToConnectText != null)
                    {
                        failedToConnectText.gameObject.SetActive(true);
                        failedToConnectText.text = $"Unity service initialization error: {e.Message}";
                    }

                    Debug.LogError($"Unity service initialization error: {e.Message}");
                    throw;
                }
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
                }
                catch (Exception e)
                {
                    if (failedToConnectText != null)
                    {
                        failedToConnectText.gameObject.SetActive(true);
                        failedToConnectText.text = $"Anonymous authentication error: {e.Message}";
                    }

                    Debug.LogError($"Anonymous authentication error: {e.Message}");
                    throw;
                }
            }

            if (AuthenticationService.Instance.IsSignedIn == false)
            {
                return;
            }

            // Connection complete, hide the connecting screen if it exists.
            if (connectingScreen != null)
            {
                connectingScreen.SetActive(false);
            }

            SetupRoomCreationUi();
        }

        private void Update()
        {
        }

        private void SetupRoomCreationUi()
        {
            InputField playerNameInputField = UiPlayer.PlayerNameInputField;
            InputField roomNameInputField = UiCreateRoom.RoomNameInputField;
            InputField roomPasswordInputField = UiCreateRoom.RoomPasswordInputField;
            InputField maxPlayersInputField = UiCreateRoom.MaxPlayersInputField;
            Dropdown sceneDropdown = UiCreateRoom.SceneDropdown;
            Button createRoomButton = UiCreateRoom.CreateRoomButton;
            Text roomCreationErrorText = UiCreateRoom.RoomCreationErrorText;

            if (maxPlayersInputField != null)
            {
                maxPlayersInputField.characterValidation = InputField.CharacterValidation.Integer;
                maxPlayersInputField.text = "4";
            }

            if (roomCreationErrorText != null)
            {
                roomCreationErrorText.gameObject.SetActive(false);
            }

            if (sceneDropdown != null)
            {
                sceneDropdown.ClearOptions();
                List<string> sceneNames = new();
                foreach (var scene in _scenes)
                {
                    sceneNames.Add(scene.Name);
                }
                sceneDropdown.AddOptions(sceneNames);
            }

            if (createRoomButton != null)
            {
                createRoomButton.onClick.AddListener(async () =>
                {
                    if (roomCreationErrorText != null)
                    {
                        roomCreationErrorText.gameObject.SetActive(false);
                    }

                    if (CanCreateRoom() == false)
                    {
                        // Show room creation error.
                        if (roomCreationErrorText != null)
                        {
                            if (UiPlayer.HasValidName == false)
                            {
                                roomCreationErrorText.text = $"Invalid player name '{UiPlayer.PlayerName}'.";
                                roomCreationErrorText.gameObject.SetActive(true);
                            }

                            if (JUNetDeathMatchManager.IsConnectedAndAuthenticated == false)
                            {
                                roomCreationErrorText.text = "Not connected or authenticated.";
                                roomCreationErrorText.gameObject.SetActive(true);
                            }

                            if (UiCreateRoom.CanCreateRoom == false)
                            {
                                roomCreationErrorText.text = "Cannot create room now.";
                                roomCreationErrorText.gameObject.SetActive(true);
                            }

                            if (UiCreateRoom.HasValidName == false)
                            {
                                roomCreationErrorText.text = "Invalid room name.";
                                roomCreationErrorText.gameObject.SetActive(true);
                            }

                            if (UiCreateRoom.MaxPlayers <= 0)
                            {
                                roomCreationErrorText.text = "Invalid players count.";
                                roomCreationErrorText.gameObject.SetActive(true);
                            }
                        }

                        return;
                    }

                    bool roomExists = await JUNetcodeMatchManagerBase.RoomExist(UiCreateRoom.RoomName);
                    if (roomExists)
                    {
                        if (roomCreationErrorText != null)
                        {
                            roomCreationErrorText.text = "A room with this name already exists.";
                            roomCreationErrorText.gameObject.SetActive(true);
                        }

                        return;
                    }

                    string selectedScene = _scenes[UiCreateRoom.SelectedScene].SceneName;
                    string selectedMap = _scenes[UiCreateRoom.SelectedScene].Name;
                    JUNetDeathMatchManager.StartMatchSettings = new(
                        sessionName: UiCreateRoom.RoomName,
                        playerName: UiPlayer.PlayerName,
                        teamMaxPlayers: UiCreateRoom.MaxPlayers,
                        startScene: selectedScene,
                        mapName: selectedMap,
                        sessionPassword: UiCreateRoom.RoomPassword
                    );

                    try
                    {
                        if (_screenInteraction != null)
                        {
                            _screenInteraction.interactable = false;
                        }

                        createRoomButton.interactable = false;
                        await JUNetDeathMatchManager.CreateOrJoinOnMatchRoom();
                        SceneManager.LoadScene(selectedScene);
                        if (LoadingScreen != null)
                        {
                            LoadingScreen.SetActive(true);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (roomCreationErrorText != null)
                        {
                            roomCreationErrorText.text = $"Failed to create or join room: {ex.Message}";
                            roomCreationErrorText.gameObject.SetActive(true);

                            if (_screenInteraction != null)
                            {
                                _screenInteraction.interactable = true;
                            }
                        }
                    }
                });
            }
        }

        private bool CanCreateRoom()
        {
            if (UiPlayer.HasValidName == false)
            {
                return false;
            }

            if (UiCreateRoom.CanCreateRoom == false)
            {
                return false;
            }

            if (JUNetDeathMatchManager.CanSearchRooms == false)
            {
                return false;
            }

            return true;
        }
    }
}
