using UnityEngine;
using UnityEngine.Events;

namespace JUTPS.FX
{
    [AddComponentMenu("JU TPS/FX/JU Skidmark System")]
    public class JUSkidmarkSystem : MonoBehaviour
    {
        [JUTPSEditor.JUHeader.JUHeader("Skid Mark Settings")]
        public GameObject skidmarkPrefab;
        public WheelCollider wheelCollider;
        public float skidThreshold = 0.5f; // Threshold for skid (how much the wheel is slipping)
        public float skidSpawnRate = 0.1f; // Rate at which skid marks spawn
        public float skidOffset = 0.02f; // Distance from the ground where the skid mark appears
        public float driftFactor = 1.5f; // Factor to compare lateral vs. forward velocity
        public float brakeThreshold = 10f; // Minimum brake torque to be considered as braking
        public float rpmThreshold = 10f; // RPM threshold for when the wheel is considered to be effectively stopped
        public bool InvertSkidDirection;

        [JUTPSEditor.JUHeader.JUHeader("Skid Mark Audio")]
        public AudioSource SkidAudioSource;
        public AudioClip SkidAudioClip;
        public float MaxVolume = 1;
        public float AudioSpeed = 5;

        private GameObject currentSkidmark;
        private float lastSkidTime;
        protected static Transform skidParent;

        [JUTPSEditor.JUHeader.JUHeader("Events")]
        public UnityEvent OnStartSkidmark;
        public UnityEvent OnPerformSkidmark;
        public UnityEvent OnStopSkidmark;

        public bool IsSkidding { get; private set; }
        private Rigidbody rb; // Reference to the Rigidbody of the car

        private VehicleSystem.JUVehicle vehicle;
        private void Start()
        {
            vehicle = GetComponentInParent<VehicleSystem.JUVehicle>();
            if(skidParent == null) skidParent = new GameObject("Skidmarks").transform;

            if (wheelCollider == null)
            {
                wheelCollider = GetComponentInParent<WheelCollider>();
            }

            SkidAudioSource = GetComponent<AudioSource>();
            if (SkidAudioSource != null && SkidAudioClip != null)
            {
                SkidAudioSource.clip = SkidAudioClip;
                SkidAudioSource.playOnAwake = false;
                SkidAudioSource.Stop();
            }

            rb = GetComponentInParent<Rigidbody>(); // Get the Rigidbody of the car
        }
        private void HandleSkidAudio()
        {
            if (SkidAudioSource == null) return;

            if (IsSkidding && SkidAudioSource.isPlaying == false) SkidAudioSource.Play();

            if (IsSkidding == false && SkidAudioSource.volume == 0 && SkidAudioSource.isPlaying) { SkidAudioSource.Stop(); }

            SkidAudioSource.volume = Mathf.Lerp(SkidAudioSource.volume, IsSkidding ? MaxVolume : 0, AudioSpeed * Time.deltaTime);
        }
        private void Update()
        {
            if (wheelCollider == null || skidmarkPrefab == null || rb == null || vehicle == null) return;

            wheelCollider.GetGroundHit(out WheelHit hit);

            // Calculate wheel velocity
            Vector3 wheelVelocity = (hit.point - transform.position) / Time.deltaTime;
            Vector3 localVelocity = transform.InverseTransformDirection(wheelVelocity);
            Vector3 skidPosition = hit.point + hit.normal * skidOffset;
            // Check if the wheel is skidding (slipping)
            IsSkidding = Mathf.Abs(localVelocity.x) > skidThreshold && Mathf.Abs(localVelocity.x) > Mathf.Abs(localVelocity.z) * driftFactor;

            if (hit.point == Vector3.zero)
            {
                IsSkidding = false;
            }

            if (vehicle != null)
            {
                if (vehicle.IsOn == false) IsSkidding = false;
            }


            // Handle Skid Mark Audio
            HandleSkidAudio();

            // Check if the wheel is braking (using brake torque)
            bool isBraking = wheelCollider.brakeTorque > brakeThreshold;

            // Check if the RPM is near zero (indicating wheel is stopping)
            bool isWheelStopping = Mathf.Abs(wheelCollider.rpm) == 0;

            // Check if the vehicle is still moving (positive or negative velocity, in global space)
            bool isMoving = rb.velocity.magnitude > 0; // Any movement, whether forward or backward
            bool isMovingForward = Vector3.Dot(rb.velocity, transform.forward) > 0; // Moving forward
            bool isMovingBackward = Vector3.Dot(rb.velocity, transform.forward) < 0; // Moving backward

            // If there is no ground hit, stop skidding
            if (hit.point == Vector3.zero)
            {
                IsSkidding = false;
                return;
            }

            // Activate skidmark if moving (forward or backward), braking, and RPM near 0
            if ((IsSkidding || (isBraking && isWheelStopping && isMoving)) && Time.time > lastSkidTime + skidSpawnRate)
            {
                if (currentSkidmark == null)
                {
                    currentSkidmark = Instantiate(skidmarkPrefab, skidPosition, Quaternion.identity, skidParent);
                    OnStartSkidmark?.Invoke();
                }
                lastSkidTime = Time.time;
            }
            else if (!(IsSkidding || (isBraking && isWheelStopping && isMoving)) && currentSkidmark != null)
            {
                Destroy(currentSkidmark, 5f); // Destroy skid mark after 5 seconds
                currentSkidmark = null;
                OnStopSkidmark?.Invoke();
            }

            // Update skidmark position and rotation only if moving or skidding
            if (currentSkidmark != null && (IsSkidding || (isBraking && isWheelStopping && isMoving)))
            {
                currentSkidmark.transform.position = skidPosition;
                currentSkidmark.transform.rotation = Quaternion.LookRotation(InvertSkidDirection ? -hit.normal : hit.normal, transform.forward);
                OnPerformSkidmark?.Invoke();
            }
        }
    }
}