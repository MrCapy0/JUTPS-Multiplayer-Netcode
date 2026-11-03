using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Multiplayer;

public class JU_UINetRoomListButton : MonoBehaviour
{
    private float _lastClickTime;

    [SerializeField] private Text _roomNameText;
    [SerializeField] private Text _playerCountText;
    [SerializeField] private Button _button;

    [Space]
    [SerializeField] private GameObject _selectedStyle;
    [SerializeField] private GameObject _unselectedStyle;

    public ISessionInfo SessionInfo { get; private set; }

    public Text RoomNameText
    {
        get => _roomNameText;
    }

    public Text PlayerCountText
    {
        get => _playerCountText;
    }

    public Button Button
    {
        get => _button;
    }

    public static JU_UINetRoomListButton SelectedButton { get; private set; }

    private void Start()
    {
        if (_button != null)
        {
            _button.onClick.AddListener(OnButtonClicked);
        }
    }

    private void Update()
    {
        if (_selectedStyle != null)
        {
            _selectedStyle.SetActive(SelectedButton == this);
        }

        if (_unselectedStyle != null)
        {
            _unselectedStyle.SetActive(SelectedButton != this);
        }
    }

    public void Configure(ISessionInfo session)
    {
        if (session == null)
        {
            _roomNameText.text = "No Room";
            _playerCountText.text = "0/0";
            return;
        }

        SessionInfo = session;
        _roomNameText.text = session.Name;
        _playerCountText.text = $"{session.MaxPlayers - session.AvailableSlots}/{session.MaxPlayers}";
    }

    private void OnButtonClicked()
    {
        float clickTime = Time.time;

        // Avoid double click.
        if (clickTime - _lastClickTime < 0.3f)
        {
            return;
        }

        _lastClickTime = clickTime;
        if (SelectedButton == this)
        {
            // Deselect the button if it is already selected.
            SelectedButton = null;
            return;
        }

        SelectedButton = this;
    }
}
