#if UNITY_EDITOR
// Assets/Scripts/Editor/Reel.cs
// Перевод: текст через Loc
using System.IO;
using UnityEditor;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.Utilets
{
    /// <summary>
    /// Съёмка серии кадров для учебника (автор, 28.10: «нужен небольшой учебник с гифками — как,
    /// что и почему делается»). Шесть кадров в секунду через <see cref="Snapshot.Now"/> — с
    /// подсказками и репликами над головой, как видит игрок; гифку из серии собирает
    /// <c>Tools/shots/gif.py</c>, он же пишет страницу учебника. Снимают прогон демо и проверка поля:
    /// действия там уже идут по сценарию, и учебник обновляется вместе с игрой.
    /// </summary>
    public static class Reel
    {
        public const string Root = "Docs/Учебник/кадры";   // ключ: папка, а не текст игрока
        private const float Step = 1f / 6f;

        private static string _folder;
        private static int _frame;
        private static float _next, _until;

        public static bool Rolling => _folder != null;

        /// <summary>Начать серию: старые кадры этой серии — долой.</summary>
        public static void Start(string name, float seconds)
        {
            Stop();
            _folder = $"{Root}/{name}";
            if (Directory.Exists(_folder))
                foreach (var f in Directory.GetFiles(_folder, "*.png")) File.Delete(f);
            Directory.CreateDirectory(_folder);
            _frame = 0;
            _next = 0f;
            _until = Time.realtimeSinceStartup + seconds;
            EditorApplication.update += Tick;
        }

        public static void Stop()
        {
            EditorApplication.update -= Tick;
            if (_folder != null) Debug.Log($"[УЧЕБНИК] {_folder}: кадров {_frame}");
            _folder = null;
        }

        private static void Tick()
        {
            if (_folder == null) return;
            if (!EditorApplication.isPlaying) { Stop(); return; }
            float now = Time.realtimeSinceStartup;
            if (now >= _until) { Stop(); return; }
            if (now < _next) return;
            _next = now + Step;
            Snapshot.Now(_folder, $"{_frame++:000}");
        }

        /// <summary>
        /// Камеру — сверху на точку: из глаз (если смотрел глазами) — в вид сверху, на
        /// <paramref name="distance"/> метров, и принять это за своё положение (RTS_Camera.Resync).
        /// </summary>
        public static void Aim(Vector3 at, float distance)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var view = cam.GetComponent<RTS_Camera>();
            if (view != null && view.FirstPersonNow) view.Switch();
            var from = at + new Vector3(0f, distance * 0.78f, -distance * 0.62f);
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
            if (view != null) view.Resync();
        }

        /// <summary>Глазами Греховода — на точку (от первого лица).</summary>
        public static void Eyes(Vector3 at)
        {
            var cam = Camera.main;
            var view = cam != null ? cam.GetComponent<RTS_Camera>() : null;
            if (view == null) return;
            if (!view.FirstPersonNow) view.Switch();
            view.Face(at);
            var d = at - cam.transform.position;
            view.Pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
        }
    }
}
#endif
