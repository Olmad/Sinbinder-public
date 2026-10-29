// Assets/Scripts/Editor/TrailerShoot.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Sinbinder.Dev;
using Sinbinder.Gameplay;

namespace Sinbinder.Utilets
{
    /// <summary>
    /// Трейлер демо: лагерь ночью — огонь, Ждан с балалайкой, пролёт ворона, лица, Греховод (автор, 29.10:
    /// «попробуй сам снять трейлер»; «обязательно должен быть пролёт ворона»). Планы стоят по музыке
    /// Tools/music/trailer.py (темп 76): охота с 25 с, удар на 44,2 с, «Путь» с 47,4, слияние с 60, последний
    /// удар — 75,8 с. Кадры — в D:\Claude\трейлер\кадры, клеит Tools/trailer/cut.py.
    ///
    ///     Unity -batchmode -projectPath … -executeMethod Sinbinder.Utilets.TrailerShoot.CampBatch
    /// </summary>
    [InitializeOnLoad]
    public static class TrailerShoot
    {
        private const string Folder = "D:/Claude/трейлер/кадры";
        private const string Active = "Sinbinder.TrailerShoot.Active";

        // Вход в Play перезагружает домен: подписка из CampBatch не доживает — подписаться снова (как прогоны).
        static TrailerShoot()
        {
            if (SessionState.GetBool(Active, false)) EditorApplication.update += Tick;
        }
        private static float _since = -1f;
        private static bool _hour;
        private static TrailerCam _rig;

        public static void CampBatch()
        {
            EditorUtility.audioMasterMute = true;
            SessionState.SetBool(Active, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Prologue_Camp.unity");
            EditorApplication.isPlaying = true;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying) return;
            if (_rig != null)
            {
                if (_rig.Finished) { SessionState.SetBool(Active, false); EditorApplication.Exit(0); }
                return;
            }
            // Уроки держат мир стоп-кадром — в ролике мир живёт.
            Lesson.Switch = false;
            CampLife.Enabled = true;
            if (!SinbinderPlayer.Exists) return;
            UI.StartPanel.ChooseFresh();
            if (UI.StartPanel.Waiting || UI.PrologueTitleUI.Showing) return;
            var pause = Core.GamePauseController.Instance;
            if (pause != null && (pause.IsPaused || pause.Halted)) return;
            // Лагерю — время встать по местам, Ждану — его час.
            // Ждан — сразу на бревне с балалайкой (SitNow), за 5 с до съёмки: в дубле 1 он к своему плану ещё шёл.
            if (_since < 0f) _since = Time.realtimeSinceStartup;
            float settled = Time.realtimeSinceStartup - _since;
            if (!_hour && settled >= 20f) { _hour = true; var z = Zhdan(); if (z != null) BalalaikaHabit.SitNow(z); }
            if (settled < 25f) return;

            var go = new GameObject("Съёмка трейлера");
            _rig = go.AddComponent<TrailerCam>();
            _rig.Folder = Folder;
            _rig.Shots = Camp();
            Debug.Log($"[ТРЕЙЛЕР] планов {_rig.Shots.Count}, {_rig.Shots.Sum(s => s.Seconds):F1} с");
        }


        private static Warrior Named(string part)
        {
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                if (w.DisplayName != null && w.DisplayName.Contains(part) && !w.IsDead) return w;
            return null;
        }

        private static Warrior Zhdan() => Named("Ждан");

        private static Vector3 Head(Component w) => w == null ? Vector3.up : w.transform.position + Vector3.up * 1.05f;

        private static Transform Raven()
        {
            var r = Object.FindFirstObjectByType<RavenFlight>();
            return r != null ? r.transform : null;
        }

        private static Vector3 Torch()
        {
            var t = GameObject.Find("Факел у дороги");
            return t != null ? t.transform.position : new Vector3(-8f, 0f, -12f);
        }

        private static Light _rim;

