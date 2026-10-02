// Assets/Scripts/Editor/CampPlan.cs
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sinbinder.EditorTools
{
    /// <summary>
    /// План лагеря сверху: ортографический кадр собранной сцены при ровном
    /// дневном свете, с сеткой через метр (через пять — ярче) и крестом
    /// в начале координат — у костра.
    ///
    /// Заведён 2 октября, когда автор попросил расставить лагерь осмысленно:
    /// «например сундук находится в спуске с холма». Кадры прогона сняты
    /// ночью и под углом — по ним не видно, что на чём стоит. План — видно,
    /// и его можно положить рядом: до и после.
    ///
    /// Сцену не сохраняет: свет и камера плана живут, пока идёт съёмка.
    /// Запуск: <c>-executeMethod Sinbinder.EditorTools.CampPlan.RenderBatch
    /// -planTag before|after|court</c> — в <c>Docs/Образцы/план/план лагеря — до|после|двор.jpg</c>;
    /// без подписи — <c>план лагеря.jpg</c>; <c>-planOut</c> — свой файл.
    /// JPEG, а не PNG: зернистая земля в PNG весит пять мегабайт кадр,
    /// а планы лежат в репозитории.
    /// </summary>
    public static class CampPlan
    {
        /// <summary>Сколько метров от центра до края кадра. Крупный план — <c>-planBox</c>.</summary>
        private static float Half = 17f;

        /// <summary>Центр плана: костёр в начале координат, холм — на юге.</summary>
        private static Vector3 Centre = new Vector3(0f, 0f, -3f);

        private const int Size = 1600;

        /// <summary>Подписи плана: латиница командной строки → имя файла.</summary>
        private static readonly System.Collections.Generic.Dictionary<string, string> Tags = new()
        {
            ["before"] = "до",
            ["after"] = "после",
            ["court"] = "двор",
        };

        public static void RenderBatch()
        {
            // Подпись — латиницей из командной строки (кириллицу оболочка
            // рабочего ПК портит): before — «до», after — «после».
            string output = "Docs/Образцы/план/план лагеря.jpg";
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-planOut") output = args[i + 1];

                // Крупный план: «x,z,полсторона» в метрах, например «0,-3.5,7».
                if (args[i] == "-planBox")
                {
                    var box = args[i + 1].Split(',');
                    if (box.Length == 3)
                    {
                        var inv = System.Globalization.CultureInfo.InvariantCulture;
                        Centre = new Vector3(float.Parse(box[0], inv), 0f, float.Parse(box[1], inv));
                        Half = float.Parse(box[2], inv);
                    }
                }
                if (args[i] == "-planTag")
                    output = $"Docs/Образцы/план/план лагеря — {(Tags.TryGetValue(args[i + 1], out var tag) ? tag : args[i + 1])}.jpg";
            }

            EditorSceneManager.OpenScene("Assets/Scenes/Prologue_Camp.unity");

            // Ровный свет сверху: ночь лагеря на плане читалась бы чернотой,
            // а длинные тени луны прятали бы то, что за ними. Солнце —
            // под углом, чтобы холм и ящики отбрасывали короткую тень:
            // высоту видно по ней.
            var sun = new GameObject("План — солнце").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(58f, 35f, 0f);

            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.50f, 0.50f, 0.52f);

            var go = new GameObject("План — камера");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Half;
            cam.transform.SetPositionAndRotation(Centre + Vector3.up * 60f, Quaternion.Euler(90f, 0f, 0f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.13f);
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 200f;

            var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = true;

            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();

            var shot = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            shot.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            RenderTexture.active = was;
            cam.targetTexture = null;

            Grid(shot);
            shot.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllBytes(output, shot.EncodeToJPG(86));
            Debug.Log($"[ПЛАН] {output}: {Size}×{Size}, {Half * 2f:0} м по стороне, "
                    + $"{Size / (Half * 2f):0.0} точки на метр, центр {Centre.x:0} {Centre.z:0}.");

            UnityEngine.Object.DestroyImmediate(shot);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        /// <summary>Сетка через метр, через пять — ярче; оси через костёр — ярче всего.</summary>
        private static void Grid(Texture2D shot)
        {
            float perMetre = Size / (Half * 2f);
            float left = Centre.x - Half, bottom = Centre.z - Half;

            for (int m = Mathf.CeilToInt(left); m <= Mathf.FloorToInt(left + Half * 2f); m++)
            {
                int px = Mathf.RoundToInt((m - left) * perMetre);
                Line(shot, px, vertical: true, Strength(m));
            }

            for (int m = Mathf.CeilToInt(bottom); m <= Mathf.FloorToInt(bottom + Half * 2f); m++)
            {
                int py = Mathf.RoundToInt((m - bottom) * perMetre);
                Line(shot, py, vertical: false, Strength(m));
            }
        }

        private static float Strength(int metre) => metre == 0 ? 0.55f : metre % 5 == 0 ? 0.30f : 0.10f;

        private static void Line(Texture2D shot, int at, bool vertical, float strength)
        {
            if (at < 0 || at >= Size) return;
            var tint = new Color(0.95f, 0.85f, 0.35f);

            for (int i = 0; i < Size; i++)
            {
                int x = vertical ? at : i, y = vertical ? i : at;
                var c = shot.GetPixel(x, y);
                shot.SetPixel(x, y, Color.Lerp(c, tint, strength));
            }
        }
    }
}
