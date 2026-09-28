using UnityEngine;

namespace JUTPS.Utility
{
    /// <summary>
    /// Very simple material offset animator.
    /// - Finds Renderer on Start()
    /// - Uses runtime material at materialIndex (renderer.materials)
    /// - Auto-detects common texture property (_MainTex, _BaseMap) if field left empty
    /// - Public API: Play, Stop, Toggle, SetSpeed, ResetOffset
    /// </summary>
    [AddComponentMenu("JUTPS/Utility/Simple Material Offset Animator")]
    [DisallowMultipleComponent]
    public class SimpleMaterialOffsetAnimator : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Material index on the renderer (0 = first).")]
        [SerializeField] private int materialIndex = 0;

        [Tooltip("Texture property to animate. Leave empty to auto-detect _MainTex/_BaseMap.")]
        [SerializeField] private string texturePropertyName = "";

        [Header("Animation")]
        [Tooltip("Offset speed in UV units per second.")]
        [SerializeField] private Vector2 speed = new Vector2(1f, 0f);

        [Tooltip("Play automatically on Start.")]
        [SerializeField] private bool playOnStart = true;

        [Tooltip("Direction: + (add) or - (subtract) the offset each frame.")]
        [SerializeField] private bool subtract = false;

        // runtime
        private Renderer rend;
        private Material runtimeMaterial;
        private int texturePropID = -1;
        private Vector2 currentOffset = Vector2.zero;
        private bool isPlaying = false;

        private static readonly string[] fallbackProps = new string[] { "_MainTex", "_BaseMap" };

        private void Start()
        {
            // find renderer
            rend = GetComponent<Renderer>();
            if (rend == null)
            {
                Debug.LogWarning($"{nameof(SimpleMaterialOffsetAnimator)}: no Renderer found on GameObject. Disabling.");
                enabled = false;
                return;
            }

            // get materials and clamp index
            var mats = rend.materials;
            if (mats == null || mats.Length == 0)
            {
                Debug.LogWarning($"{nameof(SimpleMaterialOffsetAnimator)}: Renderer has no materials. Disabling.");
                enabled = false;
                return;
            }

            if (materialIndex < 0 || materialIndex >= mats.Length)
            {
                Debug.LogWarning($"{nameof(SimpleMaterialOffsetAnimator)}: materialIndex {materialIndex} out of range (0..{mats.Length - 1}). Clamping.");
                materialIndex = Mathf.Clamp(materialIndex, 0, mats.Length - 1);
            }

            // pick runtime material (this will create/return an instance for this renderer)
            runtimeMaterial = mats[materialIndex];

            // resolve texture property
            texturePropertyName = ResolveTextureProperty(texturePropertyName, runtimeMaterial);
            if (string.IsNullOrEmpty(texturePropertyName))
            {
                Debug.LogWarning($"{nameof(SimpleMaterialOffsetAnimator)}: no valid texture property found on material. Tried {_Join(fallbackProps)}. Disabling.");
                enabled = false;
                return;
            }

            texturePropID = Shader.PropertyToID(texturePropertyName);

            // read current offset (if supported)
            currentOffset = GetMaterialOffsetSafe(runtimeMaterial, texturePropertyName);

            if (playOnStart) Play();
        }

        private void Update()
        {
            if (!isPlaying || runtimeMaterial == null) return;

            float dir = subtract ? -1f : 1f;
            currentOffset += speed * Time.deltaTime * dir;
            runtimeMaterial.SetTextureOffset(texturePropertyName, currentOffset);
        }

        #region Public API
        public void Play()
        {
            if (!enabled) return;
            isPlaying = true;
        }

        public void Stop()
        {
            isPlaying = false;
        }

        public void Toggle()
        {
            isPlaying = !isPlaying;
        }

        public void SetSpeed(Vector2 newSpeed)
        {
            speed = newSpeed;
        }

        public void ResetOffset()
        {
            currentOffset = Vector2.zero;
            if (runtimeMaterial != null)
                runtimeMaterial.SetTextureOffset(texturePropertyName, currentOffset);
        }
        #endregion

        #region Helpers
        private string ResolveTextureProperty(string requested, Material mat)
        {
            if (mat == null) return string.Empty;

            if (!string.IsNullOrEmpty(requested))
            {
                if (mat.HasProperty(requested)) return requested;
                // support if user passed *_ST
                if (requested.EndsWith("_ST"))
                {
                    var baseName = requested.Substring(0, requested.Length - 3);
                    if (mat.HasProperty(baseName)) return baseName;
                }
            }

            // fallback common names
            foreach (var p in fallbackProps)
            {
                if (mat.HasProperty(p)) return p;
            }

            return string.Empty;
        }

        private Vector2 GetMaterialOffsetSafe(Material mat, string prop)
        {
            try
            {
                return mat.GetTextureOffset(prop);
            }
            catch
            {
                return Vector2.zero;
            }
        }

        private string _Join(string[] arr)
        {
            if (arr == null || arr.Length == 0) return "";
            return string.Join(", ", arr);
        }
        #endregion

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (materialIndex < 0) materialIndex = 0;
            //if (speed == Vector2.zero) speed = new Vector2(1f, 0f);
        }
#endif
    }
}