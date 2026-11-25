// Assets/Scripts/Editor/TrailerShorts.cs
// Перевод: текст через Loc
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Sinbinder.Dev;
using Sinbinder.Gameplay;

namespace Sinbinder.Utilets
{
    /// <summary>
    /// Вертикальные Shorts 1080×1920 (автор, 24.11: «Можешь ли ты снять 5 эпичных вертикальных Shorts?»). Снимает
    /// та же <see cref="TrailerCam"/>, что и трейлер: игровое время — ровно 1/30 с на кадр. Отличие — очередь
    /// роликов в одной игре и выход без закрытия Unity: снимает верстак (Editor/Workbench, «film»).
    ///
    ///     film лагерь — Prologue_Camp: 1 огни в шаре, 2 ворон, 3 набег, 4 Греховод в бою (по порядку: набег
    ///                   начинается под вороном ролика 2, бой ролика 4 — продолжение набега ролика 3)
    ///     film поле   — Tug/Field: 5 «Перетягивание»
    ///     film 1 3    — переснять отдельные
    ///
    /// Кадры — D:\Claude\Shorts\кадры\&lt;ролик&gt;; клеит Tools/trailer/shorts.py, каждый ролик кончается на ударе
    /// музыки трейлера (Tools/music/trailer.py) — там встаёт заставка.
    /// </summary>
    public static partial class TrailerShoot
    {
        public const int ShortW = 1080, ShortH = 1920;
        private const string ShortsFolder = "D:/Claude/Shorts/кадры";

        private static Queue<KeyValuePair<string, Func<List<TrailerCam.Shot>>>> _shorts;
        private static TrailerCam _short;
        private static string _shortName;
        private static float _shortsSince = -1f;
        private static bool _shortsHour;
        private static string _shortsDone = "";

        /// <summary>Идёт ли съёмка Shorts (верстак ждёт, пока не кончится).</summary>
        public static bool ShortsBusy => _shorts != null;

        /// <summary>Что снято — ответ верстаку.</summary>
        public static string ShortsReport { get; private set; } = "";

        public static void Film(string set)
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("сначала play");
            if (_shorts != null) throw new InvalidOperationException("съёмка уже идёт");
            string which = set == "лагерь" ? "1234" : set == "поле" ? "5" : set;
            var queue = new Queue<KeyValuePair<string, Func<List<TrailerCam.Shot>>>>();
            foreach (char c in which)
            {
                switch (c)
                {
                    case '1': queue.Enqueue(new("1 Огни в шаре", BallShort)); break;
                    case '2': queue.Enqueue(new("2 Ворон над лагерем", RavenShort)); break;
                    case '3': queue.Enqueue(new("3 Набег", RaidShort)); break;
                    case '4': queue.Enqueue(new("4 Греховод в бою", HeroShort)); break;
                    case '5': queue.Enqueue(new("5 Перетягивание", TugShort)); break;
                    case ' ': case ',': break;
                    default: throw new ArgumentException("film лагерь | поле | номера 1…5, а не «" + set + "»");
                }
            }
            if (queue.Count == 0) throw new ArgumentException("нечего снимать");
            EditorUtility.audioMasterMute = true;
            _shorts = queue;
            _shortsDone = "";
            _shortsSince = -1f;
            _shortsHour = false;
            EditorApplication.update -= FilmTick;
            EditorApplication.update += FilmTick;
        }

