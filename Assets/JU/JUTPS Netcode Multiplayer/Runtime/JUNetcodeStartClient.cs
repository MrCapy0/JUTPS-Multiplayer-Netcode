using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace JU.TPS.Netcode
{
    /// <summary>
    /// Debug UI that starts the host or client and hides itself once the network starts.
    /// </summary>
    public class JUNetcodeStartClient : MonoBehaviour
    {
        [Tooltip("Button that starts a host.")]
        [SerializeField] private Button _startHostButton;

        [Tooltip("Button that starts a client.")]
        [SerializeField] private Button _startClientButton;
        
        [Tooltip("Objects disabled once the connection starts.")]
        [SerializeField] private GameObject[] _disableOnConnect;

        private void Start()
        {
            if (_startHostButton != null)
            {
                _startHostButton.onClick.AddListener(() => NetworkManager.Singleton.StartHost());
            }

            if (_startClientButton != null)
            {
                _startClientButton.onClick.AddListener(() => NetworkManager.Singleton.StartClient());
            }

            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
            {
                OnStartSession();
            }
            else
            {
                NetworkManager.Singleton.OnClientStarted += OnStartSession;
            }
        }

        private void OnStartSession()
        {
            gameObject.SetActive(false);
            foreach (GameObject obj in _disableOnConnect)
            {
                obj.SetActive(false);
            }
        }
    }
}