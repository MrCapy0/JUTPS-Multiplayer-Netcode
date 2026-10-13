using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VisualSuspensionController : MonoBehaviour
{
    [Header("References")]
    public Transform wheelModel;        // The wheel mesh moved by your physics system
    public Transform lateralPivot;      // Pivot that should move based on wheel's vertical travel

    [Header("Settings")]
    public float offsetMultiplier = 1.0f;

    private float initialWheelY;
    private float initialPivotY;

    void Start()
    {
        // store initial Y positions
        initialWheelY = wheelModel.localPosition.y;
        initialPivotY = lateralPivot.localPosition.y;
    }

    void LateUpdate()
    {
        float deltaY = wheelModel.localPosition.y - initialWheelY;
        lateralPivot.localPosition = new Vector3(lateralPivot.localPosition.x, initialPivotY + (deltaY * offsetMultiplier), lateralPivot.localPosition.z);
        wheelModel.position = lateralPivot.position;
    }
}