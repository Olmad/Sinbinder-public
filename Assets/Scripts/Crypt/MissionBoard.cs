// Assets/Scripts/Crypt/MissionBoard.cs
// Перевод: текст через Loc
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
    /// <summary>Serializable in-flight automatic expedition. Old saves have an empty list.</summary>
    [System.Serializable]
    public sealed class SavedAutoExpedition
    {
        public string MissionName;
        public string Commander;
        public List<string> Party = new List<string>();
        public bool HasSuggestion;
        public int SuggestedAction;
        public bool Recalled;
        // 0: outbound, 1: awaiting instructions, 2: returning
        public int Phase;
        public float SecondsRemaining;
    }

    public class MissionBoard : MonoBehaviour
    {
        /// <summary>Что вышло из последней вылазки. Читает карта.</summary>
        public string LastReport { get; private set; } = "";

        /// <summary>Every recorded action, not the three-line retelling.</summary>
        public ExpeditionReplayData LastReplay { get; private set; }

        /// <summary>Сколько вылазок ушло с этой доски. Спрашивает склеп демо (<see cref="CryptMap"/>).</summary>
        public int Sent { get; private set; }

        /// <summary>Сколько улучшений склепа уже принесено. Их всего два.</summary>
        public static int Upgrades { get; private set; }

        public const int UpgradeLimit = 2;
        private static readonly List<SavedAutoExpedition> ActiveJourneys =
            new List<SavedAutoExpedition>();
        private const float OutboundSeconds = 15f;
        private const float ReturnSeconds = 15f;
        private const int MaxActiveJourneys = 4;

        public static IReadOnlyList<SavedAutoExpedition> Journeys => ActiveJourneys;
        public static List<SavedAutoExpedition> SnapshotJourneys()
        {
            var result = new List<SavedAutoExpedition>();
            foreach (var item in ActiveJourneys)
                result.Add(CopyJourney(item));
            return result;
        }

        private static SavedAutoExpedition CopyJourney(SavedAutoExpedition item)
            => new SavedAutoExpedition
            {
                MissionName=item.MissionName, Commander=item.Commander,
                Party=item.Party != null ? new List<string>(item.Party) : new List<string>(),
                HasSuggestion=item.HasSuggestion, SuggestedAction=item.SuggestedAction,
                Recalled=item.Recalled, Phase=item.Phase, SecondsRemaining=item.SecondsRemaining
            };

        public static void RestoreJourneys(List<SavedAutoExpedition> saved)
        {
            ActiveJourneys.Clear();
            if (saved == null) return;
            foreach (var item in saved)
            {
                if (item==null || string.IsNullOrEmpty(item.MissionName) ||
                    string.IsNullOrEmpty(item.Commander) || item.Party==null ||
                    item.Party.Count==0 || item.Phase<0 || item.Phase>2 ||
                    float.IsNaN(item.SecondsRemaining) ||
                    float.IsInfinity(item.SecondsRemaining)) continue;
                // Older or corrupt saves cannot create an unbounded queue.
                if (ActiveJourneys.Count>=MaxActiveJourneys) break;
                var exists=false;
                foreach (var mission in MissionCatalog.All())
                    if (mission.Name==item.MissionName) exists=true;
                if (!exists) continue;
                var valid=true;
                foreach (var name in item.Party)
                {
                    if (!SquadRoster.TryGet(name,out var m) || !m.IsAway)
                    { valid=false; break; }
                }
                if (!valid) continue;
                var copy=CopyJourney(item);
                copy.SecondsRemaining=Mathf.Clamp(copy.SecondsRemaining,0f,3600f);
                ActiveJourneys.Add(copy);
            }
        }

        private void Update()
        {
            if (ActiveJourneys.Count==0 || Time.deltaTime<=0f) return;
            // Walk backwards: completed journeys are removed while the others
            // continue ticking. Autosave always sees the surviving operations.
            for (int i=ActiveJourneys.Count-1;i>=0;i--)
            {
                var journey=ActiveJourneys[i];
                if (journey.Phase==1) continue;
                journey.SecondsRemaining-=Time.deltaTime;
                if (journey.SecondsRemaining>0f) continue;

                if (journey.Phase==0)
                {
                    Mission mission=default;
                    foreach (var candidate in MissionCatalog.All())
                        if (candidate.Name==journey.MissionName) { mission=candidate; break; }
                    if (mission.Junction!=Junction.None && !journey.HasSuggestion)
                    {
                        journey.Phase=1;
                        journey.SecondsRemaining=0f;
                        LastReport=Loc.F("Отряд «{0}» прибыл к развилке. Требуется решение игрока.",
                            Loc.Name(journey.Commander));
                    }
                    else
                    {
                        journey.Phase=2;
                        journey.SecondsRemaining=ReturnSeconds;
                        LastReport=Loc.F("Отряд «{0}» на месте. Ждём возвращения и доклада.",
                            Loc.Name(journey.Commander));
                    }
                    continue;
                }
                if (journey.Phase==2)
                {
                    ActiveJourneys.RemoveAt(i);
                    CompleteJourney(journey);
                }
            }
        }

        /// <summary>Advice is addressed to a specific commander, never all squads.</summary>
        public bool OfferJourneyOrder(string commanderName, AOS.MissionAction? offer)
        {
            foreach (var journey in ActiveJourneys)
            {
                if (journey.Commander!=commanderName || journey.Phase!=1) continue;
                journey.HasSuggestion=offer.HasValue;
                journey.SuggestedAction=offer.HasValue ? (int)offer.Value : 0;
                journey.Phase=2;
                journey.SecondsRemaining=ReturnSeconds;
                LastReport=Loc.F("Совет передан старшему «{0}». Он сам примет решение.",
                    Loc.Name(commanderName));
                return true;
            }
            return false;
        }

        /// <summary>Recall the selected squad before contact, without combat or loot.</summary>
        public bool RecallJourney(string commanderName)
        {
            foreach (var journey in ActiveJourneys)
            {
                if (journey.Commander!=commanderName || journey.Phase!=0) continue;
                journey.Recalled=true;
                journey.Phase=2;
                journey.SecondsRemaining=ReturnSeconds;
                LastReport=Loc.F("Отряд «{0}» отозван. Ожидается возвращение.",
                    Loc.Name(commanderName));
                Core.SaveSystem.AutoSave();
                return true;
            }
            return false;
        }

        private void CompleteJourney(SavedAutoExpedition journey)
        {
            Mission found=default;
            bool exists=false;
            foreach (var mission in MissionCatalog.All())
                if (mission.Name==journey.MissionName)
                { found=mission; exists=true; break; }
            if (!exists) { LastReport=Loc.T("Маршрут экспедиции больше недоступен."); return; }

            var away=new List<SquadRoster.Member>();
            foreach (var name in journey.Party)
                if (SquadRoster.TryGet(name,out var member) && member.IsAway)
                {
                    member.IsCommander=member.Name==journey.Commander;
                    away.Add(member);
                }
            if (away.Count!=journey.Party.Count)
            {
                LastReport=Loc.T("Состав похода изменился. Автоматический расчёт отменён, награды не начислены.");
                return;
            }
            if (journey.Recalled)
            {
                SquadRoster.ComeBack(journey.Party,journey.Party);
                Settle(journey.Party,journey.Party);
                LastReport=Loc.F("Отряд «{0}» вернулся по вашему приказу. Боя и добычи не было.",
                    Loc.Name(journey.Commander));
                LastReplay=new ExpeditionReplayData(Loc.Name(found.Name),
                    Loc.Name(journey.Commander),journey.Party);
                LastReplay.Append(ExpeditionReplayKind.Departure,Loc.Name(journey.Commander),
                    Loc.T("Отряд вышел из склепа."));
                LastReplay.Append(ExpeditionReplayKind.March,Loc.Name(journey.Commander),
                    Loc.T("Отряд двигался к цели."));
                LastReplay.Append(ExpeditionReplayKind.CommandDecision,Loc.Name(journey.Commander),
                    Loc.T("Греховод приказал вернуться до столкновения."));
                LastReplay.Append(ExpeditionReplayKind.Return,Loc.Name(journey.Commander),
                    Loc.T("Отряд вернулся без боя и добычи."));
                ExpeditionReplayArchive.Add(LastReplay);
                Core.SaveSystem.AutoSave();
                return;
            }
            ResolveReturned(found,journey.Commander,away,new List<string>(journey.Party),
                journey.HasSuggestion
                    ? (AOS.MissionAction?)journey.SuggestedAction : null);
        }


        /// <summary>
        /// Called on real return from a personally traversed map.
        /// Items, money, HP and AOS are already real scene state, not a
        /// second fake reward from the instant automatic resolver.
        /// </summary>
        public void RecordPersonal(Mission mission, bool recoveredSomething)
        {
            Sent++;
            LastReplay = null; // no false "automatic" reconstruction of live play
            LastReport = recoveredSomething
                ? Loc.F("Греховод лично вернулся из «{0}» с находкой.", Loc.Name(mission.Name))
                : Loc.F("Греховод вернулся из «{0}». То, что осталось, ждёт следующего визита.",
                    Loc.Name(mission.Name));
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(LastReport);
        }

        /// <summary>Точки карты. Пока постоянные — карта не тасуется.</summary>
        public Mission[] Missions => MissionCatalog.All();

        /// <summary>
        /// Почему его нельзя поставить старшим. Причина обязана быть названа:
        /// серая строка без объяснения — это интерфейс, который сломался.
        ///
        /// С 16 октября нехватка навыка — не запрет (автор: «нужно, чтобы была
        /// возможность отправить и меньше… даже просто лидера»): рядовой
        /// поведёт, но уведёт троих. Это риск, и о нём строка выбора говорит
        /// сама (<see cref="Leadership.Shortfall"/>). Запрещают только телохранитель
        /// и дорога.
        /// </summary>
        public string WhyNot(SquadRoster.Member member, Mission mission)
        {
            if (!string.IsNullOrEmpty(member.Unavailable)) return Loc.T(member.Unavailable);
            if (member.IsAway) return Loc.T("В пути с другим отрядом.");
            return "";
        }

        /// <summary>Сколько людей сейчас может уйти. Ушедшие с другим отрядом — не здесь.</summary>
        public int Ready()
        {
            int ready = 0;
            foreach (var m in SquadRoster.Members)
                if (SquadRoster.CanGo(m)) ready++;
            return ready;
        }

        /// <summary>
        /// Хватит ли людей на столько, сколько советуют для этой точки.
        /// Не порог: послать можно и меньше — это риск, а не запрет.
        /// </summary>
        public bool EnoughPeople(Mission mission) => Ready() >= mission.Squad;

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
            => Send(mission, commanderName,
                    SquadRoster.Suggest(commanderName, Fits(commanderName, mission), keepExperienced: false),
                    suggestion);

        /// <summary>
        /// Сколько уйдёт по подсказке: сколько советуют, но не больше, чем
        /// старший уведёт.
        /// </summary>
        public static int Fits(string commanderName, Mission mission)
        {
            float skill = SquadRoster.TryGet(commanderName, out var m) ? m.Leadership : 0f;
            return System.Math.Min(mission.Squad, Leadership.SquadSize(skill));
        }

        /// <summary>
        /// То же, но спутников старшему выбрал игрок (с 16 октября: окно карты
        /// собирает отряд, а не подбирает его списком). Без старшего,
        /// на <c>mission.Squad</c> минус одно место.
        /// </summary>
        public string Send(Mission mission, string commanderName, ICollection<string> companions,
                           AOS.MissionAction? suggestion)
        {
            if (ActiveJourneys.Count>=MaxActiveJourneys)
                return Loc.T("Слишком много одновременных экспедиций.");
            if (Ready() == 0)
                return Loc.T("Послать некого.");

            // Вылазка — только те, кого послали сейчас. Отряд, ушедший
            // из лагеря, ещё в пути и вернётся в эпилоге: до 29 сентября
            // вылазка забирала в бой всех ушедших разом, а вернувшись,
            // вычёркивала из отряда тех, кто на неё не ходил, и снимала
            // старшего с того отряда (SquadRoster.ChooseCommander).
            var before = new HashSet<string>();
            foreach (var m in SquadRoster.Away) before.Add(m.Name);
            if (!SquadRoster.TryGet(commanderName, out var commander) ||
                !SquadRoster.CanGo(commander))
                return Loc.T("Выбранный старший недоступен.");
            if (before.Contains(commanderName))
                return Loc.F("{0} уже в пути.", Loc.Name(commanderName));

            SquadRoster.SendAway(commanderName, companions);

            // Старший — на копиях: бой (Expedition) и развилка его видят,
            // а общий отряд своего старшего не меняет.
            var away = new List<SquadRoster.Member>();
            var party = new List<string>();
            foreach (var m in SquadRoster.Away)
            {
                if (before.Contains(m.Name)) continue;
                var copy = m;
                copy.IsCommander = m.Name == commanderName;
                away.Add(copy);
                party.Add(m.Name);
            }

            if (away.Count == 0) return Loc.T("Никто не пошёл.");
            Sent++;
            ActiveJourneys.Add(new SavedAutoExpedition
            {
                MissionName=mission.Name, Commander=commanderName,
                Party=new List<string>(party), Phase=0,
                SecondsRemaining=OutboundSeconds,
                HasSuggestion=suggestion.HasValue,
                SuggestedAction=suggestion.HasValue ? (int)suggestion.Value : 0
            });
            LastReport=Loc.F("Отряд «{0}» отправлен к месту «{1}». Отряд в пути.",
                Loc.Name(commanderName),Loc.Name(mission.Name));
            Core.SaveSystem.AutoSave();
            return LastReport;
        }

        private void ResolveReturned(Mission mission, string commanderName,
            List<SquadRoster.Member> away, List<string> party,
            AOS.MissionAction? suggestion)
        {
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
            SquadRoster.ComeBack(party, survivors);

            LastReport = Report(mission, away, survivors, commanderName);

            // Что там было. До этого отчёт говорил, кто вернулся,
            // и молчал о том, почему остальные не вернулись.
            string told = Retelling.Tell(fight.Record);
            if (!string.IsNullOrEmpty(told)) LastReport += " " + told;

            if (chosen.HasValue)
            {
                LastReport += " " + JunctionCatalog.Told(chosen.Value);
                Judge(away, survivors, chosen.Value);
            }

            // Ушедших видно в склепе — сцена догоняет состав: не вернувшихся
            // в ней больше нет, вернувшимся должны.
            Settle(party, survivors);

            var shares = new List<ExpeditionLootShare>();
            int coinsForLord = 0;
            if (survivors.Count > 0)
            {
                TakeSpoils(mission, chosen);

                // Добыча — в кошель Греховода, числом (монеты — то, чем он
                // владеет и что тратит; решение автора 30 сентября). До
                // 10 октября она шла только в Казну, а Казны в склепе демо
                // нет: ограбленный обоз не приносил ни монеты, и «хочу игру,
                // где нужно грабить караваны» (автор) кончалось пустыми
                // руками. Правило «серебро закроет долг разом» записано
                // в 39-PLACES §2.1; Казна была нужна, пока платить было
                // нечем, — теперь кошель есть и плата лично тоже.
                int coin = fight.Taken + Carried(mission, chosen);
                // The same physical coin total, but AOS characters can
                // pocket a portion according to Greed/Envy, the value of
                // the find and their devotion. No soul, secret or coin
                // is created by the allocation.
                var witnesses = new List<ExpeditionLootPerson>();
                foreach(var m in away)
                {
                    if(!survivors.Contains(m.Name)) continue;
                    var drive = m.Sin switch
                    {
                        SinType.Greed => ExpeditionGreed.Greed,
                        SinType.Envy => ExpeditionGreed.Envy,
                        SinType.Pride => ExpeditionGreed.Pride,
                        _ => ExpeditionGreed.Other
                    };
                    witnesses.Add(new ExpeditionLootPerson(m.Name,drive,m.Loyalty));
                }
                var split = ExpeditionAutoLoot.Allocate(coin,witnesses);
                shares.AddRange(split.PersonalCoins);
                foreach(var share in shares)
                {
                    Warrior present = null;
                    foreach(var w in Squad())
                        if(w.DisplayName == share.Name && !w.IsDead)
                        {present = w;break;}
                    if(present!=null) present.Pocket(share.Coins);
                    else SquadRoster.CreditPocket(share.Name,share.Coins);
                    Log(Loc.F("{0} оставил себе из вылазки {1} монет.",
                        Loc.Name(share.Name),share.Coins));
                }
                coinsForLord = split.ReturnedCoins;
                if (coinsForLord > 0)
                {
                    Inventory.PlayerInventory.Instance?.AddGold(coinsForLord);
                    Log(Loc.F("Добычу ссыпали в ваш кошель: +{0}.", coinsForLord));
                }

                // Пластинка — награда вылазки песочницы (37-DEMO §8.4): тема по месту, не сила.
                var record = Core.Records.FromMission(mission.Name);
                if (record != null)
                    Log(Loc.F("С вылазки привезли: {0} — «{1}». Граммофон в склепе.", Core.Records.MetalWord(record), Loc.T(record.Title)));
            }

            LastReplay = BuildReplay(mission, commanderName, party, chosen,
                fight, survivors, shares, coinsForLord);
            ExpeditionReplayArchive.Add(LastReplay);

            // Отряд вернулся — точка, к которой игрок захочет вернуться
            // сам, и единственная, которую он не выбирает. В режиме
            // обязательств она же и единственная возможная.
            Core.SaveSystem.AutoSave();

        }

        /// <summary>
        /// Preserve EVERY engine-recorded combat event in chronological
        /// order. March and return are abstract route stages because the
        /// original instant simulator does not store world coordinates.
        /// A replay viewer must label them schematic, not pretend the
        /// warriors walked through a full real-time scene.
        /// </summary>
        private static ExpeditionReplayData BuildReplay(Mission mission, string leader,
            List<string> party, AOS.MissionAction? chosen,
            Expedition.Outcome outcome, List<string> survivors,
            List<ExpeditionLootShare> shares, int coinsForLord)
        {
            var replay = new ExpeditionReplayData(Loc.Name(mission.Name),Loc.Name(leader),party);
            replay.Append(ExpeditionReplayKind.Departure,Loc.Name(leader),
                Loc.T("Отряд вышел из склепа."));
            replay.Append(ExpeditionReplayKind.March,Loc.Name(leader),
                Loc.F("Отряд направился к месту «{0}».",Loc.Name(mission.Name)));

            if(chosen.HasValue)
                replay.Append(ExpeditionReplayKind.CommandDecision,Loc.Name(leader),
                    JunctionCatalog.Told(chosen.Value));
            if(outcome.Record != null && outcome.Record.Events != null)
            {
                foreach(var ev in outcome.Record.Events)
                {
                    if(ev==null)continue;
                    string actor=string.IsNullOrEmpty(ev.ActorName)
                        ? Loc.T("Неизвестный воин") : Loc.Name(ev.ActorName);
                    replay.Append(ExpeditionReplayKind.CombatAction,actor,
                        Retelling.Describe(ev),ev.TargetId,ev.DamageDealt,ev.HealDealt);
                }
            }

            if(outcome.Record == null)
                replay.Append(ExpeditionReplayKind.CommandDecision,Loc.Name(leader),
                    Loc.T("Бой не состоялся: решение было принято на развилке."));

            if(survivors.Count>0)
            {
                replay.Append(ExpeditionReplayKind.Loot,Loc.Name(leader),
                    Loc.F("С павших противников взяли монет: {0}.",outcome.Taken));
                foreach(var taken in shares)
                    replay.Append(ExpeditionReplayKind.Loot,Loc.Name(taken.Name),
                        Loc.F("Оставил себе {0} монет.",taken.Coins));
                replay.Append(ExpeditionReplayKind.Loot,Loc.T("Отряд"),
                    Loc.F("Общая добыча, вернувшаяся в казну: {0} монет.",coinsForLord));
            }
            else
                replay.Append(ExpeditionReplayKind.Loot,"",Loc.T("Отряд погиб; добыча осталась на месте."));

            replay.Append(ExpeditionReplayKind.Return,Loc.Name(leader),
                survivors.Count>0
                    ? Loc.F("Вернулись: {0}.",string.Join(", ",survivors))
                    : Loc.T("К склепу не вернулся никто."));
            return replay;
        }

        /// <summary>
        /// Свои живые воины в сцене, кроме Греховода, — те, кого можно послать.
        /// Состав отряда снимается с них (<see cref="SquadRoster.Remember"/>):
        /// поднятые в склепе попадают в него только так.
        /// </summary>
        public static IEnumerable<Warrior> Squad()
        {
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
                if (w != null && !w.IsDead && w.Team == Team.Player && !(w is SinbinderPlayer))
                    yield return w;
        }

        /// <summary>
        /// Сцена — по составу после вылазки. Вылазка считается разом, без
        /// сцены, а ушедшие стоят в склепе: не вернувшийся оставался бы
        /// стоять, а вернувшийся — без долга, который записан в составе,
        /// и при следующем снятии состава долг стёрся бы.
        /// </summary>
        private static void Settle(List<string> party, IReadOnlyList<string> survivors)
        {
            var stay = new HashSet<string>(survivors);
            var gone = new List<Warrior>();

            foreach (var w in Squad())
            {
                if (!party.Contains(w.DisplayName)) continue;
                if (!stay.Contains(w.DisplayName)) { gone.Add(w); continue; }
                if (!SquadRoster.TryGet(w.DisplayName, out var m)) continue;

                w.UnpaidMissions = m.UnpaidMissions;
                w.ChangeLoyalty(m.Loyalty - w.Loyalty);
            }

            foreach (var w in gone)
            {
                if (Application.isPlaying) Destroy(w.gameObject);
                else DestroyImmediate(w.gameObject);
            }
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

                Log($"{Loc.Name(m.Name)}: {Aftermath.Judged(m.Moral, chosen, m.Gender)}.");
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
            sb.Append(Loc.Name(mission.Name)).Append(". ");

            if (survivors.Count == 0)
            {
                sb.Append(Loc.T("Не вернулся никто."));

                foreach (var m in away)
                    if (m.Name == commanderName)
                    { sb.Append(' ').Append(Homecoming.Story(m.Sin, m.Gender)); break; }

                return sb.ToString();
            }

            sb.Append(Loc.T("Вернулись: "));
            for (int i = 0; i < survivors.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                // Имя по старшинству, как везде в окнах (правило имён).
                sb.Append(Loc.Name(SquadRoster.TryGet(survivors[i], out var back) ? back.ShownName : survivors[i]));
            }
            sb.Append('.');

            if (survivors.Count < away.Count)
                foreach (var m in away)
                    if (m.Name == commanderName)
                    { sb.Append(' ').Append(Homecoming.Story(m.Sin, m.Gender)); break; }

            // 47-TEXTS §2, п. 13: что должны. Добычу журнал называет сам.
            sb.Append(survivors.Count == 1
                ? Loc.T(" Ему вы должны плату за эту вылазку.")
                : Loc.T(" Каждому из них вы должны плату за эту вылазку."));
            return sb.ToString();
        }

        /// <summary>
        /// Automatic expeditions cannot fabricate rare finds. No soldier
        /// currently has the origin/acquired soul-collector trait; the
        /// player's Sinbinder must personally harvest souls in a live map.
        /// The same goes for unique secrets without a qualified finder.
        /// Ordinary bodies, coin and other mundane loot use their
        /// existing paths and are still affected by the squad's choices.
        /// </summary>
        private void TakeSpoils(Mission mission, AOS.MissionAction? chosen)
        {
            if (chosen == AOS.MissionAction.TakePeople)
                Log(Loc.T("Живых привели связанными. Склепу пока нечего с ними делать."));
            if (chosen == AOS.MissionAction.LetThemPass) return;

            // Taking ordinary cargo must not invent a soul encounter.
            if (mission.Spoils == Spoils.Souls)
                Log(Loc.T("Души остались на месте: в отряде нет собирателя душ."));

            if (mission.Spoils == Spoils.Upgrade)
                Log(Loc.T("В отряде нет искателя секретов: редкую находку не обнаружили."));

            if (mission.Spoils == Spoils.Shell)
                Log(Loc.T("Оттуда принесли тело. Оно на столе."));
        }

        /// <summary>Забыть добычу при новой игре.</summary>
        public static void Forget()
        {
            Upgrades = 0;
            ActiveJourneys.Clear();
            CryptUpgrades.Forget();
        }

        private static void Log(string line)
            => Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
    }
}
