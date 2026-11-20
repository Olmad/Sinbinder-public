// Event-faithful 3D playback of automatic AOS expeditions.
// The auto-battle resolver is non-spatial. Positions in this isolated stage
// are illustrative, while every action and consequence comes from saved facts.
// No AOS logic, rewards, damage or campaign state runs during playback.
using System;
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    internal sealed class ExpeditionReplayStage3D : IDisposable
    {
        private const int ReplayLayer = 29; // unused by game layers (TagManager.asset)
        private sealed class Figure
        {
            public string Key;
            public GameObject Root;
            public Transform Body;
            public Vector3 Home;
            public bool Friendly;
            public bool Fallen;
            public bool Escaped;
        }

        private readonly ExpeditionReplayData _data;
        private readonly Dictionary<string, Figure> _figures = new Dictionary<string, Figure>();
        private readonly List<Figure> _all = new List<Figure>();
        private GameObject _root;
        private Camera _camera;
        private RenderTexture _texture;
        private int _shown = -1;

        public RenderTexture Texture => _texture;

        public ExpeditionReplayStage3D(ExpeditionReplayData data)
        {
            if (data == null || data.Frames == null || data.Frames.Count == 0)
                throw new ArgumentException("Empty expedition replay", nameof(data));
            _data = data;
            _root = new GameObject("Expedition replay: isolated display only");
            _root.transform.position = new Vector3(12000f, 5000f, 12000f);
            _root.layer = ReplayLayer;

            MakePrimitive("Replay ground", PrimitiveType.Cube,
                new Vector3(0, -0.2f, 0), new Vector3(28f, 0.3f, 19f),
                new Color(0.19f, 0.23f, 0.20f));
            MakePrimitive("Route", PrimitiveType.Cube,
                new Vector3(0, -0.035f, 0), new Vector3(4f, 0.02f, 17f),
                new Color(0.38f, 0.32f, 0.23f));
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -12f : 12f;
                float z = (i / 2 == 0 ? -7f : 7f);
                MakePrimitive("Map marker", PrimitiveType.Cylinder,
                    new Vector3(x, 0.42f, z), new Vector3(0.52f, 0.42f, 0.52f),
                    new Color(0.45f, 0.47f, 0.43f));
            }

            var lightGo = new GameObject("Replay-only light");
            lightGo.layer = ReplayLayer;
            lightGo.transform.SetParent(_root.transform, false);
            lightGo.transform.localRotation = Quaternion.Euler(52f, -35f, 0f);
            var lamp = lightGo.AddComponent<Light>();
            lamp.type = LightType.Directional;
            lamp.intensity = 1.5f;
            lamp.cullingMask = 1 << ReplayLayer;

            var cameraGo = new GameObject("Replay-only 3D camera");
            cameraGo.layer = ReplayLayer;
            cameraGo.transform.SetParent(_root.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 19f, -19f);
            cameraGo.transform.LookAt(_root.transform.position + new Vector3(0, 0, 0));
            _camera = cameraGo.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.09f, 0.12f, 0.11f);
            _camera.cullingMask = 1 << ReplayLayer;
            _camera.orthographic = true;
            _camera.orthographicSize = 13f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 120f;
            _texture = new RenderTexture(960, 570, 16, RenderTextureFormat.ARGB32);
            _texture.name = "Sinbinder expedition replay 3D";
            _texture.Create();
            _camera.targetTexture = _texture;
            CollectActors();
        }

        private static string Key(string id, string name)
            => !string.IsNullOrEmpty(id) ? "id:" + id : "name:" + (name ?? "");

        private Figure Find(string id, string name)
        {
            if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(name)) return null;
            _figures.TryGetValue(Key(id, name), out var figure);
            return figure;
        }

        private Figure Ensure(string id, string name, bool friendly)
        {
            if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(name)) return null;
            string key = Key(id, name);
            if (_figures.TryGetValue(key, out var old)) return old;
            // Roster starts with a name but AOS events carry stable runtime
            // actor IDs. Join them instead of drawing a duplicate fighter.
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name) &&
                _figures.TryGetValue(Key("", name), out var named))
            {
                _figures[key] = named;
                return named;
            }
            var figure = new Figure { Key = key, Friendly = friendly };
            var pawn = new GameObject("AOS record · " + (name ?? id));
            pawn.layer = ReplayLayer;
            pawn.transform.SetParent(_root.transform, false);
            var body = MakePrimitive("Replay fighter", PrimitiveType.Capsule,
                Vector3.zero, new Vector3(0.85f, 1f, 0.85f),
                friendly ? new Color(0.30f, 0.75f, 0.80f) : new Color(0.83f, 0.36f, 0.30f),
                pawn.transform);
            body.transform.localPosition = Vector3.up;
            figure.Root = pawn;
            figure.Body = body.transform;
            var label = new GameObject("Recorded name");
            label.layer = ReplayLayer;
            label.transform.SetParent(pawn.transform, false);
            label.transform.localPosition = new Vector3(0f, 2.35f, 0f);
            label.transform.rotation = _camera.transform.rotation;
            var text = label.AddComponent<TextMesh>();
            text.text = name ?? "";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 46;
            text.characterSize = 0.14f;
            text.color = Color.white;
            var r = label.GetComponent<MeshRenderer>();
            if (r != null) r.sortingOrder = 4;
            _figures.Add(key, figure);
            _all.Add(figure);
            return figure;
        }

        private void CollectActors()
        {
            // First, the recorded roster. This is an illustrative formation,
            // never a claim that the physical expedition had these positions.
            if (_data.Squad != null)
                foreach (var name in _data.Squad)
                    if (!string.IsNullOrEmpty(name))
                        Ensure("", name, true);

            foreach (var frame in _data.Frames)
            {
                if (frame == null || frame.Kind != ExpeditionReplayKind.CombatAction)
                    continue;
                bool friendly = _data.Squad != null && _data.Squad.Contains(frame.Actor);
                Ensure(frame.ActorId, frame.Actor, friendly);
                bool targetFriendly = _data.Squad != null && _data.Squad.Contains(frame.TargetName);
                if (!string.IsNullOrEmpty(frame.TargetName))
                    Ensure(frame.Target, frame.TargetName, targetFriendly);
            }

            int ally = 0, enemy = 0;
            foreach (var figure in _all)
            {
                int index = figure.Friendly ? ally++ : enemy++;
                float z = figure.Friendly ? -4.6f : 4.6f;
                float x = ((index % 7) - 3) * 2.3f;
                z += (index / 7) * (figure.Friendly ? -2.4f : 2.4f);
                figure.Home = new Vector3(x, 0f, z);
                figure.Root.transform.localPosition = figure.Home;
            }
        }

        private GameObject MakePrimitive(string name, PrimitiveType primitive,
            Vector3 position, Vector3 size, Color color, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            go.layer = ReplayLayer;
            go.transform.SetParent(parent != null ? parent : _root.transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            var collider = go.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                renderer.SetPropertyBlock(block);
            }
            return go;
        }

        private void ResetFigures()
        {
            foreach (var f in _all)
            {
                f.Fallen = false; f.Escaped = false;
                f.Root.SetActive(true);
                f.Root.transform.localPosition = f.Home;
                f.Root.transform.localRotation = Quaternion.identity;
                f.Body.localPosition = Vector3.up;
                f.Body.localScale = new Vector3(0.85f, 1f, 0.85f);
            }
        }

        private void ApplyPast(ExpeditionReplayFrame frame)
        {
            if (frame == null || frame.Kind != ExpeditionReplayKind.CombatAction) return;
            var actor = Find(frame.ActorId, frame.Actor);
            if (frame.ActionCode == "Flee" && frame.Resolved && actor != null)
                actor.Escaped = true;
            if (frame.TargetKilled)
            {
                var target = Find(frame.Target, frame.TargetName);
                if (target != null) target.Fallen = true;
            }
        }

        private void RestoreFates()
        {
            foreach (var f in _all)
            {
                if (f.Escaped) { f.Root.SetActive(false); continue; }
                if (f.Fallen)
                {
                    f.Body.localPosition = new Vector3(0f, 0.18f, 0f);
                    f.Body.localScale = new Vector3(1.2f, 0.16f, 0.85f);
                }
            }
        }

        // Seeking always rebuilds state from the actual chronological record.
        // Skips, pauses, reverse and speed changes cannot reroll AOS actions.
        public void Show(int index, float fraction)
        {
            if (_root == null || _data.Frames == null || _data.Frames.Count == 0)
                return;
            index = Mathf.Clamp(index, 0, _data.Frames.Count - 1);
            if (_shown != index)
            {
                ResetFigures();
                for (int i = 0; i < index; i++) ApplyPast(_data.Frames[i]);
                RestoreFates();
                _shown = index;
            }
            var frame = _data.Frames[index];
            if (frame == null) return;
            fraction = Mathf.Clamp01(fraction);
            foreach (var f in _all)
                if (!f.Fallen && !f.Escaped)
                {
                    f.Root.transform.localPosition = f.Home;
                    f.Root.transform.localRotation = Quaternion.identity;
                    f.Body.localScale = new Vector3(0.85f, 1f, 0.85f);
                }

            if (frame.Kind != ExpeditionReplayKind.CombatAction) return;
            var actor = Find(frame.ActorId, frame.Actor);
            var target = Find(frame.Target, frame.TargetName);
            if (actor == null || actor.Escaped || actor.Fallen) return;
            if (target != null && !target.Escaped && frame.ActionCode != "Idle")
            {
                Vector3 delta = target.Home - actor.Home;
                Vector3 close = target.Home - delta.normalized * 1.45f;
                float approach = fraction < 0.5f ? fraction * 2f : (1f - fraction) * 2f;
                actor.Root.transform.localPosition = Vector3.Lerp(actor.Home, close,
                    Mathf.SmoothStep(0f, 1f, approach) * 0.85f);
                if (delta.sqrMagnitude > 0.001f)
                    actor.Root.transform.rotation = Quaternion.LookRotation(delta.normalized);
                // Re-enact the recorded target's reaction, not invented damage.
                if (frame.Resolved && fraction > 0.38f && fraction < 0.70f)
                    target.Body.localScale = new Vector3(1.05f, 0.86f, 1.05f);
            }
            if (frame.ActionCode == "Flee" && frame.Resolved)
                actor.Root.transform.localPosition = actor.Home +
                    new Vector3(0f, 0f, actor.Friendly ? -12f : 12f) * fraction;
            if (frame.TargetKilled && target != null && fraction > 0.75f)
            {
                target.Body.localPosition = new Vector3(0f, 0.18f, 0f);
                target.Body.localScale = new Vector3(1.2f, 0.16f, 0.85f);
            }
        }

        public void Dispose()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_texture != null)
            {
                _texture.Release();
                UnityEngine.Object.Destroy(_texture);
                _texture = null;
            }
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null;
        }
    }
}
