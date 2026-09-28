using JU.UI;
using UnityEngine;
using UnityEngine.UI;

namespace JU.TPS.UI
{
    /// <summary>
    /// Standard UI Damage indicator for player.
    /// </summary>
    public class JU_UIStandardDamageIndicator : UI_BaseDamageIndicator
    {
        [Space]
        [Tooltip("Base damage indicator image.")]
        [SerializeField] private Image _baseDamageIndicator;

        [Tooltip("Big damage indicator image.")]
        [SerializeField] private Image _bigDamageIndicator;

        [Space]
        [SerializeField] private Color _damageColor;
        [SerializeField] private Color _hitColor;

        public JU_UIStandardDamageIndicator() : base()
        {

        }

        /// <inheritdoc/>
        protected override void Reset()
        {
            base.Reset();

            _damageColor = new Color(1, 0, 0, 0.5f);
            _hitColor = new Color(1, 1, 1, 0.5f);
        }

        protected override void Awake()
        {
            base.Awake();

            Debug.Assert(_baseDamageIndicator != null, $"Base damage indicator is not assigned in {name}.");
            Debug.Assert(_bigDamageIndicator != null, $"Big damage indicator is not assigned in {name}.");
        }

        protected override void Update()
        {
            base.Update();

            // Control the alpha of the indicators based on the show time.
            if (IsShowing)
            {
                Color color = CurrentDamageInfo.DamageBlocked ? _hitColor : _damageColor;
                Color transparent = new Color(color.r, color.g, color.b, 0);
                _baseDamageIndicator.color = Color.Lerp(transparent, color, NormalizedFade);
                _bigDamageIndicator.color = Color.Lerp(transparent, color, NormalizedFade);
            }
        }
    }
}