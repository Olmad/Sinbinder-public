#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.EditorTools
{
    /// <summary>
    /// Сцена с залом суда души — посмотреть в редакторе, не играя сон.
    /// 05.11 их было две («с 5 судьями и с 13»); после «голосов очень мало»
    /// за кафедрой всегда тринадцать, и сцена одна. Миг — вердикт.
    /// В игре зал строится на время суда и уходит с ним; здесь он стоит в нуле.
    /// Строит <see cref="SoulCourt.Preview"/>; снимки — в D:/Claude/court.
    ///
    /// Меню: Sinbinder → Soul Court Preview Scenes. Пакетно:
    /// Unity -batchmode -executeMethod Sinbinder.EditorTools.CourtPreview.BuildBatch
    /// </summary>
    public static class CourtPreview
    {
        private const string ScenePath = "Assets/Scenes/SoulCourt.unity";
        private static readonly string[] OldPaths =
        {
            "Assets/Scenes/SoulCourt_Preview.unity", "Assets/Scenes/SoulCourt_5.unity", "Assets/Scenes/SoulCourt_13.unity",
        };

        [MenuItem("Sinbinder/Soul Court Preview Scenes")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var hall = SoulCourt.Preview(true);
            hall.transform.position = Vector3.zero;

            var cam = new GameObject("Court Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 50f;

            // Свет зала — как в игре на время суда: мира нет, только свечи, весы и компас.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.018f, 0.016f, 0.024f);
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.fog = false;

            foreach (var (name, from, look) in SoulCourt.PreviewShots(1))
            {
                cam.fieldOfView = name == "общий" ? 50f : 36f;   // как в суде: общий шире, крупные — угол наездов
                cam.transform.position = from;
                cam.transform.LookAt(look);
                Shot(cam, "SoulCourt_" + name);
            }
            cam.fieldOfView = 50f;
            cam.transform.position = SoulCourt.PreviewFrom;
            cam.transform.LookAt(SoulCourt.PreviewLook);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[СУД] Сцена для просмотра сохранена: " + ScenePath);

            foreach (var old in OldPaths)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(old) != null)
                    AssetDatabase.DeleteAsset(old);
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
