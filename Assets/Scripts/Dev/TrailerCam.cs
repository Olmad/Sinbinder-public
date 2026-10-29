// Assets/Scripts/Dev/TrailerCam.cs
// Перевод: текст через Loc
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.Dev
{
    /// <summary>
    /// Съёмка трейлера кадрами (автор, 29.10: «попробуй сам снять трейлер. У тебя же есть камеры»; «обязательно
    /// должен быть пролёт ворона»). Не запись экрана: игровое время идёт ровно на 1/30 с за кадр
    /// (<see cref="Time.captureFramerate"/>), каждый кадр рисуется в свою текстуру 1920×1080 и ложится в JPG —
    /// видео выходит плавным, сколько бы ни стоил кадр. Склеивает кадры с музыкой Tools/trailer/cut.py.
    ///
    /// Планы — под музыку трейлера (Tools/music/trailer.py, темп 76): каждый план — длина в секундах и камера
    /// по времени плана (0…1). Ставит и запускает съёмку редактор (Editor/TrailerShoot) в прогоне без окон.
    /// </summary>
    public class TrailerCam : MonoBehaviour
    {
        public const int Fps = 30;
        public const int W = 1920, H = 1080;

        public struct Shot
        {
            public string Name;
            public float Seconds;
            public Action Begin;
            /// <summary>Камера в доле плана t (0…1): откуда, куда смотреть, поле зрения.</summary>
            public Func<float, (Vector3 from, Vector3 at, float fov)> Camera;
        }

        public List<Shot> Shots = new();
        public string Folder;
        public Action Done;

        private Camera _cam;
        private RenderTexture _rt;
        private Texture2D _read;
        private int _shot = -1, _frame, _inShot, _shotFrames;

        public bool Finished { get; private set; }

        void Start()
        {
            Directory.CreateDirectory(Folder);
            foreach (var f in Directory.GetFiles(Folder, "*.jpg")) File.Delete(f);
            File.Delete(Path.Combine(Folder, "снято.txt"));
            Time.captureFramerate = Fps;
            _rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            _read = new Texture2D(W, H, TextureFormat.RGB24, false);
            Next();
        }

        private void Next()
        {
            _shot++;
            if (_shot >= Shots.Count) { Finish(); return; }
            _inShot = 0;
            _shotFrames = Mathf.Max(1, Mathf.RoundToInt(Shots[_shot].Seconds * Fps));
            try { Shots[_shot].Begin?.Invoke(); }
            catch (Exception e) { Debug.LogWarning($"[ТРЕЙЛЕР] план «{Shots[_shot].Name}»: {e.Message}"); }
            Debug.Log($"[ТРЕЙЛЕР] план {_shot + 1}/{Shots.Count} «{Shots[_shot].Name}» с кадра {_frame}");
        }

        void LateUpdate()
        {
            if (Finished || _shot >= Shots.Count) return;
            _cam = UnityEngine.Camera.main;
            if (_cam == null) return;
            // Камеру ведёт съёмка: всё, что двигает камеру игры, — молчит.
            foreach (var b in _cam.GetComponents<MonoBehaviour>()) if (b.enabled && b != this && !(b is UnityEngine.Rendering.Universal.UniversalAdditionalCameraData)) b.enabled = false;
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (c.enabled) c.enabled = false;

            float t = _shotFrames <= 1 ? 0f : _inShot / (float)(_shotFrames - 1);
            (Vector3 from, Vector3 at, float fov) pose;
            try { pose = Shots[_shot].Camera(t); }
            catch (Exception e) { Debug.LogWarning($"[ТРЕЙЛЕР] камера «{Shots[_shot].Name}»: {e.Message}"); pose = (_cam.transform.position, _cam.transform.position + _cam.transform.forward, 40f); }
            _cam.transform.position = pose.from;
            var dir = pose.at - pose.from;
            if (dir.sqrMagnitude > 1e-6f) _cam.transform.rotation = Quaternion.LookRotation(dir.normalized);
            _cam.fieldOfView = pose.fov;

            _cam.targetTexture = _rt;
            _cam.Render();
            _cam.targetTexture = null;
            var was = RenderTexture.active;
            RenderTexture.active = _rt;
            _read.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            _read.Apply(false);
            RenderTexture.active = was;
            File.WriteAllBytes(Path.Combine(Folder, $"{_frame:00000}.jpg"), _read.EncodeToJPG(92));
            _frame++;
            _inShot++;
            if (_inShot >= _shotFrames) Next();
        }

        private void Finish()
        {
            Finished = true;
            Time.captureFramerate = 0;
            // Сколько снято — для монтажа (Tools/trailer/cut.py): заставку он дописывает в ту же папку.
            File.WriteAllText(Path.Combine(Folder, "снято.txt"), _frame.ToString());
            Debug.Log($"[ТРЕЙЛЕР] снято кадров {_frame} ({_frame / (float)Fps:F1} с) в {Folder}");
            Done?.Invoke();
        }

        void OnDestroy()
        {
            if (_rt != null) _rt.Release();
        }

        // ---------------------------------------------------------------- приёмы камеры

        public static float Ease(float t) => t * t * (3f - 2f * t);

        /// <summary>Плавно из a в b.</summary>
        public static Vector3 Glide(Vector3 a, Vector3 b, float t) => Vector3.Lerp(a, b, Ease(t));

        /// <summary>Точка на кольце вокруг центра: угол в градусах, радиус, высота.</summary>
        public static Vector3 Orbit(Vector3 centre, float deg, float radius, float height)
        {
            float a = deg * Mathf.Deg2Rad;
            return centre + new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius);
        }

        /// <summary>Лёгкое «с рук»: дыхание камеры по формуле, без жребия.</summary>
        public static Vector3 Breath(float seconds, float amount)
            => new Vector3(Mathf.Sin(seconds * 1.3f) * 0.6f, Mathf.Sin(seconds * 0.9f + 1f), Mathf.Sin(seconds * 1.1f + 2f) * 0.4f) * amount;
    }
}
