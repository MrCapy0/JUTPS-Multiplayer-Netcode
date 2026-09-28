using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JUTPS.ActionScripts
{
    [AddComponentMenu("JU TPS/Additionals/JU Look At IK")]
    public class JULookAtIK : MonoBehaviour
    {
        [JUTPSEditor.JUHeader.JUHeader("Look At Settings")]
        public bool EnabledIK = true;
        public Animator animator;
        public Camera cameraToLookDirection;
        public Vector3 LookAtPosition;
        public Vector3 OffsetLookPosition;

        [JUTPSEditor.JUHeader.JUHeader("Look At IK Motion Settings")]
        public bool SmoothActivate = true;
        public float ActivateMotionSpeed = 4;
        public float DeactivateMotionSpeed = 3;

        [Range(0, 1)]
        public float HeadWeightIK = 0.3f;
        [Range(0, 1)]
        public float BodyWeightIK = 0.05f;
        [Range(0,1)]
        public float TotalWeight = 0.5f;

        [Range(0, 1)]
        public float ClampWeight = 0.6f;
        private float Weight;
        void Start()
        {
            if(cameraToLookDirection == null)cameraToLookDirection = FindAnyObjectByType<Camera>();
            if(animator == null)animator = GetComponent<Animator>();
        }
        void Update()
        {
            if (EnabledIK == false) 
            {
                Weight = 0;
                return;
            }
            LookAtPosition = transform.position + transform.up * 2 + cameraToLookDirection.transform.forward * 5 + OffsetLookPosition;
            Weight = Mathf.Lerp(Weight, SmoothActivate ? TotalWeight : 0, (SmoothActivate ? ActivateMotionSpeed : DeactivateMotionSpeed) * Time.deltaTime);
        }
        public void _EnableIK()
        {
            EnabledIK = true;
        }
        public void _ActiveLookAtSmoothly()
        {
            SmoothActivate = true;
        }
        public void _DeactiveLookAtSmoothly()
        {
            SmoothActivate = false;
        }
        private void OnAnimatorIK(int layerIndex)
        {
            if (EnabledIK == false) return;

            animator.SetLookAtPosition(LookAtPosition);
            animator.SetLookAtWeight(Weight, BodyWeightIK, HeadWeightIK, TotalWeight, ClampWeight);
        }
    }
}