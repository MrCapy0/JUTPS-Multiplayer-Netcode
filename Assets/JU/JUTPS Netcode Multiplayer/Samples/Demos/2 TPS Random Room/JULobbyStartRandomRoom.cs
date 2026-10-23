using System;
using JU.TPS.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JU.TPS.Lobby
{
    /// <summary>
    /// Lobby screen that validates the player name and loads the game scene to create or join a random room.
    /// </summary>
    public class JULobbyStartRandomRoom : MonoBehaviour
    {
        [Header("Lobby Settings")]
        [Tooltip("Name of the scene loaded when the match starts.")]
        [SerializeField] private string _gameScene;
        [Tooltip("Identifier of the lobby used to find or create a room.")]
        [SerializeField] private string _lobbyId;
        [Tooltip("Maximum number of players allowed in the room.")]
        [Range(2, 100)]
        [SerializeField] private int _maxPlayers;

        [Header("UI")]
        [Tooltip("Input field where the player types their name.")]
        [SerializeField] private InputField _playerNameText;
        [Tooltip("Button that starts the matchmaking.")]
        [SerializeField] private Button _startButton;
        [Tooltip("Text used to display warnings to the player.")]
        [SerializeField] private Text _warningText;

        private void Reset()
        {
            _maxPlayers = 2;
            _lobbyId = Guid.NewGuid().ToString(); // Each project should have a unique lobby name by default.
        }

        private void Awake()
        {
            _warningText.text = string.Empty;
            _startButton.onClick.AddListener(OnClickStartButton);
        }

        private async void Start()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                try
                {
                    // A unique profile gives each running instance its own anonymous player ID,
                    // otherwise instances on the same machine share the cached identity.
                    InitializationOptions options = new InitializationOptions().SetProfile(Guid.NewGuid().ToString("N").Substring(0, 30));
                    await UnityServices.InitializeAsync(options);
                }
                catch (ServicesCreationException e)
                {
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
                catch (AuthenticationException e)
                {
                    Debug.LogError($"Anonymous authentication error: {e.Message}");
                    throw;
                }
            }

            if (AuthenticationService.Instance.IsSignedIn == false)
            {
                return;
            }
        }

        private void Update()
        {

        }

        private void OnClickStartButton()
        {
            if (_playerNameText.text.Trim().Length == 0)
            {
                ShowWarning("Player name cannot be empty.");
                return;
            }

            if (JUNetcodeSimpleMatchManager.CanCreateOrJoinMatch == false)
            {
                ShowWarning("Cannot create or join a random room.\n Check your network connection or try again later. Also check if you have your unity's services configured on project settings.");
                return;
            }

            _warningText.text = string.Empty;
            _startButton.interactable = false;
            CreateOrJoinRandomRoom();
        }

        private void CreateOrJoinRandomRoom()
        {
            if (JUNetcodeSimpleMatchManager.CanCreateOrJoinMatch == false)
            {
                return;
            }

            JUNetcodeSimpleMatchManager.StartMatchSettings = new JUNetcodeSimpleMatchManager.MatchSettings(_lobbyId, _playerNameText.text.Trim(), _maxPlayers);

            // The NetworkManager lives in the game scene, so it must be loaded before the session starts.
            SceneManager.LoadSceneAsync(_gameScene, LoadSceneMode.Single);
        }

        private void ShowWarning(string message)
        {
            if (_warningText != null)
            {
                _warningText.text = message;
            }
        }
    }
}

