// Assets/Scripts/Crypt/MissionBoard.cs
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Что происходит, когда отряд уходит в шар и возвращается.
    ///
    /// Весь цикл склепа держится здесь: выбрал точку — выбрал старшего —
    /// они ушли — вернулись <b>другими</b>. Последнее и есть смысл зоны.
    /// Отряд, возвращающийся в том же составе и в том же настроении,
    /// превращает карту в кнопку «получить награду».
    ///
    /// <b>Бой считает движок, а не эта доска.</b>
    /// <see cref="Expedition"/> строит ушедших настоящими воинами
    /// и проводит бой через тот же резолвер, что решает всё остальное.
    /// Правило пролога §2 — «ни одна постановочная сцена не показывает
    /// того, чего движок не мог бы решить сам» — держится и здесь,
    /// хотя сцены нет вовсе.
    /// </summary>
    public class MissionBoard : MonoBehaviour
    {
        /// <summary>Что вышло из последней вылазки. Читает карта.</summary>
        public string LastReport { get; private set; } = "";

        /// <summary>Сколько улучшений склепа уже принесено. Их всего два.</summary>
        public static int Upgrades { get; private set; }

        public const int UpgradeLimit = 2;

        /// <summary>Точки карты. Пока постоянные — карта не тасуется.</summary>
        public Mission[] Missions => MissionCatalog.All();

        /// <summary>
        /// Кого можно поставить старшим на эту точку и почему нельзя
        /// остальных. Причина обязана быть названа: серая строка без
        /// объяснения — это интерфейс, который сломался.
        /// </summary>
        public string WhyNot(SquadRoster.Member member, Mission mission)
        {
            if (!string.IsNullOrEmpty(member.Unavailable)) return member.Unavailable;

            return Leadership.CanLead(member.Leadership, mission.Squad)
                ? ""
                : Leadership.Shortfall(member.Leadership, mission.Squad);
        }

        /// <summary>Хватит ли вообще людей на эту точку.</summary>
        public bool EnoughPeople(Mission mission)
        {
            int ready = 0;
            foreach (var m in SquadRoster.Members)
                if (string.IsNullOrEmpty(m.Unavailable)) ready++;

            return ready >= mission.Squad;
        }

        /// <summary>
        /// Отправить и решить. Возвращает отчёт словами.
        ///
        /// Всё происходит разом, без ожидания: вылазка — не сцена,
        /// а счёт. Смотреть в шар, пока считается бой, которого никто
        /// не видит, — занятие для полосы загрузки, а не для игрока.
        /// </summary>
        public string Send(Mission mission, string commanderName)
            => Send(mission, commanderName, null);

        /// <summary>
        /// То же, но игрок что-то предложил на развилке.
        ///
        /// Предложил, а не приказал: решает командир, и его решение
        /// может не совпасть с предложенным. Это тот же порядок, что
        /// и в бою, — просто на уровне дороги, а не тика.
        /// </summary>
        public string Send(Mission mission, string commanderName,
                           AOS.MissionAction? suggestion)
        {
            if (!EnoughPeople(mission))
                return "Столько людей не наберётся.";

            SquadRoster.ChooseCommander(commanderName);
            SquadRoster.SendAway(commanderName, mission.Squad, keepExperienced: false);

            var away = new List<SquadRoster.Member>();
            foreach (var m in SquadRoster.Away) away.Add(m);

            if (away.Count == 0) return "Никто не пошёл.";

            // Развилка решается до боя: она решает, будет ли бой вообще.
            AOS.MissionAction? chosen = mission.Junction == Junction.None
                ? null
                : Decide(mission, away, suggestion);

            bool noFight = chosen == AOS.MissionAction.LetThemPass;

            var fight = noFight
                ? new Expedition.Outcome(Names(away), 0, 0)
                : Expedition.Fight(away, mission.Guards);

            var survivors = fight.Survivors;

            // Состав меняется до отчёта: отчёт рассказывает о том, что уже
            // случилось, а не назначает это.
            SquadRoster.ComeBack(survivors);

            LastReport = Report(mission, away, survivors, commanderName);

            if (chosen.HasValue)
            {
                LastReport += " " + JunctionCatalog.Told(chosen.Value);
                Judge(away, survivors, chosen.Value);
            }

            if (survivors.Count > 0)
            {
                TakeSpoils(mission, chosen);

                // Золото несут с любой вылазки, но взять его некуда,
                // пока нет Казны. Поэтому улучшение — не прибавка
                // к числу, а разрешение числу вообще существовать.
                if (CryptUpgrades.Installed(Upgrade.Treasury))
                {
                    int coin = fight.Taken + Carried(mission, chosen);
                    if (coin > 0)
                    {
                        Inventory.PlayerInventory.Instance?.AddGold(coin);
                        Log("В казну прибыло.");
                    }
                }
            }

            // Отряд вернулся — точка, к которой игрок захочет вернуться
            // сам, и единственная, которую он не выбирает. В режиме
            // обязательств она же и единственная возможная.
            Core.SaveSystem.AutoSave();

            return LastReport;
        }

        private static List<string> Names(List<SquadRoster.Member> away)
        {
            var names = new List<string>();
            foreach (var m in away) names.Add(m.Name);
            return names;
        }

        /// <summary>
        /// Что решит командир на развилке.
        ///
        /// Спрашивает <see cref="AOS.BehaviourResolver"/> — тот же слой,
        /// что решает мирные миссии, и те же семь модулей. Предложение
        /// игрока входит в голосование как голос верности и может
        /// проиграть совести или греху.
        /// </summary>
        private static AOS.MissionAction Decide(Mission mission,
            List<SquadRoster.Member> away, AOS.MissionAction? suggestion)
        {
            var options = JunctionCatalog.Options(mission.Junction);
            if (options.Count == 0) return suggestion ?? default;

            SquadRoster.Member boss = away[0];
            foreach (var m in away) if (m.IsCommander) boss = m;

            var holder = new GameObject("Развилка");
            holder.SetActive(false);   // ничьих Awake и Update: это вопрос, не сцена

            try
            {
                var relations = new RelationshipSystem(AOS.MemoryProcessor.Instance);
                var commander = Expedition.Summon(holder, boss, relations);

                var context = new AOS.MissionContext
                {
                    // Обоз описывается теми же фактами, что были в контексте
                    // с самого начала: беззащитные, добыча, виноватых нет.
                    // Заводить под него новые поля значило бы объявить,
                    // что грабёж обоза — не то же самое, что грабёж вообще.
                    HasInnocentVictims = true,
                    HasTreasure = mission.Prize != Prize.None,
                    HasGuiltyParty = false,
                    RecentMemories = new List<AOS.MemoryRecord>(),
                    CarriedItems = new List<Inventory.InventoryItem>(),
                    HasSuggestion = suggestion.HasValue,
                    SuggestedAction = suggestion ?? default
                };

                var resolver = new AOS.BehaviourResolver();
                return resolver.DecideMission(commander, context, options);
            }
            finally
            {
                if (Application.isPlaying) Destroy(holder);
                else DestroyImmediate(holder);
            }
        }

        /// <summary>
        /// Сколько денег унесли. Уйти — значит уйти ни с чем, а увести
        /// живых — значит бросить товар: руки заняты.
        /// </summary>
        private static int Carried(Mission mission, AOS.MissionAction? chosen)
        {
            if (!chosen.HasValue) return MissionCatalog.Coin(mission.Prize);

            switch (chosen.Value)
            {
                case AOS.MissionAction.LetThemPass: return 0;
                case AOS.MissionAction.TakePeople:  return 0;
                default:                            return MissionCatalog.Coin(mission.Prize);
            }
        }

        /// <summary>
        /// Что вернувшиеся думают о том, что сделали. Правило —
        /// в <see cref="Aftermath"/>; здесь только применение.
        /// </summary>
        private static void Judge(List<SquadRoster.Member> away,
            List<string> survivors, AOS.MissionAction chosen)
        {
            foreach (var m in away)
            {
                if (!survivors.Contains(m.Name)) continue;

                float shift = Aftermath.LoyaltyShift(m.Moral, chosen);
                if (shift != 0f) SquadRoster.ShiftLoyalty(m.Name, shift);

                Log($"{m.Name}: {Aftermath.Judged(m.Moral, chosen)}.");
            }
        }

        /// <summary>
        /// Кто вернулся и чем это объясняют. Объяснение берётся
        /// у <see cref="Homecoming"/> — там же, где его берёт эпилог
        /// пролога, чтобы одно и то же не рассказывалось по-разному.
        /// </summary>
        private static string Report(Mission mission, List<SquadRoster.Member> away,
            IReadOnlyList<string> survivors, string commanderName)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(mission.Name).Append(". ");

            if (survivors.Count == 0)
            {
                sb.Append("Не вернулся никто.");

                foreach (var m in away)
                    if (m.Name == commanderName)
                    { sb.Append(' ').Append(Homecoming.Story(m.Sin)); break; }

                return sb.ToString();
            }

            sb.Append("Вернулись: ");
            for (int i = 0; i < survivors.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(survivors[i]);
            }
            sb.Append('.');

            if (survivors.Count < away.Count)
                foreach (var m in away)
                    if (m.Name == commanderName)
                    { sb.Append(' ').Append(Homecoming.Story(m.Sin)); break; }

            sb.Append(" Им теперь должны.");
            return sb.ToString();
        }

        /// <summary>
        /// Добыча. Души кладутся в жатву — то есть на полку зоны
        /// связывания, потому что полка это вид на жатву, а не свой запас.
        /// </summary>
        private void TakeSpoils(Mission mission, AOS.MissionAction? chosen)
        {
            // Развилка меняет не только деньги. Резня оставляет души,
            // уведённые живыми — тела; отпущенный обоз не оставляет
            // ничего, и это тоже исход.
            if (chosen == AOS.MissionAction.TakeEverything) BringSoul(mission.Name);
            if (chosen == AOS.MissionAction.TakePeople)
                Log("Живых увели. Что с ними делать, склеп ещё не решил.");
            if (chosen == AOS.MissionAction.LetThemPass) return;

            switch (mission.Spoils)
            {
                case Spoils.Souls:
                    BringSoul(mission.Name);
                    break;

                case Spoils.Upgrade:
                    if (CryptUpgrades.Bring(out var what))
                    {
                        Upgrades++;
                        Log($"Оттуда принесли: {CryptUpgrades.Name(what)}. "
                          + "Найдётся гнездо — поставите.");
                    }
                    else Log("Такое у вас уже есть, и второго места нет.");
                    break;

                case Spoils.Shell:
                    // Тела на столе не кончаются: подставка — витрина,
                    // а не склад. Считать оболочки имеет смысл там, где
                    // их добывают, и это работа основной игры.
                    Log("Оттуда принесли тело. Оно на столе.");
                    break;
            }
        }

        /// <summary>
        /// Принесённая душа. Свежесть — «Принятие»: её везли,
        /// а не собирали в момент смерти. Тот же <see cref="SoulDecay"/>,
        /// что и у настоящей жатвы, иначе привезённая душа отличалась бы
        /// от собранной ничем, кроме происхождения.
        /// </summary>
        private static void BringSoul(string from)
        {
            var souls = SoulManager.Instance;
            if (souls == null) return;

            // Грех выбирается по имени точки, а не броском: две вылазки
            // на одну точку обязаны приносить одно и то же.
            int hash = 0;
            foreach (char c in from) hash = (hash * 31 + c) & 0x7FFFFFFF;

            var sin = (SinType)(hash % 7);
            var moral = (MoralType)(hash / 7 % 3);

            var soul = new SoulData("Безымянная душа", sin, moral, 1, 40f + hash % 40);
            var kept = SoulDecay.Harvest(soul, SoulQuality.Acceptance);

            if (kept == null) return;

            souls.PutBack(new SoulManager.Kept(kept, SoulQuality.Acceptance));
            Log("Оттуда принесли душу. Она на полке.");
        }

        /// <summary>Забыть добычу при новой игре.</summary>
        public static void Forget()
        {
            Upgrades = 0;
            CryptUpgrades.Forget();
        }

        private static void Log(string line)
            => Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
    }
}
