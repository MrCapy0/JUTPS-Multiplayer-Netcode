using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class JUDistanceOptimizer : MonoBehaviour
{
    // Public configuration
    [Tooltip("Camera used to calculate distance. If null, Camera.main is used.")]
    public Camera referenceCamera;

    [Tooltip("How often (in seconds) distance checks are performed. Higher reduces CPU usage.")]
    public float checkInterval = 0.5f;

    [Tooltip("If true, the system will auto-sort events by distanceThreshold descending at Start.")]
    public bool sortEventsByDistance = true;

    [Tooltip("List of optimization distance events. Only one event (the first matching) is applied at a time.")]
    public List<OptimizationDistanceEvent> events = new List<OptimizationDistanceEvent>();

    // runtime state
    private int _currentActiveEventIndex = -1;
    private float _nextCheckTime = 0f;

    private void Reset()
    {
        referenceCamera = Camera.main;
    }

    private void Start()
    {
        if (referenceCamera == null)
            referenceCamera = Camera.main;

        if (sortEventsByDistance)
            events = events.OrderByDescending(e => e.distanceThreshold).ToList();

        // Prepare events runtime containers
        foreach (var e in events)
            e.PrepareRuntime();

        _nextCheckTime = Time.time + UnityEngine.Random.Range(0f, checkInterval); // stagger first check a bit
    }

    private void Update()
    {
        if (Time.time < _nextCheckTime) return;
        _nextCheckTime = Time.time + Mathf.Max(0.01f, checkInterval);

        if (referenceCamera == null) referenceCamera = Camera.main;
        if (referenceCamera == null) return;

        float dist = Vector3.Distance(referenceCamera.transform.position, transform.position);

        int matchIndex = -1;
        for (int i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            bool cond = ev.applyWhenBeyond ? dist >= ev.distanceThreshold : dist <= ev.distanceThreshold;
            if (cond)
            {
                matchIndex = i;
                break; // pick the first (events sorted descending by threshold recommended)
            }
        }

        if (matchIndex != _currentActiveEventIndex)
        {
            // revert previous
            if (_currentActiveEventIndex >= 0 && _currentActiveEventIndex < events.Count)
                events[_currentActiveEventIndex].RevertAllAppliedChanges();

            // apply new
            if (matchIndex >= 0)
                events[matchIndex].ApplyAll(this);

            _currentActiveEventIndex = matchIndex;
        }

        // Update manual animator stepping for active event if needed
        if (_currentActiveEventIndex >= 0)
        {
            events[_currentActiveEventIndex].RuntimeManualAnimatorStep(Time.deltaTime);
        }
    }

    private void OnDisable()
    {
        // Revert everything on disable to avoid leaving scene in odd state
        RevertAll();
    }

    private void OnDestroy()
    {
        RevertAll();
    }

    /// <summary>
    /// Force recompute immediately (useful from other scripts).
    /// </summary>
    public void ForceUpdateNow()
    {
        _nextCheckTime = Time.time;
        Update();
    }

    /// <summary>
    /// Revert any active optimization and clear runtime states.
    /// </summary>
    public void RevertAll()
    {
        for (int i = 0; i < events.Count; i++)
        {
            events[i].RevertAllAppliedChanges();
        }
        _currentActiveEventIndex = -1;
    }

    #region Nested Types

    [Serializable]
    public class OptimizationDistanceEvent
    {
        [Header("Identification")]
        public string eventName = "New Optimization Event";

        [Tooltip("Distance threshold in world units. When applyWhenBeyond==true, event applies when distance >= threshold.")]
        public float distanceThreshold = 20f;

        [Tooltip("If true, applies when distance is beyond threshold (>=). If false, applies when distance is within (<=).")]
        public bool applyWhenBeyond = true;

        [Header("General")]
        [Tooltip("If true, auto-collect components from this GameObject and its children according to each optimization's 'auto collect' setting.")]
        public bool autoCollectFromChildren = true;

        [Header("Optimization Types (choose one or more)")]
        [EnumFlags]
        public OptimizationMode modes = OptimizationMode.None;

        [Header("Physics Optimization")]
        [Tooltip("If true, Rigidbody components will be set to isKinematic when optimization is applied.")]
        public bool setRigidbodiesKinematic = true;
        [Tooltip("If true, Collider components will be disabled when optimization is applied.")]
        public bool disableColliders = false;
        [Tooltip("If true & autoCollectFromChildren==true, all Rigidbody components in children will be collected.")]
        public bool physicsAutoCollect = true;
        [Tooltip("Explicit rigidbodies to affect (optional). If empty and physicsAutoCollect true, children rigidbodies will be auto-collected.")]
        public List<Rigidbody> explicitRigidbodies = new List<Rigidbody>();
        [Tooltip("Explicit colliders to affect (optional). If empty and physicsAutoCollect true, child colliders will be auto-collected.")]
        public List<Collider> explicitColliders = new List<Collider>();

        [Header("FrameRate Animation Optimization")]
        [Tooltip("If true, animator components will be stepped manually at targetAnimatorFPS (animator.enabled will be turned off). If false, set Animator.updateMode to the selected update mode.")]
        public bool manualAnimatorStep = true;
        [Tooltip("When manualAnimatorStep==true, this is the target frames-per-second for animator updates. Set to >0.")]
        public float targetAnimatorFPS = 10f;
        [Tooltip("If not manual stepping, set animator update mode to this value.")]
        public AnimatorUpdateMode fallbackAnimatorUpdateMode = AnimatorUpdateMode.Normal;
        [Tooltip("Auto collect animators from children.")]
        public bool animatorAutoCollect = true;
        [Tooltip("Explicit animators to affect (optional). If empty and animatorAutoCollect true, child animators will be auto-collected.")]
        public List<Animator> explicitAnimators = new List<Animator>();

        [Header("Disable Rendering Optimization")]
        [Tooltip("Disable MeshRenderer / SkinnedMeshRenderer components.")]
        public bool disableRenderers = true;
        [Tooltip("If true and autoCollectFromChildren==true, collect child renderers automatically.")]
        public bool rendererAutoCollect = true;
        [Tooltip("Explicit renderers to affect (optional).")]
        public List<Renderer> explicitRenderers = new List<Renderer>();

        [Header("Disable Script Optimization")]
        [Tooltip("Disable specific MonoBehaviours when optimization is active.")]
        public bool disableScripts = true;
        [Tooltip("If true and autoCollectFromChildren==true, collect all MonoBehaviours (except this DistanceOptimizer) to be optionally disabled.")]
        public bool scriptsAutoCollect = false;
        [Tooltip("Explicit scripts to disable (optional).")]
        public List<MonoBehaviour> explicitScriptsToDisable = new List<MonoBehaviour>();

        // --- runtime ---
        [NonSerialized] internal OptimizationRuntimeState runtimeState;

        internal void PrepareRuntime()
        {
            runtimeState = new OptimizationRuntimeState();
            runtimeState.Reset();
        }

        internal void ApplyAll(JUDistanceOptimizer owner)
        {
            if (runtimeState == null) PrepareRuntime();

            // Auto-collect if necessary
            if (owner != null && autoCollectFromChildren)
            {
                if (physicsAutoCollect && explicitRigidbodies.Count == 0)
                {
                    explicitRigidbodies = owner.GetComponentsInChildren<Rigidbody>(true).ToList();
                }
                if (physicsAutoCollect && explicitColliders.Count == 0)
                {
                    explicitColliders = owner.GetComponentsInChildren<Collider>(true).ToList();
                }
                if (animatorAutoCollect && explicitAnimators.Count == 0)
                {
                    explicitAnimators = owner.GetComponentsInChildren<Animator>(true).ToList();
                }
                if (rendererAutoCollect && explicitRenderers.Count == 0)
                {
                    explicitRenderers = owner.GetComponentsInChildren<Renderer>(true).ToList();
                }
                if (scriptsAutoCollect && explicitScriptsToDisable.Count == 0)
                {
                    // collect MonoBehaviours except this optimizer on the same object
                    var allMB = owner.GetComponentsInChildren<MonoBehaviour>(true);
                    explicitScriptsToDisable = new List<MonoBehaviour>();
                    foreach (var mb in allMB)
                    {
                        if (mb == null) continue;
                        if (mb is JUDistanceOptimizer) continue;
                        if (mb.gameObject == owner.gameObject && mb == owner.GetComponent<JUDistanceOptimizer>()) continue;
                        explicitScriptsToDisable.Add(mb);
                    }
                }
            }

            // Physics
            if (modes.HasFlag(OptimizationMode.PhysicsOptimization))
                ApplyPhysicsOptimization();

            // Animators
            if (modes.HasFlag(OptimizationMode.FrameRateAnimationOptimization))
                ApplyAnimatorOptimization();

            // Renderers
            if (modes.HasFlag(OptimizationMode.DisableRenderingOptimization))
                ApplyRendererOptimization();

            // Scripts
            if (modes.HasFlag(OptimizationMode.DisableScriptOptimization))
                ApplyScriptDisabling();
        }

        internal void RevertAllAppliedChanges()
        {
            if (runtimeState == null) return;

            // Revert in reverse order to be safe
            RevertScriptDisabling();
            RevertRendererOptimization();
            RevertAnimatorOptimization();
            RevertPhysicsOptimization();

            runtimeState.Reset();
        }

        #region Animator logic (manual stepping fallback)

        private void ApplyAnimatorOptimization()
        {
            runtimeState.EnsureAnimatorState();

            var anims = explicitAnimators ?? new List<Animator>();
            foreach (var a in anims)
            {
                if (a == null) continue;

                if (!runtimeState.animatorOriginals.ContainsKey(a))
                {
                    runtimeState.animatorOriginals[a] = new AnimatorOriginalState
                    {
                        enabled = a.enabled,
                        updateMode = a.updateMode,
                        cullingMode = a.cullingMode
                    };
                }

                if (manualAnimatorStep && targetAnimatorFPS > 0f)
                {
                    // disable automatic animator updating; we'll call Animator.Update manually
                    a.enabled = false;
                    // track manual stepping state
                    if (!runtimeState.manualAnimatorTimers.ContainsKey(a))
                        runtimeState.manualAnimatorTimers[a] = 0f;
                }
                else
                {
                    // not manual: adjust update mode to fallback
                    a.updateMode = fallbackAnimatorUpdateMode;
                }
            }
        }

        private void RevertAnimatorOptimization()
        {
            if (runtimeState?.animatorOriginals == null) return;

            foreach (var kv in runtimeState.animatorOriginals)
            {
                var a = kv.Key;
                var orig = kv.Value;
                if (a == null) continue;
                a.enabled = orig.enabled;
                a.updateMode = orig.updateMode;
                a.cullingMode = orig.cullingMode;
            }

            runtimeState.animatorOriginals.Clear();
            runtimeState.manualAnimatorTimers.Clear();
        }

        internal void RuntimeManualAnimatorStep(float deltaTime)
        {
            if (runtimeState == null || !manualAnimatorStep || targetAnimatorFPS <= 0f) return;
            float stepPeriod = 1f / Mathf.Max(1f, targetAnimatorFPS);

            // iterate animators we modified
            var anims = explicitAnimators ?? new List<Animator>();
            foreach (var a in anims)
            {
                if (a == null) continue;

                // only step animators we previously disabled (to avoid double-step)
                if (!runtimeState.animatorOriginals.ContainsKey(a)) continue;

                // accumulate
                float t = runtimeState.manualAnimatorTimers.ContainsKey(a) ? runtimeState.manualAnimatorTimers[a] : 0f;
                t += deltaTime;
                while (t >= stepPeriod)
                {
                    // call Update to advance the animator by stepPeriod
                    // Note: Animator.Update accepts seconds as float
                    a.Update(stepPeriod);
                    t -= stepPeriod;
                }
                runtimeState.manualAnimatorTimers[a] = t;
            }
        }

        #endregion

        #region Renderer logic

        private void ApplyRendererOptimization()
        {
            runtimeState.EnsureRendererState();
            var rends = explicitRenderers ?? new List<Renderer>();
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!runtimeState.rendererOriginals.ContainsKey(r))
                {
                    runtimeState.rendererOriginals[r] = r.enabled;
                }
                r.enabled = !disableRenderers ? true : false;
            }
        }

        private void RevertRendererOptimization()
        {
            if (runtimeState?.rendererOriginals == null) return;
            foreach (var kv in runtimeState.rendererOriginals)
            {
                var r = kv.Key;
                bool orig = kv.Value;
                if (r == null) continue;
                r.enabled = orig;
            }
            runtimeState.rendererOriginals.Clear();
        }

        #endregion

        #region Script disabling logic

        private void ApplyScriptDisabling()
        {
            runtimeState.EnsureScriptState();
            var list = explicitScriptsToDisable ?? new List<MonoBehaviour>();
            foreach (var mb in list)
            {
                if (mb == null) continue;
                if (!runtimeState.scriptOriginals.ContainsKey(mb))
                    runtimeState.scriptOriginals[mb] = mb.enabled;
                mb.enabled = false;
            }
        }

        private void RevertScriptDisabling()
        {
            if (runtimeState?.scriptOriginals == null) return;
            foreach (var kv in runtimeState.scriptOriginals)
            {
                var mb = kv.Key;
                var orig = kv.Value;
                if (mb == null) continue;
                mb.enabled = orig;
            }
            runtimeState.scriptOriginals.Clear();
        }

        #endregion

        #region Physics logic

        private void ApplyPhysicsOptimization()
        {
            runtimeState.EnsurePhysicsState();

            var rbs = explicitRigidbodies ?? new List<Rigidbody>();
            foreach (var rb in rbs)
            {
                if (rb == null) continue;
                if (!runtimeState.rigidbodyOriginals.ContainsKey(rb))
                {
                    runtimeState.rigidbodyOriginals[rb] = new RigidbodyOriginalState
                    {
                        isKinematic = rb.isKinematic,
                        useGravity = rb.useGravity
                    };
                }
                if (setRigidbodiesKinematic)
                {
                    rb.isKinematic = true;
                    if (rb.isKinematic) return;
                    rb.angularVelocity = Vector3.zero;
                    rb.velocity = Vector3.zero;
                }
            }

            var cols = explicitColliders ?? new List<Collider>();
            foreach (var col in cols)
            {
                if (col == null) continue;
                if (!runtimeState.colliderOriginals.ContainsKey(col))
                {
                    runtimeState.colliderOriginals[col] = col.enabled;
                }
                if (disableColliders)
                    col.enabled = false;
            }
        }

        private void RevertPhysicsOptimization()
        {
            if (runtimeState?.rigidbodyOriginals != null)
            {
                foreach (var kv in runtimeState.rigidbodyOriginals)
                {
                    var rb = kv.Key;
                    var orig = kv.Value;
                    if (rb == null) continue;
                    rb.isKinematic = orig.isKinematic;
                    rb.useGravity = orig.useGravity;
                }
                runtimeState.rigidbodyOriginals.Clear();
            }

            if (runtimeState?.colliderOriginals != null)
            {
                foreach (var kv in runtimeState.colliderOriginals)
                {
                    var col = kv.Key;
                    bool orig = kv.Value;
                    if (col == null) continue;
                    col.enabled = orig;
                }
                runtimeState.colliderOriginals.Clear();
            }
        }

        #endregion
    }

    [Flags]
    public enum OptimizationMode
    {
        None = 0,
        PhysicsOptimization = 1 << 0,
        FrameRateAnimationOptimization = 1 << 1,
        DisableRenderingOptimization = 1 << 2,
        DisableScriptOptimization = 1 << 3
    }

    #endregion

    #region Runtime State Helper Classes

    [Serializable]
    internal class OptimizationRuntimeState
    {
        // Animator
        public Dictionary<Animator, AnimatorOriginalState> animatorOriginals;
        public Dictionary<Animator, float> manualAnimatorTimers;

        // Renderers
        public Dictionary<Renderer, bool> rendererOriginals;

        // Scripts
        public Dictionary<MonoBehaviour, bool> scriptOriginals;

        // Physics
        public Dictionary<Rigidbody, RigidbodyOriginalState> rigidbodyOriginals;
        public Dictionary<Collider, bool> colliderOriginals;

        public void Reset()
        {
            animatorOriginals = new Dictionary<Animator, AnimatorOriginalState>();
            manualAnimatorTimers = new Dictionary<Animator, float>();
            rendererOriginals = new Dictionary<Renderer, bool>();
            scriptOriginals = new Dictionary<MonoBehaviour, bool>();
            rigidbodyOriginals = new Dictionary<Rigidbody, RigidbodyOriginalState>();
            colliderOriginals = new Dictionary<Collider, bool>();
        }

        public void EnsureAnimatorState()
        {
            if (animatorOriginals == null) animatorOriginals = new Dictionary<Animator, AnimatorOriginalState>();
            if (manualAnimatorTimers == null) manualAnimatorTimers = new Dictionary<Animator, float>();
        }

        public void EnsureRendererState()
        {
            if (rendererOriginals == null) rendererOriginals = new Dictionary<Renderer, bool>();
        }

        public void EnsureScriptState()
        {
            if (scriptOriginals == null) scriptOriginals = new Dictionary<MonoBehaviour, bool>();
        }

        public void EnsurePhysicsState()
        {
            if (rigidbodyOriginals == null) rigidbodyOriginals = new Dictionary<Rigidbody, RigidbodyOriginalState>();
            if (colliderOriginals == null) colliderOriginals = new Dictionary<Collider, bool>();
        }
    }

    [Serializable]
    internal struct AnimatorOriginalState
    {
        public bool enabled;
        public AnimatorUpdateMode updateMode;
        public AnimatorCullingMode cullingMode;
    }

    [Serializable]
    internal struct RigidbodyOriginalState
    {
        public bool isKinematic;
        public bool useGravity;
    }

    #endregion
}

/// <summary>
/// Small helper attribute so enums can behave like flags in the inspector.
/// (If your Unity version supports [EnumFlags], remove this and use Unity's built-in.)
/// </summary>
public class EnumFlagsAttribute : PropertyAttribute { }