        /// <summary>Холодный контровой свет у ворона: чёрная птица на ночном небе иначе не видна (дубль 1).</summary>
        private static void Rim(Transform bird, Vector3 camera)
        {
            if (_rim == null)
            {
                _rim = new GameObject("свет ворона").AddComponent<Light>();
                _rim.type = LightType.Point; _rim.color = new Color(0.62f, 0.72f, 1f); _rim.range = 4f; _rim.shadows = LightShadows.None;
            }
            _rim.intensity = bird != null ? 2.2f : 0f;
            if (bird != null) _rim.transform.position = bird.position + (bird.position - camera).normalized * 1.2f + Vector3.up * 0.8f;
        }

        /// <summary>
        /// Поставить воина и держать: ноги выключены, сам стоит, где сказано, лицом куда сказано. В дубле 1
        /// Греховода переставили в начале — и он ушёл к своей палатке: в конце ролика камера сняла холм.
        /// </summary>
        private static void Pin(Warrior w, Vector3 at, Vector3 face)
        {
            if (w == null) return;
            if (UnityEngine.AI.NavMesh.SamplePosition(at, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas)) at = hit.position;
            var legs = w.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (legs != null && legs.enabled) { if (legs.isOnNavMesh) legs.ResetPath(); legs.enabled = false; }
            var look = face - at; look.y = 0f;
            w.transform.SetPositionAndRotation(at, Quaternion.LookRotation(look.sqrMagnitude > 1e-4f ? look : Vector3.forward));
        }

