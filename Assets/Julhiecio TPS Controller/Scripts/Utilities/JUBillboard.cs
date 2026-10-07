using UnityEngine;

namespace JUTPS.Utilities
{
    [DisallowMultipleComponent]
    public class JUBillboard : MonoBehaviour
    {
        public Camera targetCamera;

        [Header("Billboard")]
        public bool invertForward = false;

        [Header("Distance Trigger")]
        public float visibleDistance = 10f;

        [Header("Scale")]
        public float visibleScale = 1f;
        public float hiddenScale = 0f;
        public float smoothSpeed = 10f;

        Transform _cam;
        float _currentScale;

        void OnEnable()
        {
            if (!targetCamera)
                targetCamera = Camera.main;

            if (targetCamera)
                _cam = targetCamera.transform;

            _currentScale = hiddenScale;
        }

        void LateUpdate()
        {
            if (!_cam) return;

            Vector3 toCam = _cam.position - transform.position;

            // Billboard
            transform.rotation = invertForward
                ? Quaternion.LookRotation(-toCam.normalized, Vector3.up)
                : Quaternion.LookRotation(toCam.normalized, Vector3.up);

            // Distance check
            float distance = toCam.magnitude;
            float targetScale = distance <= visibleDistance ? visibleScale : hiddenScale;

            // Smooth scale
            _currentScale = Mathf.Lerp(_currentScale, targetScale * distance, smoothSpeed * Time.deltaTime);

            transform.localScale = Vector3.one * _currentScale;
        }
    }
}