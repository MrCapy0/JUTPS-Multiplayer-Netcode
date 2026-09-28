using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using JUTPS.JUInputSystem;
using JUTPS.CameraSystems;

namespace JUTPS.CustomEditors
{
    [CustomEditor(typeof(TPSCameraController))]
    public class TPSCameraControllerEditor : Editor
    {
        public bool CameraSettings, CameraAutoRotator, CameraRecoilSettings, CameraCustomState, CameraDefaultStates;

        private TPSCameraController camTarget;

        private GUIContent Icon(string iconName, string text)
        {
            var content = EditorGUIUtility.IconContent(iconName);
            content.text = " " + text;
            return content;
        }

        private void OnEnable()
        {
            camTarget = (TPSCameraController)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            JUTPSEditor.CustomEditorUtilities.JUTPSTitle("Camera Controller");

            var toolbarStyle = JUTPSEditor.CustomEditorStyles.Toolbar();

            EditorGUIUtility.SetIconSize(new Vector2(16, 16));

            CameraSettings = GUILayout.Toggle(
                CameraSettings,
                Icon("d_FrameCapture", "Camera Settings"),
                toolbarStyle);
            CameraSettingsVariables(camTarget);

            CameraAutoRotator = GUILayout.Toggle(
                CameraAutoRotator,
                Icon("d_preAudioLoopOff", "Camera Auto Rotator"),
                toolbarStyle);
            DrawAutoRotatorVariables(camTarget);

            CameraRecoilSettings = GUILayout.Toggle(
                CameraRecoilSettings,
                Icon("d_Navigation", "Camera Recoil Settings"),
                toolbarStyle);
            DrawCameraRecoilSettings(camTarget);

            CameraDefaultStates = GUILayout.Toggle(
                CameraDefaultStates,
                Icon("d_animationvisibilitytoggleon", "Default Camera States"),
                toolbarStyle);
            DrawDefaultCameraStateSettings();

            CameraCustomState = GUILayout.Toggle(
                CameraCustomState,
                Icon("d_scenevis_scene_hover", "Custom Camera States"),
                toolbarStyle);
            DrawCustomCameraStateSettings();

            EditorGUIUtility.SetIconSize(Vector2.zero);

            serializedObject.ApplyModifiedProperties();
        }

        public void CameraSettingsVariables(TPSCameraController target)
        {
            if (CameraSettings)
            {
                var propInputAsset = serializedObject.FindProperty(nameof(target.InputAsset));
                propInputAsset.objectReferenceValue = EditorGUILayout.ObjectField("Input Asset", propInputAsset.objectReferenceValue, typeof(JUPlayerCharacterInputAsset), false) as JUPlayerCharacterInputAsset;

                var propTargetToFollow = serializedObject.FindProperty("TargetToFollow");
                propTargetToFollow.objectReferenceValue = EditorGUILayout.ObjectField("Target To Follow", propTargetToFollow.objectReferenceValue, typeof(Transform), true) as Transform;

                var raycast_camera_collision = serializedObject.FindProperty("CameraCollisionLayerMask");
                EditorGUILayout.PropertyField(raycast_camera_collision);

                var raycast_crosshair_camera = serializedObject.FindProperty("CrosshairRaycastLayerMask");
                EditorGUILayout.PropertyField(raycast_crosshair_camera);

                if (((LayerMask)raycast_camera_collision.intValue).value == 0)
                {
                    // keep behavior: set default mask if none assigned
                    target.CameraCollisionLayerMask = JUTPSEditor.LayerMaskUtilities.CrosshairMask();
                }

                var propFollowUp = serializedObject.FindProperty("FollowUpTarget");
                propFollowUp.boolValue = EditorGUILayout.ToggleLeft("  Follow Up Target", propFollowUp.boolValue, JUTPSEditor.CustomEditorStyles.MiniLeftButtonStyle());

                var propGeneralSens = serializedObject.FindProperty("GeneralSensibility");
                propGeneralSens.floatValue = EditorGUILayout.Slider("  General Sensibility", propGeneralSens.floatValue, 0, 5);

                var propGeneralVertSens = serializedObject.FindProperty("GeneralVerticalSensibility");
                propGeneralVertSens.floatValue = EditorGUILayout.Slider("  General Vertical Sensibility", propGeneralVertSens.floatValue, 0, 5);

                var propLockCursor = serializedObject.FindProperty(nameof(target.LockCursor));
                propLockCursor.boolValue = EditorGUILayout.Toggle("  Lock Cursor", propLockCursor.boolValue);

                var propHideCursor = serializedObject.FindProperty(nameof(target.HideCursor));
                propHideCursor.boolValue = EditorGUILayout.Toggle("  Hide Cursor", propHideCursor.boolValue);

                var propInvertH = serializedObject.FindProperty(nameof(target.InvertHorizontal));
                propInvertH.boolValue = EditorGUILayout.Toggle("  Invert Horizontal", propInvertH.boolValue);

                var propInvertV = serializedObject.FindProperty(nameof(target.InvertVertical));
                propInvertV.boolValue = EditorGUILayout.Toggle("  Invert Vertical", propInvertV.boolValue);
            }
        }

