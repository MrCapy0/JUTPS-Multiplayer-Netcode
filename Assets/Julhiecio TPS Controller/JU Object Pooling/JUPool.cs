using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.SceneManagement;

namespace JUTPS.PoolSystem
{
    /// <summary>
    /// Static object pool system used to instantiate, cache, and recycle GameObjects.
    /// </summary>
    public class JUPool
    {
        /// <summary>
        /// Represents the result of a pool retrieval operation.
        /// </summary>
        public enum ResultModes
        {
            /// <summary>
            /// A new instance was instantiated because the pool was empty.
            /// </summary>
            New,

            /// <summary>
            /// An inactive instance was recycled and reused from the pool.
            /// </summary>
            Reused,

            /// <summary>
            /// Was not possible to recycle or recycle from pool. Maybe the original prefab was destroyed or unloaded?
            /// </summary>
            Failed
        }

        /// <summary>
        /// Internal component acting as a manager for a single prefab.
        /// </summary>
        private class JUPoolManager : MonoBehaviour
        {
            private GameObject _defaultInstance;
            private Stack<GameObject> _pool;

            /// <summary>
            /// The current number of inactive objects cached and available in this pool.
            /// </summary>
            public int PoolFreeSize => _pool?.Count ?? 0;

            private void Awake()
            {
#if UNITY_EDITOR
                void OnPlayModeChanged(UnityEditor.PlayModeStateChange mode)
                {
                    if (mode == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                    {
                        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                        if (gameObject != null) Destroy(gameObject);
                    }
                }

                UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
            }

            /// <summary>
            /// Initializes the manager structure with reference prefab instance.
            /// </summary>
            public void Setup(GameObject defaultInstance)
            {
                _pool = new Stack<GameObject>();
                _defaultInstance = defaultInstance;
            }

            /// <summary>
            /// Retrieves or instantiates a usable GameObject.
            /// </summary>
            public ResultModes Get(out GameObject spawnedObject)
            {
                // The original prefab was unloaded or destroyed. This manager is not necessary anymore.
                if (!_defaultInstance)
                {
                    spawnedObject = null;
                    Destroy(gameObject);
                    return ResultModes.Failed;
                }

                // Clean up any missing references (manually destroyed objects) sitting in the stack.
                while (_pool.Count > 0)
                {
                    GameObject obj = _pool.Pop();

                    if (obj != null)
                    {
                        obj.SetActive(true);
                        spawnedObject = obj;
                        return ResultModes.Reused;
                    }
                }

                // Fallback to instantiation if the pool container is empty.
                GameObject newObj = GameObject.Instantiate(_defaultInstance);

                _objectPerPool ??= new();
                _objectPerPool[newObj] = this;

                spawnedObject = newObj;
                return ResultModes.New;
            }

            /// <summary>
            /// Recycles an active instance back into the inactive cache stack.
            /// </summary>
            public void Release(GameObject poolObject)
            {
                Debug.Assert(poolObject, "Cannot release a null GameObject back into the pool.");

                // Avoid duplicating the object inside the pool stack.
                if (_pool.Contains(poolObject))
                {
                    return;
                }

                poolObject.SetActive(false);
                _pool.Push(poolObject);
            }

            /// <summary>
            /// Schedules a delayed pool release execution using a native coroutine pipeline.
            /// </summary>
            public void Release(GameObject poolObject, float time)
            {
                StartCoroutine(WaitAndRelease(poolObject, time));
            }

            private IEnumerator WaitAndRelease(GameObject poolObject, float time)
            {
                yield return new WaitForSeconds(time);

                if (poolObject != null)
                {
                    Release(poolObject);
                }
            }
        }

        private static Dictionary<GameObject, JUPoolManager> _objectPerPool;
        private static Dictionary<GameObject, JUPoolManager> _managers;