        private static void FilmTick()
        {
            if (_shorts == null) { EditorApplication.update -= FilmTick; return; }
            if (!EditorApplication.isPlaying) { EndShorts("игра остановилась"); return; }
            if (_short != null)
            {
                HideWorldLabels();
                if (!_short.Finished) return;
                _shortsDone += $"\n{_shortName}: {_short.Frames} кадров ({_short.Frames / (float)TrailerCam.Fps:F1} с)";
                UnityEngine.Object.Destroy(_short.gameObject);
                _short = null;
                if (_shorts.Count == 0) { EndShorts(null); return; }
            }

            // Как трейлер (Tick): уроки не держат мир, лагерь живёт, лагерю — 25 с встать по местам, Ждану — на бревно.
            Lesson.Switch = false;
            CampLife.Enabled = true;
            if (!SinbinderPlayer.Exists) return;
            UI.StartPanel.ChooseFresh();
            if (UI.StartPanel.Waiting || UI.PrologueTitleUI.Showing) return;
            var pause = Core.GamePauseController.Instance;
            if (pause != null && (pause.IsPaused || pause.Halted)) return;
            if (_shortsSince < 0f) _shortsSince = Time.realtimeSinceStartup;
            float settled = Time.realtimeSinceStartup - _shortsSince;
            if (!_shortsHour && settled >= 20f) { _shortsHour = true; var z = Zhdan(); if (z != null) BalalaikaHabit.SitNow(z); }
            if (settled < 25f) return;
            // Туман — и в лагере (FogOfWar ставится на любую землю): неразведанная земля чёрная (полоса вверху
            // кадра), охотники в нём спрятаны. В ролике видно всё.
            FogOfWar.ShotClear(true);
            FogOfWar.Filming = true;

            var next = _shorts.Dequeue();
            _shortName = next.Key;
            _short = new GameObject("Съёмка Shorts — " + next.Key).AddComponent<TrailerCam>();
            _short.Width = ShortW;
            _short.Height = ShortH;
            _short.Folder = ShortsFolder + "/" + next.Key;
            _short.Shots = next.Value();
            Debug.Log($"[SHORTS] «{next.Key}»: планов {_short.Shots.Count}");
        }

