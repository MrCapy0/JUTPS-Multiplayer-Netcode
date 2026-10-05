using JUTPS.WeaponSystem;
using Unity.Netcode;
using UnityEngine;

namespace JU.TPS.Netcode
{
    /// <summary>
    /// Netcode multiplayer synchronization for <see cref="Weapon"/>.
    /// </summary>
    public class JUNetcodeWeapon : JUNetcodeItem<Weapon>
    {
        private NetworkVariable<int> _netTotalBullets = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<int> _netBulletsPerMagazine = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<int> _netBulletsAmounts = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<int> _netNumberOfShotgunBulletsPerShot = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<bool> _netInfiniteAmmo = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<float> _fireRate = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float> _precision = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float> _netLossOfAccuracyPerShot = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<float> _netRecoilForce = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float> _netRecoilForceRotation = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<float> _netSliderMovementOffset = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float> _netSliderMovementSpeed = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float> _netWeaponPositionSpeed = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float> _netWeaponRotationSpeed = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                Item.OnShot.AddListener(ShotRpc);
                return;
            }

            _netTotalBullets.OnValueChanged += UpdateTotalBullets;
            _netBulletsPerMagazine.OnValueChanged += UpdateBulletsPerMagazine;
            _netBulletsAmounts.OnValueChanged += UpdateBulletsAmounts;
            _netNumberOfShotgunBulletsPerShot.OnValueChanged += UpdateNumberOfShotgunBulletsPerShot;
            _netInfiniteAmmo.OnValueChanged += UpdateInfiniteAmmo;
            _fireRate.OnValueChanged += UpdateFireRate;
            _precision.OnValueChanged += UpdatePrecision;
            _netLossOfAccuracyPerShot.OnValueChanged += UpdateLossOfAccuracyPerShot;
            _netRecoilForce.OnValueChanged += UpdateRecoilForce;
            _netRecoilForceRotation.OnValueChanged += UpdateRecoilForceRotation;
            _netSliderMovementOffset.OnValueChanged += UpdateSliderMovementOffset;
            _netSliderMovementSpeed.OnValueChanged += UpdateSliderMovementSpeed;
            _netWeaponPositionSpeed.OnValueChanged += UpdateWeaponPositionSpeed;
            _netWeaponRotationSpeed.OnValueChanged += UpdateWeaponRotationSpeed;

