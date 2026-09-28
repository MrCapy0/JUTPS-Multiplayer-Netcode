using UnityEngine;
using JUTPS;

[DisallowMultipleComponent]
public class JURagdollImpactDetector : MonoBehaviour
{
    [Header("Detection")]
    public bool enableDetection = true;
    public bool OnlyDriving = true;
    [SerializeField] private float velocityChangeThreshold = 12f;
    [SerializeField] private float collisionImpactThreshold = 8f;
    [SerializeField] private float ragdollCooldown = 0.5f;
    [SerializeField] private float reEnableDelay = 1f;

    private JUCharacterController character;
    private Rigidbody playerRb;
    private Rigidbody vehicleRb;

    private Vector3 lastVelocity;
    private Vector3 lastPosition;

    private float lastRagdollTime;
    private float reEnableTimer;

    private bool isTemporarilyDisabled;

    void Awake()
    {
        character = GetComponent<JUCharacterController>();
        playerRb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        character.DriveVehicles.OnEnterVehicle.AddListener(OnEnterVehicle);
        character.DriveVehicles.OnExitVehicle.AddListener(OnExitVehicle);
    }

    void OnDisable()
    {
        character.DriveVehicles.OnEnterVehicle.RemoveListener(OnEnterVehicle);
        character.DriveVehicles.OnExitVehicle.RemoveListener(OnExitVehicle);
    }

    void OnEnterVehicle()
    {
        isTemporarilyDisabled = true;
        reEnableTimer = reEnableDelay;

        CacheVehicleRb();
    }

    void OnExitVehicle()
    {
        vehicleRb = null;
        lastVelocity = Vector3.zero;
        lastPosition = transform.position;
    }

    void CacheVehicleRb()
    {
        var vehicle = character.DriveVehicles.CurrentVehicle;
        if (vehicle != null)
            vehicleRb = vehicle.GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (character == null) return;

        if (!enableDetection || character.IsDead || character.IsRagdolled) return;

        if (character.DriveVehicles.IsCharacterEntering) return;

        if (character.IsDriving == false && OnlyDriving) return;


        //  Delay in entering vehicles
        if (isTemporarilyDisabled)
        {
            reEnableTimer -= Time.fixedDeltaTime;
            if (reEnableTimer <= 0f)
                isTemporarilyDisabled = false;
            else
                return;
        }

        //  Cooldown
        if (Time.time < lastRagdollTime + ragdollCooldown) return;

        Vector3 currentVelocity = GetVelocity();

        float sqrDeltaV = (currentVelocity - lastVelocity).sqrMagnitude;
        float thresholdSqr = velocityChangeThreshold * velocityChangeThreshold;

        if (sqrDeltaV > thresholdSqr)
        {
            TriggerRagdoll();
            return;
        }

        lastVelocity = currentVelocity;
        lastPosition = transform.position;
    }

    Vector3 GetVelocity()
    {
        if (character.DriveVehicles.IsDriving && vehicleRb != null)
            return vehicleRb.velocity;

        if (playerRb != null && !playerRb.isKinematic)
            return playerRb.velocity;

        return (transform.position - lastPosition) / Time.fixedDeltaTime;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!enableDetection) return;
        if (character == null) return;
        if (character.IsDriving == false && OnlyDriving) return;
        if (character.IsDead) return;
        if (Time.time < lastRagdollTime + ragdollCooldown) return;

        float impactSqr = collision.relativeVelocity.sqrMagnitude;
        float thresholdSqr = collisionImpactThreshold * collisionImpactThreshold;

        if (impactSqr > thresholdSqr)
        {
            TriggerRagdoll();
        }
    }

    void TriggerRagdoll()
    {
        lastRagdollTime = Time.time;

        //character.DriveVehicles.ExitVehicle();
        character.Ragdoller.Fall();
    }
}