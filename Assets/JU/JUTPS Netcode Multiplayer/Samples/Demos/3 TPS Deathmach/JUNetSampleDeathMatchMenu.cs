using System;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
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
            [SerializeField] private Text _selectedPlayerTypeText;
            [SerializeField] private Button _nextPlayerTypeButton;
            [SerializeField] private Button _previousPlayerTypeButton;
            [SerializeField] private Button _startRandomMatchButton;

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

        [SerializeField] private PlayerSettings[] _players;
        [SerializeField] private UiPlayerSettings _uiPlayerSettings;
        [SerializeField] private UiRoomSettings _uiRoomSettings;
        [SerializeField] private UiConnectionScreenSetting _uiConnectionScreen;

        public PlayerSettings[] Players
        {
            get => _players;
        }

        public UiPlayerSettings UiPlayer
        {
            get => _uiPlayerSettings;
        }

        public UiRoomSettings UiRoom
        {
            get => _uiRoomSettings;
        }

        public UiConnectionScreenSetting UiConnectionScreen
        {
            get => _uiConnectionScreen;
        }

        public static bool IsConnected
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
        }
    }
}
