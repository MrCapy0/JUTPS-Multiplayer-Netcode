using Unity.Netcode;
using UnityEngine;

namespace JU.TPS.Netcode
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(IHealth))]
    [RequireComponent(typeof(NetworkObject))]
    public class JUNetcodeHealth : NetworkBehaviour
    {
        private struct HitInfo : INetworkSerializable
        {
            public float Damage;
            public Vector3 HitPosition;
            public Vector3 HitDirection;
            public Vector3 HitOriginPosition;
            public NetworkObjectReference DamageOwner;
            public bool WasKilled;

            public HitInfo(IHealth.DamageInfo damageInfo)
            {
                Damage = damageInfo.Damage;
                HitPosition = damageInfo.HitPosition;
                HitDirection = damageInfo.HitDirection;
                HitOriginPosition = damageInfo.HitOriginPosition;
                DamageOwner = damageInfo.DamageOwner != null ? damageInfo.DamageOwner.GetComponent<NetworkObject>() : default;
                WasKilled = false;
            }

            public HitInfo(IHealth.DamageResultInfo other)
            {
                Damage = other.RealDamage;
                HitPosition = other.HitPosition;
                HitDirection = other.HitDirection;
                HitOriginPosition = other.HitOriginPosition;
                DamageOwner = other.DamageOwner;
                WasKilled = other.WasKilled;
            }

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Damage);
                serializer.SerializeValue(ref HitPosition);
                serializer.SerializeValue(ref HitDirection);
                serializer.SerializeValue(ref HitOriginPosition);
                serializer.SerializeValue(ref DamageOwner);
                serializer.SerializeValue(ref WasKilled);
            }

            public IHealth.DamageInfo ToDamageInfo()
            {
                return new IHealth.DamageInfo
                {
                    Damage = Damage,
                    HitPosition = HitPosition,
                    HitDirection = HitDirection,
                    HitOriginPosition = HitOriginPosition,
                    DamageOwner = DamageOwner.TryGet(out var obj) ? obj.gameObject : null,
                };
            }
        }

        private struct AddHealthInfo : INetworkSerializable
        {
            public float HealthAdded;
            public NetworkObjectReference Source;

            public AddHealthInfo(IHealth.AddedHealthInfo info)
            {
                HealthAdded = info.HealthAdded;
                Source = default;

                if (info.Source != null)
                {
                    if (info.Source is GameObject go)
                    {
                        Source = go.GetComponent<NetworkObject>();
                    }
                    else if (info.Source is Component comp)
                    {
                        Source = comp.GetComponent<NetworkObject>();
                    }
                    else
                    {
                        Debug.LogError($"Unsupported health source type on Netcode: {info.Source.GetType()}");
                    }
                }
            }

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref HealthAdded);
                serializer.SerializeValue(ref Source);
            }

            public IHealth.AddedHealthInfo ToAddedHealthInfo()
            {
                return new IHealth.AddedHealthInfo
                {
                    HealthAdded = HealthAdded,
                    Source = Source.TryGet(out NetworkObject obj) ? obj.gameObject : null,
                };
            }
        }

        private IHealth _health;

        private bool _isSyncing;
        private bool _healthReceived;
        private bool _maxHealthReceived;

        private NetworkVariable<bool> _netIsDead = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float> _netHealth = new(-1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float> _netMaxHealth = new(-1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>
        /// Gets the <see cref="IHealth"/> component attached to this GameObject.
        /// </summary>
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

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner == true)
            {
                _netHealth.Value = Health.Health;
                _netMaxHealth.Value = Health.MaxHealth;
                _netIsDead.Value = Health.IsDead;
            }
            else
            {
                _netHealth.OnValueChanged += OnNetHealthChanged;
                _netMaxHealth.OnValueChanged += OnNetMaxHealthChanged;
                _netIsDead.OnValueChanged += OnNetIsDeadChanged;

                Health.SubscribeOnBeforeDamaged(OnBeforeDamageClient);

                if (_netIsDead.Value == true)
                {
                    Health.Kill();
                }
            }

            _healthReceived = _netHealth.Value >= 0f;
            _maxHealthReceived = _netMaxHealth.Value > 0f;

            Health.OnDamaged += OnDamaged;
            Health.OnHealthAdded += OnHealthAdded;
            Health.OnResetHealth += OnResetHealth;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (IsOwner == false)
            {
                _netHealth.OnValueChanged -= OnNetHealthChanged;
                _netMaxHealth.OnValueChanged -= OnNetMaxHealthChanged;
                _netIsDead.OnValueChanged -= OnNetIsDeadChanged;

                Health.UnsubscribeOnBeforeDamaged(OnBeforeDamageClient);
            }

            _healthReceived = false;
            _maxHealthReceived = false;

            Health.OnDamaged -= OnDamaged;
            Health.OnHealthAdded -= OnHealthAdded;
            Health.OnResetHealth -= OnResetHealth;
        }

        private void Update()
        {
            if (IsSpawned == false)
            {
                return;
            }

            UpdateIfOwner();
            UpdateIfNotOwner();
        }

        private void UpdateIfOwner()
        {
            if (IsOwner == false)
            {
                return;
            }

            _netHealth.Value = Health.Health;
            _netMaxHealth.Value = Health.MaxHealth;
            _netIsDead.Value = Health.IsDead;
        }

        private void UpdateIfNotOwner()
        {
            if (IsOwner == true || _healthReceived == false || _maxHealthReceived == false)
            {
                return;
            }

            _isSyncing = true;

            Health.Protection = 0;
            Health.IsInvencible = false;
            Health.SpamHitTime = 0f;

            if (Health.IsDead == false && _netHealth.Value >= 0f)
            {
                Health.SetHealth(_netHealth.Value);
                Health.SetMaxHealth(_netMaxHealth.Value);
            }

            _isSyncing = false;
        }

        private void OnDamaged(IHealth.DamageResultInfo damageInfo)
        {
            if (IsOwner == true)
            {
                ApplyDamageOnClientsRpc(new HitInfo(damageInfo));
                return;
            }

            if (_isSyncing == true)
            {
                return;
            }

            HitInfo request = new HitInfo(damageInfo);
            request.Damage = damageInfo.SuggestiveDamage;
            RequestDamageToOwnerRpc(request);
        }

        private void OnResetHealth()
        {
            if (IsOwner == false)
            {
                ResetHealthOwnerRpc();
                return;
            }

            OnResetHealthClientsRpc();
            _netHealth.Value = Health.Health;
            _netMaxHealth.Value = Health.MaxHealth;
        }

        [Rpc(SendTo.Owner)]
        private void ResetHealthOwnerRpc()
        {
            Health.ResetHealth();
        }

        [Rpc(SendTo.NotMe)]
        private void OnResetHealthClientsRpc()
        {
            if (IsOwner == true)
            {
                return;
            }

            Health.ResetHealth();
        }

        [Rpc(SendTo.Owner)]
        private void RequestDamageToOwnerRpc(HitInfo damageInfo)
        {
            if (IsOwner == false)
            {
                return;
            }

            Health.DoDamage(damageInfo.ToDamageInfo());
        }

        private void OnHealthAdded(IHealth.AddedHealthInfo healthAddedInfo)
        {
            if (IsOwner == true)
            {
                return;
            }

            OnOwnerHealthAddedRpc(new AddHealthInfo(healthAddedInfo));
        }

        [Rpc(SendTo.NotMe)]
        private void ApplyDamageOnClientsRpc(HitInfo damageInfo)
        {
            if (IsOwner == true)
            {
                return;
            }

            IHealth.DamageInfo info = damageInfo.ToDamageInfo();

            // Only the owner can kill the character.
            // Prevent non-owners dying by non-synchronized damage.
            if (damageInfo.WasKilled == false)
            {
                info.Damage = Mathf.Min(info.Damage, Health.Health - 0.1f);
            }
            else
            {
                info.Damage = Mathf.Max(info.Damage, Health.Health + 0.1f);
            }

            _isSyncing = true;
            Health.DoDamage(info);
            if (Health.IsDead == false)
            {
                Health.SetHealth(_netHealth.Value);
            }
            _isSyncing = false;
        }

        [Rpc(SendTo.Everyone)]
        private void OnOwnerHealthAddedRpc(AddHealthInfo healthAddedInfo)
        {
            if (IsOwner == true)
            {
                return;
            }

            _isSyncing = true;
            Health.AddHealth(healthAddedInfo.ToAddedHealthInfo());
            Health.SetHealth(_netHealth.Value);
            _isSyncing = false;
        }

        private IHealth.DamageInfo OnBeforeDamageClient(IHealth.DamageInfo damageInfo)
        {
            if (_isSyncing == true)
            {
                return damageInfo;
            }

            damageInfo.Damage = 0;
            return damageInfo;
        }

        private void OnNetHealthChanged(float previous, float current)
        {
            _healthReceived |= current >= 0f;
        }

        private void OnNetMaxHealthChanged(float previous, float current)
        {
            _maxHealthReceived |= current > 0f;
        }

        private void OnNetIsDeadChanged(bool previous, bool current)
        {
            if (current == true)
            {
                Health.Kill();
            }
        }
    }
}