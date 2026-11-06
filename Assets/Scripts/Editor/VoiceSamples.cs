#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sinbinder.EditorTools
{
    /// <summary>
    /// Образцы голоса прямо из кода игры — в WAV (автор, 06.11: «нужно переработать
    /// голоса вообще — они раздражают»). Те же слоги, что звучат в суде
    /// (<see cref="Audio.VoiceGenerator.Render"/>): судья, голос суда, голос без души.
    /// Я не слышу — автор слушает, я смотрю спектр (Tools/music/spectro.py).
    ///
    ///     Unity -batchmode -projectPath … -executeMethod Sinbinder.EditorTools.VoiceSamples.Run
    ///
    /// Папка — переменная SINBINDER_VOICE_OUT, иначе Logs/голоса.
    /// </summary>
    public static class VoiceSamples
    {
        public static void Run()
        {
            string dir = System.Environment.GetEnvironmentVariable("SINBINDER_VOICE_OUT");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.dataPath, "..", "Logs", "голоса");
            Directory.CreateDirectory(dir);

            Write(dir, "игра — судья, глубина 0,8", 0.8f,
                  "В нём чревоугодие. Тяжёлому сытому брюху драка ни к чему. Поэтому я — за приказ.");
            Write(dir, "игра — голос суда, глубина 0,62", 0.62f,
                  "Почему он не послушался? Решал не он один — решала его душа. Вот её суд.");
            Write(dir, "игра — голос без души", 1f,
                  "Ну что, владыка, опять мои кости ноют? К дождю, видать.");
            Debug.Log("[ГОЛОС] образцы: " + dir);
            EditorApplication.Exit(0);
        }

        private static void Write(string dir, string name, float depth, string words)
        {
            var go = new GameObject("Голос — образец");
            var voice = go.AddComponent<Audio.VoiceGenerator>();
            voice.Depth = depth;
            // Сколько звучит строка в игре — как у реплики суда (SoulCourt.Line).
            float seconds = Mathf.Min(Gameplay.Herald.Hold(words) - 0.4f, words.Length / 40f * 1.2f + 0.3f);
            var data = voice.Render(words, seconds, out int rate);
            Object.DestroyImmediate(go);

            using var file = new FileStream(Path.Combine(dir, name + ".wav"), FileMode.Create);
            using var w = new BinaryWriter(file);
            int bytes = data.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + bytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(rate);
            w.Write(rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(bytes);
            foreach (float x in data) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(x * 32767f), -32768, 32767));
            Debug.Log($"[ГОЛОС] {name}: {data.Length / (float)rate:F1} с, пик {Peak(data):F2}");
        }

        private static float Peak(float[] data)
        {
            float p = 0f;
            foreach (float x in data) p = Mathf.Max(p, Mathf.Abs(x));
            return p;
        }
    }
}
#endif
