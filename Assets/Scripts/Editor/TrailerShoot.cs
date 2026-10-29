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
            // Тело — видно: от первого лица RTS_Camera прячет Греховода (ShowHero), и в дубле 2 камера,
            // глядя ему в лицо, сняла холм за ним.
            foreach (var r in w.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        }

        /// <summary>Середина живых охотников (n — сколько их); нет — огонь.</summary>
        private static Vector3 Enemies(out int n)
        {
            n = 0; var sum = Vector3.zero;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                if (w.Team == Team.Enemy && !w.IsDead) { sum += w.transform.position; n++; }
            return n == 0 ? Vector3.zero : sum / n;
        }

        private static Warrior Nearest(Team team, Vector3 to)
        {
            Warrior best = null; float d = float.MaxValue;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w.Team != team || w.IsDead || w is SinbinderPlayer) continue;
                float k = (w.transform.position - to).sqrMagnitude;
                if (k < d) { d = k; best = w; }
            }
            return best;
        }

        /// <summary>Где бьются: между охотником, ближним к огню, и ближним к нему своим; набега нет — огонь.</summary>
        private static Vector3 Fight()
        {
            var e = Nearest(Team.Enemy, Vector3.zero);
            if (e == null) return Vector3.zero;
            var ally = Nearest(Team.Player, e.transform.position);
            var at = ally != null && (ally.transform.position - e.transform.position).sqrMagnitude < 100f
                   ? (e.transform.position + ally.transform.position) * 0.5f : e.transform.position;
            // Сглаживание: середина схватки прыгает, когда сменяется ближний, — камера за ней тянется, не дёргается.
            _fight = _fightSet ? Vector3.Lerp(_fight, at, 0.06f) : at;
            _fightSet = true;
            return _fight;
        }

        private static Vector3 _fight;
        private static bool _fightSet;

        /// <summary>
        /// Набег — сейчас, без строки на чёрном: пока она на экране, мир стоит, а в ролике её не видно (холсты
        /// выключены) — вышло бы три секунды застывшего кадра.
        /// </summary>
        private static void Raid()
        {
            var title = Object.FindFirstObjectByType<UI.PrologueTitleUI>();
            if (title != null) title.gameObject.SetActive(false);
            var director = Object.FindFirstObjectByType<PrologueDirector>();
            Debug.Log($"[ТРЕЙЛЕР] набег: {(RaidEvent.Stage(director) ? "развёрнут" : "НЕ развёрнут")}");
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
                                           fire + Vector3.up * 0.6f, 40f), () => { Rim(null, Vector3.zero); Damageable.Untouchable = SinbinderPlayer.Instance.GetComponent<Damageable>(); });
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
            bool raided = false;
            Add("ворон сбоку", 8f, t =>
            {
                var r = Raven();
                if (r == null) return (TrailerCam.Orbit(fire, 300f, 8f, 4f), fire, 40f);
                if (!raided && t > 0.3f) { raided = true; Raid(); }
                // Вдвое ближе дубля 1 (птица была с ноготь): 1,7 м сбоку, поле уже.
                var cam = r.position + r.right * 1.7f + Vector3.up * 0.25f - r.forward * 0.4f;
                Rim(r, cam);
                return (cam, r.position + r.forward * 0.3f, 38f);
            }, () => RavenFlight.LaunchAny());

            // ---- Охота, 25–44,2 с: набег (автор, 29.10, дубль 3: «а где нападение?»). Охотники вышли ещё под
            //      вороном (Raid); здесь — они, схватка у огня, Карган в бою, охотник крупно.
            Add("охотники выходят", 4f, t =>
            {
                var c = Enemies(out int n);
                if (n == 0) return (TrailerCam.Glide(new Vector3(-30f, 1.6f, -24f), new Vector3(-20f, 1.5f, -15f), t), fire + Vector3.up * 1.4f, 40f);
                var back = c - fire; back.y = 0f; back = back.normalized;
                return (c + back * (4.5f - t * 1.2f) + Vector3.up * 1.3f + Vector3.Cross(Vector3.up, back) * 1.2f, fire + Vector3.up * 1.2f, 40f);
            }, () => Rim(null, Vector3.zero));
            Add("факел", 3f, t =>
            {
                var c = Torch() + Vector3.up * 1.85f;
                return (TrailerCam.Glide(c + new Vector3(2.6f, 0.1f, -2.2f), c + new Vector3(2.0f, 0.2f, -1.7f), t), c, 30f);
            });
            Add("схватка", 4f, t =>
            {
                var f = Fight();
                return (TrailerCam.Orbit(f, 30f + t * 45f, 3.4f, 1.3f) + TrailerCam.Breath(t * 4f, 0.05f), f + Vector3.up * 0.9f, 36f);
            });
            Add("Карган в бою", 3.2f, t =>
            {
                var k = Named("Карган");
                if (k == null) { var f = Fight(); return (TrailerCam.Orbit(f, 200f, 3f, 1.4f), f + Vector3.up, 32f); }
                return (Head(k) + k.transform.forward * (2.4f - t * 0.6f) + k.transform.right * 0.5f + Vector3.up * 0.1f, Head(k), 32f);
            });
            Add("охотник крупно", 2.5f, t =>
            {
                var e = Nearest(Team.Enemy, fire);
                if (e == null) return (TrailerCam.Orbit(fire, 150f, 6f, 1.5f), fire + Vector3.up, 40f);
                return (Head(e) + e.transform.forward * (2.0f - t * 0.4f) - e.transform.right * 0.4f, Head(e), 30f);
            });
            Add("вверх перед ударом", 2.5f, t =>
            {
                var f = Fight();
                return (TrailerCam.Glide(f + new Vector3(0f, 1.6f, -3.5f), f + new Vector3(0f, 13f, -8f), t), f, 45f);
            });

            // ---- Удар, 44,2–47,2: камера падает сверху в схватку — не стоит, как в дубле 3 («в середине всё замирает»).
            Add("удар — в схватку", 3f, t =>
            {
                var f = Fight();
                float k = 1f - (1f - t) * (1f - t) * (1f - t);   // резко в начале, мягко в конце
                return (Vector3.Lerp(f + new Vector3(0f, 16f, -7f), f + new Vector3(1.5f, 2.2f, -3.2f), k), f + Vector3.up * 0.8f, 44f);
            });
            // ---- Путь, 47,2–59,8: бой сбоку проездом, Греховод в бою, ворон над боем.
            Add("бой сбоку", 4.6f, t =>
            {
                var f = Fight();
                var side = new Vector3(1f, 0f, 0.35f).normalized;
                return (f + side * (-5f + t * 10f) + new Vector3(0f, 1.4f, -5f), f + Vector3.up * 0.9f, 38f);
            });
            Add("Греховод в бою", 4f, t =>
            {
                var p = SinbinderPlayer.Instance;
                foreach (var r in p.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                return (Head(p) - p.transform.forward * (3f - t * 0.6f) + p.transform.right * 0.9f + Vector3.up * 0.7f,
                        Head(p) + p.transform.forward * 6f, 44f);
            });
            Add("ворон над боем", 4f, t =>
            {
                var r = Raven();
                if (r == null) { var f = Fight(); return (TrailerCam.Orbit(f, 90f + t * 30f, 10f, 6f), f, 42f); }
                var cam = r.position + r.right * 1.8f + Vector3.up * 0.3f - r.forward * 0.4f;
                Rim(r, cam);
                return (cam, r.position + r.forward * 0.3f, 40f);
            }, () => RavenFlight.Launch(Named("Карган")));
            // ---- Слияние, 59,8–71,8: ворон сзади — и вторая волна, широко.
            Add("ворон сзади", 6f, t =>
            {
                var r = Raven();
                if (r == null) return (TrailerCam.Orbit(fire, 40f, 14f, 7f), fire, 45f);
                var cam = r.position - r.forward * 2.2f + Vector3.up * 0.35f;
                Rim(r, cam);
                return (cam, r.position + r.forward * 6f + Vector3.up * 0.6f, 48f);
            });
            Add("вторая волна", 6f, t =>
            {
                Rim(null, Vector3.zero);
                var c = Enemies(out int n);
                if (n == 0) c = Fight();
                return (TrailerCam.Orbit(c, 250f + t * 35f, 11f, 3.2f), c + Vector3.up, 40f);
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
