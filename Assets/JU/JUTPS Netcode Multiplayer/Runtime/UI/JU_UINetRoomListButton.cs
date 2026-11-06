using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Multiplayer;
using JU.TPS.Netcode;
using JU.TPS.Netcode.UI;

/// <summary>
/// Represents a UI button for a multiplayer room in the room list component <see cref="JU_UINetRoomList"/>.
/// </summary>
public class JU_UINetRoomListButton : MonoBehaviour
{
    private float _lastClickTime;

    [Tooltip("The text component displaying the room name.")]
    [SerializeField] private Text _roomNameText;

    [Tooltip("The text component displaying the player count.")]
    [SerializeField] private Text _playerCountText;

    [Tooltip("The text component displaying the map name.")]
    [SerializeField] private Text _mapNameText;

    [Tooltip("The button component to select the room.")]
    [SerializeField] private Button _button;

    [Space]
    [Tooltip("The GameObject representing the selected style.")]
    [SerializeField] private GameObject _selectedStyle;

    [Tooltip("The GameObject representing the unselected style.")]
    [SerializeField] private GameObject _unselectedStyle;

    /// <summary>
    /// The session information associated with this room list button.
    /// </summary>
    public ISessionInfo SessionInfo { get; private set; }

    /// <summary>
    /// The text component displaying the room name.
    /// </summary>
    public Text RoomNameText
    {
        get => _roomNameText;
    }

    /// <summary>
    /// The text component displaying the player count.
    /// </summary>
    public Text PlayerCountText
    {
        get => _playerCountText;
    }

    /// <summary>
    /// The text component displaying the map name.
    /// </summary>
    public Text MapNameText
    {
        get => _mapNameText;
    }

    /// <summary>
    /// The button component to select the room.
    /// </summary>
    public Button Button
    {
        get => _button;
    }

    /// <summary>
    /// The GameObject representing the selected style.
    /// </summary>
    public GameObject SelectedStyle
    {
        get => _selectedStyle;
    }

    /// <summary>
    /// The GameObject representing the unselected style.
    /// </summary>
    public GameObject UnselectedStyle
    {
        get => _unselectedStyle;
    }

    /// <summary>
    /// The currently selected room list button.
    /// </summary>
    public static JU_UINetRoomListButton SelectedButton { get; private set; }

    private void Start()
    {
        if (_button != null)
        {
            _button.onClick.AddListener(OnButtonClicked);
        }

        if (_selectedStyle != null)
        {
            _selectedStyle.SetActive(false);
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

    /// <summary>
    /// Call it after spawn this button to configure it with the session information.
    /// </summary>
    /// <param name="session"></param>
    public void Configure(ISessionInfo session)
    {
        if (session == null)
        {
            _roomNameText.text = "No Room";
            _playerCountText.text = "0/0";
            return;
        }

        SessionInfo = session;
        _roomNameText.text = session.Id;
        _playerCountText.text = $"{session.MaxPlayers - session.AvailableSlots}/{session.MaxPlayers}";

        if (_mapNameText != null)
        {
            _mapNameText.text = JUNetcodeMatchManagerBase.GetMapName(session);
        }
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
