using System.Collections.Generic;
using JUTPS;
using UnityEngine;
using UnityEngine.Events;

namespace JU.UI
{
    /// <summary>
    /// UI Damage indicator for player.
    /// Shows the direction of the damage source relative to the player camera.
    /// </summary>
    public abstract class UI_BaseDamageIndicator : MonoBehaviour
    {
        private class IndicatorManager : MonoBehaviour
        {
            private IHealth _playerHealth;

            public UI_BaseDamageIndicator Template { get; set; }

            public event UnityAction<IHealth.DamageResultInfo> OnShowIndicator;

            private void Start()
            {
                JUGameManager.OnPlayerChanged += OnPlayerChanged;

                SetupPlayer();
            }

            private void OnDestroy()
            {
                if (_playerHealth != null)
                {
                    _playerHealth.OnDamaged -= OnPlayerDamaged;
                }

                JUGameManager.OnPlayerChanged -= OnPlayerChanged;
            }

            private void OnPlayerChanged(GameObject player)
            {
                SetupPlayer();
            }

            private void OnPlayerDamaged(IHealth.DamageResultInfo info)
            {
                if (!gameObject.activeInHierarchy)
                {
                    return;
                }

                ShowIndicator(info);
            }

            private void SetupPlayer()
            {
                // Player was not changed.
                if ((_playerHealth != null && JUGameManager.PlayerController != null) ||
                    (_playerHealth == null && JUGameManager.PlayerController == null))
                {
                    if (_playerHealth is MonoBehaviour healthComponent && healthComponent.gameObject == JUGameManager.PlayerController.gameObject)
                    {
                        return;
                    }
                }

                // Unsubscribe from previous player health events.
                if (_playerHealth != null)
                {
                    _playerHealth.OnDamaged -= OnPlayerDamaged;
                    _playerHealth = null;
                }

                // Subscribe to the new player health events.
                if (JUGameManager.PlayerController)
                {
                    _playerHealth = JUGameManager.PlayerController.GetComponent<IHealth>();

                    if (_playerHealth != null)
                    {
                        _playerHealth.OnDamaged += OnPlayerDamaged;
                    }
                }
            }

            private void ShowIndicator(IHealth.DamageResultInfo info)
            {
                if (!Template)
                {
                    return;
                }

                UI_BaseDamageIndicator indicator = Instantiate(Template, Template.transform.parent);
                indicator.ShowIndicator(info);

                OnShowIndicator?.Invoke(info);
            }
        }

        private enum ShowMode
        {
            Static,
            Dynamic
        }

        private enum FadeMode
        {
            Linear,
            EaseOutCircular
        }

        private static Dictionary<Transform, IndicatorManager> _managers;

        [SerializeField] private float _showDuration;
        [SerializeField] private float _fadeDuration;
        [SerializeField] private FadeMode _fadeMode;
        [SerializeField] private ShowMode _showMode;

        public static event UnityAction<IHealth.DamageResultInfo> OnShow;

        private static Camera Camera { get; set; }
        private IndicatorManager Manager { get; set; }

        /// <summary>
        /// Is showing hit indicator.
        /// </summary>
        public bool IsShowing { get; private set; }

        /// <summary>
        /// Current show time of the indicator.
        /// </summary>
        public float CurrentShowTime { get; private set; }

        /// <summary>
        /// Damage info of the indicator.
        /// </summary>
        public IHealth.DamageResultInfo CurrentDamageInfo { get; private set; }

        /// <summary>
        /// Normalized fade value of the indicator. Ranges from 0 to 1, where 0 is fully transparent and 1 is fully opaque.
        /// </summary>
        public float NormalizedFade { get; private set; }

        /// <summary>
        /// Show duration of the indicator.
        /// </summary>
        public float ShowDuration
        {
            get => _showDuration;
            set
            {
                _showDuration = value;
                Debug.Assert(_showDuration >= 0, $"Show duration must be greater than or equal to 0. Current value: {_showDuration}");
            }
        }

        /// <summary>
        /// Fade out duration of the indicator.
        /// </summary>
        public float FadeDuration
        {
            get => _fadeDuration;
            set
            {
                _fadeDuration = value;
                Debug.Assert(_fadeDuration >= 0, $"Fade duration must be greater than or equal to 0. Current value: {_fadeDuration}");
            }
        }

        protected UI_BaseDamageIndicator()
        {

        }

        /// <summary>
        /// Called by editor when the component is reset to default values.
        /// </summary>
        protected virtual void Reset()
        {
            _showDuration = 0.5f;
            _fadeDuration = 0.5f;
            _fadeMode = FadeMode.EaseOutCircular;
            _showMode = ShowMode.Dynamic;
        }

