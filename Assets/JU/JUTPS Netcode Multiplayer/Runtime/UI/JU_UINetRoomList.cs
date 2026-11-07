using System.Collections;
using System.Collections.Generic;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace JU.TPS.Netcode.UI
{
    /// <summary>
    /// UI component for displaying a list of multiplayer rooms.
    /// </summary>
    public class JU_UINetRoomList : MonoBehaviour
    {
        /// <summary>
        /// Pagination settings for the room list.
        /// </summary>
        [System.Serializable]
        public struct PaginationSettings
        {
            [Tooltip("The container GameObject that holds the room list buttons.")]
            [SerializeField] private GameObject _container;

            [Tooltip("The text component that displays the current page number.")]
            [SerializeField] private Text _pageIndicator;

            [Tooltip("The button component used to navigate to the next page of rooms.")]
            [SerializeField] private Button _nextPageButton;

            [Tooltip("The button component used to navigate to the previous page of rooms.")]
            [SerializeField] private Button _previousPageButton;

            [Space]
            [Range(1, 100)]
            [Tooltip("The number of rooms to display per page.")]
            [SerializeField] private int _pageSize;

            /// <summary>
            /// The container GameObject that holds the room list buttons.
            /// </summary>
            public GameObject Container
            {
                get => _container;
            }

            /// <summary>
            /// The text component that displays the current page number.
            /// </summary>
            public Text PageIndicator
            {
                get => _pageIndicator;
            }

            /// <summary>
            /// The button component used to navigate to the next page of rooms.
            /// </summary>
            public Button NextPageButton
            {
                get => _nextPageButton;
            }

            /// <summary>
            /// The button component used to navigate to the previous page of rooms.
            /// </summary>
            public Button PreviousPageButton
            {
                get => _previousPageButton;
            }

            /// <summary>
            /// The number of rooms to display per page.
            /// </summary>
            public int PageSize
            {
                get => _pageSize;
            }

            internal void Reset()
            {
                _pageSize = 10;
            }

            internal void OnValidate()
            {
                _pageSize = Mathf.Max(1, _pageSize);
            }
        }

        private List<ISessionInfo> _roomsResult;
        private List<JU_UINetRoomListButton> _roomListButtons;

        [Header("Screens")]
        [Tooltip("The container GameObject that holds the room list of available rooms.")]
        [SerializeField] private GameObject _roomsContainer;

        [Tooltip("The screen displayed while the room list is being refreshed.")]
        [SerializeField] private GameObject _refreshScreen;

        [Tooltip("The screen displayed when no rooms are available.")]
        [SerializeField] private GameObject _noRoomsScreen;

        [Header("Filters")]
        [Tooltip("The dropdown used to filter rooms by password requirement.\nMust have 3 options: All, WithoutPassword, WithPassword.")]
        [SerializeField] private Dropdown _passwordFilterDropdown;

        [Header("Room List")]
        [Tooltip("The prefab used to create room list buttons.\nCan be a gameObject in the scene or a prefab.")]
        [SerializeField] private JU_UINetRoomListButton _roomButtonPrefab;

        [Tooltip("The pagination settings for the room list.")]
        [SerializeField] private PaginationSettings _pagination;

        [Tooltip("The button used to refresh the room list.")]
        [SerializeField] private Button _refreshButton;

        /// <summary>
        /// Indicates whether the room list is currently being refreshed.
        /// </summary>
        public bool IsRefreshing { get; private set; }

        /// <summary>
        /// The current page number of the room list.
        /// </summary>
        public int CurrentPage { get; private set; }

        /// <summary>
        /// The current password filter applied to the room list.
        /// </summary>
        public JUNetcodeMatchManagerBase.PasswordFilter CurrentPasswordFilter { get; private set; }

        /// <summary>
        /// The container GameObject that holds the room list of available rooms.
        /// </summary>
        public GameObject RoomsContainer
        {
            get => _roomsContainer;
        }

        /// <summary>
        /// The screen displayed while the room list is being refreshed.
        /// </summary>
        public GameObject RefreshScreen
        {
            get => _refreshScreen;
        }

        /// <summary>
        /// The screen displayed when no rooms are available.
        /// </summary>
        public GameObject NoRoomsScreen
        {
            get => _noRoomsScreen;
        }

        /// <summary>
        /// The button used to refresh the room list.
        /// </summary>
        public Button RefreshButton
        {
            get => _refreshButton;
        }

        /// <summary>
        /// The dropdown used to filter rooms by password requirement.
        /// </summary>
        public Dropdown PasswordFilterDropdown
        {
            get => _passwordFilterDropdown;
        }

        /// <summary>
        /// Indicates whether there are any available rooms.
        /// </summary>
        public bool HasRooms
        {
            get => _roomsResult != null && (_roomsResult.Count > 0 || CurrentPage > 0);
        }

        /// <summary>
        /// The number of available rooms.
        /// </summary>
        public int RoomCount
        {
            get => _roomsResult != null ? _roomsResult.Count : 0;
        }

        /// <summary>
        /// Indicates whether the room list can be refreshed.
        /// </summary>
        public bool CanRefreshRoomList
        {
            get
            {
#if UNITY_EDITOR
                if (Application.isPlaying == false)
                {
                    return false;
                }
#endif

                return IsRefreshing == false && JUNetcodeMatchManagerBase.CanSearchRooms == true;
            }
        }

        /// <summary>
        /// Indicates whether the room list can go to the next page.
        /// </summary>
        public bool CanGoToNextPage
        {
            get
            {
                if (JUNetcodeMatchManagerBase.IsConnectedAndAuthenticated == false || _roomsResult == null)
                {
                    return false;
                }

                // The current page is full, probably indicating there are more rooms to show on the next page.
                return _roomsResult.Count >= _pagination.PageSize;
            }
        }

        /// <summary>
        /// Indicates whether the room list can go to the previous page.
        /// </summary>
        public bool CanGoToPreviousPage
        {
            get
            {
                if (JUNetcodeMatchManagerBase.IsConnectedAndAuthenticated == false || _roomsResult == null)
                {
                    return false;
                }

                // The current page is not the first page.
                return CurrentPage > 0;
            }
        }

        /// <summary>
        /// The list of available room results. Returns null if no rooms are available.
        /// </summary>
        public IEnumerable<ISessionInfo> RoomResults
        {
            get
            {
                if (_roomsResult == null)
                {
                    return null;
                }

                return _roomsResult;
            }
        }

        private void Reset()
        {
            _pagination.Reset();
        }

        private void OnValidate()
        {
            _pagination.OnValidate();
        }

        private void Awake()
        {
            _roomListButtons = new();
            Debug.Assert(_roomsContainer != null, $"{nameof(_roomsContainer)} is not assigned on {gameObject.name}.");
            Debug.Assert(_roomButtonPrefab != null, $"{nameof(_roomButtonPrefab)} is not assigned on {gameObject.name}.");
            Debug.Assert(_pagination.Container != null, $"{nameof(_pagination)}.{nameof(_pagination.Container)} is not assigned on {gameObject.name}.");
            Debug.Assert(_pagination.PageSize > 0, $"{nameof(_pagination)}.{nameof(_pagination.PageSize)} must be greater than 0 on {gameObject.name}.");
            Debug.Assert(_pagination.NextPageButton != null, $"{nameof(_pagination)}.{nameof(_pagination.NextPageButton)} is not assigned on {gameObject.name}.");
            Debug.Assert(_pagination.PreviousPageButton != null, $"{nameof(_pagination)}.{nameof(_pagination.PreviousPageButton)} is not assigned on {gameObject.name}.");

            // A template button is a scene's object when have a valid scene.
            // when is a scene's object it should be deactivated to serve as a template later.
            if (_roomButtonPrefab != null && _roomButtonPrefab.gameObject.scene.IsValid() == true)
            {
                _roomButtonPrefab.gameObject.SetActive(false);
            }

            // Setup pagination.
            if (_pagination.NextPageButton != null)
            {
                _pagination.NextPageButton.onClick.AddListener(SetNextPage);
            }
            if (_pagination.PreviousPageButton != null)
            {
                _pagination.PreviousPageButton.onClick.AddListener(SetPreviousPage);
            }

            if (_refreshButton != null)
            {
                _refreshButton.onClick.AddListener(RefreshList);
            }

            // Filters.
            if (_passwordFilterDropdown != null)
            {
                if (_passwordFilterDropdown.options.Count != 3)
                {
                    Debug.LogWarning($"{nameof(_passwordFilterDropdown)} does not have correct options.\n Expected 3 options for {nameof(JUNetcodeMatchManagerBase.PasswordFilter)}.");
                    Debug.LogWarning($"Auto assigning default options for {nameof(_passwordFilterDropdown)}.");
                    _passwordFilterDropdown.ClearOptions();
                    _passwordFilterDropdown.AddOptions(new List<string> { "All", "WithoutPassword", "WithPassword" });
                    _passwordFilterDropdown.value = 0; // Set default to "All".
                }

                if (_passwordFilterDropdown.value < 0 || _passwordFilterDropdown.value >= 3)
                {
                    _passwordFilterDropdown.value = 0; // Set default to "All" if the value is out of range.
                }

                CurrentPasswordFilter = (JUNetcodeMatchManagerBase.PasswordFilter)_passwordFilterDropdown.value;
                _passwordFilterDropdown.onValueChanged.AddListener(value =>
                {
                    CurrentPasswordFilter = (JUNetcodeMatchManagerBase.PasswordFilter)value;
                });
            }
        }

        private void OnEnable()
        {
            RefreshListOnConnect();
        }

        private void OnDisable()
        {
            ClearList();

            IsRefreshing = false;
        }

        private void Update()
        {
            if (_refreshButton != null)
            {
                _refreshButton.interactable = CanRefreshRoomList;
            }
        }

        private void RefreshListOnConnect()
        {
            IEnumerator RefreshOnConnect()
            {
                ShowRefreshScreen();
                yield return new WaitWhile(() =>
                {
                    return JUNetcodeMatchManagerBase.IsConnectedAndAuthenticated == false;
                });

                RefreshList();
            }

            if (JUNetcodeMatchManagerBase.IsConnectedAndAuthenticated == true)
            {
                RefreshList();
            }
            else
            {
                StartCoroutine(RefreshOnConnect());
            }
        }

        [ContextMenu("Refresh Room List")]
        public async void RefreshList()
        {
            if (CanRefreshRoomList == false)
            {
                return;
            }

            if (_roomsContainer == null)
            {
                Debug.LogError($"{nameof(_roomsContainer)} is not assigned on {gameObject.name}.");
                return;
            }

            ClearList();
            ShowRefreshScreen();
            if (JUNetcodeMatchManagerBase.IsConnectedAndAuthenticated == false)
            {
                ShowNoRoomsScreen();
                return;
            }

            IsRefreshing = true;
            try
            {
                if (JUNetcodeMatchManagerBase.CanSearchRooms == true)
                {
                    IEnumerable<ISessionInfo> result = await JUNetcodeMatchManagerBase.FindRooms(CurrentPage, _pagination.PageSize, CurrentPasswordFilter);
                    if (result != null)
                    {
                        _roomsResult = new List<ISessionInfo>(result);
                    }
                }

                if (CurrentPage == 0 && _roomsResult.Count == 0)
                {
                    ShowNoRoomsScreen();
                }
                else
                {
                    ShowRoomsScreen();
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"Failed to refresh room list: {exception}");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private void ClearList()
        {
            if (_roomListButtons == null)
            {
                return;
            }

            foreach (JU_UINetRoomListButton button in _roomListButtons)
            {
                if (button == null)
                {
                    continue;
                }

                Destroy(button.gameObject);
            }

            _roomListButtons.Clear();
        }

        private void ShowRefreshScreen()
        {
            if (_refreshScreen != null)
            {
                _refreshScreen.SetActive(true);
            }

            if (_noRoomsScreen != null)
            {
                _noRoomsScreen.SetActive(false);
            }

            if (_pagination.Container != null)
            {
                _pagination.Container.SetActive(false);
            }
        }

        private void ShowNoRoomsScreen()
        {
            if (_refreshScreen != null)
            {
                _refreshScreen.SetActive(false);
            }

            if (_noRoomsScreen != null)
            {
                _noRoomsScreen.SetActive(true);
            }

            if (_pagination.Container != null)
            {
                _pagination.Container.SetActive(false);
            }
        }

        private void ShowRoomsScreen()
        {
            if (_roomsContainer != null)
            {
                _roomsContainer.SetActive(true);
            }

            if (_noRoomsScreen != null)
            {
                _noRoomsScreen.SetActive(false);
            }

            if (_refreshScreen != null)
            {
                _refreshScreen.SetActive(false);
            }

            if (_pagination.Container != null)
            {
                _pagination.Container.SetActive(true);
                if (_pagination.NextPageButton != null)
                {
                    _pagination.NextPageButton.gameObject.SetActive(CanGoToNextPage);
                }
                if (_pagination.PreviousPageButton != null)
                {
                    _pagination.PreviousPageButton.gameObject.SetActive(CanGoToPreviousPage);
                }

                if (_pagination.PageIndicator != null)
                {
                    _pagination.PageIndicator.text = $"{CurrentPage + 1}";
                }
            }

            if (_roomsContainer != null && _roomsContainer.activeInHierarchy == true)
            {
                foreach (ISessionInfo s in _roomsResult)
                {
                    JU_UINetRoomListButton roomButton = Instantiate(_roomButtonPrefab, _roomsContainer.transform);
                    roomButton.gameObject.SetActive(true);
                    roomButton.Configure(s);
                    _roomListButtons.Add(roomButton);
                }
            }
        }

        /// <summary>
        /// Sets the next page of the room list if available.
        /// </summary>
        public void SetNextPage()
        {
            if (CanGoToNextPage == false)
            {
                return;
            }

            CurrentPage++;
            RefreshList();
        }

        /// <summary>
        /// Sets the previous page of the room list if available.
        /// </summary>
        public void SetPreviousPage()
        {
            if (CanGoToPreviousPage == false)
            {
                CurrentPage = 0;
                RefreshList();
                return;
            }

            CurrentPage--;
            RefreshList();
        }
    }
}