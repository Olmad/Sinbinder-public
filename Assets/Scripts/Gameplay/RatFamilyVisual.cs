// Assets/Scripts/Gameplay/RatFamilyVisual.cs
// Крысиное семейство (v306, переделано «под крыс» 05.11): тела — OBJ из
// Tools/rats/generate_rat_models.py, хвост — RatTail (гнётся волной).
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    public static class RatFamilyVisual
    {
        public static readonly string[] Models = { "Mother", "Pip", "Dot", "Sprig", "Moss", "Tuck" };
        private static readonly Dictionary<string, Material> Palette = new Dictionary<string, Material>();

        // Масть — крысиная, приглушённая: те же числа в превью генератора (COATS).
        private static readonly Color[] Coats =
        {
            new Color(.27f,.25f,.23f), // мать: тёмная серо-бурая
            new Color(.40f,.31f,.24f), // Пискун: бурый
            new Color(.36f,.36f,.34f), // Пятнышко: серый, светлое пятно на крупе
            new Color(.47f,.39f,.29f), // Прыг: песочный
            new Color(.27f,.29f,.25f), // Мох: тёмный, в зелень
            new Color(.45f,.41f,.38f)  // Соня: светлый серо-бежевый
        };

        // Модель — ~1,5 единицы от носа до крупа: мать ~33 см, крысята ~17–19
        // (у пакета v306 мать была 66 см — с кошку).
        private static readonly float[] Sizes = { .22f, .135f, .125f, .14f, .13f, .125f };

        // Длина тела модели по крысам (SHAPE в генераторе): от неё корень и длина хвоста.
        private static readonly float[] BodyLength = { 1f, .95f, .92f, 1f, .9f, .9f };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { Palette.Clear(); }

        public static Transform Build(Transform parent, int index, out RatTail tail)
        {
            var visual = new GameObject("Модель крысы").transform;
            visual.SetParent(parent, false);
            index = Mathf.Clamp(index, 0, 5);
            float size = Sizes[index];
            string key = Models[index];
            GameObject body = Resources.Load<GameObject>("Rats/Models/" + key + "Body");
            if (body != null)
            {
                var instance = Object.Instantiate(body, visual, false);
                instance.name = "Тело " + key;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one * size;
                ApplyMaterials(instance, index);
            }
            else
            {
                Debug.LogWarning("[КРЫСЫ] Нет OBJ модели Rats/Models/" + key + "Body. Включён резервный силуэт.");
                FallbackBody(visual, index, size);
            }
            // Хвост длиной с тело, толстый у корня, вторая половина — по земле.
            float len = BodyLength[index];
            tail = RatTail.Grow(visual, new Vector3(0f, .20f, -.66f * len) * size,
                length: 1.45f * len * size, rootRadius: .05f * size, tipRadius: .008f * size,
                height: .20f * size, material: Mat("Tail", new Color(.48f, .40f, .38f)), phase: index * 1.37f);
            return visual;
        }

        public static Material Soil => Mat("Soil",new Color(.13f,.11f,.10f));
        public static Material Pebble => Mat("Pebble",new Color(.32f,.28f,.23f));

        private static Material Mat(string key, Color color)
        {
            if (Palette.TryGetValue(key, out var found) && found != null) return found;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) throw new System.InvalidOperationException("[КРЫСЫ] В проекте нет доступного шейдера.");
            var material = new Material(shader) { name = "Rat_" + key };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", key.Contains("Eyes") ? .72f : .12f);
            Palette[key] = material;
            return material;
        }

        private static void ApplyMaterials(GameObject root, int index)
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var original = renderer.sharedMaterials;
                if (original == null || original.Length == 0) continue;
                var assigned = new Material[original.Length];
                for (int i = 0; i < original.Length; i++)
                {
                    // OBJ exports separate objects and MTL groups. Works for both import layouts.
                    string part = original[i] != null ? original[i].name : "";
                    if (string.IsNullOrEmpty(part) || part.Contains("Default")) part = renderer.gameObject.name;
                    Color color = Pick(part, index);
                    string matKey = index + ":" + part + ":" + i;
                    assigned[i] = Mat(matKey, color);
                }
                renderer.sharedMaterials = assigned;
            }
        }

        private static Color Pick(string part, int index)
        {
            // Розовое — приглушённое, серо-розовое: яркий пастельный розовый
            // читался мультяшной мышью посреди тёмного лагеря (05.11).
            part = part.ToLowerInvariant();
            if (part.Contains("spot")) return new Color(.66f,.64f,.59f);
            if (part.Contains("earinner")) return new Color(.50f,.38f,.37f);
            if (part.Contains("ear")) return Coats[index]*.8f;
            if (part.Contains("belly")) return Color.Lerp(Coats[index], new Color(.62f,.58f,.52f), .45f);
            if (part.Contains("glint")) return new Color(.9f,.9f,.85f);
            if (part.Contains("eyes")) return new Color(.03f,.03f,.03f);
            if (part.Contains("nose")) return new Color(.50f,.36f,.36f);
            if (part.Contains("paw")) return new Color(.52f,.42f,.40f);
            if (part.Contains("whisk")) return new Color(.55f,.53f,.50f);
            if (part.Contains("tail")) return new Color(.48f,.40f,.38f);
            return Coats[index];
        }

        private static void FallbackBody(Transform visual, int index, float size)
        {
            Primitive(visual,"Fur",new Vector3(0f,.25f,-.12f)*size,new Vector3(.56f,.41f,.85f)*size, Coats[index]);
            Primitive(visual,"Head",new Vector3(0f,.38f,.31f)*size,new Vector3(.42f,.40f,.40f)*size,Coats[index]);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Primitive(visual,"Ear",new Vector3(sign*.18f,.56f,.29f)*size,new Vector3(.20f,.27f,.10f)*size,new Color(.66f,.40f,.41f));
                Primitive(visual,"Eye",new Vector3(sign*.15f,.43f,.48f)*size,new Vector3(.08f,.08f,.08f)*size,Color.black);
            }
            Primitive(visual,"Nose",new Vector3(0f,.31f,.59f)*size,new Vector3(.14f,.1f,.18f)*size,new Color(.73f,.42f,.44f));
        }

        private static void Primitive(Transform parent, string name, Vector3 at, Vector3 scale, Color color)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = at;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = Mat("Fallback:"+name+color.ToString(),color);
            var collider = part.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
        }
    }
}
