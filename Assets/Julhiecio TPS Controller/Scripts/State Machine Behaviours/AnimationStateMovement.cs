using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace JUTPS.AnimatorStateMachineBehaviours
{
    public class AnimationStateMovement : StateMachineBehaviour
    {
        [SerializeField] private AnimationCurve XMovement;
        [SerializeField] private float IntensityX = 1;
        [SerializeField] private AnimationCurve YMovement;
        [SerializeField] private float IntensityY = 1;
        [SerializeField] private AnimationCurve ZMovement;
        [SerializeField] private float IntensityZ = 1;
        [SerializeField] private MovementType MoveWith;

        private Rigidbody rb;
        private enum MovementType { Transform, Rigidbody }
        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            float XMove = XMovement.Evaluate(stateInfo.normalizedTime);
            float YMove = YMovement.Evaluate(stateInfo.normalizedTime);
            float ZMove = ZMovement.Evaluate(stateInfo.normalizedTime);

            Vector3 Movement = new Vector3(XMove * IntensityX, YMove * IntensityY, ZMove * IntensityZ) * Time.deltaTime;
            Movement = animator.transform.TransformDirection(Movement);

            if (MoveWith == MovementType.Transform)
            {
                animator.transform.position += Movement;
            }
            else
            {
                if (rb == null) rb = animator.GetComponent<Rigidbody>();

                if (rb == null)
                {
                    // Cancel movement if rigidbody can not be found
                    return;
                }

                // Apply rigidbody physics movement
                rb.MovePosition(rb.position + Movement);
            }
        }
    }
}