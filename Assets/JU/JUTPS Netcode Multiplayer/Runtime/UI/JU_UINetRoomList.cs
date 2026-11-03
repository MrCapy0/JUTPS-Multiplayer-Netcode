using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace JU.TPS.Netcode.UI
{
    public class JU_UINetRoomList : MonoBehaviour
    {
        [System.Serializable]
        public struct PaginationSettings
        {
            [SerializeField] private GameObject _container;
            [SerializeField] private Text _pageIndicator;
            [SerializeField] private Button _nextPageButton;
            [SerializeField] private Button _previousPageButton;

            [Space]
            [SerializeField] private int _pageSize;

            public GameObject Container
            {
                get => _container;
            }

            public Text PageIndicator
            {
                get => _pageIndicator;
            }

            public Button NextPageButton
            {
                get => _nextPageButton;
            }

            public Button PreviousPageButton
            {
                get => _previousPageButton;
            }

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

        private float _lastRefreshTime;

        private QuerySessionsResults _roomsResult;
        private List<JU_UINetRoomListButton> _roomListButtons;

        [SerializeField] private GameObject _roomsContainer;
        [SerializeField] private GameObject _refreshScreen;
        [SerializeField] private GameObject _noRoomsScreen;
        [SerializeField] private Button _refreshButton;

        [Space]
        [SerializeField] private JU_UINetRoomListButton _roomButtonPrefab;
        [SerializeField] private PaginationSettings _pagination;

        public bool IsRefreshing { get; private set; }

        public int CurrentPage { get; private set; }

        public GameObject RoomsContainer
        {
            get => _roomsContainer;
        }

        public GameObject RefreshScreen
        {
            get => _refreshScreen;
        }

        public GameObject NoRoomsScreen
        {
            get => _noRoomsScreen;
        }

        public Button RefreshButton
        {
            get => _refreshButton;
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

        public bool HasRooms
        {
            get => _roomsResult != null && _roomsResult.Sessions.Count > 0;
        }

        public int RoomCount
        {
            get => _roomsResult != null ? _roomsResult.Sessions.Count : 0;
        }

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

                return IsConnected && IsRefreshing == false && (Time.unscaledTime - _lastRefreshTime >= 1f);
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
                    return IsConnected == false;
                });

                RefreshList();
            }

            if (IsConnected == true)
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
            if (IsConnected == false)
            {
                ShowNoRoomsScreen();
                return;
            }

            IsRefreshing = true;
            try
            {
                _lastRefreshTime = Time.unscaledTime;
                _roomsResult = await MultiplayerService.Instance.QuerySessionsAsync(new()
                {
                    Skip = CurrentPage * _pagination.PageSize,
                    Count = _pagination.PageSize
                });
                if (_roomsResult.Sessions.Count == 0)
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

            bool isLastPage = CurrentPage >= (_roomsResult.Sessions.Count - 1);
            int RoomCount = _roomsResult.Sessions.Count;
            if (_pagination.Container != null)
            {
                _pagination.Container.SetActive(true);
                if (_pagination.NextPageButton != null)
                {
                    _pagination.NextPageButton.gameObject.SetActive(isLastPage == false);
                }
                if (_pagination.PreviousPageButton != null)
                {
                    _pagination.PreviousPageButton.gameObject.SetActive(CurrentPage > 0 && RoomCount >= _pagination.PageSize);
                }

                if (_pagination.PageIndicator != null)
                {
                    if (RoomCount >= _pagination.PageSize)
                    {
                        _pagination.PageIndicator.gameObject.SetActive(true);
                        _pagination.PageIndicator.text = $"Page {CurrentPage + 1} of {_roomsResult.Sessions.Count}";
                    }
                    else
                    {
                        _pagination.PageIndicator.gameObject.SetActive(false);
                    }
                }
            }

            if (_roomsContainer != null && _roomsContainer.activeInHierarchy == true && HasRooms)
            {
                foreach (ISessionInfo s in _roomsResult.Sessions)
                {
                    JU_UINetRoomListButton roomButton = Instantiate(_roomButtonPrefab, _roomsContainer.transform);
                    roomButton.gameObject.SetActive(true);
                    roomButton.Configure(s);
                    _roomListButtons.Add(roomButton);
                }
            }
        }

        public void SetNextPage()
        {
            if (HasRooms == false || CurrentPage >= _roomsResult.Sessions.Count)
            {
                return;
            }

            CurrentPage++;
            RefreshList();
        }

        public void SetPreviousPage()
        {
            if (HasRooms == false || CurrentPage == 0)
            {
                CurrentPage = 0;
                return;
            }

            CurrentPage--;
            RefreshList();
        }
    }
}