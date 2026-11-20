// Read-only mesh-only preview for WorldForge composition templates.
// Never Instantiate(prefab): doing so could execute MonoBehaviours, physics or
// particle systems merely because the author moves a placement cursor.
using UnityEngine;

namespace Sinbinder.Gameplay
{
    internal static class MapTemplateGhost
    {
        private const int MaxDepth = 20;

        // Returns an inert hierarchy of MeshFilter + MeshRenderer only. All
        // geometry/material assets are shared read-only with their source.
        // A missing mesh or an over-complex hierarchy falls back to editor discs.
        internal static GameObject Create(GameObject prefab, Material overrideMaterial,
            int maxNodes, out int nodesUsed)
        {
            nodesUsed = 0;
            if (prefab == null || maxNodes < 1) return null;
            var ghost = new GameObject("WorldForge template mesh preview");
            int inspected = 0, meshes = 0;
            if (!Copy(prefab.transform, ghost.transform, overrideMaterial,
                    0, maxNodes, ref inspected, ref meshes) || meshes == 0)
            {
                Object.Destroy(ghost);
                return null;
            }
            nodesUsed = inspected;
            ghost.SetActive(false);
            return ghost;
        }

        // Reproduce the local transform tree rather than baking a guessed
        // position for nested OBJ/FBX renderers. Scripts, Animator, Colliders,
        // lights and other behaviour components are intentionally not copied.
        private static bool Copy(Transform from, Transform to, Material material,
            int depth, int budget, ref int visited, ref int meshes)
        {
            if (from == null || depth > MaxDepth || ++visited > budget) return false;

            if (from.gameObject.TryGetComponent<MeshFilter>(out var sourceMesh) &&
                from.gameObject.TryGetComponent<MeshRenderer>(out var sourceRenderer) &&
                sourceRenderer.enabled && sourceMesh.sharedMesh != null)
            {
                var mesh = to.gameObject.AddComponent<MeshFilter>();
                mesh.sharedMesh = sourceMesh.sharedMesh;
                var renderer = to.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = material != null
                    ? new[] { material }
                    : sourceRenderer.sharedMaterials;
                meshes++;
            }

            for (int i = 0; i < from.childCount; i++)
            {
                Transform child = from.GetChild(i);
                if (child == null || !child.gameObject.activeSelf) continue;
                var copy = new GameObject("Preview mesh node");
                copy.transform.SetParent(to, false);
                copy.transform.localPosition = child.localPosition;
                copy.transform.localRotation = child.localRotation;
                copy.transform.localScale = child.localScale;
                if (!Copy(child, copy.transform, material, depth + 1,
                        budget, ref visited, ref meshes))
                    return false;
            }
            return true;
        }
    }
}
