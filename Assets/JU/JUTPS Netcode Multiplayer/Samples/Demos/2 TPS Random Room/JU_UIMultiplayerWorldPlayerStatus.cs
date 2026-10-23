using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JU.TPS.Netcode
{
    /// <summary>
    /// World-space status indicator (player name) that follows a remote player on screen.
    /// The first instance found acts as the hidden template cloned for each remote player.
    /// </summary>
    public class JU_UIMultiplayerWorldPlayerStatus : MonoBehaviour
    {
        private static JU_UIMultiplayerWorldPlayerStatus _template;
        private static Camera _camera;

        [Tooltip("Root object of the indicator, toggled on and off.")]
        [SerializeField] private GameObject _container;
        [Tooltip("Text that displays the player name.")]
        [SerializeField] private Text _playerNameText;

        /// <summary>Player tracked by this indicator.</summary>
        public JUPlayerPropertiesBase PlayerProperties { get; private set; }

        private void Start()
        {
            if (_template == null)
            {
                _template = this;
                gameObject.SetActive(false);

                JU_PlayerStatusManager.Setup();
                return;
            }

            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (_camera == null || _camera.isActiveAndEnabled == false)
            {
                _camera = Camera.main;
            }

            Debug.Assert(_camera, "There are no active cameras in the scene.");

            if (PlayerProperties == null)
            {
                Destroy(gameObject);
                return;
            }

            if (PlayerProperties.gameObject.activeInHierarchy == false)
            {
                _container.SetActive(false);
                return;
            }

            Vector3 targetWorldPosition = PlayerProperties.UpperCenter + (Vector3.up * 0.2f);

            Vector3 directionToTarget = (targetWorldPosition - _camera.transform.position).normalized;
            targetWorldPosition = _camera.transform.position + directionToTarget; // Avoid clipping issues with the camera.

            // The target is behind the camera, hide the UI indicator.
            if (directionToTarget == Vector3.zero || Vector3.Dot(directionToTarget / directionToTarget.magnitude, _camera.transform.forward) < 0f)
            {
                _container.SetActive(false);
                return;
            }

            _container.SetActive(true);
            Vector3 screenPosition = _camera.WorldToScreenPoint(targetWorldPosition);

            // Update the UI indicator's position on the screen.
            _container.transform.position = screenPosition;
            _playerNameText.text = PlayerProperties.PlayerName;
        }

        private class JU_PlayerStatusManager : MonoBehaviour
        {
            private static JU_PlayerStatusManager _instance;

            private readonly Dictionary<JUPlayerPropertiesBase, JU_UIMultiplayerWorldPlayerStatus> _indicators;

            public JU_PlayerStatusManager()
            {
                _indicators = new Dictionary<JUPlayerPropertiesBase, JU_UIMultiplayerWorldPlayerStatus>();
            }

            private void Awake()
            {
                if (_instance != null && _instance != this)
                {
                    Destroy(gameObject);
                    return;
                }

                _instance = this;

                JUNetcodeMatchManagerBase.RawInstance.SubscribeOnPlayerSpawned(OnPlayerSpawned);
                JUNetcodeMatchManagerBase.RawInstance.SubscribeOnPlayerLeft(OnPlayerLeft);
            }

            private void OnDestroy()
            {
                if (_instance == this)
                {
                    _instance = null;
                }

                if (JUNetcodeMatchManagerBase.RawInstance != null)
                {
                    JUNetcodeMatchManagerBase.RawInstance.UnsubscribeOnPlayerSpawned(OnPlayerSpawned);
                    JUNetcodeMatchManagerBase.RawInstance.UnsubscribeOnPlayerLeft(OnPlayerLeft);
                }
            }

            public static void Setup()
            {
                if (_instance == null)
                {
                    // TODO: Check parent.
                    _instance = _template.transform.parent.gameObject.AddComponent<JU_PlayerStatusManager>();
                }
            }

            private void SpawnIndicator(JUPlayerPropertiesBase playerProperties)
            {
                if (playerProperties == null)
                {
                    return;
                }

                JU_UIMultiplayerWorldPlayerStatus indicator = Instantiate(_template, transform);
                indicator.PlayerProperties = playerProperties;

                _indicators.Add(playerProperties, indicator);
                indicator.gameObject.SetActive(true);
            }

            private void OnPlayerSpawned(JUPlayerPropertiesBase player)
            {
                if (player.IsLocalPlayer)
                {
                    return;
                }

                SpawnIndicator(player);
            }

            private void OnPlayerLeft(JUPlayerPropertiesBase player)
            {
                if (player.IsLocalPlayer)
                {
                    return;
                }

                JU_UIMultiplayerWorldPlayerStatus indicator = _indicators[player];
                _indicators.Remove(player);
                Destroy(indicator.gameObject);
            }
        }
    }
}