        public void DrawAutoRotatorVariables(TPSCameraController target)
        {
            if (CameraAutoRotator)
            {
                var propEnableAuto = serializedObject.FindProperty("EnableAutoRotator");
                propEnableAuto.boolValue = EditorGUILayout.ToggleLeft("  Auto Rotator", propEnableAuto.boolValue, JUTPSEditor.CustomEditorStyles.MiniLeftButtonStyle());
                if (propEnableAuto.boolValue)
                {
                    var propAutoRotateTime = serializedObject.FindProperty("AutoRotateTime");
                    propAutoRotateTime.floatValue = EditorGUILayout.FloatField("  Time to Auto Rotation", propAutoRotateTime.floatValue);

                    var propAutoRotationSpeed = serializedObject.FindProperty("AutoRotationSpeed");
                    propAutoRotationSpeed.floatValue = EditorGUILayout.Slider("  Rotation Speed", propAutoRotationSpeed.floatValue, 0, 60);
                    EditorGUILayout.Space();
                }

                var propEnableVehicle = serializedObject.FindProperty("EnableVehicleAutoRotation");
                propEnableVehicle.boolValue = EditorGUILayout.ToggleLeft("  Vehicle Auto Rotator", propEnableVehicle.boolValue, JUTPSEditor.CustomEditorStyles.MiniLeftButtonStyle());
                if (propEnableVehicle.boolValue)
                {
                    var propVehicleAutoRotateTime = serializedObject.FindProperty("VehicleAutoRotateTime");
                    propVehicleAutoRotateTime.floatValue = EditorGUILayout.FloatField("  Time to Auto Rotation", propVehicleAutoRotateTime.floatValue);

                    var propVehicleAutoRotationSpeed = serializedObject.FindProperty("VehicleAutoRotationSpeed");
                    propVehicleAutoRotationSpeed.floatValue = EditorGUILayout.Slider("  Rotation Speed", propVehicleAutoRotationSpeed.floatValue, 0, 60);
                    EditorGUILayout.Space();
                }
            }
        }

        public void DrawCameraRecoilSettings(TPSCameraController target)
        {
            if (CameraRecoilSettings)
            {
                var propRecoilReaction = serializedObject.FindProperty("CameraRecoilReaction");
                propRecoilReaction.boolValue = EditorGUILayout.ToggleLeft("  Camera Recoil Reaction", propRecoilReaction.boolValue, JUTPSEditor.CustomEditorStyles.MiniLeftButtonStyle());

                if (propRecoilReaction.boolValue)
                {
                    var propRecoilRotateCamera = serializedObject.FindProperty("RecoilRotateCamera");
                    propRecoilRotateCamera.boolValue = EditorGUILayout.ToggleLeft("  Rotate Camera On Recoil", propRecoilRotateCamera.boolValue, JUTPSEditor.CustomEditorStyles.MiniLeftButtonStyle());

                    var propRecoilSens = serializedObject.FindProperty("CameraRecoilSensibility");
                    propRecoilSens.floatValue = EditorGUILayout.Slider("  Recoil Sensibility", propRecoilSens.floatValue, 0, 2);
                }
            }
        }

        public void DrawDefaultCameraStateSettings()
        {
            if (CameraDefaultStates)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                var DefaultCamStates = serializedObject.FindProperty("NormalCameraState");
                EditorGUILayout.PropertyField(DefaultCamStates);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                var DefaultCamStates2 = serializedObject.FindProperty("FireModeCameraState");
                EditorGUILayout.PropertyField(DefaultCamStates2);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(camTarget.AimModeCameraState)));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(camTarget.AimingSwaySettings)));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                var DefaultCamStates4 = serializedObject.FindProperty("DrivingVehicleCameraState");
                EditorGUILayout.PropertyField(DefaultCamStates4);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                var DefaultCamStates5 = serializedObject.FindProperty("DeadPlayerCameraState");
                EditorGUILayout.PropertyField(DefaultCamStates5);
                GUILayout.EndHorizontal();
            }
        }

        public void DrawCustomCameraStateSettings()
        {
            if (CameraCustomState)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                var CustomCamStates = serializedObject.FindProperty("CustomCameraStates");
                EditorGUILayout.PropertyField(CustomCamStates);
                GUILayout.EndHorizontal();
            }
        }
    }
}