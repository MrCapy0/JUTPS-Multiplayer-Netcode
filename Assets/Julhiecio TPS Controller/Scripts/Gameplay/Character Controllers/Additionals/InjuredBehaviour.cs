using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JUTPS.ActionScripts
{
    [AddComponentMenu("JU TPS/Third Person System/Additionals/JU Injured Behaviour")]
    public class InjuredBehaviour : JUTPSActions.JUTPSAction
    {
        private float startWalkSpeed, startRunSpeed, startSprintSpeed;


        public float injuredWalkSpeed = 0.4f, injuredRunSpeed = 0.8f, injuredSprintSpeed = 1.1f;

        public JULookAtIK lookAt;
        public Transform leftHandTargetIK;
        public bool Injured;
        void Start()
        {
            startWalkSpeed = TPSCharacter.WalkSpeed;
            startRunSpeed = TPSCharacter.RunSpeed;
            startSprintSpeed = TPSCharacter.SprintingSpeedMax;
        }

        // Update is called once per frame
        void Update()
        {
            if(TPSCharacter.CharacterHealth.Health < 20 && Injured == false)
            {
                TPSCharacter.WalkSpeed = injuredWalkSpeed;
                TPSCharacter.RunSpeed = injuredRunSpeed;
                TPSCharacter.SprintingSpeedMax = injuredSprintSpeed;

                Injured = true;
            }
            if (TPSCharacter.CharacterHealth.Health > 20 && Injured == true)
            {
                TPSCharacter.WalkSpeed = startWalkSpeed;
                TPSCharacter.RunSpeed = startRunSpeed;
                TPSCharacter.SprintingSpeedMax = startSprintSpeed;

                Injured = false;
            }

            if(lookAt != null && Injured)
            {
                lookAt.LookAtPosition = transform.position + transform.forward * 3;
            }
        }
        private void OnAnimatorIK(int layerIndex)
        {
            if (!Injured) return;

            anim.SetIKPosition(AvatarIKGoal.LeftHand, leftHandTargetIK.position);
            anim.SetIKRotation(AvatarIKGoal.LeftHand, leftHandTargetIK.rotation);

            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0.8f);
            anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0.8f);

        }
    }
}