        protected virtual void Awake()
        {
            CreateManagerIfNull();

            if (!IsShowing)
            {
                gameObject.SetActive(false);
            }

            NormalizedFade = 1f;
        }

        protected virtual void OnDestroy()
        {
        }

        protected virtual void Update()
        {
            float fadeTime = Mathf.Max(CurrentShowTime - ShowDuration, 0);
            NormalizedFade = 1f - Mathf.Clamp01(fadeTime / FadeDuration);

            switch (_fadeMode)
            {
                case FadeMode.Linear:
                    break;
                case FadeMode.EaseOutCircular:
                    NormalizedFade = 1 - Mathf.Sqrt(1 - Mathf.Pow(NormalizedFade, 2));
                    break;
                default:
                    throw new System.NotImplementedException($"Fade mode {_fadeMode} is not implemented.");
            }

            if (IsShowing)
            {
                CurrentShowTime += Time.deltaTime;
                if (CurrentShowTime >= _showDuration + _fadeDuration)
                {
                    IsShowing = false;
                }

                if (_showMode == ShowMode.Dynamic)
                {
                    UpdateRotation();
                }
            }

            if (!IsShowing)
            {
                Destroy(gameObject);
            }
        }

        private void UpdateRotation()
        {
            if (Camera == null || !Camera.isActiveAndEnabled)
            {
                Camera = Camera.main;
            }

            Debug.Assert(Camera != null, "Camera.main is null. Please ensure there is a main camera in the scene.");

            Vector3 fromPosition = CurrentDamageInfo.HitOriginPosition;
            if (fromPosition == Vector3.zero && CurrentDamageInfo.DamageOwner != null)
            {
                if (CurrentDamageInfo.DamageOwner)
                {
                    if (CurrentDamageInfo.DamageOwner.TryGetComponent(out Collider collider))
                    {
                        fromPosition = collider.bounds.center;
                    }
                    else
                    {
                        fromPosition = CurrentDamageInfo.DamageOwner.transform.position;
                    }
                }

                // There is no a source.
                else
                {
                    IsShowing = false;
                }
            }

            Vector3 hitDirection = Camera.transform.InverseTransformPoint(fromPosition);
            float angle = Mathf.Atan2(hitDirection.x, hitDirection.z) * Mathf.Rad2Deg;
            transform.localEulerAngles = new Vector3(0, 0, -angle);
        }

        private void CreateManagerIfNull()
        {
            // Ensure dictionary exists.
            _managers ??= new Dictionary<Transform, IndicatorManager>();

            // Use the parent transform as dictionary key. If parent is null (root), use this transform as fallback.
            Transform parent = transform.parent ?? transform;

            // If an entry already exists, validate it.
            if (_managers.TryGetValue(parent, out IndicatorManager existingManager))
            {
                // If the existing manager reference was destroyed (null), remove the stale entry so we can recreate.
                if (existingManager == null)
                {
                    _managers.Remove(parent);
                }
                else
                {
                    // Reuse existing manager.
                    Manager = existingManager;
                    return;
                }
            }

            // Create a new manager attached to the parent GameObject.
            IndicatorManager manager = parent.gameObject.AddComponent<IndicatorManager>();
            manager.Template = this;
            manager.OnShowIndicator += (info) =>
            {
                OnShow?.Invoke(info);
            };

            _managers.Add(parent, manager);
            Manager = manager;

            Debug.Log($"[UI_BaseDamageIndicator] Created IndicatorManager for parent '{parent.name}' (instance id {parent.GetInstanceID()}).");
        }

        /// <summary>
        /// Check if the indicator can be shown for the given damage info.
        /// </summary>
        /// <param name="info">Damage info.</param>
        /// <returns></returns>
        public virtual bool CanShowIndicator(IHealth.DamageResultInfo info)
        {
            // Check if the player is dead.
            if (info.DamagedObject.IsDead)
            {
                return false;
            }

            return info.DamageOwner != null || info.HitOriginPosition != Vector3.zero;
        }

        /// <summary>
        /// Show the indicator for the given damage info.
        /// </summary>
        /// <param name="info"></param>
        public virtual void ShowIndicator(IHealth.DamageResultInfo info)
        {
            if (!CanShowIndicator(info))
            {
                Destroy(gameObject);
                return;
            }

            IsShowing = true;
            CurrentShowTime = 0;
            CurrentDamageInfo = info;
            gameObject.SetActive(true);
            UpdateRotation();
        }
    }
}