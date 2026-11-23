// Assets/Scripts/Editor/CleanShot.cs
// Кадр без интерфейса: своя камера рендерит мир в текстуру (затемнения, панели и подписи
// экранного холста в неё не попадают). Для шара на столе совета и для кадров мест.
// Верстак: call Sinbinder.EditorTools.CleanShot.Take "имя" "x,y,z" "x,y,z" [ширина] [высота] [поле зрения]
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sinbinder.EditorTools
{
    public static class CleanShot
    {
        public const string Folder = "Logs/workbench/shots";

        public static string Take(string name, string from, string to)
            => Take(name, from, to, 1600, 900, 40f);

        public static string Take(string name, string from, string to, int width, int height, float fov)
        {
            var a = Parse(from);
            var b = Parse(to);
            var go = new GameObject("Чистый кадр — камера");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.transform.position = a;
                cam.transform.rotation = Quaternion.LookRotation((b - a).normalized, Vector3.up);
                cam.fieldOfView = fov;
                cam.nearClipPlane = 0.02f;
                cam.farClipPlane = 600f;
                var main = Camera.main;
                if (main != null)
                {
                    cam.clearFlags = main.clearFlags;
                    cam.backgroundColor = main.backgroundColor;
                    cam.cullingMask = main.cullingMask;
                }
                var path = Save(cam, name, width, height);
                return path;
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>Отрендерить камеру в PNG (Logs/workbench/shots/имя.png). Вернёт полный путь.</summary>
        public static string Save(Camera cam, string name, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            rt.Create();
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Directory.CreateDirectory(Folder);
            var file = Path.GetFullPath($"{Folder}/{name}.png");
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            return file;
        }

        private static Vector3 Parse(string s)
        {
            var p = s.Split(',');
            return new Vector3(float.Parse(p[0], CultureInfo.InvariantCulture),
                               float.Parse(p[1], CultureInfo.InvariantCulture),
                               float.Parse(p[2], CultureInfo.InvariantCulture));
        }
    }
}
