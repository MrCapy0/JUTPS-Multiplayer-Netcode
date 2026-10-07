using Unity.Netcode;
using UnityEngine;

namespace JU.TPS.Netcode
{
    /// <summary>
    /// Netcode synchronization implementation for <see cref="IHealth"/> component.
    /// </summary>
    [RequireComponent(typeof(IHealth))]
    [RequireComponent(typeof(NetworkObject))]
    public class JUNetcodeHealth : NetworkBehaviour
    {
        private IHealth _health;
        private IHealth.DamageResultInfo _lastDamageResultInfo;

        public IHealth Health
        {
            get
            {
                if (_health == null)
                {
                    _health = GetComponent<IHealth>();
                }

                return _health;
            }
        }

        private NetworkVariable<float> _netHealth = new NetworkVariable<float>(-1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float> _netMaxHealth = new NetworkVariable<float>(-1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float> _netProtection = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<bool> _netIsInvincible = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                UpdateIfOwner();
                return;
            }

            _netHealth.OnValueChanged += UpdateHealth;
            _netMaxHealth.OnValueChanged += UpdateMaxHealth;
            _netProtection.OnValueChanged += UpdateProtection;
            _netIsInvincible.OnValueChanged += UpdateIsInvincible;

            ApplyHealthState();
            UpdateProtection(_netProtection.Value, _netProtection.Value);
            UpdateIsInvincible(_netIsInvincible.Value, _netIsInvincible.Value);

            _health.SubscribeOnBeforeDamaged(OnBeforeDamage);
            _health.OnDamaged += OnDamaged;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            _netHealth.OnValueChanged -= UpdateHealth;
            _netMaxHealth.OnValueChanged -= UpdateMaxHealth;
            _netProtection.OnValueChanged -= UpdateProtection;
            _netIsInvincible.OnValueChanged -= UpdateIsInvincible;
        }

        private void Update()
        {
            UpdateIfOwner();
        }

        private void UpdateIfOwner()
        {
            if (IsOwner == false || IsSpawned == false)
            {
                return;
            }

            _netHealth.Value = Health.Health;
            _netMaxHealth.Value = Health.MaxHealth;
            _netProtection.Value = Health.Protection;
            _netIsInvincible.Value = Health.IsInvencible;
        }

        private void UpdateHealth(float previous, float current)
        {
            ApplyHealthState();
        }

        private void UpdateMaxHealth(float previous, float current)
        {
            ApplyHealthState();
        }

        private void ApplyHealthState()
        {
            if (_netMaxHealth.Value <= 0f || _netHealth.Value < 0f)
            {
                return;
            }

            if (Health.MaxHealth != _netMaxHealth.Value)
            {
                Health.SetMaxHealth(_netMaxHealth.Value);
            }

            if (Health.IsDead == true && _netHealth.Value > 0f)
            {
                Health.ResetHealth();
                Health.SetHealth(_netHealth.Value);
            }

            if (Health.IsDead == false && Health.Health != _netHealth.Value)
            {
                Health.SetHealth(_netHealth.Value);
            }
        }

        private void UpdateProtection(float previous, float current)
        {
            Health.Protection = current;
        }

        private void UpdateIsInvincible(bool previous, bool current)
        {
            Health.IsInvencible = current;
        }

        private IHealth.DamageInfo OnBeforeDamage(IHealth.DamageInfo damageInfo)
        {
            if (IsOwner == false || IsSpawned == false)
            {
                damageInfo.Damage = 0;
                return damageInfo;
            }

            return damageInfo;
        }

        private void OnDamaged(IHealth.DamageResultInfo info)
        {
            if (IsOwner == true)
            {
                return;
            }

            _lastDamageResultInfo = info;
            AddDamageRpc();
        }

        [Rpc(SendTo.NotMe)]
        private void AddDamageRpc()
        {
            Health.DoDamage(new IHealth.DamageInfo()
            {
                Damage = _lastDamageResultInfo.SuggestiveDamage,
                DamageOwner = _lastDamageResultInfo.DamageOwner,
                HitDirection = _lastDamageResultInfo.HitDirection,
                HitOriginPosition = _lastDamageResultInfo.HitOriginPosition,
                HitPosition = _lastDamageResultInfo.HitPosition
            });
        }
    }
}