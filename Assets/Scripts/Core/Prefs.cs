// Assets/Scripts/Core/Prefs.cs
// Настройки игры (громкость, графика, клавиши, мини-карта…) — тем же вызовом, что PlayerPrefs, но не всегда
// в реестр Windows. Автор, 26.11: «на рабочем ПК ничего не должно сохраняться». Когда DataFolder.OffPc
// (редактор с рабочей папкой Claude или сборка на рабочем ПК) — файлом «настройки.txt» рядом с сохранениями;
// иначе — как прежде, PlayerPrefs.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Sinbinder.Core
{
    public static class Prefs
    {
        private static Dictionary<string, string> _map;
        private static string _file;

        private static bool File_ => DataFolder.OffPc;

        private static void Load()
        {
            if (_map != null) return;
            _map = new Dictionary<string, string>();
            _file = Path.Combine(DataFolder.Root, "настройки.txt");
            try
            {
                if (!File.Exists(_file)) return;
                foreach (var line in File.ReadAllLines(_file, Encoding.UTF8))
                {
                    int tab = line.IndexOf('\t');
                    if (tab > 0) _map[Unescape(line.Substring(0, tab))] = Unescape(line.Substring(tab + 1));
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[НАСТРОЙКИ] не прочитались: " + e.Message); }
        }

        private static void Flush()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var kv in _map) sb.Append(Escape(kv.Key)).Append('\t').Append(Escape(kv.Value)).Append('\n');
                File.WriteAllText(_file, sb.ToString(), new UTF8Encoding(false));
            }
            catch (System.Exception e) { Debug.LogWarning("[НАСТРОЙКИ] не записались: " + e.Message); }
        }

        private static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "");
        private static string Unescape(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char c = s[++i];
                    sb.Append(c == 't' ? '\t' : c == 'n' ? '\n' : c);
                }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }

        private static bool Raw(string key, out string value)
        {
            Load();
            return _map.TryGetValue(key, out value);
        }

        private static void Put(string key, string value) { Load(); _map[key] = value; Flush(); }

        public static int GetInt(string key, int fallback = 0)
        {
            if (!File_) return PlayerPrefs.GetInt(key, fallback);
            return Raw(key, out var s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }

        public static float GetFloat(string key, float fallback = 0f)
        {
            if (!File_) return PlayerPrefs.GetFloat(key, fallback);
            return Raw(key, out var s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }

        public static string GetString(string key, string fallback = "")
        {
            if (!File_) return PlayerPrefs.GetString(key, fallback);
            return Raw(key, out var s) ? s : fallback;
        }

        public static void SetInt(string key, int value)
        {
            if (!File_) { PlayerPrefs.SetInt(key, value); return; }
            Put(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void SetFloat(string key, float value)
        {
            if (!File_) { PlayerPrefs.SetFloat(key, value); return; }
            Put(key, value.ToString("R", CultureInfo.InvariantCulture));
        }

        public static void SetString(string key, string value)
        {
            if (!File_) { PlayerPrefs.SetString(key, value); return; }
            Put(key, value ?? "");
        }

        public static bool HasKey(string key)
        {
            if (!File_) return PlayerPrefs.HasKey(key);
            return Raw(key, out _);
        }

        public static void DeleteKey(string key)
        {
            if (!File_) { PlayerPrefs.DeleteKey(key); return; }
            Load();
            if (_map.Remove(key)) Flush();
        }

        public static void DeleteAll()
        {
            if (!File_) { PlayerPrefs.DeleteAll(); return; }
            Load();
            _map.Clear();
            Flush();
        }

        /// <summary>Файл пишется сразу при каждой перемене; реестру — его Save.</summary>
        public static void Save()
        {
            if (!File_) PlayerPrefs.Save();
        }
    }
}
