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
            Roll(dir);
            Death(dir, "игра — Бог Смерти ведёт суд",
                  "Почему он не послушался? Решал не он один — решала его душа. Вот её суд.",
                  "Так решает каждый воин в отряде: приказ даёшь ты, а взвешивают его голоса.");
            Debug.Log("[ГОЛОС] образцы: " + dir);
            EditorApplication.Exit(0);
        }

        private static void Write(string dir, string name, float depth, string words)
        {
            var go = new GameObject("Голос — образец");
            var voice = go.AddComponent<Audio.VoiceGenerator>();
            voice.Depth = depth;
            // Сколько звучит строка в игре — как у реплики суда (SoulCourt.Line).
            float seconds = Mathf.Min(Gameplay.Herald.Hold(words) - 0.4f, words.Length / 40f * 1.6f + 0.4f);
            var data = voice.Render(words, seconds, out int rate);
            Object.DestroyImmediate(go);

            Save(Path.Combine(dir, name + ".wav"), data, rate);
            Debug.Log($"[ГОЛОС] {name}: {data.Length / (float)rate:F1} с, пик {Peak(data):F2}");
        }

        /// <summary>
        /// Перекличка суда: каждый судья своим голосом (SoulCourt.Cast) — своя строка,
        /// подряд, с паузой. Эха под сводами здесь нет: его даёт фильтр источника в игре.
        /// </summary>
        private static void Roll(string dir)
        {
            var lines = new (string module, string words)[]
            {
                ("Pride", "Я — Гордыня. Я не склоняю головы."),
                ("Fear", "Я — Страх. Я берегу жизнь."),
                ("Wrath", "Я — Гнев. Я не прощаю обид."),
                ("Sloth", "Я — Уныние. Я берегу силы."),
                ("Envy", "Я — Зависть. Я вижу, у кого больше."),
                ("Loyalty", "Я — Верность. Я слышу владыку."),
                ("Gluttony", "Я — Чревоугодие. Мне всегда мало."),
            };
            var all = new System.Collections.Generic.List<float>();
            int rate = 22050;
            foreach (var (module, words) in lines)
            {
                var go = new GameObject("Судья — образец");
                var voice = go.AddComponent<Audio.VoiceGenerator>();
                var cast = Gameplay.SoulCourt.Cast(module);
                voice.Depth = cast.depth;
                voice.Kind = cast.kind;
                voice.Tempo = cast.tempo;
                float seconds = Mathf.Min(Gameplay.Herald.Hold(words) - 0.4f, words.Length / 40f * 1.6f + 0.4f);
                var data = voice.Render(words, seconds, out rate);
                Object.DestroyImmediate(go);
                all.AddRange(data);
                all.AddRange(new float[rate / 3]);
            }
            Save(Path.Combine(dir, "игра — суд, голоса судей.wav"), all.ToArray(), rate);
            Debug.Log($"[ГОЛОС] перекличка суда: {all.Count / (float)rate:F1} с");
        }

        /// <summary>Бог Смерти на суде (Audio.DeathVoice.Speech): строки подряд, с эхом склепа.</summary>
        private static void Death(string dir, string name, params string[] lines)
        {
            const int rate = 22050;
            var all = new System.Collections.Generic.List<float>();
            int at = 0;
            for (int l = 0; l < lines.Length; l++)
            {
                var data = Audio.DeathVoice.Speech(lines[l], out float spoken);
                // Как в игре: следующая строка — через 0,8 с после сказанного,
                // хвост прошлой гаснет за полсекунды под новой (DeathVoice.Utter).
                int next = at + Mathf.RoundToInt((spoken + 0.8f) * rate);
                bool last = l == lines.Length - 1;
                for (int i = 0; i < data.Length; i++)
                {
                    int j = at + i;
                    float k = last || j < next ? 1f : 1f - (j - next) / (0.5f * rate);
                    if (k <= 0f) break;
                    while (all.Count <= j) all.Add(0f);
                    all[j] += data[i] * k;
                }
                at = next;
            }
            Save(Path.Combine(dir, name + ".wav"), all.ToArray(), rate);
            Debug.Log($"[ГОЛОС] {name}: {all.Count / (float)rate:F1} с, пик {Peak(all.ToArray()):F2}");
        }

        private static void Save(string path, float[] data, int rate)
        {
            using var file = new FileStream(path, FileMode.Create);
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
