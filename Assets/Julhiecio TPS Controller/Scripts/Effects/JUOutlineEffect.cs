using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JUTPS.FX {
    [AddComponentMenu("JU TPS/Effects/JU Outline Effect")]
    public class JUOutlineEffect : MonoBehaviour
    {
        public Shader OutlineShader;
        public float OutlineRadius = 0.015f;
        public Color OutlineColor = Color.white;
        private Renderer[] RENDERERS;
        private Material OutlineMaterial;
        public bool DisableOnStart = true;
        void Start()
        {
            RENDERERS = GetAllRenderers(gameObject);
            OutlineMaterial = CreateOutlineMaterial(OutlineShader, OutlineColor, OutlineRadius);
            ApplyOutlineMaterial(RENDERERS, OutlineMaterial, OutlineRadius, OutlineColor);

            if (DisableOnStart) DisableOutline();
        }
        
        public void DisableOutline()
        {
            if (OutlineMaterial == null || RENDERERS.Length == 0)
            {
                return;
            }

            foreach (Renderer r in RENDERERS)
            {
                List<Material> mats = new List<Material>(r.materials);

                // Remove outline material
                if (mats[mats.Count - 1].shader == OutlineMaterial.shader) mats.Remove(mats[mats.Count - 1]);
               
                r.materials = mats.ToArray();
            }
        } 
        public void EnableOutline()
        {
            if(OutlineMaterial == null || RENDERERS.Length == 0)
            {
                return;
            }

            // Add Outline Material
            OutlineMaterial.SetFloat("_Outline_Size", OutlineRadius);
            OutlineMaterial.SetColor("_Outline_Color", OutlineColor);

            foreach (Renderer r in RENDERERS)
            {
                List<Material> mats = new List<Material>(r.materials);
                mats.Add(OutlineMaterial);
                r.materials = mats.ToArray();
            }
        }

        public void ApplyOutlineMaterial(float outlineRadius, Color outlineColor)
        {
            // Check if already has shader to remove
            foreach (Renderer r in RENDERERS)
            {
                List<Material> mats = new List<Material>(r.materials);

                if (mats[mats.Count - 1].shader == OutlineMaterial.shader)
                {
                    //Debug.Log("Delete outline material to re-apply");
                    mats.Remove(mats[mats.Count - 1]);
                }

                r.materials = mats.ToArray();
            }

            // Add Outline Material
            OutlineMaterial.SetFloat("_Outline_Size", outlineRadius);
            OutlineMaterial.SetColor("_Outline_Color", outlineColor);

            foreach (Renderer r in RENDERERS)
            {
                List<Material> mats = new List<Material>(r.materials);
                mats.Add(OutlineMaterial);
                r.materials = mats.ToArray();
            }
        }

        public static void ApplyOutlineMaterial(Renderer[] renderers, Material outlineMat, float outlineRadius = 0.1f, Color outlineColor = default(Color))
        {
            // Check if already has shader to remove
            foreach (Renderer r in renderers)
            {
                List<Material> mats = new List<Material>(r.materials);

                if (mats[mats.Count-1].shader == outlineMat.shader)
                {
                    //Debug.Log("Delete outline material to re-apply");
                    mats.Remove(mats[mats.Count-1]);
                }

                r.materials = mats.ToArray();
            }

            // Add Outline Material
            outlineMat.SetFloat("_Outline_Size", outlineRadius);
            outlineMat.SetColor("_Outline_Color", outlineColor);

            foreach (Renderer r in renderers)
            {
                List<Material> mats = new List<Material>(r.materials);
                mats.Add(outlineMat);
                r.materials = mats.ToArray();
            }
        }
        public static Material CreateOutlineMaterial(Shader outlineShader, Color outlineColor, float outlineRadius = 0.1f)
        {
            Material outlineMaterial = new Material(outlineShader);
            outlineMaterial.SetFloat("_Outline_Size", outlineRadius);
            outlineMaterial.SetColor("_Outline_Color", outlineColor);

            return outlineMaterial;
        }
        public static Renderer[] GetAllRenderers(GameObject target)
        {
            return target.GetComponentsInChildren<Renderer>();
        }
    }
}