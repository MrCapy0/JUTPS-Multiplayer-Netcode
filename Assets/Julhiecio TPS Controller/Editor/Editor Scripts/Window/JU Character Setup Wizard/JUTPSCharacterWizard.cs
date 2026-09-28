using UnityEngine;
using UnityEditor;
using JUTPSEditor;
using JUTPS;

public class JUCharacterSetupWizard : EditorWindow
{
    private enum Tab { Setup, Copy }
    private Tab currentTab;

    private GameObject character;
    private GameObject sourceCharacter;

    private float moveSpeed = 3;
    private float rotationSpeed = 3;
    private float stoppingSpeed = 2;

    private bool curvedMovement = true;
    private bool lerpRotation = true;
    private bool useRootMotion = false;

    private bool addFootplacer = true;
    private bool addFootstep = true;
    private bool addBodyLean = false;
    private bool addDrivingAnimation = false;
    private bool addRagdoll = false;
    private bool addInventory = true;

    private bool addHitboxes = false;
    private bool addZombieAI = false;
    private bool addPatrolAI = false;

    [MenuItem("Tools/JUTPS/Character Setup Wizard")]
    public static void Open()
    {
        GetWindow<JUCharacterSetupWizard>("Character Wizard");
    }

    private void OnGUI()
    {
        DrawHeader();
        DrawTabs();

        GUILayout.Space(10);

        switch (currentTab)
        {
            case Tab.Setup:
                DrawSetupTab();
                break;
            case Tab.Copy:
                DrawCopyTab();
                break;
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("JU Character Setup Wizard", EditorStyles.boldLabel);
    }

    private void DrawTabs()
    {
        currentTab = (Tab)GUILayout.Toolbar((int)currentTab, new string[] { "Setup", "Copy" }, JUTPSEditor.CustomEditorStyles.Toolbar());
    }

    // =========================================================
    // SETUP TAB
    // =========================================================

    private void DrawSetupTab()
    {
        character = (GameObject)EditorGUILayout.ObjectField("Character", character, typeof(GameObject), true);

        bool hasSetup = HasInitialSetup(character);

        if (hasSetup)
        {
            EditorGUILayout.HelpBox("Character already has a controller setup.", MessageType.Warning);
        }

        GUILayout.Space(10);

        DrawPresets();

        GUILayout.Space(10);

        DrawSettings();
        DrawFeatures();

        GUILayout.Space(15);

        GUI.enabled = character != null && !hasSetup;

        if (GUILayout.Button("Setup Character", JUTPSEditor.CustomEditorStyles.MiniToolbar(), GUILayout.Height(35)))
        {
            ExecuteSetup();
        }

        GUI.enabled = true;
    }

    // =========================================================
    // COPY TAB
    // =========================================================

    private void DrawCopyTab()
    {
        character = (GameObject)EditorGUILayout.ObjectField("Target Character", character, typeof(GameObject), true);
        sourceCharacter = (GameObject)EditorGUILayout.ObjectField("Source Character", sourceCharacter, typeof(GameObject), false);

        GUILayout.Space(10);

        GUI.enabled = character != null && sourceCharacter != null;

        if (GUILayout.Button("Copy Items From Source", JUTPSEditor.CustomEditorStyles.Toolbar(), GUILayout.Height(35)))
        {
            if (!HasInitialSetup(character))
            {
                ApplyAdvancedPreset();
                ExecuteSetup();
            }

            CopyItemsFromSource();
        }

        GUI.enabled = true;
    }

    // =========================================================
    // SETUP CHECK
    // =========================================================

    private bool HasInitialSetup(GameObject obj)
    {
        if (obj == null) return false;

        // Adjust this to your actual controller type
        return obj.GetComponent<JUTPS.JUCharacterController>() != null;
    }

    // =========================================================
    // PRESETS
    // =========================================================

    private void DrawPresets()
    {
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Advanced")) ApplyAdvancedPreset();
        if (GUILayout.Button("Simple")) ApplySimplePreset();
        if (GUILayout.Button("Sidescroller")) ApplySidescrollerPreset();
        if (GUILayout.Button("TopDown")) ApplyTopDownPreset();

        GUILayout.EndHorizontal();
    }

    private void ApplyAdvancedPreset()
    {
        moveSpeed = 3;
        rotationSpeed = 3;
        stoppingSpeed = 4f;

        useRootMotion = true;

        addFootplacer = true;
        addFootstep = true;
        addDrivingAnimation = true;
        addBodyLean = true;
        addRagdoll = true;
        addInventory = true;
    }

    private void ApplySimplePreset()
    {
        moveSpeed = 3;
        rotationSpeed = 6;
        stoppingSpeed = 3f;

        useRootMotion = false;

        addFootplacer = false;
        addFootstep = true;
        addDrivingAnimation = false;
        addBodyLean = false;
        addRagdoll = false;
        addInventory = false;
    }

    private void ApplySidescrollerPreset()
    {
        ApplySimplePreset();
    }

    private void ApplyTopDownPreset()
    {
        ApplyAdvancedPreset();
    }

    // =========================================================
    // SETTINGS
    // =========================================================

    private void DrawSettings()
    {
        moveSpeed = EditorGUILayout.FloatField("Move Speed", moveSpeed);
        rotationSpeed = EditorGUILayout.FloatField("Rotation Speed", rotationSpeed);
        stoppingSpeed = EditorGUILayout.FloatField("Stopping Speed", stoppingSpeed);

        curvedMovement = EditorGUILayout.Toggle("Curved Movement", curvedMovement);
        lerpRotation = EditorGUILayout.Toggle("Lerp Rotation", lerpRotation);
        useRootMotion = EditorGUILayout.Toggle("Root Motion", useRootMotion);
    }

    private void DrawFeatures()
    {
        addFootplacer = EditorGUILayout.Toggle("Foot Placement", addFootplacer);
        addFootstep = EditorGUILayout.Toggle("Footstep", addFootstep);
        addBodyLean = EditorGUILayout.Toggle("Body Lean", addBodyLean);
        addDrivingAnimation = EditorGUILayout.Toggle("Driving Animation", addDrivingAnimation);
        addRagdoll = EditorGUILayout.Toggle("Ragdoll", addRagdoll);
        addInventory = EditorGUILayout.Toggle("Inventory", addInventory);

        addHitboxes = EditorGUILayout.Toggle("Hitboxes", addHitboxes);
        addZombieAI = EditorGUILayout.Toggle("Zombie AI", addZombieAI);
        addPatrolAI = EditorGUILayout.Toggle("Patrol AI", addPatrolAI);
    }

    // =========================================================
    // EXECUTION
    // =========================================================

    private void ExecuteSetup()
    {
        Selection.activeGameObject = character;
        JUTPSQuickSetup.SetupCharacterController(
            character,
            moveSpeed,
            rotationSpeed,
            stoppingSpeed,
            curvedMovement,
            lerpRotation,
            useRootMotion,
            addFootplacer,
            addFootstep,
            addDrivingAnimation,
            addBodyLean,
            addRagdoll,
            addInventory
        );

        if (addHitboxes) JUTPSQuickSetup.SetupHitBoxes();
        if (addZombieAI) JUTPSQuickSetup.AddZombieAI();
        if (addPatrolAI) JUTPSQuickSetup.AddPatrolAI();
    }

    // =========================================================
    // COPY ITEMS
    // =========================================================

    private void CopyItemsFromSource()
    {
        if (sourceCharacter == null || character == null)
            return;

        Animator anim = character.GetComponent<Animator>();
        if (anim == null)
        {
            Debug.LogWarning("Animator not found on target character.");
            return;
        }

        var sourceInventory = sourceCharacter.GetComponent<JUTPS.InventorySystem.JUInventory>();
        var targetInventory = character.GetComponent<JUTPS.InventorySystem.JUInventory>();

        if (sourceInventory == null || targetInventory == null)
        {
            Debug.LogWarning("Inventory not found.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        foreach (var item in sourceInventory.AllHoldableItems)
        {
            if (item == null) continue;

            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(item.gameObject);
            if (prefab == null) continue;

            Transform targetBone = item.IsLeftHandItem
                ? anim.GetBoneTransform(HumanBodyBones.LeftHand)
                : anim.GetBoneTransform(HumanBodyBones.RightHand);

            if (targetBone == null) continue;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            // Register creation
            Undo.RegisterCreatedObjectUndo(instance, "Copy Item");


            instance.transform.SetParent(targetBone, true);
            // Record transform before modifying
            Undo.RecordObject(instance.transform, "Set Transform");

            instance.transform.localPosition = item.transform.localPosition;
            instance.transform.localRotation = item.transform.localRotation;
            instance.transform.localScale = item.transform.localScale;
        }

        Undo.CollapseUndoOperations(group);

        Debug.Log("Items copied from source.");
    }
}