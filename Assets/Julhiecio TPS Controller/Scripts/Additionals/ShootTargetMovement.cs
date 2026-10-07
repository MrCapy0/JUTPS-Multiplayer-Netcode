using UnityEngine;
namespace JUTPS.Utilities
{
    [AddComponentMenu("JU TPS/Utilities/ShootTarget Movement")]
    public class ShootTargetMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float Speed = 3f;
        private bool rightmovement;

        [Header("Impact Effect")]
        public bool EnabledImpactMovementEffect = true;
        public AnimationCurve ImpactCurve;
        public Vector3 Axis = new Vector3(1,0,0);
        public float ImpactIntensity = 10; 
        public float RotationSpeed = 10;
        public float AnimationSpeed = 2;
        public float currentTime = 0;
        private bool invertDirection = false;
        private void Start()
        {
            currentTime = 1;
            rightmovement = (Random.Range(0, 10) > 5);
        }
        void Update()
        {
            currentTime = Mathf.Clamp01(currentTime);

            Movement();
            Impact();
        }
        void Movement()
        {
            transform.Translate((rightmovement ? Speed : -Speed) * Time.deltaTime, 0, 0);
        }
        void Impact()
        {
            if (!EnabledImpactMovementEffect) return;

            currentTime += Time.deltaTime * AnimationSpeed;
            float impactCurveValue = ImpactCurve.Evaluate(currentTime) * (invertDirection ? -ImpactIntensity : ImpactIntensity);
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(Axis*impactCurveValue), RotationSpeed * Time.deltaTime);

        }
        private void OnCollisionEnter(Collision collision)
        {
            if (collision.collider.CompareTag("Bullet"))
            {
                currentTime = 0;
                
                // Get bullet direciton
                Vector3 collisionPos = collision.transform.position;
                collisionPos.y = transform.position.y;
                Vector3 bulletDirection = (transform.position - JUGameManager.PlayerController.transform.position).normalized;

                invertDirection = Vector3.Dot(bulletDirection, transform.forward) < 0.5f;
            }
            else
            {
                rightmovement = !rightmovement;
            }
        }
    }
}
