// Visual-only enhancement for both story altar and the functional
// BindingDevice in crypts. No new colliders, interaction handlers or AOS code.
using System.Collections.Generic;
using Sinbinder.Crypt;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sinbinder.Gameplay
{
    public static class AltarVisualEnhancer
    {
        private static readonly HashSet<int> Decorated = new HashSet<int>();
        private static Material _stone;
        private static Material _metal;
        private static Material _light;
        private static Material _bone;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            Decorated.Clear();
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
            Enhance();
        }

        private static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Enhance();
        }

        public static void Enhance()
        {
            var devices = Object.FindObjectsByType<BindingDevice>(
                FindObjectsSortMode.None);
            foreach (var device in devices)
                if (device != null && device.gameObject.activeInHierarchy)
                    Install(device.gameObject, false);

            // The prologue's Altar.fbx is an independent story prop,
            // whereas a Crypt prefab uses an interactable BindingDevice.
            var story = GameObject.Find("Altar");
            if (story != null && story.GetComponentInChildren<BindingDevice>() == null)
                Install(story, true);
        }

        private static void Install(GameObject anchor, bool story)
        {
            int id = anchor.GetInstanceID();
            if (!Decorated.Add(id)) return;

            EnsureMaterials();

            // Keep our decorations in world space, NOT under imported FBX:
            // Blender props use a rotated root and an x100 unit multiplier.
            var root = new GameObject(story
                ? "Алтарь — камень, руны и свет"
                : "Алтарь связывания — декоративная отделка");
            root.transform.position = anchor.transform.position;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            // Raised footprint stays below the Soul/Shell socket height and
            // creates no blockers for the real player's NavMesh movement.
            Piece(root.transform, "Ступень ритуала", PrimitiveType.Cube,
                new Vector3(0f, 0.085f, 0f),
                new Vector3(3.65f, 0.17f, 2.45f), _stone);
            Piece(root.transform, "Бронзовый кант", PrimitiveType.Cube,
                new Vector3(0f, 0.205f, 0f),
                new Vector3(3.35f, 0.055f, 2.17f), _metal);
            Piece(root.transform, "Верхняя плита", PrimitiveType.Cube,
                new Vector3(0f, 0.26f, 0f),
                new Vector3(3.15f, 0.08f, 1.99f), _stone);

            // Runic plates form an open semicircle, with the soul and shell
            // sockets unobstructed and the operating handle still reachable.
            for (int i = 0; i < 9; i++)
            {
                float angle = (i * 26f + 15f) * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * 1.35f;
                float z = Mathf.Sin(angle) * 0.80f;
                var rune = Piece(root.transform, "Печать " + (i + 1),
                    PrimitiveType.Cube, new Vector3(x, 0.314f, z),
                    new Vector3(0.12f, 0.018f, 0.29f), _light);
                rune.transform.localRotation = Quaternion.Euler(0f, i * 26f + 15f, 0f);
            }

            // A pair of carved pillars frames the device but is offset far
            // enough to avoid blocking the existing sockets or lever.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 1.66f;
                Piece(root.transform, "Опора " + side, PrimitiveType.Cylinder,
                    new Vector3(x, 0.56f, 0.18f),
                    new Vector3(0.20f, 0.42f, 0.20f), _stone);
                Piece(root.transform, "Оковка опоры " + side, PrimitiveType.Cylinder,
                    new Vector3(x, 0.89f, 0.18f),
                    new Vector3(0.29f, 0.04f, 0.29f), _metal);
                Piece(root.transform, "Камень души " + side, PrimitiveType.Sphere,
                    new Vector3(x, 1.03f, 0.18f),
                    new Vector3(0.21f, 0.32f, 0.21f), _light);
            }

            // An additional overhead sigil is only for the cutscene prop:
            // don't put it above functional BindingDevice sockets.
            if (story)
            {
                Piece(root.transform, "Сердце старого алтаря", PrimitiveType.Sphere,
                    new Vector3(0f, 1.38f, 0f),
                    new Vector3(0.25f, 0.36f, 0.25f), _light);
                Piece(root.transform, "Костяной знак", PrimitiveType.Cube,
                    new Vector3(0f, 1.05f, -0.82f),
                    new Vector3(0.74f, 0.075f, 0.13f), _bone);
            }

            var glow = new GameObject("Холодный свет душ");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 1.43f, 0.18f);
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.32f, 0.77f, 0.87f);
            light.range = 4.2f;
            light.intensity = 1.25f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
        }

        private static GameObject Piece(Transform parent, string name,
            PrimitiveType primitive, Vector3 at, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(primitive);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = at;
            obj.transform.localScale = scale;
            // A visual detail must never block the altar interaction ray,
            // pathfinding, attack, body carrying or the binding sockets.
            var collision = obj.GetComponent<Collider>();
            if (collision != null)
            {
                collision.enabled = false;
                Object.Destroy(collision);
            }
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;
            return obj;
        }

        private static Material MaterialOf(Color color, float metallic,
            float smoothness, bool emissive)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return null;

            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (emissive && mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", color * 2.5f);
                mat.EnableKeyword("_EMISSION");
            }
            return mat;
        }

        private static void EnsureMaterials()
        {
            if (_stone != null) return;
            _stone = MaterialOf(new Color(0.13f, 0.17f, 0.19f), 0.05f, 0.26f, false);
            _metal = MaterialOf(new Color(0.46f, 0.32f, 0.17f), 0.7f, 0.72f, false);
            _light = MaterialOf(new Color(0.18f, 0.73f, 0.83f), 0.15f, 0.66f, true);
            _bone = MaterialOf(new Color(0.70f, 0.64f, 0.51f), 0.0f, 0.31f, false);
        }
    }
}
