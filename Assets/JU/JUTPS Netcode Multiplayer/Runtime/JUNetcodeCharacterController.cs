using JUTPS;
using JUTPS.CameraSystems;
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
        private JUCharacterController _tps;

        private NetworkTransform _networkTransform;
        private NetworkRigidbody _networkRigidbody;

        private NetworkVariable<int> _rightItemId = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<Vector2> _netMoveDirection = new(Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<bool> _netRunning = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<bool> _netSprinting = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<bool> _netCrouch = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<bool> _netProne = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<bool> _netRolling = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<bool> _netAiming = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<bool> _netFireMode = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<bool> _netFireModeIk = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<bool> _netRagdolled = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<int> _netMeleeAttackRequests = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<Vector3> _netLookAtPosition = new(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private bool _meleeAttackRequestConsumed;
        private float _meleeAttackRequestResetTime;

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

                if (NetworkRigidbody == null)
                {
                    _networkRigidbody = gameObject.AddComponent<NetworkRigidbody>();
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
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

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
        }

        private void EnsureSettings()
        {
            if (Application.isPlaying == false)
            {
                NetworkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            }
        }

        private void UpdateIfOwner()
        {
            if (IsOwner == false)
            {
                return;
            }

            // Updateitem in use.
            if (!CharacterController.HoldableItemInUseRightHand)
            {
                _rightItemId.Value = -1;
            }
            else
            {
                _rightItemId.Value = CharacterController.HoldableItemInUseRightHand.ItemSwitchID;
            }

            _netRolling.Value = CharacterController.IsRolling;
            _netCrouch.Value = CharacterController.IsCrouched;
            _netProne.Value = CharacterController.IsProne;
            _netSprinting.Value = CharacterController.IsSprinting;
            _netRagdolled.Value = CharacterController.IsRagdolled;
            _netRunning.Value = CharacterController.IsRunning;
            _netAiming.Value = CharacterController.IsAiming;
            _netFireMode.Value = CharacterController.FiringMode;
            _netFireModeIk.Value = CharacterController.FiringModeIK;
            _netMoveDirection.Value = new Vector3(CharacterController.HorizontalX, CharacterController.VerticalY);

            bool meleeAttackInput = CharacterController.EnableMeleeWeaponsAttacks ? CharacterController.Inputs.IsMeleeWeaponAttackTriggered : false;
            bool punchInputDown = CharacterController.EnablePunchAttacks ? CharacterController.Inputs.IsPunchTriggered : false;

            if (meleeAttackInput == true || punchInputDown == true)
            {
                if (CharacterController.RightHandWeapon == null)
                {
                    _netMeleeAttackRequests.Value += 1;
                    _meleeAttackRequestResetTime = Time.time + 2f / NetworkManager.NetworkConfig.TickRate;
                }
            }
            else if (_netMeleeAttackRequests.Value > 0 && Time.time >= _meleeAttackRequestResetTime)
            {
                _netMeleeAttackRequests.Value -= 1;
            }

            Vector3 lookAtPosition = CharacterController.GetLookPosition();
            if (CharacterController.RightHandWeapon != null && CharacterController.FiringModeIK && CharacterController.RightHandWeapon.CameraRaycastHit.point != Vector3.zero)
            {
                RaycastHit hit = CharacterController.RightHandWeapon.CameraRaycastHit;
                Vector3 hitDirection = (hit.point - CharacterController.RightHandWeapon.Shoot_Position.position).normalized;
                if (Vector3.Dot(hitDirection, CharacterController.RightHandWeapon.Shoot_Position.forward) > 0.7f)
                {
                    lookAtPosition = hit.point;
                }
            }
            _netLookAtPosition.Value = lookAtPosition;
        }

        private void UpdateIfNonOwner()
        {
            if (IsOwner)
            {
                return;
            }

            CharacterController.LeftHandIKPositionTarget.localPosition = CharacterController.IKPositionLeftHand.localPosition;
            CharacterController.LeftHandIKPositionTarget.localEulerAngles = CharacterController.IKPositionLeftHand.localEulerAngles;

            CharacterController.RightHandIKPositionTarget.localPosition = CharacterController.IKPositionRightHand.localPosition;
            CharacterController.RightHandIKPositionTarget.localEulerAngles = CharacterController.IKPositionRightHand.localEulerAngles;

            CharacterController.LookAtPosition = _netLookAtPosition.Value;

            if (CharacterController.IsCrouched != _netCrouch.Value)
            {
                if (_netCrouch.Value)
                {
                    CharacterController._Crouch();
                }
                else
                {
                    CharacterController._GetUp();
                }
            }

            if (CharacterController.IsProne != _netProne.Value)
            {
                if (_netProne.Value)
                {
                    CharacterController._Prone();
                }
                else
                {
                    // In the controller GetUp is called twice idk why.
                    CharacterController._GetUp();
                    CharacterController._GetUp();
                }
            }

            if (CharacterController.IsRolling != _netRolling.Value)
            {
                if (_netRolling.Value)
                {
                    CharacterController._Roll();
                }
            }

            CharacterController.IsRunning = _netRunning.Value;
            CharacterController.IsSprinting = _netSprinting.Value;
            CharacterController.IsRagdolled = _netRagdolled.Value;
            CharacterController.IsAiming = _netAiming.Value;
            CharacterController.FiringMode = _netFireMode.Value;
            CharacterController.FiringModeIK = _netFireModeIk.Value;

            Vector2 moveDirection = _netMoveDirection.Value;
            bool useMeleeAttack = _netMeleeAttackRequests.Value > 0 && !_meleeAttackRequestConsumed;

            if (useMeleeAttack == true)
            {
                moveDirection = Vector2.zero;
                CharacterController.DefaultUseOfAllItems(true, true, true, false, false, false, true);
                _meleeAttackRequestConsumed = true;
            }
            else if (_netMeleeAttackRequests.Value <= 0)
            {
                _meleeAttackRequestConsumed = false;
            }

            CharacterController.HorizontalX = moveDirection.x;
            CharacterController.VerticalY = moveDirection.y;

            if (CharacterController.Ragdoller)
            {
                if (_netRagdolled.Value && CharacterController.Ragdoller.State == JUTPS.PhysicsScripts.AdvancedRagdollController.RagdollState.Animated)
                {
                    CharacterController.Ragdoller.State = JUTPS.PhysicsScripts.AdvancedRagdollController.RagdollState.Ragdolled;
                }
                else if (_netRagdolled.Value == false && CharacterController.Ragdoller.State == JUTPS.PhysicsScripts.AdvancedRagdollController.RagdollState.Ragdolled)
                {
                    CharacterController.Ragdoller.State = JUTPS.PhysicsScripts.AdvancedRagdollController.RagdollState.BlendToAnim;
                }
            }

            if (useMeleeAttack == false && CharacterController.Inventory.CurrentRightHandItemID != _rightItemId.Value)
            {
                CharacterController.SwitchToItem(_rightItemId.Value, true);
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (IsOwner)
                return;

            // Apply IK weights for non-owner characters.
            if (CharacterController.IsDead || CharacterController.InverseKinematics == false)
                return;

            if (CharacterController.IsRolling == false && CharacterController.IsDriving == false)
            {
                CharacterController.LeftHandToRespectiveIKPosition(CharacterController.LeftHandWeightIK, CharacterController.LeftHandWeightIK * CharacterController.LeftElbowAdjustWeight);
                CharacterController.RightHandToRespectiveIKPosition(CharacterController.RightHandWeightIK, CharacterController.RightHandWeightIK * CharacterController.RightElbowAdjustWeight);

                Vector3 LookingPosition = CharacterController.GetLookPosition();

                // Body Look At IK
                float ProneBodyWeight = (CharacterController.LookAtBodyWeight == 0) ? 0 : 0.1f;
                float BodyWeight = CharacterController.IsProne ? ProneBodyWeight : CharacterController.LookAtBodyWeight;

                float LookingIntensity = Vector3.Dot(transform.forward, (LookingPosition - transform.position).normalized);
                CharacterController.LookAtIK(LookingPosition, LookingIntensity * CharacterController.LookWeightIK, BodyWeight, CharacterController.HeadIKBodyWeight);
            }
        }
    }
}