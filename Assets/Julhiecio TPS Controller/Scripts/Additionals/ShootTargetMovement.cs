using UnityEngine;
namespace JUTPS.Utilities
{
    [AddComponentMenu("JU TPS/Utilities/ShootTarget Movement")]
    public class ShootTargetMovement : MonoBehaviour
    {
        public float Speed = 3f;
        private bool rightmovement;

        private void Start()
        {
            rightmovement = (Random.Range(0, 10) > 5);
        }
        void Update()
        {
            if (rightmovement == true)
            {
                transform.Translate(Speed * Time.deltaTime, 0, 0);
            }
            else
            {
                transform.Translate(-Speed * Time.deltaTime, 0, 0);
            }
        }
        private void OnCollisionEnter(Collision collision)
        {
            rightmovement = !rightmovement;
        }
    }
}
