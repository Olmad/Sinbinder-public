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
            // Час Ждана — за 13 с до съёмки: к его плану (13–17 с) он дошёл и сел, а час (35 с) ещё не кончился.
            if (_since < 0f) _since = Time.realtimeSinceStartup;
            float settled = Time.realtimeSinceStartup - _since;
            if (!_hour && settled >= 12f) { _hour = true; var z = Zhdan(); if (z != null) BalalaikaHabit.HourNow(z); }
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

        private static List<TrailerCam.Shot> Camp()
        {
            var fire = Vector3.zero;
            var list = new List<TrailerCam.Shot>();
            void Add(string name, float seconds, System.Func<float, (Vector3, Vector3, float)> cam, System.Action begin = null)
                => list.Add(new TrailerCam.Shot { Name = name, Seconds = seconds, Camera = cam, Begin = begin });

            // ---- Костёр, 0–13 с: сверху вниз к огню, потом кругом у огня.
            Add("сверху к огню", 7f, t => (TrailerCam.Glide(fire + new Vector3(-6f, 22f, -24f), fire + new Vector3(-1.5f, 2.6f, -7f), t),
                                           fire + Vector3.up * 0.6f, 40f),
                () =>
                {
                    // Греховод — к огню, лицом к нему: его лицо — в конце ролика.
                    var p = SinbinderPlayer.Instance;
                    var at = fire + new Vector3(0.6f, 0f, -3.4f);
                    if (UnityEngine.AI.NavMesh.SamplePosition(at, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas)) at = hit.position;
                    var legs = p.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (legs != null && legs.isOnNavMesh) { legs.Warp(at); legs.ResetPath(); }
                    p.transform.SetPositionAndRotation(at, Quaternion.LookRotation(fire - at));
                });
            Add("кругом у огня", 6f, t => (TrailerCam.Orbit(fire, 235f + t * 40f, 3.6f, 1.0f) + TrailerCam.Breath(t * 6f, 0.04f),
                                           fire + Vector3.up * 0.7f, 38f));
            Add("Ждан с балалайкой", 4f, t =>
            {
                var z = Zhdan();
                if (z == null) return (TrailerCam.Orbit(fire, 120f, 2.6f, 0.9f), fire, 34f);
                var from = z.transform.position + z.transform.forward * (1.9f - t * 0.35f) + z.transform.right * 0.6f + Vector3.up * 0.95f;
                return (from, z.transform.position + Vector3.up * 0.7f, 32f);
            });
            // ---- Пролёт ворона сбоку, 17–25 с.
            Add("ворон сбоку", 8f, t =>
            {
                var r = Raven();
                if (r == null) return (TrailerCam.Orbit(fire, 300f, 8f, 4f), fire, 40f);
                return (r.position + r.right * 3.2f + Vector3.up * 0.4f - r.forward * 0.8f, r.position + r.forward * 0.6f, 45f);
            }, () => RavenFlight.LaunchAny());

            // ---- Охота, 25–44,2 с: короткие планы, ниже и ближе.
            Add("из леса", 4f, t => (TrailerCam.Glide(new Vector3(-34f, 0.9f, -26f), new Vector3(-24f, 1.0f, -18f), t) + TrailerCam.Breath(t * 4f, 0.06f),
                                     fire + Vector3.up, 45f));
            Add("факел", 3f, t =>
            {
                var c = Torch() + Vector3.up * 1.85f;
                return (TrailerCam.Glide(c + new Vector3(1.4f, -0.1f, -1.2f), c + new Vector3(0.9f, 0.0f, -0.8f), t), c, 35f);
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
            Add("облёт лагеря", 12.6f, t => (TrailerCam.Orbit(fire, 200f + t * 100f, 26f, 13f), fire + Vector3.up, 42f));
            // ---- Слияние, 59,8–71,8: ворон сзади — и к дороге.
            Add("ворон сзади", 7f, t =>
            {
                var r = Raven();
                if (r == null) return (TrailerCam.Orbit(fire, 40f, 14f, 7f), fire, 45f);
                return (r.position - r.forward * 3f + Vector3.up * 1.0f, r.position + r.forward * 4f, 50f);
            }, () => RavenFlight.LaunchAny());
            Add("к дороге", 5f, t =>
            {
                var c = Torch();
                return (TrailerCam.Glide(fire + new Vector3(0f, 8f, -5f), c + new Vector3(0f, 4f, -6f), t), c + Vector3.up * 1.5f, 42f);
            });
            // ---- Конец, 71,8–75,8: лицо Греховода — ближе и ближе; на последнем ударе — тьма (её кладёт cut.py).
            Add("Греховод", 3.2f, t =>
            {
                var p = SinbinderPlayer.Instance;
                return (Head(p) + Vector3.up * 0.35f + p.transform.forward * (3f - t * 1.7f), Head(p) + Vector3.up * 0.35f, 28f);
            });
            Add("глаза", 0.8f, t =>
            {
                var p = SinbinderPlayer.Instance;
                return (Head(p) + Vector3.up * 0.4f + p.transform.forward * (1.1f - t * 0.2f), Head(p) + Vector3.up * 0.4f, 26f);
            });
            return list;
        }
    }
}