        private static List<TrailerCam.Shot> Camp()
        {
            var greh = Vector3.zero + new Vector3(0.6f, 0f, -3.4f);
            var fire = Vector3.zero;
            var list = new List<TrailerCam.Shot>();
            void Add(string name, float seconds, System.Func<float, (Vector3, Vector3, float)> cam, System.Action begin = null)
                => list.Add(new TrailerCam.Shot { Name = name, Seconds = seconds, Camera = cam, Begin = begin });

            // ---- Костёр, 0–13 с: сверху вниз к огню, потом кругом у огня.
            Add("сверху к огню", 7f, t => (TrailerCam.Glide(fire + new Vector3(-6f, 22f, -24f), fire + new Vector3(-1.5f, 2.6f, -7f), t),
                                           fire + Vector3.up * 0.6f, 40f), () => Rim(null, Vector3.zero));
            Add("кругом у огня", 6f, t => (TrailerCam.Orbit(fire, 235f + t * 40f, 3.6f, 1.0f) + TrailerCam.Breath(t * 6f, 0.04f),
                                           fire + Vector3.up * 0.7f, 38f));
            Add("Ждан с балалайкой", 4f, t =>
            {
                var z = Zhdan();
                if (z == null) return (TrailerCam.Orbit(fire, 120f, 2.6f, 0.9f), fire, 34f);
                // Шире, чем в дубле 1 (балалайка закрывала весь кадр): три с лишним метра, сбоку, огонь — в кадре.
                var from = z.transform.position + z.transform.forward * (3.4f - t * 0.4f) + z.transform.right * 1.6f + Vector3.up * 1.2f;
                return (from, z.transform.position + Vector3.up * 0.65f - z.transform.right * 0.6f, 36f);
            });
            // ---- Пролёт ворона сбоку, 17–25 с.
            Add("ворон сбоку", 8f, t =>
            {
                var r = Raven();
                if (r == null) return (TrailerCam.Orbit(fire, 300f, 8f, 4f), fire, 40f);
                // Вдвое ближе дубля 1 (птица была с ноготь): 1,7 м сбоку, поле уже.
                var cam = r.position + r.right * 1.7f + Vector3.up * 0.25f - r.forward * 0.4f;
                Rim(r, cam);
                return (cam, r.position + r.forward * 0.3f, 38f);
            }, () => RavenFlight.LaunchAny());

            // ---- Охота, 25–44,2 с: короткие планы, ниже и ближе.
            Add("из леса", 4f, t => (TrailerCam.Glide(new Vector3(-30f, 1.6f, -24f), new Vector3(-20f, 1.5f, -15f), t) + TrailerCam.Breath(t * 4f, 0.06f),
                                     fire + Vector3.up * 1.4f, 40f), () => Rim(null, Vector3.zero));
            Add("факел", 3f, t =>
            {
                var c = Torch() + Vector3.up * 1.85f;
                return (TrailerCam.Glide(c + new Vector3(2.6f, 0.1f, -2.2f), c + new Vector3(2.0f, 0.2f, -1.7f), t), c, 30f);
            });
            Add("лица у огня", 4f, t => (TrailerCam.Orbit(fire, 80f + t * 30f, 4.6f, 1.25f), fire + Vector3.up, 30f));
            Add("Карган", 3.2f, t =>
            {
                var k = Named("Карган");
                if (k == null) return (TrailerCam.Orbit(fire, 20f, 4f, 1.4f), fire, 30f);
                return (Head(k) + k.transform.forward * (2.2f - t * 0.6f) + Vector3.up * 0.1f, Head(k), 30f);
            });
            Add("палатки", 2.5f, t => (TrailerCam.Orbit(fire, 150f + t * 15f, 12f, 2.2f), fire + Vector3.up, 40f));
            Add("вверх перед ударом", 2.5f, t => (TrailerCam.Glide(fire + new Vector3(0f, 1.6f, -3.5f), fire + new Vector3(0f, 13f, -8f), t), fire, 45f));

            // ---- Удар, 44,2–47,2: лагерь сверху.
            Add("лагерь сверху", 3f, t => (TrailerCam.Orbit(fire, 260f + t * 6f, 6f, 30f), fire, 50f));
            // ---- Путь, 47,2–59,8: облёт лагеря.
            bool launched = false;
            Add("облёт лагеря", 12.6f, t =>
            {
                // Ворону — взлететь заранее: к своему плану он уже в воздухе (в дубле 1 сидел на плече у Каргана).
                if (!launched && t > 0.8f) { launched = true; RavenFlight.Launch(Named("Карган")); }
                return (TrailerCam.Orbit(fire, 200f + t * 100f, 26f, 13f), fire + Vector3.up, 42f);
            });
            // ---- Слияние, 59,8–71,8: ворон сзади — и к дороге.
            Add("ворон сзади", 7f, t =>
            {
                var r = Raven();
                if (r == null) return (TrailerCam.Orbit(fire, 40f, 14f, 7f), fire, 45f);
                var cam = r.position - r.forward * 2.2f + Vector3.up * 0.7f;
                Rim(r, cam);
                return (cam, r.position + r.forward * 5f, 48f);
            });
            Add("к дороге", 5f, t =>
            {
                Rim(null, Vector3.zero);
                var c = Torch();
                return (TrailerCam.Glide(fire + new Vector3(0f, 8f, -5f), c + new Vector3(0f, 4f, -6f), t), c + Vector3.up * 1.5f, 42f);
            });
            // ---- Конец, 71,8–75,8: лицо Греховода — ближе и ближе; на последнем ударе — тьма (её кладёт cut.py).
            Add("Греховод", 3.2f, t =>
            {
                var p = SinbinderPlayer.Instance;
                Pin(p, greh, fire);
                return (Head(p) + Vector3.up * 0.35f + p.transform.forward * (3f - t * 1.7f), Head(p) + Vector3.up * 0.35f, 28f);
            });
            Add("глаза", 0.8f, t =>
            {
                var p = SinbinderPlayer.Instance;
                Pin(p, greh, fire);
                return (Head(p) + Vector3.up * 0.4f + p.transform.forward * (1.1f - t * 0.2f), Head(p) + Vector3.up * 0.4f, 26f);
            });
            return list;
        }
    }
}
