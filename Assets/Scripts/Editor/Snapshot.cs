// Assets/Scripts/Editor/Snapshot.cs
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sinbinder.Utilets
{
    /// <summary>
    /// Снимки сцен в файлы: то, чего у пакетного режима не было никогда.
    ///
    /// Проект собирается и проверяется без человека — и потому целый
    /// класс поломок оставался невидимым. Предметы, уменьшенные в сто раз
    /// и уложенные набок, простояли так неделю: сцены собирались, объекты
    /// стояли по местам, все проверки молчали. Увидеть это можно было
    /// только глазами.
    ///
    /// Снимок — это глаза. Камера рисует в текстуру и текстура ложится
    /// в <c>Docs/Образцы/сцены</c>: смотреть может и автор, и тот, кто
    /// собирал сцену. Работает в пакетном режиме, но <b>без
    /// <c>-nographics</c></b>: без устройства рисования рисовать нечем.
    /// </summary>
    public static class Snapshot
    {
        private const string Out = "Docs/Образцы/сцены";
        private const int Width = 1600;
        private const int Height = 900;

        /// <summary>Что снимаем и откуда. Пустой взгляд — камерой сцены.</summary>
        private static readonly (string Scene, string Name, Vector3 From, Vector3 At)[] Shots =
        {
            ("Assets/Scenes/Сакура.unity", "сакура", Vector3.zero, Vector3.zero),
            ("Assets/Scenes/Сакура.unity", "сакура-ближе",
                new Vector3(-3.6f, 1.35f, -3.1f), new Vector3(0.4f, 1.1f, 1.2f)),
            ("Assets/Scenes/Prologue_Camp.unity", "лагерь", Vector3.zero, Vector3.zero),
            ("Assets/Scenes/Prologue_Camp.unity", "лагерь-костёр",
                new Vector3(-4.5f, 2.2f, -6.5f), new Vector3(0f, 1.1f, 0f)),
            ("Assets/Scenes/Crypt_Entrance.unity", "склеп", Vector3.zero, Vector3.zero),
            ("Assets/Scenes/Crypt_Entrance.unity", "склеп-зал",
                new Vector3(-3.2f, 2.4f, -1.4f), new Vector3(0.8f, 1.2f, 5.2f)),
        };

        [MenuItem("Sinbinder/Снимки сцен")]
        public static void All()
        {
            Directory.CreateDirectory(Out);

            string open = "";
            int made = 0;

            foreach (var (path, name, from, at) in Shots)
            {
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[СНИМОК] {path}: нет такой сцены.");
                    continue;
                }

                if (open != path)
                {
                    EditorSceneManager.OpenScene(path);
                    open = path;
                }

                // Без интерфейса: в несыгранной сцене он весь разом
                // виден, и чёрное полотно заставки закрывает собой кадр.
                // Интерфейс снимает прогон — там он живой.
                if (Shot(name, from, at, Out, withUi: false)) made++;
            }

            Debug.Log($"[СНИМОК] Готово: {made} из {Shots.Length}. Лежат в {Out}.");
        }

        /// <summary>Точка входа для пакетного режима.</summary>
        public static void AllBatch() => All();

        /// <summary>
        /// Снять то, что на экране сейчас, — не открывая сцен.
        ///
        /// Этим пользуется прогон демо: у него в кадре есть то, чего
        /// в собранной сцене нет вовсе — воины, одежда, полосы, подсказки.
        /// Снимок редактора показывает декорацию, снимок прогона — игру.
        /// </summary>
        public static void Now(string folder, string name)
        {
            Directory.CreateDirectory(folder);

            Shot(name, Vector3.zero, Vector3.zero, folder);
        }

        /// <summary>
        /// Портрет: кадр вблизи одного бойца.
        ///
        /// Гардероб надевается в игре, а в собранной сцене воинов нет
        /// вовсе — значит единственное место, где одежду вообще можно
        /// увидеть, это прогон. С двадцати двух метров тактической камеры
        /// капюшон от шлема не отличить, поэтому камера на миг подходит
        /// вплотную и возвращается на место.
        /// </summary>
        public static void Portrait(string folder, string name, Transform who)
            => Portrait(folder, name, who, who == null ? Vector3.zero : who.forward);

        /// <summary>
        /// Портрет с заданной стороны. Пустое направление — с той, с какой
        /// на бойца смотрит игрок.
        /// </summary>
        public static void Portrait(string folder, string name, Transform who, Vector3 dir)
        {
            if (who == null) return;

            Directory.CreateDirectory(folder);

            // Середина тела, а не точка опоры: у ног смотреть не на что.
            var body = who.GetComponentInChildren<SkinnedMeshRenderer>();
            var at = body != null ? body.bounds.center : who.position + Vector3.up;

            // Отходим на два роста, а не на два метра. Греховод выше
            // воинов, и мерка в метрах резала ему голову: в кадр попадал
            // плащ и ничего больше.
            float tall = body != null ? Mathf.Max(0.5f, body.bounds.size.y) : 1.2f;

            // Целимся выше середины: лицо и плечи важнее сапог.
            at += Vector3.up * tall * 0.18f;

            // Смотрим с той стороны, с которой на бойца смотрит игрок,
            // а не «спереди по модели»: боец поворачивается по ходу боя,
            // и оба первых захода — и +Z, и −Z — дали спину.
            Vector3 toEye;

            if (dir.sqrMagnitude > 0.01f)
            {
                toEye = dir.normalized;
            }
            else
            {
                var eye = Camera.main != null ? Camera.main.transform.position
                                              : at + new Vector3(0f, 6f, -6f);

                toEye = eye - at;
                toEye.y = 0f;
                if (toEye.sqrMagnitude < 0.01f) toEye = Vector3.back;
                toEye.Normalize();
            }

            // Чуть вбок и сверху: в лоб не виден наплечник, в профиль плащ.
            var side = Quaternion.Euler(0f, 28f, 0f) * toEye + Vector3.up * 0.32f;

            var from = at + side.normalized * (tall * 1.75f);

            // Между камерой и бойцом может оказаться палатка, холм или
            // частокол — первый же портрет вышел изнутри земли. Упёрлись
            // во что-то по дороге — встаём перед ним.
            if (Physics.Linecast(at, from, out var wall))
                from = wall.point + (at - from).normalized * -0.25f;

            // И никогда не из-под земли: боец стоит на склоне чаще, чем
            // на ровном, и четверть метра высоты тут решает всё.
            if (from.y < at.y - 0.2f) from.y = at.y - 0.2f;

            Debug.Log($"[ПОРТРЕТ] {name}: из {from.x:0.0} {from.y:0.0} {from.z:0.0} "
                    + $"на {at.x:0.0} {at.y:0.0} {at.z:0.0}, сам стоит "
                    + $"{who.position.x:0.0} {who.position.y:0.0} {who.position.z:0.0}, "
                    + $"тело — {(body == null ? "нет" : body.name)}");

            Shot(name, from, at, folder, withUi: false);
        }

        private static bool Shot(string name, Vector3 from, Vector3 at, string folder = Out,
                                 bool withUi = true)
        {
            var camera = Camera.main != null
                       ? Camera.main
                       : Object.FindFirstObjectByType<Camera>();

            if (camera == null)
            {
                Debug.LogWarning($"[СНИМОК] {name}: камеры в сцене нет.");
                return false;
            }

            // Свой взгляд — временный: камеру сцены не трогаем, иначе
            // снимок менял бы сцену, которую снимает.
            var was = (camera.transform.position, camera.transform.rotation, camera.fieldOfView);
            bool shifted = from != at;

            if (shifted)
            {
                camera.transform.position = from;
                camera.transform.rotation = Quaternion.LookRotation((at - from).normalized);
                camera.fieldOfView = folder == Out ? 42f : 30f;
            }

            // Экранный холст (Overlay) рисуется мимо камеры, прямо на экран, —
            // а экрана в пакетном режиме нет: в кадр мира он не попадает.
            // Его снимаем отдельно и кладём сверху (Ui). ScreenCapture тут
            // тоже молчит: ему тоже нужен экран.
            var canvases = new List<Canvas>();
            if (withUi)      // портрет снимают без подсказок и сумы
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                        canvases.Add(canvas);

            // Частицы в редакторе стоят: искры над костром и лепестки
            // сакуры существуют, но на снимке сцены их не было ни одной.
            // Прогоняем их на несколько секунд вперёд — ровно настолько,
            // чтобы облако успело сложиться.
            if (!Application.isPlaying)
                foreach (var particles in Object.FindObjectsByType<ParticleSystem>(
                             FindObjectsSortMode.None))
                    particles.Simulate(4f, true, true);

            var pixels = Grab(camera, 4);
            string how = canvases.Count == 0 ? "" : Ui.Lay(camera, canvases, pixels);

            if (shifted)
            {
                camera.transform.position = was.position;
                camera.transform.rotation = was.rotation;
                camera.fieldOfView = was.fieldOfView;
            }

            // Средняя яркость по сетке: читать полтора миллиона точек
            // незачем, а провал рисования виден и по тысяче.
            float light = 0f;
            int taken = 0;
            for (int y = 0; y < Height; y += 29)
            for (int x = 0; x < Width; x += 29)
            {
                var c = pixels[y * Width + x];
                light += (c.r + c.g + c.b) / (3f * 255f);
                taken++;
            }
            light = taken == 0 ? 0f : light / taken;

            var shot = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            shot.SetPixels32(pixels);
            shot.Apply();

            string file = $"{folder}/{name}.png";
            File.WriteAllBytes(file, shot.EncodeToPNG());
            Object.DestroyImmediate(shot);

            // Совсем чёрный снимок — это не «ночь», это провал рисования:
            // ради него всё и заведено, и промолчать о нём нельзя.
            // Чёрный кадр не всегда провал: на переходе между сценами
            // экран затемняется нарочно, и снимок шага честно это ловит.
            // Но если чёрные все подряд — рисовать нечем, и это провал.
            if (light < 0.01f)
                Debug.LogWarning($"[СНИМОК] {name}: кадр чёрный ({light:0.000}) — "
                               + "затемнение перехода или нечем рисовать.");
            else
                Debug.Log($"[СНИМОК] {name}: {file}, света {light:0.000}, "
                        + $"камера {camera.transform.position.x:0.0} "
                        + $"{camera.transform.position.y:0.0} "
                        + $"{camera.transform.position.z:0.0}, "
                        + $"наклон {camera.transform.eulerAngles.x:0.} гр."
                        + (how.Length == 0 ? "" : ", " + how));

            return true;
        }

        /// <summary>Кадр камеры в точки: рисует в свою текстуру и читает её.</summary>
        private static Color32[] Grab(Camera camera, int msaa)
        {
            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = msaa
            };

            camera.targetTexture = texture;
            Canvas.ForceUpdateCanvases();
            camera.Render();

            var read = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            read.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            read.Apply();
            RenderTexture.active = previous;

            camera.targetTexture = null;

            var pixels = read.GetPixels32();
            Object.DestroyImmediate(read);
            texture.Release();
            Object.DestroyImmediate(texture);
            return pixels;
        }

        /// <summary>
        /// Интерфейс поверх кадра — своей камерой и мимо постобработки.
        ///
        /// В игре экранный холст рисуется после всей картинки, мимо зерна,
        /// виньетки и цветных краёв. На снимке он до 26 сентября висел
        /// на игровой камере и проходил через всё это: буквы двоились
        /// краями, углы панелей темнели под виньеткой. Кадры, по которым
        /// судили об интерфейсе и из которых отбирали показ, выходили хуже
        /// самой игры.
        ///
        /// Теперь мир снимается основной камерой со всей обработкой, а холсты
        /// — своей камерой, без неё, дважды: на чёрном и на белом. Разница
        /// двух кадров — ровно то, сколько мира видно сквозь интерфейс
        /// в каждой точке, так что прозрачные панели, края букв и тени
        /// ложатся на мир точно так, как в игре. Смешивание — в линейном
        /// пространстве, как смешивает сама игра (проект линейный).
        ///
        /// Стопкой камер URP не вышло: наложение в стопке, запущенной
        /// из кода, рисуется, а холстов на нём нет (проба 26 сентября).
        /// </summary>
        private static class Ui
        {
            /// <summary>Снять холсты и положить их на кадр. Возвращает, как вышло.</summary>
            public static string Lay(Camera main, List<Canvas> canvases, Color32[] world)
            {
                int layer = FreeLayer();
                if (layer < 0) return "интерфейса нет: свободного слоя не нашлось";

                var go = new GameObject("Снимок: интерфейс") { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);

                var ui = go.AddComponent<Camera>();
                ui.fieldOfView = main.fieldOfView;
                ui.nearClipPlane = main.nearClipPlane;
                ui.farClipPlane = main.nearClipPlane + 1f;
                ui.cullingMask = 1 << layer;
                ui.clearFlags = CameraClearFlags.SolidColor;
                ui.allowHDR = false;
                ui.allowMSAA = false;

                var data = ui.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;

                // Холсты — на свой слой, чтобы камера интерфейса не видела мира,
                // и на эту камеру. Всё возвращается, как было.
                var layers = new List<(GameObject Go, int Layer)>();
                foreach (var canvas in canvases)
                {
                    foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
                    {
                        layers.Add((t.gameObject, t.gameObject.layer));
                        t.gameObject.layer = layer;
                    }

                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = ui;
                    canvas.planeDistance = ui.nearClipPlane + 0.05f;
                }

                ui.backgroundColor = Color.black;
                var black = Grab(ui, 1);
                ui.backgroundColor = Color.white;
                var white = Grab(ui, 1);

                foreach (var canvas in canvases)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.worldCamera = null;
                }
                foreach (var (g, l) in layers)
                    if (g != null) g.layer = l;
                Object.DestroyImmediate(go);

                for (int i = 0; i < world.Length; i++)
                {
                    var s = world[i];
                    var b = black[i];
                    var w = white[i];
                    world[i] = new Color32(Mix(s.r, b.r, w.r), Mix(s.g, b.g, w.g), Mix(s.b, b.b, w.b), 255);
                }

                return "интерфейс поверх";
            }

            /// <summary>
            /// Точка кадра: интерфейс на чёрном плюс мир, насколько его видно
            /// сквозь интерфейс (белое минус чёрное). В линейных величинах.
            /// </summary>
            private static byte Mix(byte world, byte black, byte white)
            {
                float b = Linear[black];
                float through = Mathf.Clamp01(Linear[white] - b);
                float c = Mathf.Clamp01(b + Linear[world] * through);
                return Encoded[Mathf.RoundToInt(c * (Encoded.Length - 1))];
            }

            private static readonly float[] Linear = BuildLinear();
            private static readonly byte[] Encoded = BuildEncoded();

            private static float[] BuildLinear()
            {
                var table = new float[256];
                for (int i = 0; i < 256; i++) table[i] = Mathf.GammaToLinearSpace(i / 255f);
                return table;
            }

            private static byte[] BuildEncoded()
            {
                var table = new byte[4096];
                for (int i = 0; i < table.Length; i++)
                    table[i] = (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(Mathf.LinearToGammaSpace(i / (table.Length - 1f))) * 255f);
                return table;
            }

            /// <summary>
            /// Слой без имени — им в проекте не пользуется никто. С конца,
            /// но не последний: им Unity рисует свои превью.
            /// </summary>
            private static int FreeLayer()
            {
                for (int i = 30; i >= 8; i--)
                    if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) return i;
                return -1;
            }
        }
    }
}
