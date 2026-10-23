using JUTPS.VehicleSystem;
using Unity.Netcode;
using UnityEngine;

namespace JU.TPS.Netcode
{
    /// <summary>
    /// Synchronizes the inputs and state of a <see cref="JUWheeledVehicle"/> from its owner to the other clients.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(JUWheeledVehicle))]
    public class JUNetcodeVehicle : NetworkBehaviour
    {
        private JUWheeledVehicle _vehicle;

        private readonly NetworkVariable<float> _netVerticalInput;
        private readonly NetworkVariable<float> _netHorizontalInput;
        private readonly NetworkVariable<float> _netBrakeInput;
        private readonly NetworkVariable<bool> _netIsOn;

        /// <summary>
        /// The <see cref="JUWheeledVehicle"/> component attached to this GameObject.
        /// </summary>
        public JUWheeledVehicle Vehicle
        {
            get => _vehicle;
        }

        /// <summary>
        /// Creates the synchronized variables.
        /// </summary>
        public JUNetcodeVehicle()
        {
            _netVerticalInput = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
            _netHorizontalInput = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
            _netBrakeInput = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
            _netIsOn = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        }

        private void Start()
        {
            _vehicle = GetComponent<JUWheeledVehicle>();
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

        /// <summary>
        /// Set the client that owns this vehicle.
        /// </summary>
        /// <param name="ownerClientId">Id of the new owner client.</param>
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
    }
}
