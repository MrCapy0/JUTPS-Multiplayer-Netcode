using JUTPS.JUInputSystem;
using UnityEngine;
using UnityEngine.Events;

namespace JUTPS
{
	/// <summary>
	/// Stores informations about player and platform input system.
	/// </summary>
	[AddComponentMenu("JU TPS/Gameplay/Game/Game Manager")]
	public class JUGameManager : MonoBehaviour
	{
		private static JUCharacterController _playerController;

		[SerializeField] private bool _simulateMobileDevice;

		/// <summary>
		/// The player controll instance.
		/// If null, will try find and return a <see cref="JUCharacterController"/> with tag "Player".
		/// </summary>
		public static JUCharacterController PlayerController
		{
			get
			{
				if (!_playerController)
				{
					GameObject playerObject = GameObject.FindWithTag("Player");

					if (playerObject)
					{
						_playerController = playerObject.GetComponent<JUCharacterController>();
						OnPlayerChanged?.Invoke(_playerController ? _playerController.gameObject : null);
					}
				}

				return _playerController;
			}
			set
			{
				if (_playerController != value)
				{
					_playerController = value;
					OnPlayerChanged?.Invoke(value ? value.gameObject : null);
				}

				_playerController = value;
			}
		}

		/// <summary>
		/// The main instance.
		/// </summary>
		public static JUGameManager Instance { get; private set; }

		/// <summary>
		/// Return true if is using touch inputs.
		/// </summary>
		public static bool IsMobileControls { get; private set; }

		public static event UnityAction<GameObject> OnPlayerChanged;

		private void Awake()
		{
			if (Instance && Instance != this)
			{
				Destroy(this);
				return;
			}

			Instance = this;
#if UNITY_ANDROID && !UNITY_EDITOR
			_simulateMobileDevice = SystemInfo.deviceType == DeviceType.Handheld;
#endif

#if !UNITY_EDITOR
            if (SystemInfo.deviceModel.Contains("iPad"))
            {
                _simulateMobileDevice = true;
            }
            else if (SystemInfo.deviceModel.Contains("iPhone"))
            {
                _simulateMobileDevice = true;
            }
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                _simulateMobileDevice = true;
            }
#endif

			IsMobileControls = _simulateMobileDevice;
		}

		private void Update()
		{
			IsMobileControls = _simulateMobileDevice;

		}

		private void OnDestroy()
		{
			PlayerController = null;
		}
	}
}