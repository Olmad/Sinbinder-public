// Assets/Scripts/Editor/Snapshot.cs
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
            ("Assets/Scenes/Prologue_Raid.unity", "набег", Vector3.zero, Vector3.zero),
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
        {
            if (who == null) return;

            Directory.CreateDirectory(folder);

            // Середина тела, а не точка опоры: у ног смотреть не на что.
            var body = who.GetComponentInChildren<SkinnedMeshRenderer>();
            var at = body != null ? body.bounds.center : who.position + Vector3.up;

            // Три четверти спереди-сбоку: в профиль не виден плащ,
            // в лоб — наплечник. Чуть сверху, как смотрит игрок.
            var side = who.rotation * new Vector3(0.75f, 0.35f, 1.25f);
            var from = at + side.normalized * 2.1f;

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

            // Холст на время снимка переезжает на камеру. Экранный холст
            // (Overlay) рисуется мимо камеры, прямо на экран, — а экрана
            // в пакетном режиме нет, и снимок выходил без совета, подсказок
            // и полос, то есть без игры. ScreenCapture тут тоже молчит:
            // ему тоже нужен экран.
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var moved = new System.Collections.Generic.List<Canvas>();

            foreach (var canvas in canvases)
            {
                if (!withUi) break;      // портрет снимают без подсказок и сумы
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + 0.05f;
                moved.Add(canvas);
            }

            Canvas.ForceUpdateCanvases();

            // Частицы в редакторе стоят: искры над костром и лепестки
            // сакуры существуют, но на снимке сцены их не было ни одной.
            // Прогоняем их на несколько секунд вперёд — ровно настолько,
            // чтобы облако успело сложиться.
            if (!Application.isPlaying)
                foreach (var particles in Object.FindObjectsByType<ParticleSystem>(
                             FindObjectsSortMode.None))
                    particles.Simulate(4f, true, true);

            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };

            var shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);

            camera.targetTexture = texture;
            camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            shot.Apply();
            RenderTexture.active = previous;

            camera.targetTexture = null;

            foreach (var canvas in moved)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }

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
                var c = shot.GetPixel(x, y);
                light += (c.r + c.g + c.b) / 3f;
                taken++;
            }
            light = taken == 0 ? 0f : light / taken;

            string file = $"{folder}/{name}.png";
            File.WriteAllBytes(file, shot.EncodeToPNG());

            Object.DestroyImmediate(shot);
            texture.Release();
            Object.DestroyImmediate(texture);

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
                        + $"наклон {camera.transform.eulerAngles.x:0.} гр.");

            return true;
        }
    }
}