        static JUPool()
        {
            // Subscribe to clear memory between scenes to avoid critical static-reference memory leaks
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneUnloaded(Scene current)
        {
            _objectPerPool?.Clear();
            _managers?.Clear();
        }

        private static JUPoolManager GetPoolManager(GameObject prefab)
        {
            if (_managers == null)
            {
                _managers = new();

#if UNITY_EDITOR
                void OnChangePlayMode(UnityEditor.PlayModeStateChange mode)
                {
                    if (mode == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                    {
                        UnityEditor.EditorApplication.playModeStateChanged -= OnChangePlayMode;
                        if (_objectPerPool != null)
                        {
                            foreach (var keyPair in _objectPerPool)
                            {
                                if (keyPair.Key)
                                {
                                    GameObject.Destroy(keyPair.Key);
                                }
                            }
                            _objectPerPool.Clear();
                        }
                    }
                }
                UnityEditor.EditorApplication.playModeStateChanged += OnChangePlayMode;
#endif
            }

            _managers.TryGetValue(prefab, out JUPoolManager manager);
            return manager;
        }

        /// <summary>
        /// Retrieves an object from the pool. The retrieved object will be automatically activated.
        /// </summary>
        /// <param name="prefab">The reference prefab identifier for the pool.</param>
        /// <param name="spawnedObject">Outputs the resulting active GameObject instance.</param>
        /// <returns>A <see cref="ResultModes"/> stating whether the object was freshly created or recycled.</returns>
        public static ResultModes Get(GameObject prefab, out GameObject spawnedObject)
        {
            CreatePoolIfNeeded(prefab);
            return GetPoolManager(prefab).Get(out spawnedObject);
        }

        /// <summary>
        /// Retrieves an object from the pool, activates it, and applies the specified spatial positioning parameters.
        /// </summary>
        /// <param name="prefab">The reference prefab identifier for the pool.</param>
        /// <param name="position">World space destination position matrix.</param>
        /// <param name="rotation">World space orientation rotation quaternion.</param>
        /// <param name="spawnedObject">Outputs the resulting active GameObject instance.</param>
        /// <returns>A <see cref="ResultModes"/> stating whether the object was freshly created or recycled.</returns>
        public static ResultModes Get(GameObject prefab, Vector3 position, Quaternion rotation, out GameObject spawnedObject)
        {
            if (prefab == null)
            {
                spawnedObject = null;
                return ResultModes.Failed;
            }

            ResultModes mode = Get(prefab, out spawnedObject);

            if (spawnedObject)
            {
                spawnedObject.transform.SetPositionAndRotation(position, rotation);
            }

            return mode;
        }

        /// <summary>
        /// Despawns and returns an active instance back to its origin pool tracking structure, disabling its hierarchy.
        /// </summary>
        /// <param name="poolObject">The active instance to be recycled.</param>
        public static void Release(GameObject poolObject)
        {
            if (!poolObject) return;

            if (_objectPerPool == null || !_objectPerPool.ContainsKey(poolObject))
            {
                GameObject.Destroy(poolObject);
                return;
            }

            JUPoolManager manager = _objectPerPool[poolObject];
            if (manager != null)
            {
                manager.Release(poolObject);
            }
            else
            {
                GameObject.Destroy(poolObject);
                _objectPerPool.Remove(poolObject);
            }
        }

        /// <summary>
        /// Despawns and returns an active instance back to its origin pool after a specific time delay has passed.
        /// </summary>
        /// <param name="poolObject">The active instance to be recycled.</param>
        /// <param name="time">Execution delay timer sequence in seconds.</param>
        public static void Release(GameObject poolObject, float time)
        {
            if (!poolObject) return;

            if (_objectPerPool == null || !_objectPerPool.ContainsKey(poolObject))
            {
                GameObject.Destroy(poolObject, time);
                return;
            }

            JUPoolManager manager = _objectPerPool[poolObject];
            if (manager != null)
            {
                manager.Release(poolObject, time);
            }
            else
            {
                GameObject.Destroy(poolObject, time);
                _objectPerPool.Remove(poolObject);
            }
        }

        /// <summary>
        /// Queries the current size count of inactive items available inside the tracking pool registry matching the given prefab identifier.
        /// </summary>
        /// <param name="prefab">The tracking reference blueprint asset identity key.</param>
        /// <returns>Integer number indicating the size count of dormant, ready-to-use entries.</returns>
        public static int GetPoolFreeSize(GameObject prefab)
        {
            JUPoolManager manager = GetPoolManager(prefab);
            if (!manager) return 0;

            return manager.PoolFreeSize;
        }

        private static void CreatePoolIfNeeded(GameObject prefab)
        {
            _managers ??= new();
            if (_managers.TryGetValue(prefab, out JUPoolManager manager))
            {
                if (!manager) _managers.Remove(prefab);
            }

            if (!manager)
            {
                manager = new GameObject($"[ JU POOL MANAGER ] {prefab.name}").AddComponent<JUPoolManager>();
                manager.gameObject.hideFlags = HideFlags.HideAndDontSave;
                manager.Setup(prefab);

                _managers.Add(prefab, manager);
            }
        }
    }
}