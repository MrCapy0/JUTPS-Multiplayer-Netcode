using JUTPS.CameraSystems;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JU.TPS.Netcode
{
    public class JU_UINetcodeGameOver : MonoBehaviour
    {
        [System.Serializable]
        public struct RespawnTimerSettings
        {
            public GameObject Container;
            public Text TimerText;
        }

        private bool _defaultCursorVisible;
        private CursorLockMode _defaultCursorLockMode;

        [SerializeField] private GameObject _screenContainer;

        [Space]
        [SerializeField] private Button _respawnButton;
        [SerializeField] private RespawnTimerSettings _enabledRespawnTimer;
        [SerializeField] private RespawnTimerSettings _respawnTimer;

        public event UnityAction<bool> OnSetScreenActive;

        public bool IsShowing
        {
            get => _screenContainer.activeSelf;
        }

        public Button RespawnButton
        {
            get => _respawnButton;
        }

        public RespawnTimerSettings RespawnTimer
        {
            get => _respawnTimer;
        }

        public RespawnTimerSettings EnabledRespawnTimer
        {
            get => _enabledRespawnTimer;
        }

        public JUNetcodeMatchManagerBase MatchManager
        {
            get => JUNetcodeMatchManagerBase.RawInstance;
        }

        private void Awake()
        {
            Debug.Assert(_screenContainer != null, "Screen container is not assigned.");
            _screenContainer.SetActive(false);

            if (MatchManager == null)
            {
                return;
            }

            if (_respawnButton != null)
            {
                _respawnButton.onClick.AddListener(OnRespawnButtonClicked);
            }

            MatchManager.SubscribeOnPlayerDie(OnPlayerDie);
            MatchManager.SubscribeOnPlayerRespawn(OnPlayerRespawn);
        }

        private void OnDestroy()
        {
            if (MatchManager == null)
            {
                return;
            }

            MatchManager.UnsubscribeOnPlayerDie(OnPlayerDie);
            MatchManager.UnsubscribeOnPlayerRespawn(OnPlayerRespawn);
        }

        private void Start()
        {

        }

        private void Update()
        {
            if (_respawnButton != null)
            {
                _respawnButton.interactable = MatchManager.LocalPlayerEnableRespawnTimer <= 0f && MatchManager.LocalPlayerRespawnTriggered == false;
            }

            bool respawnEnabled = MatchManager.Respawn.Enabled;
            if (_enabledRespawnTimer.Container != null)
            {
                bool isEnablingRespawn = MatchManager.LocalPlayerEnableRespawnTimer > 0f && respawnEnabled;
                _enabledRespawnTimer.Container.SetActive(isEnablingRespawn);
                if (_enabledRespawnTimer.TimerText != null && isEnablingRespawn)
                {
                    _enabledRespawnTimer.TimerText.text = Mathf.CeilToInt(MatchManager.LocalPlayerEnableRespawnTimer).ToString();
                }
            }

            if (_respawnTimer.Container != null)
            {
                bool isRespawnTimerActive = MatchManager.LocalPlayerRespawnTimer > 0f && MatchManager.LocalPlayerRespawnTriggered == true && respawnEnabled;
                _respawnTimer.Container.SetActive(isRespawnTimerActive);
                if (_respawnTimer.TimerText != null && isRespawnTimerActive)
                {
                    _respawnTimer.TimerText.text = Mathf.CeilToInt(MatchManager.LocalPlayerRespawnTimer).ToString();
                }
            }
        }

        private void SetScreenActive(bool isActive)
        {
            if (_screenContainer.activeSelf == isActive)
            {
                return;
            }

            _screenContainer.SetActive(isActive);
            OnSetScreenActive?.Invoke(isActive);

            if (isActive == true)
            {
                _defaultCursorVisible = Cursor.visible;
                _defaultCursorLockMode = Cursor.lockState;
                JUCameraController.LockMouse(Lock: false, Hide: false);
            }
            else
            {
                Cursor.visible = _defaultCursorVisible;
                Cursor.lockState = _defaultCursorLockMode;
            }

            if (_respawnButton != null)
            {
                _respawnButton.gameObject.SetActive(MatchManager.Respawn.Enabled);
                _respawnButton.interactable = false;
            }

            if (_enabledRespawnTimer.Container != null)
            {
                _enabledRespawnTimer.Container.SetActive(false);
            }

            if (_respawnTimer.Container != null)
            {
                _respawnTimer.Container.SetActive(false);
            }
        }

        public void Hide()
        {
            SetScreenActive(false);
        }

        private void Show()
        {
            SetScreenActive(true);
        }

        private void OnPlayerDie(JUPlayerPropertiesBase playerProperties)
        {
            if (playerProperties.IsLocalPlayer == false)
            {
                return;
            }

            Show();
        }

        private void OnPlayerRespawn(JUPlayerPropertiesBase playerProperties)
        {
            if (playerProperties.IsLocalPlayer == false)
            {
                return;
            }

            Hide();
        }

        private void OnRespawnButtonClicked()
        {
            MatchManager.TryTriggerRespawn();
        }
    }
}