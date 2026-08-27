using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

public static class IconGenerator
{
    #if UNITY_EDITOR
    [MenuItem("Tools/Generate AOS Icons")]
    public static void GenerateIcons()
    {
        string path = "Assets/Resources/Icons/";
        if (!System.IO.Directory.Exists(path))
            System.IO.Directory.CreateDirectory(path);

        CreateIcon(path + "Attack.png", Color.red, "⚔");
        CreateIcon(path + "SaveAlly.png", Color.blue, "🛡");
        CreateIcon(path + "Loot.png", Color.yellow, "💰");
        CreateIcon(path + "Flee.png", Color.white, "🏃");
        CreateIcon(path + "Idle.png", Color.gray, "❓");

        AssetDatabase.Refresh();
        Debug.Log("Иконки AOS созданы в " + path);
    }

    static void CreateIcon(string filePath, Color color, string label)
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - size / 2f;
                float dy = y - size / 2f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                
                if (dist < size / 2f - 4f)
                {
                    float alpha = dist < size / 2f - 12f ? 1f : 0.5f;
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, alpha);
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        byte[] pngData = tex.EncodeToPNG();
        System.IO.File.WriteAllBytes(filePath, pngData);
        Object.DestroyImmediate(tex);
    }
    #endif
}