using JUTPS;
using JUTPS.CameraSystems;
using JUTPS.PhysicsScripts;
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
        private enum Flags
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
        private enum TriggeredInputFlags
        {
            MeleeAttackTriggered = 1 << 0,
            PunchAttackTriggered = 1 << 1,
            RollTriggered = 1 << 2,
            ReloadTriggered = 1 << 3
        }

        private float _enableWeaponSwitchingTime;
        private JUCharacterController _tps;

        private NetworkTransform _networkTransform;
        private NetworkRigidbody _networkRigidbody;
        private NetworkTransform _hipsNetworkTransform;

        private NetworkVariable<Vector2> _netMoveAxis = new NetworkVariable<Vector2>(Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<Vector3> _netLookPosition = new NetworkVariable<Vector3>(Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<Flags> _netStateFlags = new NetworkVariable<Flags>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<int> _netRightHandItem = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<AdvancedRagdollController.RagdollState> _netRagdollState = new NetworkVariable<AdvancedRagdollController.RagdollState>(AdvancedRagdollController.RagdollState.Animated, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private Flags _clientLastFlags;
        private TriggeredInputFlags _clientLastTriggeredInputFlags;

        public GameObject UserInterfacePrefab;
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
                    hipsNetworkTransform.SyncRotAngleX = false;
                    hipsNetworkTransform.SyncRotAngleY = true;
                    hipsNetworkTransform.SyncRotAngleZ = false;
                    hipsNetworkTransform.SyncScaleX = false;
                    hipsNetworkTransform.SyncScaleY = false;
                    hipsNetworkTransform.SyncScaleZ = false;
                }
            }

            EnsureSettings();
        }

        private void OnValidate()
        {
            EnsureSettings();
        }

        private void Awake()
        {
            Transform characterHips = CharacterController.anim.GetBoneTransform(HumanBodyBones.Hips);
            if (characterHips != null)
            {
                _hipsNetworkTransform = characterHips.gameObject.GetComponent<NetworkTransform>();
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _enableWeaponSwitchingTime = 0;

            if (IsOwner)
            {
                if (CharacterController.IsPlayer)
                {
                    Instantiate(UserInterfacePrefab);
                    var cameraController = Instantiate(CameraControllerPrefab);
                    CharacterController.MyPivotCamera = cameraController;
                }
            }
            else
            {
                CharacterController.tag = "Untagged";
                CharacterController.MyPivotCamera = null;
                CharacterController.UseDefaultControllerInput = false;
                CharacterController.Inputs = null;

                CharacterController.IKPositionLeftHand.SetParent(CharacterController.transform);
                CharacterController.IKPositionRightHand.SetParent(CharacterController.transform);
                CharacterController.LeftHandIKPositionTarget.SetParent(CharacterController.transform);
                CharacterController.RightHandIKPositionTarget.SetParent(CharacterController.transform);
            }
        }

        public override void OnNetworkDespawn()
        {
        }

        private void Update()
        {
            UpdateIfOwner();
            UpdateIfNonOwner();

            if (HipsNetworkTransform && CharacterController.Ragdoller)
            {
                HipsNetworkTransform.enabled = CharacterController.Ragdoller.State != AdvancedRagdollController.RagdollState.Animated;
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

            _netRightHandItem.Value = CharacterController.Inventory.CurrentRightHandItemID;

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
            if (meleeAttackTriggered) TriggerMeleeInputRpc();
            if (punchInputTriggered) TriggerPunchInputRpc();
            if (isRollTriggered) TriggerRollInputRpc();
            if (reloadTriggered) TriggerReloadInputRpc();

            _netStateFlags.Value = flags;

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
                ShotInput: shotInputPressed,
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
                if ((_netRightHandItem.Value != CharacterController.CurrentItemIDRightHand) ||
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
        }

        [Rpc(SendTo.NotMe)]
        private void TriggerMeleeInputRpc()
        {
            if (IsOwner == true)
            {
                return;
            }

            _clientLastTriggeredInputFlags |= TriggeredInputFlags.MeleeAttackTriggered;
        }

        [Rpc(SendTo.NotMe)]
        private void TriggerPunchInputRpc()
        {
            if (IsOwner == true)
            {
                return;
            }

            _clientLastTriggeredInputFlags |= TriggeredInputFlags.PunchAttackTriggered;
        }

        [Rpc(SendTo.NotMe)]
        private void TriggerRollInputRpc()
        {
            if (IsOwner == true)
            {
                return;
            }

            _clientLastTriggeredInputFlags |= TriggeredInputFlags.RollTriggered;
        }

        [Rpc(SendTo.NotMe)]
        private void TriggerReloadInputRpc()
        {
            if (IsOwner == true)
            {
                return;
            }

            _clientLastTriggeredInputFlags |= TriggeredInputFlags.ReloadTriggered;
        }
    }
}