using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class AutoStartClient : MonoBehaviour
{
    public Button StartHostButton;
    public Button StartClientButton;
    public GameObject[] ToDisable;

    void Start()
    {
        StartHostButton.onClick.AddListener(() => NetworkManager.Singleton.StartHost());
        StartClientButton.onClick.AddListener(() => NetworkManager.Singleton.StartClient());

        NetworkManager.Singleton.OnClientStarted += OnStartSession;
    }

    private void OnStartSession()
    {
        gameObject.SetActive(false);
        foreach (var obj in ToDisable)
        {
            obj.SetActive(false);
        }
    }
}
