using JUTPS;
using JUTPS.ActionScripts;
using JUTPS.CameraSystems;
using JUTPS.InteractionSystem;
using JUTPS.PhysicsScripts;
using JUTPS.VehicleSystem;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace JU.TPS.Netcode
{
    /// <summary>
    /// Netcode multiplayer synchronization for <see cref="JUCharacterController"/>.
    /// </summary>
    [RequireComponent(typeof(JUCharacterController))]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(NetworkRigidbody))]
    public class JUNetcodeCharacterController : NetworkBehaviour
    {
        [System.Flags]
        private enum Flags : ushort
        {
            // States.
            IsRunning = 1 << 0,
            IsSprinting = 1 << 1,
            IsCrouching = 1 << 2,
            IsProne = 1 << 3,
            CanMove = 1 << 4,
            CanRotate = 1 << 5,
            DisableAllMove = 1 << 6,

            // Inputs.
            ShotInputPressed = 1 << 10,
            ReloadTriggered = 1 << 11,
            AimInputPressed = 1 << 12,
            AimInputTriggered = 1 << 13,
        }

        [System.Flags]
        private enum TriggeredInputFlags : byte
        {
            MeleeAttackTriggered = 1 << 0,
            PunchAttackTriggered = 1 << 1,
            RollTriggered = 1 << 2,
            ReloadTriggered = 1 << 3
        }

        private ulong _vehicleOriginalOwnerId;
        private float _enableWeaponSwitchingTime;

        private JUCharacterController _tps;
        private DriveVehicles _driveAbility;
        private NetworkTransform _networkTransform;
        private NetworkRigidbody _networkRigidbody;
        private NetworkTransform _hipsNetworkTransform;
        private NetworkRigidbody _hipsNetworkRigidbody;

        private NetworkVariable<Vector2> _netMoveAxis;
        private NetworkVariable<Vector3> _netLookPosition;
        private NetworkVariable<Flags> _netStateFlags;
        private NetworkVariable<byte> _netRightHandItem;
        private NetworkVariable<AdvancedRagdollController.RagdollState> _netRagdollState;
        private NetworkVariable<NetworkObjectReference> _netCurrentVehicle;
        private NetworkVariable<bool> _netIsDriving;

        private Flags _clientLastFlags;
        private TriggeredInputFlags _clientLastTriggeredInputFlags;

        /// <summary>User interface prefab instantiated for the owning player.</summary>
        [Tooltip("User interface prefab instantiated for the owning player.")]
        public GameObject UserInterfacePrefab;

        /// <summary>Camera controller prefab instantiated for the owning player.</summary>
        [Tooltip("Camera controller prefab instantiated for the owning player.")]
        public TPSCameraController CameraControllerPrefab;

        /// <summary>
        /// Gets the <see cref="JUCharacterController"/> component attached to this GameObject.
        /// </summary>
        public JUCharacterController CharacterController
        {
            get
            {
                if (_tps == null)
                {
                    _tps = GetComponent<JUCharacterController>();
                }

                return _tps;
            }
        }

        /// <summary>
        /// Gets the <see cref="NetworkTransform"/> component attached to this GameObject.
        /// </summary>
        public NetworkTransform NetworkTransform
        {
            get
            {
                if (_networkTransform == null)
                {
                    _networkTransform = GetComponent<NetworkTransform>();
                }

                return _networkTransform;
            }
        }

        /// <summary>
        /// Gets the <see cref="NetworkRigidbody"/> component attached to this GameObject.
        /// </summary>
        public NetworkRigidbody NetworkRigidbody
        {
            get
            {
                if (_networkRigidbody == null)
                {
                    _networkRigidbody = GetComponent<NetworkRigidbody>();
                }

                return _networkRigidbody;
            }
        }

        /// <summary>
        /// Gets the <see cref="NetworkTransform"/> component attached to the character's hips.
        /// </summary>
        public NetworkTransform HipsNetworkTransform
        {
            get
            {
                if (_hipsNetworkTransform == null)
                {
                    Transform characterHips = CharacterController.anim.GetBoneTransform(HumanBodyBones.Hips);
                    if (characterHips != null)
                    {
                        _hipsNetworkTransform = characterHips.gameObject.GetComponent<NetworkTransform>();
                    }
                }

                return _hipsNetworkTransform;
            }
        }

        /// <summary>
        /// Gets the <see cref="NetworkRigidbody"/> component attached to the character's hips.
        /// </summary>
        public NetworkRigidbody HipsNetworkRigidbody
        {
            get
            {
                if (_hipsNetworkRigidbody == null && HipsNetworkTransform != null)
                {
                    _hipsNetworkRigidbody = HipsNetworkTransform.GetComponent<NetworkRigidbody>();
                }

                return _hipsNetworkRigidbody;
            }
        }

        /// <summary>
        /// Creates the owner-written network variables.
        /// </summary>
        public JUNetcodeCharacterController()
        {
            _netMoveAxis = new NetworkVariable<Vector2>(Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
            _netLookPosition = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
            _netStateFlags = new NetworkVariable<Flags>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
            _netRightHandItem = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
            _netRagdollState = new NetworkVariable<AdvancedRagdollController.RagdollState>(AdvancedRagdollController.RagdollState.Animated, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
            _netCurrentVehicle = new NetworkVariable<NetworkObjectReference>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
            _netIsDriving = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        }

        private void Reset()
        {
            if (Application.isPlaying == false)
            {
                if (NetworkObject == null)
                {
                    gameObject.AddComponent<NetworkObject>();
                }

                if (NetworkTransform == null)
                {
                    _networkTransform = gameObject.AddComponent<NetworkTransform>();
                }

                _networkTransform.SyncRotAngleX = false;
                _networkTransform.SyncRotAngleY = false;
                _networkTransform.SyncRotAngleZ = false;
                _networkTransform.SyncScaleX = false;
                _networkTransform.SyncScaleY = false;
                _networkTransform.SyncScaleZ = false;

                if (NetworkRigidbody == null)
                {
                    _networkRigidbody = gameObject.AddComponent<NetworkRigidbody>();
                }

                if (CharacterController.Ragdoller != null)
                {
                    Transform characterHips = CharacterController.anim.GetBoneTransform(HumanBodyBones.Hips);
                    if (characterHips != null)
                    {
                        // Child NetworkBehaviours use the root NetworkObject; a nested one would not be spawnable.
                        NetworkTransform hipsNetworkTransform = characterHips.gameObject.GetComponent<NetworkTransform>();
                        if (hipsNetworkTransform == null)
                        {
                            hipsNetworkTransform = characterHips.gameObject.AddComponent<NetworkTransform>();
                        }

                        NetworkRigidbody hipsNetworkRigidbody = characterHips.gameObject.GetComponent<NetworkRigidbody>();
                        if (hipsNetworkRigidbody == null)
                        {
                            hipsNetworkRigidbody = characterHips.gameObject.AddComponent<NetworkRigidbody>();
                        }

                        hipsNetworkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
                        hipsNetworkTransform.SyncRotAngleX = true;
                        hipsNetworkTransform.SyncRotAngleY = true;
                        hipsNetworkTransform.SyncRotAngleZ = true;
                        hipsNetworkTransform.SyncScaleX = false;
                        hipsNetworkTransform.SyncScaleY = false;
                        hipsNetworkTransform.SyncScaleZ = false;
                    }
                }
            }

            EnsureSettings();
        }

        private void OnValidate()
        {
            EnsureSettings();
        }

        private void Start()
        {
            Transform characterHips = CharacterController.anim.GetBoneTransform(HumanBodyBones.Hips);
            if (characterHips != null)
            {
                _hipsNetworkTransform = characterHips.gameObject.GetComponent<NetworkTransform>();
            }

            _driveAbility = GetComponent<DriveVehicles>();
            if (_driveAbility != null)
            {
                _driveAbility.OnStartEnterVehicle += OnStartEnterVehicle;
                _driveAbility.OnStartExitVehicle += OnStartExitVehicle;
                _driveAbility.OnCancelEnterVehicle += OnCancelEnterVehicle;
            }
        }

        private void Update()
        {
            UpdateIfOwner();
            UpdateIfNonOwner();

            if (HipsNetworkTransform && CharacterController.Ragdoller)
            {
                HipsNetworkTransform.enabled = CharacterController.Ragdoller.State != AdvancedRagdollController.RagdollState.Animated;
            }

            if (CharacterController.Ragdoller != null && HipsNetworkRigidbody)
            {
                bool syncHips = CharacterController.Ragdoller.State == AdvancedRagdollController.RagdollState.Ragdolled;
                HipsNetworkRigidbody.enabled = syncHips;
                HipsNetworkTransform.enabled = syncHips;
            }
        }

        /// <inheritdoc/>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _enableWeaponSwitchingTime = 0;

            if (IsOwner)
            {
                if (CharacterController.IsPlayer)
                {
                    if (UserInterfacePrefab != null)
                    {
                        Instantiate(UserInterfacePrefab);
                    }

                    if (CameraControllerPrefab != null)
                    {
                        TPSCameraController cameraController = Instantiate(CameraControllerPrefab);
                        CharacterController.MyPivotCamera = cameraController;
                    }
                }
            }
            else
            {
                CharacterController.tag = "Untagged";
                CharacterController.MyPivotCamera = null;
                CharacterController.UseDefaultControllerInput = false;
                CharacterController.Inputs = null;

                if (CharacterController.TryGetComponent(out JUInteractionSystem interactionSystem))
                {
                    interactionSystem.UseDefaultInputs = false;
                }

                // The client started ragdoll but the server needs to be notified.
                CharacterController.OnStartRagdollCollision += OnRagdollStartedRpc;
            }
        }

        private void EnsureSettings()
        {
            if (Application.isPlaying == false)
            {
                NetworkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;

                if (HipsNetworkTransform)
                {
                    HipsNetworkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
                    HipsNetworkTransform.InLocalSpace = false;
                }
            }
        }

        private void UpdateIfOwner()
        {
            if (IsOwner == false)
            {
                return;
            }

            bool shotInputPressed = CharacterController.Inputs.IsShotPressed;
            bool meleeAttackTriggered = CharacterController.Inputs.IsMeleeWeaponAttackTriggered;
            bool punchInputTriggered = CharacterController.Inputs.IsPunchTriggered;
            bool reloadTriggered = CharacterController.Inputs.IsReloadTriggered;
            bool aimInputPressed = CharacterController.Inputs.IsAimPressed;
            bool isRollTriggered = CharacterController.Inputs.IsRollTriggered;

            Vector2 moveDirection = CharacterController.Inputs.MoveAxis;
            if (CharacterController.MyPivotCamera != null)
            {
                Vector3 cameraForward = CharacterController.MyPivotCamera.mCamera.transform.forward;
                Vector3 cameraRight = CharacterController.MyPivotCamera.mCamera.transform.right;

                cameraForward = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
                cameraRight = Vector3.ProjectOnPlane(cameraRight, Vector3.up);
                cameraForward /= cameraForward.magnitude;
                cameraRight /= cameraRight.magnitude;

                Vector3 moveDirection3D = moveDirection.y * cameraForward + moveDirection.x * cameraRight;
                moveDirection.x = moveDirection3D.x;
                moveDirection.y = moveDirection3D.z;
            }

            _netRightHandItem.Value = (byte)CharacterController.Inventory.CurrentRightHandItemID;
            _netMoveAxis.Value = moveDirection;

            Flags flags = 0;

            // State flags.
            if (CharacterController.IsRunning) flags |= Flags.IsRunning;
            if (CharacterController.IsSprinting) flags |= Flags.IsSprinting;
            if (CharacterController.IsCrouched) flags |= Flags.IsCrouching;
            if (CharacterController.IsProne) flags |= Flags.IsProne;
            if (CharacterController.CanMove) flags |= Flags.CanMove;
            if (CharacterController.CanRotate) flags |= Flags.CanRotate;
            if (CharacterController.DisableAllMove) flags |= Flags.DisableAllMove;

            // Inputs flags.
            if (shotInputPressed) flags |= Flags.ShotInputPressed;
            if (aimInputPressed) flags |= Flags.AimInputPressed;

            // Trigger input RPCs.
            if (meleeAttackTriggered) ClientTriggerMeleeInputRpc();
            if (punchInputTriggered) ClientTriggerPunchInputRpc();
            if (isRollTriggered) ClientTriggerRollInputRpc();
            if (reloadTriggered) ClientTriggerReloadInputRpc();

            _netStateFlags.Value = flags;
            _netIsDriving.Value = CharacterController.IsDriving;

            Vector3 lookAtPosition = CharacterController.GetLookPosition();
            if (_enableWeaponSwitchingTime > 0.3f && CharacterController.RightHandWeapon != null && CharacterController.FiringModeIK && CharacterController.RightHandWeapon.CameraRaycastHit.point != Vector3.zero)
            {
                RaycastHit hit = CharacterController.RightHandWeapon.CameraRaycastHit;
                Vector3 hitDirection = (hit.point - CharacterController.RightHandWeapon.Shoot_Position.position).normalized;
                if (Vector3.Dot(hitDirection, CharacterController.RightHandWeapon.Shoot_Position.forward) > 0.7f)
                {
                    lookAtPosition = hit.point;
                }
            }

            if (_enableWeaponSwitchingTime <= 0.3f)
            {
                _enableWeaponSwitchingTime += Time.deltaTime;
            }

            _netLookPosition.Value = lookAtPosition;

            if (CharacterController.Ragdoller != null)
            {
                _netRagdollState.Value = CharacterController.Ragdoller.State;
            }
        }

        private void UpdateIfNonOwner()
        {
            if (IsOwner && IsSpawned)
            {
                return;
            }

            Flags flags = _netStateFlags.Value;

            bool shotInputPressed = flags.HasFlag(Flags.ShotInputPressed);
            bool aimInputPressed = flags.HasFlag(Flags.AimInputPressed);
            bool shotInputTriggered = flags.HasFlag(Flags.ShotInputPressed) && !_clientLastFlags.HasFlag(Flags.ShotInputPressed);
            bool meleeAttackTriggered = _clientLastTriggeredInputFlags.HasFlag(TriggeredInputFlags.MeleeAttackTriggered);
            bool punchAttackTriggered = _clientLastTriggeredInputFlags.HasFlag(TriggeredInputFlags.PunchAttackTriggered);
            bool rollTriggered = _clientLastTriggeredInputFlags.HasFlag(TriggeredInputFlags.RollTriggered);
            bool reloadTriggered = _clientLastTriggeredInputFlags.HasFlag(TriggeredInputFlags.ReloadTriggered);

            _clientLastFlags = flags;

            // Better to sync, i don't need to worry about triggered/holding button pressed.
            CharacterController.AimMode = JUTPS.CharacterBrain.JUCharacterBrain.PressAimMode.HoldToAim;
            CharacterController.LookAtPosition = _netLookPosition.Value;

            CharacterController.CanMove = flags.HasFlag(Flags.CanMove);
            CharacterController.CanRotate = flags.HasFlag(Flags.CanRotate);
            CharacterController.DisableAllMove = flags.HasFlag(Flags.DisableAllMove);

            CharacterController.ControllerInputs(
                moveAxis: _netMoveAxis.Value,
                shotInput: shotInputPressed,
                shotInputDown: shotInputTriggered,
                meleeAttackInput: meleeAttackTriggered,
                punchInputDown: punchAttackTriggered,
                reloadTriggered: reloadTriggered,
                aimInput: aimInputPressed,
                aimInputDown: false,
                isRunPressed: false,
                isRunPerformed: false,
                isRollTriggered: rollTriggered,
                isJumpTriggered: false
            );

            bool isCrouching = flags.HasFlag(Flags.IsCrouching);
            if (isCrouching != CharacterController.IsCrouched)
            {
                if (isCrouching)
                {
                    CharacterController._Crouch();
                }
                else
                {
                    CharacterController._GetUp();
                }
            }

            bool isProne = flags.HasFlag(Flags.IsProne);
            if (isProne != CharacterController.IsProne)
            {
                if (isProne)
                {
                    CharacterController._Prone();
                }
                else
                {
                    CharacterController._GetUp();
                }
            }

            CharacterController.IsRunning = (flags & Flags.IsRunning) != 0;
            CharacterController.IsSprinting = (flags & Flags.IsSprinting) != 0;

            if (CharacterController.IsWeaponSwitching == false)
            {
                if ((_netRightHandItem.Value != CharacterController.Inventory.CurrentRightHandItemID) ||
                    (_netRightHandItem.Value > 0 && CharacterController.HoldableItemInUseRightHand == null))
                {
                    CharacterController.SwitchToItem(_netRightHandItem.Value);
                }
            }

            _clientLastTriggeredInputFlags = 0;

            if (CharacterController.Ragdoller)
            {
                if (_netRagdollState.Value == AdvancedRagdollController.RagdollState.Ragdolled && CharacterController.Ragdoller.State != AdvancedRagdollController.RagdollState.Ragdolled)
                {
                    CharacterController.Ragdoller.State = _netRagdollState.Value;
                }

                AdvancedRagdollController.RagdollState netRagdollState = _netRagdollState.Value;
                AdvancedRagdollController.RagdollState characterRagdollState = CharacterController.Ragdoller.State;

                if (netRagdollState == AdvancedRagdollController.RagdollState.BlendToAnim ||
                    netRagdollState == AdvancedRagdollController.RagdollState.Animated ||
                    netRagdollState == AdvancedRagdollController.RagdollState.WaitStablePosition)
                {
                    if (characterRagdollState != AdvancedRagdollController.RagdollState.BlendToAnim &&
                    characterRagdollState != AdvancedRagdollController.RagdollState.Animated &&
                    characterRagdollState != AdvancedRagdollController.RagdollState.WaitStablePosition)
                    {
                        CharacterController.Ragdoller.State = AdvancedRagdollController.RagdollState.WaitStablePosition;
                    }
                }
            }

            // Avoid syncronization mistakes.
            if (_driveAbility != null && _driveAbility.CurrentVehicle == null && _netCurrentVehicle.Value.TryGet(out NetworkObject currentVehicleNetworkObject))
            {
                DriveVehicle(currentVehicleNetworkObject, true);
            }
        }

        private void DriveVehicle(NetworkObject netVehicle, bool immediately)
        {
            if (_driveAbility == null)
            {
                return;
            }

            JUVehicle vehicle = netVehicle.GetComponent<JUVehicle>();
            _driveAbility.DriveVehicle(vehicle, vehicle.GetComponent<JUVehicleCharacterIK>(), immediately);
            NetworkTransform.enabled = false;

            if (IsOwner)
            {
                _vehicleOriginalOwnerId = netVehicle.OwnerClientId;
                _netCurrentVehicle.Value = netVehicle;
                JUNetcodeVehicle netcodeVehicle = GetNetcodeVehicle(netVehicle);

                netcodeVehicle.SetOwnership(OwnerClientId);
            }
        }

        private void OnStartEnterVehicle(JUVehicle vehicle)
        {
            if (vehicle.TryGetComponent(out NetworkObject vehicleNetworkObject) == false)
            {
                Debug.LogError($"Vehicle {vehicle} does not have a NetworkObject component.");
                return;
            }

            if (IsOwner)
            {
                JUNetcodeVehicle netcodeVehicle = GetNetcodeVehicle(vehicleNetworkObject);

                _netCurrentVehicle.Value = vehicleNetworkObject;
                _vehicleOriginalOwnerId = vehicleNetworkObject.OwnerClientId;
                netcodeVehicle.SetOwnership(OwnerClientId);
            }

            ClientDriveVehicleRpc(vehicleNetworkObject);
        }

        private void OnCancelEnterVehicle(JUVehicle vehicle)
        {
            OnCancelEnterVehicleRpc();
        }

        private void OnStartExitVehicle(JUVehicle vehicle)
        {
            OnStartExitVehicleRpc();
        }

        [Rpc(SendTo.NotMe)]
        private void ClientTriggerMeleeInputRpc()
        {
            if (IsOwner == true)
            {
                return;
            }

            _clientLastTriggeredInputFlags |= TriggeredInputFlags.MeleeAttackTriggered;
        }

        [Rpc(SendTo.NotMe)]
        private void ClientTriggerPunchInputRpc()
        {
            if (IsOwner == true)
            {
                return;
            }

            _clientLastTriggeredInputFlags |= TriggeredInputFlags.PunchAttackTriggered;
        }

        [Rpc(SendTo.NotMe)]
        private void ClientTriggerRollInputRpc()
        {
            if (IsOwner == true)
            {
                return;
            }

            _clientLastTriggeredInputFlags |= TriggeredInputFlags.RollTriggered;
        }

        [Rpc(SendTo.NotMe)]
        private void ClientTriggerReloadInputRpc()
        {
            if (IsOwner == true)
            {
                return;
            }

            _clientLastTriggeredInputFlags |= TriggeredInputFlags.ReloadTriggered;
        }

        [Rpc(SendTo.NotMe)]
        private void ClientDriveVehicleRpc(NetworkObjectReference vehicleRef)
        {
            if (_driveAbility == null)
            {
                return;
            }

            DriveVehicle(vehicleRef, false);
        }

        [Rpc(SendTo.NotMe)]
        private void OnStartExitVehicleRpc()
        {
            if (_driveAbility == null)
            {
                return;
            }

            NetworkObject currentVehicleNetworkObject = GetNetworkObject(_driveAbility.CurrentVehicle);
            JUNetcodeVehicle netcodeVehicle = GetNetcodeVehicle(currentVehicleNetworkObject);
            netcodeVehicle.SetOwnership(_vehicleOriginalOwnerId);
            _driveAbility.ExitVehicle();
            NetworkTransform.enabled = true;

            if (IsOwner)
            {
                _netCurrentVehicle.Value = new NetworkObjectReference();
            }
        }

        [Rpc(SendTo.NotMe)]
        private void OnCancelEnterVehicleRpc()
        {
            if (_driveAbility == null)
            {
                return;
            }

            NetworkObject currentVehicleNetworkObject = GetNetworkObject(_driveAbility.CurrentVehicle);
            JUNetcodeVehicle netcodeVehicle = GetNetcodeVehicle(currentVehicleNetworkObject);
            netcodeVehicle.SetOwnership(_vehicleOriginalOwnerId);
            _driveAbility.CancelEnterVehicle();
            NetworkTransform.enabled = true;

            if (IsOwner)
            {
                _netCurrentVehicle.Value = new NetworkObjectReference();
            }
        }

        [Rpc(SendTo.Owner)]
        private void OnRagdollStartedRpc()
        {
            if (IsOwner == true)
            {
                CharacterController.Ragdoller.State = AdvancedRagdollController.RagdollState.Ragdolled;
                _netRagdollState.Value = AdvancedRagdollController.RagdollState.Ragdolled;
            }
        }

        private JUNetcodeVehicle GetNetcodeVehicle(NetworkObject vehicleNetworkObject)
        {
            JUNetcodeVehicle netcodeVehicle = vehicleNetworkObject.GetComponent<JUNetcodeVehicle>();
            if (netcodeVehicle == null)
            {
                Debug.LogError($"Vehicle {vehicleNetworkObject.name} does not have a {nameof(JUNetcodeVehicle)} component.");
            }

            return netcodeVehicle;
        }

        private NetworkObject GetNetworkObject(MonoBehaviour monoBehaviour)
        {
            return GetNetworkObject(monoBehaviour.gameObject);
        }

        private NetworkObject GetNetworkObject(GameObject gameObject)
        {
            NetworkObject networkObject = gameObject.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"GameObject {gameObject.name} does not have a {nameof(NetworkObject)} component.");
            }

            return networkObject;
        }
    }
}