            UpdateTotalBullets(_netTotalBullets.Value, _netTotalBullets.Value);
            UpdateBulletsPerMagazine(_netBulletsPerMagazine.Value, _netBulletsPerMagazine.Value);
            UpdateBulletsAmounts(_netBulletsAmounts.Value, _netBulletsAmounts.Value);
            UpdateNumberOfShotgunBulletsPerShot(_netNumberOfShotgunBulletsPerShot.Value, _netNumberOfShotgunBulletsPerShot.Value);
            UpdateInfiniteAmmo(_netInfiniteAmmo.Value, _netInfiniteAmmo.Value);
            UpdateFireRate(_fireRate.Value, _fireRate.Value);
            UpdatePrecision(_precision.Value, _precision.Value);
            UpdateLossOfAccuracyPerShot(_netLossOfAccuracyPerShot.Value, _netLossOfAccuracyPerShot.Value);
            UpdateRecoilForce(_netRecoilForce.Value, _netRecoilForce.Value);
            UpdateRecoilForceRotation(_netRecoilForceRotation.Value, _netRecoilForceRotation.Value);
            UpdateSliderMovementOffset(_netSliderMovementOffset.Value, _netSliderMovementOffset.Value);
            UpdateSliderMovementSpeed(_netSliderMovementSpeed.Value, _netSliderMovementSpeed.Value);
            UpdateWeaponPositionSpeed(_netWeaponPositionSpeed.Value, _netWeaponPositionSpeed.Value);
            UpdateWeaponRotationSpeed(_netWeaponRotationSpeed.Value, _netWeaponRotationSpeed.Value);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            _netTotalBullets.OnValueChanged -= UpdateTotalBullets;
            _netBulletsPerMagazine.OnValueChanged -= UpdateBulletsPerMagazine;
            _netBulletsAmounts.OnValueChanged -= UpdateBulletsAmounts;
            _netNumberOfShotgunBulletsPerShot.OnValueChanged -= UpdateNumberOfShotgunBulletsPerShot;
            _netInfiniteAmmo.OnValueChanged -= UpdateInfiniteAmmo;
            _fireRate.OnValueChanged -= UpdateFireRate;
            _precision.OnValueChanged -= UpdatePrecision;
            _netLossOfAccuracyPerShot.OnValueChanged -= UpdateLossOfAccuracyPerShot;
            _netRecoilForce.OnValueChanged -= UpdateRecoilForce;
            _netRecoilForceRotation.OnValueChanged -= UpdateRecoilForceRotation;
            _netSliderMovementOffset.OnValueChanged -= UpdateSliderMovementOffset;
            _netSliderMovementSpeed.OnValueChanged -= UpdateSliderMovementSpeed;
            _netWeaponPositionSpeed.OnValueChanged -= UpdateWeaponPositionSpeed;
            _netWeaponRotationSpeed.OnValueChanged -= UpdateWeaponRotationSpeed;
        }

        protected override void UpdateIfOwner()
        {
            base.UpdateIfOwner();

            if (IsOwner == false)
            {
                return;
            }

            _netTotalBullets.Value = Item.TotalBullets;
            _netBulletsPerMagazine.Value = Item.BulletsPerMagazine;
            _netBulletsAmounts.Value = Item.BulletsAmounts;
            _netNumberOfShotgunBulletsPerShot.Value = Item.NumberOfShotgunBulletsPerShot;
            _netInfiniteAmmo.Value = Item.InfiniteAmmo;

            _fireRate.Value = Item.Fire_Rate;
            _precision.Value = Item.Precision;
            _netLossOfAccuracyPerShot.Value = Item.LossOfAccuracyPerShot;

            _netRecoilForce.Value = Item.RecoilForce;
            _netRecoilForceRotation.Value = Item.RecoilForceRotation;

            _netSliderMovementOffset.Value = Item.SliderMovementOffset;
            _netSliderMovementSpeed.Value = Item.SliderMovementSpeed;
            _netWeaponPositionSpeed.Value = Item.WeaponPositionSpeed;
            _netWeaponRotationSpeed.Value = Item.WeaponRotationSpeed;
        }

        private void UpdateTotalBullets(int previous, int current)
        {
            Item.TotalBullets = current;
        }

        private void UpdateBulletsPerMagazine(int previous, int current)
        {
            Item.BulletsPerMagazine = current;
        }

        private void UpdateBulletsAmounts(int previous, int current)
        {
            Item.BulletsAmounts = current;
        }

        private void UpdateNumberOfShotgunBulletsPerShot(int previous, int current)
        {
            Item.NumberOfShotgunBulletsPerShot = current;
        }

        private void UpdateInfiniteAmmo(bool previous, bool current)
        {
            Item.InfiniteAmmo = current;
        }

        private void UpdateFireRate(float previous, float current)
        {
            Item.Fire_Rate = current;
        }

        private void UpdatePrecision(float previous, float current)
        {
            Item.Precision = current;
        }

        private void UpdateLossOfAccuracyPerShot(float previous, float current)
        {
            Item.LossOfAccuracyPerShot = current;
        }

        private void UpdateRecoilForce(float previous, float current)
        {
            Item.RecoilForce = current;
        }

        private void UpdateRecoilForceRotation(float previous, float current)
        {
            Item.RecoilForceRotation = current;
        }

        private void UpdateSliderMovementOffset(float previous, float current)
        {
            Item.SliderMovementOffset = current;
        }

        private void UpdateSliderMovementSpeed(float previous, float current)
        {
            Item.SliderMovementSpeed = current;
        }

        private void UpdateWeaponPositionSpeed(float previous, float current)
        {
            Item.WeaponPositionSpeed = current;
        }

        private void UpdateWeaponRotationSpeed(float previous, float current)
        {
            Item.WeaponRotationSpeed = current;
        }

        [Rpc(SendTo.NotMe)]
        private void ShotRpc()
        {
            Item.Shot();
        }
    }
}