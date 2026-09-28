using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace JUTPS.PhysicsScripts
{
    public class JUIgnoreCollision : MonoBehaviour
    {
        [JUTPSEditor.JUHeader.JUHeader("Main Colliders")]
        public List<Collider> groupA = new List<Collider>();

        [JUTPSEditor.JUHeader.JUHeader("Colliders To Ignore")]
        public List<Collider> groupB = new List<Collider>();

        void Start()
        {
            IgnoreAll();
        }

        public void IgnoreAll()
        {
            foreach (var colA in groupA)
            {
                if (colA == null) continue;

                foreach (var colB in groupB)
                {
                    if (colB == null) continue;

                    Physics.IgnoreCollision(colA, colB, true);
                }
            }
        }

        public void EnableAll()
        {
            foreach (var colA in groupA)
            {
                if (colA == null) continue;

                foreach (var colB in groupB)
                {
                    if (colB == null) continue;

                    Physics.IgnoreCollision(colA, colB, false);
                }
            }
        }
    }
}