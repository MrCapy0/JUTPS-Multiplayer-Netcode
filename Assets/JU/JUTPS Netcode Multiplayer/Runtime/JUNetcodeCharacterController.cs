using System.Collections;
using JUTPS;
using JUTPS.CameraSystems;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Netcode multiplayer synchronization for <see cref="JUCharacterController"/>.
/// </summary>
[RequireComponent(typeof(JUCharacterController))]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(NetworkAnimator))]
[RequireComponent(typeof(NetworkRigidbody))]
public class JUNetcodeCharacterController : NetworkBehaviour
{
    private struct IKTransformData : INetworkSerializable, System.IEquatable<IKTransformData>
    {
        public Vector3 Position;
        public Vector3 Rotation;

        public bool Equals(IKTransformData other)
        {
            return Position == other.Position &&
                   Rotation == other.Rotation;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
        }
    }

    private JUCharacterController _tps;

    private NetworkTransform _networkTransform;
    private NetworkAnimator _networkAnimator;
    private NetworkRigidbody _networkRigidbody;

    private NetworkVariable<int> _leftItemId = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<int> _rightItemId = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private NetworkVariable<Vector3> _lookAtPosition = new(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private NetworkVariable<float> _lookWeightIK = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> _armsWeightIK = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> _leftHandWeightIK = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> _rightHandWeightIK = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private NetworkVariable<IKTransformData> _leftHandIK = new(new IKTransformData(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<IKTransformData> _rightHandIK = new(new IKTransformData(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

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
    /// Gets the <see cref="NetworkAnimator"/> component attached to this GameObject.
    /// </summary>
    public NetworkAnimator NetworkAnimator
    {
        get
        {
            if (_networkAnimator == null)
            {
                _networkAnimator = GetComponent<NetworkAnimator>();
            }

            return _networkAnimator;
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

            if (NetworkAnimator == null)
            {
                _networkAnimator = gameObject.AddComponent<NetworkAnimator>();
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
            CharacterController.enabled = false;
            CharacterController.MyPivotCamera = null;
            CharacterController.UseDefaultControllerInput = false;

            StartCoroutine(InitialSwitch());

            CharacterController.IKPositionLeftHand.SetParent(CharacterController.transform);
            CharacterController.IKPositionRightHand.SetParent(CharacterController.transform);
            CharacterController.LeftHandIKPositionTarget.SetParent(CharacterController.transform);
            CharacterController.RightHandIKPositionTarget.SetParent(CharacterController.transform);
        }

        _leftItemId.OnValueChanged += OnItemSwitched;
        _rightItemId.OnValueChanged += OnItemSwitched;
    }

    public override void OnNetworkDespawn()
    {
        _leftItemId.OnValueChanged -= OnItemSwitched;
        _rightItemId.OnValueChanged -= OnItemSwitched;
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
            NetworkAnimator.AuthorityMode = NetworkAnimator.AuthorityModes.Owner;
            NetworkAnimator.Animator = GetComponent<Animator>();
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

        // Update animator IK weights.
        _lookWeightIK.Value = CharacterController.LookWeightIK;
        _armsWeightIK.Value = CharacterController.ArmsWeightIK;
        _leftHandWeightIK.Value = CharacterController.LeftHandWeightIK;
        _rightHandWeightIK.Value = CharacterController.RightHandWeightIK;

        // Update IK positions for hands.
        Vector3 leftHandPos = CharacterController.IKPositionLeftHand.position;
        Vector3 leftHandRot = CharacterController.IKPositionLeftHand.eulerAngles;
        Vector3 rightHandPos = CharacterController.IKPositionRightHand.position;
        Vector3 rightHandRot = CharacterController.IKPositionRightHand.eulerAngles;

        leftHandPos = CharacterController.transform.InverseTransformPoint(leftHandPos);
        rightHandPos = CharacterController.transform.InverseTransformPoint(rightHandPos);

        _leftHandIK.Value = new IKTransformData()
        {
            Position = leftHandPos,
            Rotation = leftHandRot,
        };

        _rightHandIK.Value = new IKTransformData()
        {
            Position = rightHandPos,
            Rotation = rightHandRot,
        };

        _lookAtPosition.Value = CharacterController.GetLookPosition();
    }

    private void UpdateIfNonOwner()
    {
        if (IsOwner)
        {
            return;
        }

        CharacterController.IKPositionLeftHand.localPosition = _leftHandIK.Value.Position;
        CharacterController.IKPositionLeftHand.eulerAngles = _leftHandIK.Value.Rotation;
        CharacterController.LeftHandIKPositionTarget.localPosition = CharacterController.IKPositionLeftHand.localPosition;
        CharacterController.LeftHandIKPositionTarget.localEulerAngles = CharacterController.IKPositionLeftHand.localEulerAngles;

        CharacterController.IKPositionRightHand.localPosition = _rightHandIK.Value.Position;
        CharacterController.IKPositionRightHand.eulerAngles = _rightHandIK.Value.Rotation;
        CharacterController.RightHandIKPositionTarget.localPosition = CharacterController.IKPositionRightHand.localPosition;
        CharacterController.RightHandIKPositionTarget.localEulerAngles = CharacterController.IKPositionRightHand.localEulerAngles;

        CharacterController.LookAtPosition = _lookAtPosition.Value;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (IsOwner)
            return;

        // Apply IK weights for non-owner characters.

        CharacterController.LeftHandWeightIK = _leftHandWeightIK.Value;
        CharacterController.RightHandWeightIK = _rightHandWeightIK.Value;
        CharacterController.LookWeightIK = _lookWeightIK.Value;
        CharacterController.ArmsWeightIK = _armsWeightIK.Value;

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

    private void OnItemSwitched(int previousValue, int newValue)
    {
        if (IsOwner)
        {
            return;
        }

        CharacterController.SwitchToItem(_rightItemId.Value, true);
    }

    private IEnumerator InitialSwitch()
    {
        yield return new WaitForSeconds(0.2f);
        CharacterController.SwitchToItem(_rightItemId.Value, true);
    }
}
