// Проба сохранений (автор, 23.11: «полную проверку все и везде где нужно и как нужно сохранять»).
// Настоящий путь: снимок → файл (JSON) → порча живого состояния → возврат на месте и со сменой сцены.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Sinbinder.AOS;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.EditorTools
{
    public static class SaveProbe
    {
        private static SaveGame _saved;
        private static string _who;
        private const string Target = "проба-спасший";
        private static string LogPath => Path.Combine(Application.dataPath, "..", "Logs", "save-probe.txt");

        private static void Write(string line)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.AppendAllText(LogPath, line + "\n", new UTF8Encoding(false));
            Debug.Log("[ПРОБА ЗАПИСИ] " + line);
        }

        private static Warrior Squaddie()
        {
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                if (w != null && !w.IsDead && w.Team == Team.Player && !(w is SinbinderPlayer) && w.Soul != null
                    && (_who == null || w.DisplayName == _who)) return w;
            return null;
        }

        /// <summary>Записать: у воина — память и подвиг, Греховод ранен вполовину. Потом всё испортить.</summary>
        public static string Prepare()
        {
            if (!EditorApplication.isPlaying) return "нужна игра";
            File.WriteAllText(LogPath, "", new UTF8Encoding(false));
            _who = null;
            var w = Squaddie();
            var hero = SinbinderPlayer.Exists ? SinbinderPlayer.Instance : null;
            if (w == null || hero == null || MemoryProcessor.Instance == null) return "нет воина, Греховода или памяти";
            _who = w.DisplayName;
            MemoryProcessor.Instance.CreateMemory(w, "AllySavedMe", Target, EmotionType.Joy, 0.8f);
            w.Reputation.Deeds.Add(new DeedRecord { Type = DeedType.SaveAlly, Importance = 0.7f });
            w.Reputation.Respect = 11f;
            hero.SetHealthFraction(0.5f);
            _saved = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(SaveSystem.Snapshot()));
            Write($"=== ЗАПИСАНО: {_who} помнит «спас меня», подвигов {w.Reputation.Deeds.Count}, уважение 11; Греховод 50%; " +
                  $"в файле памяти AOS: {_saved.AosMemories.Count}, здоровье Греховода {_saved.HeroHealth:P0}");

            // Порча: память стёрта, подвиги сняты, Греховод цел.
            MemoryProcessor.ForgetAll();
            w.Reputation.Deeds.Clear();
            w.Reputation.Respect = 0f;
            hero.SetHealthFraction(1f);
            Write("  испорчено: память пуста, подвигов 0, Греховод 100%");
            return State("после порчи");
        }

        /// <summary>Загрузка на месте (без смены сцены).</summary>
        public static string LoadHere()
        {
            if (_saved == null) return "сначала Prepare";
            bool ok = SaveSystem.Restore(JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(_saved)));
            return State("загрузка на месте " + (ok ? "принята" : "ОТКАЗ"));
        }

        /// <summary>Загрузка со сменой сцены (как «С начала доли» / загрузка из меню).</summary>
        public static string LoadReload()
        {
            if (_saved == null) return "сначала Prepare";
            var hero = SinbinderPlayer.Exists ? SinbinderPlayer.Instance : null;
            MemoryProcessor.ForgetAll();
            hero?.SetHealthFraction(1f);
            bool ok = SaveSystem.ReturnTo(JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(_saved)), reload: true);
            Write("  загрузка со сменой сцены " + (ok ? "начата" : "ОТКАЗ"));
            return ok ? "сцена грузится" : "отказ";
        }

        public static string Check() => State("после смены сцены");

        private static string State(string when)
        {
            var w = Squaddie();
            var hero = SinbinderPlayer.Exists ? SinbinderPlayer.Instance : null;
            var sb = new StringBuilder(when + ": ");
            if (w == null) sb.Append($"воина {_who} нет; ");
            else
            {
                bool remembers = false;
                if (MemoryProcessor.Instance != null)
                    foreach (var m in MemoryProcessor.Instance.GetMemories(w))
                        if (m.EventType == "AllySavedMe" && m.TargetID == Target) remembers = true;
                bool deed = w.Reputation.Deeds.Exists(d => d.Type == DeedType.SaveAlly);
                sb.Append($"{w.DisplayName}: помнит {(remembers ? "ДА" : "нет")}, подвиг {(deed ? "ДА" : "нет")}, уважение {w.Reputation.Respect:F0}; ");
            }
            sb.Append(hero != null ? $"Греховод {hero.HP / hero.MaxHP:P0}" : "Греховода нет");
            Write("  " + sb);
            return sb.ToString();
        }
    }
}
