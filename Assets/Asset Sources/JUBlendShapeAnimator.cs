using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SkinnedMeshRenderer))]
public class JUBlendShapeAnimator : MonoBehaviour
{
    [Serializable]
    public class AnimBlendShape
    {
        public string BlendShapeName;
        public AnimationCurve AnimationCurve = AnimationCurve.Linear(0f, 0f, 1f, 100f);
        public float Duration = 1f;
        public bool Loop = false;

        [NonSerialized] public float CurrentTime;
        [NonSerialized] public int BlendShapeIndex = -1;

        public void ResetTime()
        {
            CurrentTime = 0f;
        }

        public float Evaluate(float deltaTime)
        {
            if (Duration <= 0f) return 0f;

            CurrentTime += deltaTime;

            if (Loop)
            {
                CurrentTime %= Duration;
            }

            float normalizedTime = Mathf.Clamp01(CurrentTime / Duration);
            return AnimationCurve.Evaluate(normalizedTime);
        }
    }

    [Serializable]
    public class AnimationState
    {
        public string StateName;

        [Header("Animator Sync")]
        public bool UseAnimatorState = false;
        public string AnimatorStateName;

        [Header("BlendShapes")]
        public List<AnimBlendShape> BlendShapes = new List<AnimBlendShape>();

        public void ResetState()
        {
            foreach (var bs in BlendShapes)
            {
                bs.ResetTime();
            }
        }
    }

    [Header("Core")]
    public int CurrentAnimationID = 0;
    public List<AnimationState> AnimationStates = new List<AnimationState>();

    [Header("Blending")]
    public float CrossfadeDuration = 0.25f;

    [Header("Animator")]
    public Animator Animator;
    public int AnimatorLayer = 0;

    private SkinnedMeshRenderer _renderer;
    private Mesh _mesh;

    private int _currentID = -1;
    private int _previousID = -1;

    private float _blendTimer = 0f;
    private bool _isBlending = false;

    private void Awake()
    {
        _renderer = GetComponent<SkinnedMeshRenderer>();
        _mesh = _renderer.sharedMesh;

        InitializeBlendShapeIndices();
    }

    private void Update()
    {
        if (AnimationStates == null || AnimationStates.Count == 0) return;

        HandleAnimatorDrivenState();

        if (CurrentAnimationID < 0 || CurrentAnimationID >= AnimationStates.Count) return;

        if (_currentID != CurrentAnimationID)
        {
            StartCrossfade(_currentID, CurrentAnimationID);
        }

        UpdateAnimation();
    }

    private void HandleAnimatorDrivenState()
    {
        if (Animator == null) return;

        var stateInfo = Animator.GetCurrentAnimatorStateInfo(AnimatorLayer);

        for (int i = 0; i < AnimationStates.Count; i++)
        {
            var state = AnimationStates[i];

            if (!state.UseAnimatorState) continue;

            if (stateInfo.IsName(state.AnimatorStateName))
            {
                CurrentAnimationID = i;
                return;
            }
        }
    }

    private void StartCrossfade(int from, int to)
    {
        _previousID = from;
        _currentID = to;

        if (_currentID >= 0 && _currentID < AnimationStates.Count)
        {
            AnimationStates[_currentID].ResetState();
        }

        _blendTimer = 0f;
        _isBlending = (_previousID != -1 && CrossfadeDuration > 0f);
    }

    private void UpdateAnimation()
    {
        float deltaTime = Time.deltaTime;

        if (_isBlending)
        {
            _blendTimer += deltaTime;
            float t = Mathf.Clamp01(_blendTimer / CrossfadeDuration);

            AnimateBlended(_previousID, _currentID, t, deltaTime);

            if (t >= 1f)
            {
                _isBlending = false;
            }
        }
        else
        {
            AnimateSingle(_currentID, deltaTime);
        }
    }

    private void AnimateSingle(int stateID, float deltaTime)
    {
        var state = AnimationStates[stateID];

        foreach (var bs in state.BlendShapes)
        {
            if (bs.BlendShapeIndex < 0) continue;

            float value = bs.Evaluate(deltaTime);
            _renderer.SetBlendShapeWeight(bs.BlendShapeIndex, value);
        }
    }

    private void AnimateBlended(int fromID, int toID, float t, float deltaTime)
    {
        var toState = AnimationStates[toID];

        Dictionary<int, float> blendedValues = new Dictionary<int, float>();

        if (fromID >= 0 && fromID < AnimationStates.Count)
        {
            var fromState = AnimationStates[fromID];

            foreach (var bs in fromState.BlendShapes)
            {
                if (bs.BlendShapeIndex < 0) continue;

                float value = bs.Evaluate(deltaTime);
                blendedValues[bs.BlendShapeIndex] = value * (1f - t);
            }
        }

        foreach (var bs in toState.BlendShapes)
        {
            if (bs.BlendShapeIndex < 0) continue;

            float value = bs.Evaluate(deltaTime);

            if (blendedValues.ContainsKey(bs.BlendShapeIndex))
            {
                blendedValues[bs.BlendShapeIndex] += value * t;
            }
            else
            {
                blendedValues[bs.BlendShapeIndex] = value * t;
            }
        }

        foreach (var pair in blendedValues)
        {
            _renderer.SetBlendShapeWeight(pair.Key, pair.Value);
        }
    }

    private void InitializeBlendShapeIndices()
    {
        if (_mesh == null) return;

        foreach (var state in AnimationStates)
        {
            foreach (var bs in state.BlendShapes)
            {
                bs.BlendShapeIndex = _mesh.GetBlendShapeIndex(bs.BlendShapeName);
            }
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Set to First AnimationState")]
#endif
    public void PopulateFirstStateWithAllBlendShapes()
    {
        if (_renderer == null)
            _renderer = GetComponent<SkinnedMeshRenderer>();

        if (_renderer == null || _renderer.sharedMesh == null)
            return;

        if (AnimationStates == null || AnimationStates.Count == 0)
            return;

        Mesh mesh = _renderer.sharedMesh;
        AnimationState state = AnimationStates[0];

        state.BlendShapes.Clear();

        int blendShapeCount = mesh.blendShapeCount;

        for (int i = 0; i < blendShapeCount; i++)
        {
            string shapeName = mesh.GetBlendShapeName(i);

            AnimBlendShape newBlendShape = new AnimBlendShape
            {
                BlendShapeName = shapeName,
                AnimationCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f),
                Duration = 1f,
                Loop = true
            };

            newBlendShape.BlendShapeIndex = i;

            state.BlendShapes.Add(newBlendShape);
        }
    }
}