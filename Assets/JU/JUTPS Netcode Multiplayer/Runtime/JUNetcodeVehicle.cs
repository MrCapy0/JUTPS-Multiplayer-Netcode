using JUTPS.VehicleSystem;
using Unity.Netcode;
using UnityEngine;

namespace JU.TPS.Netcode
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(JUWheeledVehicle))]
    public class JUNetcodeVehicle : NetworkBehaviour
    {
        private JUWheeledVehicle _vehicle;

        private NetworkVariable<float> _netVerticalInput = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float> _netHorizontalInput = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float> _netBrakeInput = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<bool> _netIsOn = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>
        /// The <see cref="JUWheeledVehicle"/> component attached to this GameObject.
        /// </summary>
        public JUWheeledVehicle Vehicle
        {
            get
            {
                if (_vehicle == null)
                {
                    _vehicle = GetComponent<JUWheeledVehicle>();
                }

                return _vehicle;
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
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

            Vehicle.UsePlayerInputs = true;
            _netVerticalInput.Value = Vehicle.Vertical;
            _netHorizontalInput.Value = Vehicle.Horizontal;
            _netBrakeInput.Value = Vehicle.Brake;
            _netIsOn.Value = Vehicle.IsOn;
        }

        private void UpdateIfNotOwner()
        {
            if (IsOwner == true)
            {
                return;
            }

            Vehicle.UsePlayerInputs = false;
            Vehicle.Vertical = _netVerticalInput.Value;
            Vehicle.Horizontal = _netHorizontalInput.Value;
            Vehicle.Brake = _netBrakeInput.Value;
            Vehicle.IsOn = _netIsOn.Value;
        }

        /// <summary>
        /// Set the client that owns this vehicle.
        /// </summary>
        /// <param name="ownerClientId"></param>
        public void SetOwnership(ulong ownerClientId)
        {
            SetOwnershipRpc(ownerClientId);
        }

        [Rpc(SendTo.Authority)]
        private void SetOwnershipRpc(ulong ownerClientId)
        {
            if (HasAuthority == false)
            {
                return;
            }

            NetworkObject.ChangeOwnership(ownerClientId);
        }
    }
}