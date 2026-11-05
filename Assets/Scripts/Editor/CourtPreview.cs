#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.EditorTools
{
    /// <summary>
    /// Две сцены с залом суда души — сравнить в редакторе, не играя сон (автор,
    /// 05.11: «суд в двух вариациях… с 5 судьями и с 13»): только сидящие за
    /// столом — и они же плюс молчащие голоса в тени. Обе — в миг вердикта.
    /// В игре зал строится на время суда и уходит с ним; здесь он стоит в нуле,
    /// камера — на общем плане суда. Строит <see cref="SoulCourt.Preview"/>.
    ///
    /// Меню: Sinbinder → Soul Court Preview Scenes. Пакетно:
    /// Unity -batchmode -executeMethod Sinbinder.EditorTools.CourtPreview.BuildBatch
    /// </summary>
    public static class CourtPreview
    {
        private const string FivePath = "Assets/Scenes/SoulCourt_5.unity";
        private const string ThirteenPath = "Assets/Scenes/SoulCourt_13.unity";
        private const string OldPath = "Assets/Scenes/SoulCourt_Preview.unity";

        [MenuItem("Sinbinder/Soul Court Preview Scenes")]
        public static void Build()
        {
            Save(false, FivePath);
            Save(true, ThirteenPath);

            // Первая сцена 05.11 была одна; её место заняли две для сравнения.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(OldPath) != null)
                AssetDatabase.DeleteAsset(OldPath);
        }

        private static void Save(bool shadows, string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var hall = SoulCourt.Preview(shadows);
            hall.transform.position = Vector3.zero;

            var cam = new GameObject("Court Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 36f;
            cam.transform.position = SoulCourt.PreviewFrom;
            cam.transform.LookAt(SoulCourt.PreviewLook);

            // Свет зала — как в игре на время суда: мира нет, только свечи и компас.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.018f, 0.016f, 0.024f);
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.fog = false;

            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[СУД] Сцена для просмотра сохранена: " + path);
            Shot(cam, System.IO.Path.GetFileNameWithoutExtension(path));
        }

        /// <summary>Снимок общего плана в D:/Claude/court — глянуть, не открывая редактор.</summary>
        private static void Shot(Camera cam, string name)
        {
            try
            {
                var rt = new RenderTexture(1600, 900, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                tex.Apply();
                cam.targetTexture = null;
                RenderTexture.active = null;
                System.IO.Directory.CreateDirectory("D:/Claude/court");
                System.IO.File.WriteAllBytes("D:/Claude/court/" + name + ".png", tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(tex);
            }
            catch (Exception e) { Debug.LogWarning("[СУД] Снимок не вышел: " + e.Message); }
        }

        public static void BuildBatch()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
#endif
