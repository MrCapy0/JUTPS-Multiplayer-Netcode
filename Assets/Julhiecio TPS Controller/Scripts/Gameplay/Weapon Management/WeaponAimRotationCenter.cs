using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace JUTPS.WeaponSystem
{

    [AddComponentMenu("JU TPS/Weapon System/Weapon Aim Rotation Center")]
    public class WeaponAimRotationCenter : MonoBehaviour
    {
        public int WeaponPositionsLengh;
        public List<string> WeaponPositionName = new List<string>();
        public List<Transform> WeaponPositionTransform = new List<Transform>();
        public List<int> ID = new List<int>();

        public List<Vector3> _storedLocalPositions = new List<Vector3>();
        public List<Quaternion> _storedLocalRotations = new List<Quaternion>();

        //[Header("Clamp Rotation")]
        public bool LimitLocalRotations = true;
        public Vector3 MinAngles = new Vector3(-75, -20, -20);
        public Vector3 MaxAngles = new Vector3(75, 20, 20);

        public void CreateWeaponPositionReference(string name)
        {
            //Add name
            WeaponPositionName.Add(name);

            //Create and set transform position reference
            Transform NewPositionReference = JUGizmoDrawer.CreateRightHandGizmo().transform;
            NewPositionReference.GetComponent<JUGizmoDrawer>().ModelToDraw = JUGizmoDrawer.DrawMesh.ArmedHand;
            NewPositionReference.name = name;
            NewPositionReference.SetParent(transform.GetChild(0));

            NewPositionReference.localPosition = Vector3.zero;
            NewPositionReference.localEulerAngles = new Vector3(0F, 11.383F, -94.913F);

            WeaponPositionTransform.Add(NewPositionReference);

            Vector3 NewPosition = NewPositionReference.position;
            _storedLocalPositions.Add(NewPosition);

            Quaternion NewRotation = NewPositionReference.rotation;
            _storedLocalRotations.Add(NewRotation);

            WeaponPositionsLengh++;
            ID.Add(WeaponPositionsLengh);

            StoreLocalTransform();
            UpdateSwitchID();
        }
        public void RemoveWeaponPositionReference(int index)
        {
            WeaponPositionName.RemoveAt(index);
            if (WeaponPositionTransform[index].gameObject != null)
            {
                DestroyImmediate(WeaponPositionTransform[index].gameObject);
            }

            WeaponPositionTransform.RemoveAt(index);
            ID.RemoveAt(index);
            WeaponPositionsLengh = WeaponPositionName.Count - 1;

            _storedLocalPositions.RemoveAt(index);
            _storedLocalRotations.RemoveAt(index);
            StoreLocalTransform();
            UpdateSwitchID();
        }
        public void StoreLocalTransform()
        {
            //Reset Stored Positions and Rotation
            _storedLocalPositions = new List<Vector3>();
            _storedLocalRotations = new List<Quaternion>();

            //Store transform locations
            foreach (Transform w in WeaponPositionTransform)
            {
                if (w != null)
                {
                    _storedLocalPositions.Add(w.localPosition);
                    _storedLocalRotations.Add(w.localRotation);
                }
            }
        }
        public void UpdateSwitchID()
        {
            for (int i = 0; i < WeaponPositionName.Count; i++)
            {
                ID[i] = i;
                WeaponPositionTransform[i].name = WeaponPositionName[i];
            }
        }

        private void LateUpdate()
        {
            if (!LimitLocalRotations) return;

            ClampRotation(MinAngles, MaxAngles);
        }

        private void Start()
        {
            StoreLocalTransform();
        }
        public void ClampRotation(Vector3 minAngles, Vector3 maxAngles)
        {
            Vector3 angles = transform.localEulerAngles;

            angles.x = Mathf.Clamp(Mathf.DeltaAngle(0, angles.x), minAngles.x, maxAngles.x);
            angles.y = Mathf.Clamp(Mathf.DeltaAngle(0, angles.y), minAngles.y, maxAngles.y);
            angles.z = Mathf.Clamp(Mathf.DeltaAngle(0, angles.z), minAngles.z, maxAngles.z);

            transform.localEulerAngles = angles;
        }
    }
}