// Assets/Scripts/Core/Records.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Core
{
    /// <summary>
    /// Пластинки граммофона (37-DEMO §8; автор, 16.10: «граммофон с пластинками, и на них
    /// будут треки из демо»; 27.10: «скрытая пластинка с крысиной „Лунной сонатой“ лежит
    /// рядом с граммофоном и открывает счёт скрытых пластинок. Счёт — в меню паузы»).
    ///
    /// Три пути к пластинке, и все — то, что игрок прошёл сам, без жребия:
    /// * <b>прозвучала</b> — тема сцены сыграла в игре (<see cref="Audio.Music.Started"/>);
    /// * <b>скрытая</b> — найдена в мире (первая — «Лунная соната» у граммофона);
    /// * <b>с вылазки</b> — привёз вернувшийся отряд (песочница, по месту вылазки).
    ///
    /// Коллекция — игрока, а не партии: живёт между играми (PlayerPrefs), как и выбор графики.
    /// Пластинка даёт тему, а не силу: балансировать нечего.
    /// </summary>
    public static class Records
    {
        public enum Way { Heard, Hidden, Mission }

        public sealed class Record
        {
            public readonly string Id, Title, Clip, Where, Source;
            public readonly Way How;

            public Record(string id, string title, string clip, string where, Way how, string source)
            {
                Id = id; Title = title; Clip = clip; Where = where; How = how; Source = source;
            }
        }

        /// <summary>Порядок — порядок на граммофоне. Clip — путь в Resources.</summary>
        public static readonly Record[] All =
        {
            new("Moonlight", Loc.N("«Лунная соната» составом «Крыс»"), "Records/Moonlight",
                Loc.N("пластинка лежала у граммофона"), Way.Hidden, ""),
            new("Lullaby", Loc.N("Колыбельная «Крыс»"), "Music/Lullaby",
                Loc.N("так звучала дверь в игру"), Way.Heard, "Lullaby"),
            new("RatSong", Loc.N("Крысиная песня"), "Music/RatSong",
                Loc.N("так звучал лагерь"), Way.Heard, "RatSong"),
            new("RatLoot", Loc.N("Делят добычу"), "Music/RatLoot",
                Loc.N("так лагерь делил добычу"), Way.Heard, "RatLoot"),
            new("Scatter", Loc.N("Шуганули"), "Music/Scatter",
                Loc.N("так звучал набег"), Way.Heard, "Scatter"),
            new("Hunted", Loc.N("The Hunted"), "Music/Hunted",
                Loc.N("так звучало бегство из лагеря"), Way.Heard, "Hunted"),
            new("Lament", Loc.N("Нас мало"), "Music/Lament",
                Loc.N("так звучал склеп"), Way.Heard, "Lament"),
            new("HuntBegins", Loc.N("The Hunt Begins"), "Records/HuntBegins",
                Loc.N("привезли с Соляного обоза"), Way.Mission, Loc.N("Соляной обоз")),
            new("GateOfBone", Loc.N("The Gate of Bone"), "Records/GateOfBone",
                Loc.N("привезли из Старого гарнизона"), Way.Mission, Loc.N("Старый гарнизон")),
            new("RatPack", Loc.N("The Rat Pack"), "Records/RatPack",
                Loc.N("привезли с Сожжённой заставы"), Way.Mission, Loc.N("Сожжённая застава")),
            new("PawnToQueen", Loc.N("From Pawn to Queen"), "Records/PawnToQueen",
                Loc.N("привезли из Придорожной часовни"), Way.Mission, Loc.N("Придорожная часовня")),
        };

        private const string Key = "sinbinder.records";
        private static HashSet<string> _have;

        /// <summary>Открылась новая пластинка — граммофон и журнал скажут.</summary>
        public static event System.Action<Record> Opened;

        private static HashSet<string> Have
        {
            get
            {
                if (_have != null) return _have;
                _have = new HashSet<string>();
                foreach (var id in PlayerPrefs.GetString(Key, "").Split(','))
                    if (!string.IsNullOrEmpty(id)) _have.Add(id);
                return _have;
            }
        }

        public static bool Has(Record r) => r != null && Have.Contains(r.Id);

        /// <summary>
        /// Металл пластинки — сразу видно, чего стоила находка (автор, 27.10): медная —
        /// услышанная тема (собирается сама), серебряная — привёз отряд с вылазки,
        /// золотая — скрытая, найдена в тайнике.
        /// </summary>
        public enum Metal { Copper, Silver, Gold }

        public static Metal MetalOf(Record r) => r == null ? Metal.Copper
            : r.How == Way.Hidden ? Metal.Gold : r.How == Way.Mission ? Metal.Silver : Metal.Copper;

        public static string MetalWord(Record r) => MetalOf(r) switch
        {
            Metal.Gold => Loc.T("Золотая пластинка"),
            Metal.Silver => Loc.T("Серебряная пластинка"),
            _ => Loc.T("Медная пластинка"),
        };

        /// <summary>Открытые, в порядке граммофона.</summary>
        public static List<Record> Owned()
        {
            var list = new List<Record>();
            foreach (var r in All) if (Has(r)) list.Add(r);
            return list;
        }

        /// <summary>Скрытых найдено и всего — для меню паузы («Скрытые пластинки: 1 из 1»).</summary>
        public static (int found, int total) HiddenCount()
        {
            int found = 0, total = 0;
            foreach (var r in All)
            {
                if (r.How != Way.Hidden) continue;
                total++;
                if (Has(r)) found++;
            }
            return (found, total);
        }

        /// <summary>Прозвучала тема сцены: открыть её пластинку (имя записи — как у Music).</summary>
        public static void Heard(string file)
        {
            foreach (var r in All)
                if (r.How == Way.Heard && r.Source == file) Open(r);
        }

        /// <summary>Вернулись с вылазки: её пластинка — в коллекцию. Ложь — у вылазки пластинки нет или уже есть.</summary>
        public static Record FromMission(string missionName)
        {
            foreach (var r in All)
                if (r.How == Way.Mission && r.Source == missionName && !Has(r)) { Open(r); return r; }
            return null;
        }

        public static Record ById(string id)
        {
            foreach (var r in All) if (r.Id == id) return r;
            return null;
        }

        /// <summary>Новая игра: забыть золотые (скрытые) — тайники снова полны. Медные и серебряные остаются.</summary>
        public static void ForgetHidden()
        {
            bool changed = false;
            foreach (var r in All)
                if (r.How == Way.Hidden && Have.Remove(r.Id)) changed = true;
            if (!changed) return;
            PlayerPrefs.SetString(Key, string.Join(",", Have));
            PlayerPrefs.Save();
        }

        public static bool Open(Record r)
        {
            if (r == null || !Have.Add(r.Id)) return false;
            PlayerPrefs.SetString(Key, string.Join(",", Have));
            PlayerPrefs.Save();
            Opened?.Invoke(r);
            return true;
        }
    }
}