        /// <summary>Надписи в мире («Мешок» над павшим, FallenBag) — не для ролика; новые появляются в бою, гасим каждый тик.</summary>
        private static void HideWorldLabels()
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            {
                var r = t.GetComponent<MeshRenderer>();
                if (r != null && r.enabled) r.enabled = false;
            }
        }

        private static void EndShorts(string failure)
        {
            if (_short != null) UnityEngine.Object.Destroy(_short.gameObject);
            _short = null;
            _shorts = null;
            Time.captureFramerate = 0;
            EditorApplication.update -= FilmTick;
            FogOfWar.Filming = false;
            FogOfWar.ShotClear(false);
            ShortsReport = (failure != null ? "ОШИБКА: " + failure : "снято в " + ShortsFolder) + _shortsDone;
        }

        // Вертикальный кадр уже горизонтального: поле зрения (по вертикали) шире трейлерного, иначе в кадр
        // влезает одна голова без плеч.
        private static float Tall(float fov) => fov * 1.25f;

        private static List<TrailerCam.Shot> Shots(out Action<string, float, Func<float, (Vector3, Vector3, float)>, Action> add)
        {
            var list = new List<TrailerCam.Shot>();
            add = (name, seconds, cam, begin) => list.Add(new TrailerCam.Shot { Name = name, Seconds = seconds, Camera = cam, Begin = begin });
            return list;
        }

        // ---------------------------------------------------------------- 1. Огни в шаре

        private const BindingFlags Inside = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static T BallField<T>(CrystalBall ball, string name)
        {
            var f = typeof(CrystalBall).GetField(name, Inside);
            return f != null ? (T)f.GetValue(ball) : default;
        }

        private static void BallState(CrystalBall ball, int lit, int dying, float k, float intensity, Color colour, bool red, bool showing = true)
        {
            typeof(CrystalBall).GetField("_showing", Inside)?.SetValue(ball, showing);
            typeof(CrystalBall).GetField("_lit", Inside)?.SetValue(ball, lit);
            typeof(CrystalBall).GetField("_dying", Inside)?.SetValue(ball, dying);
            typeof(CrystalBall).GetField("_dyingK", Inside)?.SetValue(ball, k);
            typeof(CrystalBall).GetProperty("IsAlarmed")?.GetSetMethod(true)?.Invoke(ball, new object[] { red });
            var glow = BallField<Light>(ball, "_glow");
            if (glow != null) { glow.color = colour; glow.intensity = intensity; }
        }

        /// <summary>После ролика 1 шар — снова спокойный, с двумя огнями (до совета), и дышит сам.</summary>
        private static void BallCalm()
        {
            var ball = UnityEngine.Object.FindFirstObjectByType<CrystalBall>();
            if (ball == null) return;
            BallState(ball, 2, -1, 0f, BallField<float>(ball, "_calmIntensity"), BallField<Color>(ball, "_calm"), false, showing: false);
        }

        /// <summary>
        /// Сцена, которую автор любит больше всех (21.10: «кат-сцена с гаснущими огоньками в сфере — очень крутая»):
        /// к шару на столе совета — три огня — шар краснеет — огни гаснут по одному, вспышкой и провалом, как в
        /// CrystalBall.Extinguish, но по кадрам: та считает настоящее время, а съёмка идёт медленнее жизни.
        /// Огонь костра — за шаром.
        /// </summary>
        private static List<TrailerCam.Shot> BallShort()
        {
            var list = Shots(out var add);
            var ball = UnityEngine.Object.FindFirstObjectByType<CrystalBall>();
            if (ball == null) { Debug.LogWarning("[SHORTS] шара в сцене нет"); return list; }
            Color calm = BallField<Color>(ball, "_calm"), alarm = BallField<Color>(ball, "_alarm");
            float calmI = BallField<float>(ball, "_calmIntensity"), alarmI = BallField<float>(ball, "_alarmIntensity");
            var b = ball.transform.position;
            var body = ball.GetComponent<Renderer>();
            float r = body != null ? Mathf.Max(0.05f, body.bounds.extents.y) : 0.15f;
            var away = b; away.y = 0f;   // костёр — в начале координат: камера за шаром, огонь — за ним в кадре
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
            Vector3 At(float dist, float up, float swing) => b + Quaternion.AngleAxis(swing, Vector3.up) * away * dist + Vector3.up * up;
            float Breath(float s) => 1f + 0.18f * Mathf.Sin(s * 0.6f * Mathf.PI * 2f);
            const int squads = 3;
            const float die = 1.5f, gap = 2.1f, beat = die + gap;

            add("к шару", 5f, t =>
            {
                BallState(ball, squads, -1, 0f, calmI * Breath(t * 5f), calm, false);
                return (TrailerCam.Glide(At(r * 40f, r * 26f, -14f), At(r * 13f, r * 4.5f, -4f), t), b + Vector3.up * r * 0.3f,
                        Mathf.Lerp(54f, 40f, TrailerCam.Ease(t)));
            }, null);
            add("три огня", 4f, t =>
            {
                BallState(ball, squads, -1, 0f, calmI * Breath(5f + t * 4f), calm, false);
                // Дубль 1: с 12 радиусов шар — с треть ширины кадра, огни — точки. Ближе и ниже: огни на фоне костра.
                return (At(r * (11f - t * 4f), r * (3.4f - t * 1.6f), -4f + t * 10f), b + Vector3.up * r * 0.3f, 36f);
            }, null);
            add("тревога", 2.5f, t =>
            {
                float k = TrailerCam.Ease(Mathf.Clamp01(t / 0.5f));
                BallState(ball, squads, -1, 0f, Mathf.Lerp(calmI, alarmI, k), Color.Lerp(calm, alarm, k), k > 0.5f);
                return (At(r * (7f - t * 0.8f), r * (1.8f - t * 0.3f), 6f + t * 2f), b + Vector3.up * r * 0.3f, 34f);
            }, null);
            add("гаснут", beat * squads, t =>
            {
                float s = t * beat * squads;
                int i = Mathf.Min(squads - 1, Mathf.FloorToInt(s / beat));
                float w = s - i * beat;
                int lit = squads - i;
                if (w < die)
                {
                    float k = w / die;
                    float level = k < 0.25f ? Mathf.Lerp(1f, 2.4f, k / 0.25f) : Mathf.Lerp(2.4f, 0.08f, (k - 0.25f) / 0.75f);
                    BallState(ball, lit, lit - 1, k, alarmI * level, alarm, true);
                }
                else BallState(ball, lit - 1, -1, 0f, alarmI * 0.08f, alarm, true);
                return (At(r * (6f - t * 1.1f), r * (1.4f - t * 0.3f), 8f + t * 3f) + TrailerCam.Breath(s, 0.004f),
                        b + Vector3.up * r * 0.3f, 34f);
            }, null);
            add("тьма", 1.2f, t =>
            {
                BallState(ball, 0, -1, 0f, alarmI * Mathf.Lerp(0.08f, 0.02f, t), alarm, true);
                return (At(r * 4.9f, r * 1.1f, 11f), b + Vector3.up * r * 0.3f, 34f);
            }, null);
            return list;
        }

        // ---------------------------------------------------------------- 2. Ворон над лагерем (0–25 с музыки)

        private static List<TrailerCam.Shot> RavenShort()
        {
            var list = Shots(out var add);
            var fire = Vector3.zero;
            add("сверху к огню", 7f, t => (TrailerCam.Glide(fire + new Vector3(-6f, 24f, -20f), fire + new Vector3(-1.4f, 2.4f, -6f), t),
                                           fire + Vector3.up * 0.7f, Mathf.Lerp(54f, 46f, t)),
                () => { BallCalm(); Rim(null, Vector3.zero); Damageable.Untouchable = SinbinderPlayer.Instance.GetComponent<Damageable>(); });
            // Снизу, у самых углей: пламя и искры уходят в ночное небо — вертикальный кадр для того и вертикальный.
            add("огонь снизу", 6f, t => (TrailerCam.Orbit(fire, 235f + t * 40f, 2.9f, 0.35f) + TrailerCam.Breath(t * 6f, 0.04f),
                                         fire + Vector3.up * (1.2f + t * 0.5f), 56f), null);
            add("Ждан с балалайкой", 4f, t =>
            {
                var z = Zhdan();
                if (z == null) return (TrailerCam.Orbit(fire, 120f, 2.6f, 0.9f), fire, Tall(34f));
                var from = z.transform.position + z.transform.forward * (3.4f - t * 0.4f) + z.transform.right * 1.6f + Vector3.up * 1.2f;
                return (from, z.transform.position + Vector3.up * 0.65f - z.transform.right * 0.6f, Tall(36f));
            }, () =>
            {
                // Дубль 1: за 25 с ролика 1 Ждан встал и ушёл к палатке — посадить на бревно в начале плана.
                var z = Zhdan();
                if (z != null) BalalaikaHabit.SitNow(z);
            });
            bool raided = false;
            // Дубль 2: Raven() — первый ворон в сцене, а он сидел на палатке; поднят другой. Камера — за поднятым.
            Transform bird = null;
            // Набег поднимается здесь, под вороном: в ролике 3 охотники уже у лагеря.
            add("ворон сбоку", 8f, t =>
            {
                var r = bird != null ? bird : Raven();
                if (r == null) return (TrailerCam.Orbit(fire, 300f, 8f, 4f), fire, Tall(40f));
                if (!raided && t > 0.3f) { raided = true; Raid(); }
                // Дубль 1: вровень с птицей — полкадра закрыла палатка. Дубль 2: снизу — на взлёте камера ушла
                // в полотно палатки (птица сидела на ней), дальше одно небо. Сверху-сзади: птица в верхней трети,
                // под ней лагерь и огонь.
                var flat = r.forward; flat.y = 0f;
                if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
                flat.Normalize();
                var side = Vector3.Cross(Vector3.up, flat);
                var cam = r.position - flat * (3.2f - t * 0.6f) + side * 1.4f + Vector3.up * 1.3f;
                Rim(r, cam);
                return (cam, r.position + flat * 2.5f - Vector3.up * 2.4f, Tall(44f));
            }, () =>
            {
                var up = RavenFlight.LaunchAny();
                bird = up != null ? up.transform : null;
            });
            return list;
        }

        // ---------------------------------------------------------------- 3. Набег (25–47,2 с: охота и удар)

        private static List<TrailerCam.Shot> RaidShort()
        {
            var list = Shots(out var add);
            var fire = Vector3.zero;
            add("охотники выходят", 4f, t =>
            {
                Engage();
                var c = Enemies(out int n);
                if (n == 0) return (TrailerCam.Glide(new Vector3(-30f, 1.6f, -24f), new Vector3(-20f, 1.5f, -15f), t), fire + Vector3.up * 1.4f, Tall(40f));
                var back = c - fire; back.y = 0f; back = back.normalized;
                return (c + back * (4.5f - t * 1.2f) + Vector3.up * 1.1f + Vector3.Cross(Vector3.up, back) * 1.2f, fire + Vector3.up * 1.4f, Tall(40f));
            }, () =>
            {
                Rim(null, Vector3.zero);
                // Переснимают один ролик 3 — набега ещё не было: поднять.
                Enemies(out int n);
                if (n == 0) Raid();
            });
            add("факел", 3f, t =>
            {
                var c = Torch() + Vector3.up * 1.85f;
                return (TrailerCam.Glide(c + new Vector3(2.6f, -0.4f, -2.2f), c + new Vector3(2.0f, -0.1f, -1.7f), t), c, Tall(30f));
            }, null);
            // Дубль 4: «схватка» у Fight() — пустая земля, а свет боя стоял там же, вдали от Каргана (он бился
            // в темноте). Схватка — это Карган и его ближний враг; свет — за ними.
            Vector3 Brawl()
            {
                var k = Named("Карган");
                if (k == null) return Fight();
                var e = Nearest(Team.Enemy, k.transform.position);
                var at = e != null && (e.transform.position - k.transform.position).sqrMagnitude < 64f
                    ? (k.transform.position + e.transform.position) * 0.5f : k.transform.position;
                if (_fightLight != null) _fightLight.transform.position = at + new Vector3(0.8f, 3.2f, -0.6f);
                return at;
            }
            add("схватка", 4f, t =>
            {
                Engage();
                var f = Brawl();
                return (TrailerCam.Orbit(f, 30f + t * 45f, 3.4f, 1.3f) + TrailerCam.Breath(t * 4f, 0.05f), f + Vector3.up * 0.9f, Tall(36f));
            }, null);
            add("Карган в бою", 3.2f, t =>
            {
                Engage();
                var k = Named("Карган");
                if (k == null) { var f = Fight(); return (TrailerCam.Orbit(f, 200f, 3f, 1.4f), f + Vector3.up, Tall(32f)); }
                return (Head(k) + k.transform.forward * (2.4f - t * 0.6f) + k.transform.right * 0.5f - Vector3.up * 0.2f, Head(k), Tall(32f));
            }, null);
            add("охотник крупно", 2.5f, t =>
            {
                Engage();
                var e = Nearest(Team.Enemy, fire);
                if (e == null) return (TrailerCam.Orbit(fire, 150f, 6f, 1.5f), fire + Vector3.up, Tall(40f));
                return (Head(e) + e.transform.forward * (2.0f - t * 0.4f) - e.transform.right * 0.4f - Vector3.up * 0.15f, Head(e), Tall(30f));
            }, null);
            // Дубль 1: середина схватки ушла в тёмное поле — удар пришёлся в пустую землю. Удар — в Каргана:
            // свои дерутся не дальше 12 м от огня (Engage), то есть на свету.
            Vector3 Kargan() => Brawl();
            add("вверх перед ударом", 2.5f, t =>
            {
                Engage();
                var f = Kargan();
                // Дубль 2: с 15 м сверху — чёрная земля. Подъём ниже, огонь лагеря остаётся в кадре.
                return (TrailerCam.Glide(f + new Vector3(0f, 1.6f, -3.5f), f + new Vector3(0f, 7f, -6.5f), t), f + Vector3.up, Tall(45f));
            }, null);
            add("удар — в схватку", 3f, t =>
            {
                Engage();
                var f = Kargan();
                float k = 1f - (1f - t) * (1f - t) * (1f - t);
                return (Vector3.Lerp(f + new Vector3(0f, 8f, -6.5f), f + new Vector3(1.4f, 1.5f, -3.0f), k), f + Vector3.up * 1.0f, Tall(44f));
            }, null);
            return list;
        }

        // ---------------------------------------------------------------- 4. Греховод в бою (47,2–75,8 с: путь и последний удар)

        private static List<TrailerCam.Shot> HeroShort()
        {
            var list = Shots(out var add);
            var fire = Vector3.zero;
            var greh = new Vector3(0.6f, 0f, -3.4f);
            // Дубль 1: середина схватки — в тёмном поле, ворон сел на скелета у белого пятна с надписью над телом,
            // вторая волна — чернота. Бой — вокруг Каргана: свои дерутся у огня (Engage), на свету.
            Vector3 Kargan()
            {
                var k = Named("Карган");
                return k != null ? k.transform.position : Fight();
            }
            add("бой у огня", 4.6f, t =>
            {
                Engage();
                var f = Kargan();
                var side = new Vector3(1f, 0f, 0.35f).normalized;
                return (f + side * (-3.5f + t * 7f) + new Vector3(0f, 1.1f, -4f), f + Vector3.up * 1.0f, Tall(38f));
            }, null);
            add("Греховод в бою", 4f, t =>
            {
                Engage();
                var p = SinbinderPlayer.Instance;
                Pin(p, greh, Fight());
                // Снизу вверх: владыка над схваткой.
                return (Head(p) + p.transform.forward * (3.0f - t * 0.8f) - p.transform.right * 1.0f - Vector3.up * 0.7f,
                        Head(p) + Vector3.up * 0.2f, Tall(34f));
            }, null);
            add("огонь и схватка", 5f, t =>
            {
                Engage();
                return (TrailerCam.Orbit(fire, 60f + t * 35f, 3.1f, 0.35f) + TrailerCam.Breath(t * 5f, 0.04f),
                        fire + Vector3.up * (1.3f + t * 0.4f), 56f);
            }, () => Rim(null, Vector3.zero));
            add("владыка у огня", 5f, t =>
            {
                Engage();
                var p = SinbinderPlayer.Instance;
                Pin(p, greh, fire);
                // Снизу, по кругу: над ним — ночь, на лице — огонь.
                var around = Quaternion.AngleAxis(-25f + t * 30f, Vector3.up) * p.transform.forward;
                return (Head(p) + around * 2.6f - Vector3.up * 1.0f, Head(p) + Vector3.up * 0.25f, Tall(36f));
            }, null);
            add("вторая волна", 6f, t =>
            {
                Engage();
                var c = Kargan();
                return (TrailerCam.Orbit(c, 250f + t * 35f, 6.5f, 2.4f), c + Vector3.up, Tall(40f));
            }, null);
            add("Греховод", 3.2f, t =>
            {
                var p = SinbinderPlayer.Instance;
                Pin(p, greh, fire);
                return (Head(p) + Vector3.up * 0.35f + p.transform.forward * (3f - t * 1.7f), Head(p) + Vector3.up * 0.2f, Tall(28f));
            }, null);
            add("глаза", 0.8f, t =>
            {
                var p = SinbinderPlayer.Instance;
                Pin(p, greh, fire);
                return (Head(p) + Vector3.up * 0.4f + p.transform.forward * (1.1f - t * 0.2f), Head(p) + Vector3.up * 0.4f, Tall(26f));
            }, null);
            return list;
        }

        // ---------------------------------------------------------------- 5. «Перетягивание» (22–47,2 с: охота и удар)

        private static Light _izbaLamp;

        private static List<TrailerCam.Shot> TugShort()
        {
            var list = Shots(out var add);
            var v = Village();
            var vc = Centre(v);
            Transform lord = RivalLord.All.Count > 0 ? RivalLord.All[0].transform : null;
            Transform crypt = TugMatch.Instance != null && TugMatch.Instance.Crypts.Count > 0 ? TugMatch.Instance.Crypts[0] : null;
            Vector3 Lord() => lord != null ? lord.position : vc;
            Vector3 Crypt() => crypt != null ? crypt.position : vc;
            Warrior LordWarrior() => lord != null ? lord.GetComponent<Warrior>() : null;

            // Дубль 3: начинался тёмным обзором карты — первая секунда Shorts решает, листать ли дальше.
            // Теперь сразу соперник на троне, потом его отряд, деревня, изба, склеп. Обзор карты убран.
            // Дубль 1: с 2,4 м соперник — во весь кадр без головы; склеп с 9 м — стена и ворота во весь кадр.
            add("соперник", 3.6f, t => (Head(LordWarrior()) + (lord != null ? lord.forward : Vector3.forward) * (4.4f - t * 1.0f) - Vector3.up * 0.5f,
                                        Head(LordWarrior()) + Vector3.up * 0.2f, Tall(32f)), null);
            add("его отряд", 3.4f, t => (TrailerCam.Orbit(Lord(), 30f + t * 40f, 7f, 2.2f), Lord() + Vector3.up, Tall(40f)), null);
            add("деревня", 4f, t => (TrailerCam.Orbit(vc, 200f + t * 40f, 12f, 4f), vc + Vector3.up * 2f, Tall(40f)), null);
            add("изба изнутри", 3f, t =>
            {
                var izba = v != null ? v.GetComponentInChildren<IzbaInterior>() : null;
                if (izba == null) return (TrailerCam.Orbit(vc, 250f, 10f, 5f), vc, Tall(40f));
                var roof = izba.transform.childCount > 1 ? izba.transform.GetChild(1) : null;
                if (roof != null) foreach (var r in roof.GetComponentsInChildren<Renderer>()) r.enabled = false;
                var c = izba.transform.position;
                var from = c + izba.transform.forward * (3.5f - t) + Vector3.up * (6.5f - t);
                // Дубль 3: изнутри — тёмно-синяя каша. Тёплый свет, будто лучина; ролик кончится — уйдёт со сценой.
                if (_izbaLamp == null)
                {
                    _izbaLamp = new GameObject("свет избы").AddComponent<Light>();
                    _izbaLamp.type = LightType.Point; _izbaLamp.color = new Color(1f, 0.72f, 0.45f);
                    _izbaLamp.range = 7f; _izbaLamp.intensity = 2.4f; _izbaLamp.shadows = LightShadows.None;
                }
                _izbaLamp.transform.position = c + Vector3.up * 2.2f;
                return (from, c + Vector3.up * 0.5f, Tall(40f));
            }, null);
            add("склеп", 3.2f, t => (TrailerCam.Orbit(Crypt(), 70f + t * 25f, 24f - t * 4f, 4f), Crypt() + Vector3.up * 3f, Tall(40f)), null);
            add("вверх", 3.5f, t => (TrailerCam.Glide(TrailerCam.Orbit(Crypt(), 95f, 20f, 4f), Crypt() + new Vector3(0f, 22f, 14f), t), Crypt(), Tall(45f)), null);
            add("склеп сверху", 3.5f, t => (Crypt() + new Vector3(0f, 30f - t * 5f, 5f), Crypt(), Tall(46f)), null);
            return list;
        }
    }
}
