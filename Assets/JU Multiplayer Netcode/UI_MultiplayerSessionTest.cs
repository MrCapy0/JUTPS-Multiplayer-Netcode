using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class AutoStartClient : MonoBehaviour
{
    public Button StartHostButton;
    public Button StartClientButton;

    void Start()
    {
        StartHostButton.onClick.AddListener(StartHost);
        StartClientButton.onClick.AddListener(StartClient);
    }

    private void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        gameObject.SetActive(false);
    }
    
    private void StartClient()
    {
        NetworkManager.Singleton.StartClient();
        gameObject.SetActive(false);
    }
}
