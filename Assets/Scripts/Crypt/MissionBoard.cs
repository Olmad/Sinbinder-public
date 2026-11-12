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
        public string JourneyId;
        public List<string> Party = new List<string>();
        public bool HasSuggestion;
        public int SuggestedAction;
        public bool Recalled;
        // 0: outbound, 1: awaiting instructions, 2: returning
        public int Phase;
        public float SecondsRemaining;
        // Combat resolves at the encounter, not retrospectively upon homecoming.
        // These results travel with the squad and survive a save/load.
        public bool EncounterResolved;
        public bool EncounterFought;
        public bool BattleWon;
        public bool HasChosenAction;
        public int ChosenAction;
        public List<string> Survivors = new List<string>();
        public List<ExpeditionSurvivorHealth> SurvivorHealth =
            new List<ExpeditionSurvivorHealth>();
        public int TakenCoins;
        public int FoesFallen;
        public string BattleSummary;
        public ExpeditionReplayData EncounterTrace;
    }

    public class MissionBoard : MonoBehaviour
    {
        /// <summary>Что вышло из последней вылазки. Читает карта.</summary>
        // Reports belong to the world, not a particular scene's table.
        // When the expedition event runs in another scene, its result must
        // still be visible on the next crypt map.
        private static string _lastWorldReport = "";
        public static string SnapshotReport() => _lastWorldReport;
        public static void RestoreReport(string report)
        {
            _lastWorldReport=report ?? "";
            _lastWorldReplay=null;
        }

        public string LastReport
        {
            get => _lastWorldReport;
            private set => _lastWorldReport=value ?? "";
        }

        /// <summary>Every recorded action, not the three-line retelling.</summary>
        private static ExpeditionReplayData _lastWorldReplay;
        public ExpeditionReplayData LastReplay
        {
            get => _lastWorldReplay;
            private set => _lastWorldReplay=value;
        }

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
                JourneyId=item.JourneyId,
                Party=item.Party != null ? new List<string>(item.Party) : new List<string>(),
                HasSuggestion=item.HasSuggestion, SuggestedAction=item.SuggestedAction,
                Recalled=item.Recalled, Phase=item.Phase, SecondsRemaining=item.SecondsRemaining,
                EncounterResolved=item.EncounterResolved, EncounterFought=item.EncounterFought,
                BattleWon=item.BattleWon,
                HasChosenAction=item.HasChosenAction, ChosenAction=item.ChosenAction,
                Survivors=item.Survivors == null ? new List<string>() : new List<string>(item.Survivors),
                SurvivorHealth=CopyHealth(item.SurvivorHealth),
                TakenCoins=item.TakenCoins, FoesFallen=item.FoesFallen,
                BattleSummary=item.BattleSummary ?? "",
                EncounterTrace=CopyTrace(item.EncounterTrace)
            };

        private static List<ExpeditionSurvivorHealth> CopyHealth(
            List<ExpeditionSurvivorHealth> source)
        {
            var result=new List<ExpeditionSurvivorHealth>();
            if(source==null) return result; // compatibility with older saves
            foreach(var item in source)
                if(item!=null)
                    result.Add(new ExpeditionSurvivorHealth
                    {
                        Name=item.Name,
                        Fraction=item.Fraction
                    });
            return result;
        }

        private static ExpeditionReplayData CopyTrace(ExpeditionReplayData source)
        {
            if (source==null) return null;
            var copy=new ExpeditionReplayData(source.MissionName,source.Leader,source.Squad);
            if (source.Frames!=null)
                foreach (var frame in source.Frames)
                    if(frame!=null)
                        copy.Append(frame.Kind,frame.Actor,frame.Description,frame.Target,
                            frame.Damage,frame.Healing,frame.Round);
            return copy;
        }

        public static void RestoreJourneys(List<SavedAutoExpedition> saved)
        {
            ActiveJourneys.Clear();
            if (saved == null) return;
            var reserved=new HashSet<string>();
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
                // A malformed save must not resolve one warrior in several
                // overlapping expeditions and pay the same spoils twice.
                if (!valid || !item.Party.Contains(item.Commander)) continue;
                var local=new HashSet<string>();
                foreach (var name in item.Party)
                    if (string.IsNullOrEmpty(name) || !local.Add(name) ||
                        reserved.Contains(name)) valid=false;
                if (!valid || (item.Recalled && item.EncounterResolved)) continue;
                if(item.EncounterResolved)
                {
                    if(item.Survivors==null || item.TakenCoins<0 ||
                        item.FoesFallen<0 || item.EncounterTrace==null ||
                        item.Survivors.Count>item.Party.Count ||
                        item.Survivors.Exists(n => !item.Party.Contains(n))) continue;
                    var uniqueSurvivors=new HashSet<string>();
                    foreach(var name in item.Survivors)
                        if(!uniqueSurvivors.Add(name)) valid=false;
                    if(item.HasChosenAction)
                    {
                        Mission mission=default;
                        foreach(var candidate in MissionCatalog.All())
                            if(candidate.Name==item.MissionName) { mission=candidate; break; }
                        if(!JunctionCatalog.Options(mission.Junction).Contains(
                            (AOS.MissionAction)item.ChosenAction)) valid=false;
                    }
                }
                if(!valid) continue;
                // Only successfully validated journeys reserve actors.
                foreach (var name in item.Party) reserved.Add(name);
                var copy=CopyJourney(item);
                if (string.IsNullOrEmpty(copy.JourneyId))
                    copy.JourneyId=System.Guid.NewGuid().ToString("N");
                copy.SecondsRemaining=Mathf.Clamp(copy.SecondsRemaining,0f,3600f);
                ActiveJourneys.Add(copy);
            }
        }

        // The persistent WorldClock owns game time when present.
        // Keep a local fallback for editor test scenes without the clock.
        private void Update()
        {
            if (!ExpeditionWorldClock.IsRunning)
                AdvanceJourneys(Time.deltaTime);
        }

        /// <summary>
        /// Expedition time belongs to the world, not to a visible UI panel.
        /// CryptMap can call this on its disabled child MissionBoard while
        /// the expedition table is hidden. A paused game passes zero delta.
        /// </summary>
        public void AdvanceJourneys(float gameDelta, bool canDeliver = true)
        {
            // The playable personal expedition is a virtual map inside
            // the crypt scene. Never materialize an automatic crew there.
            if (PersonalExpedition.Active) canDeliver=false;
            if (ActiveJourneys.Count==0 || gameDelta<=0f ||
                float.IsNaN(gameDelta) || float.IsInfinity(gameDelta)) return;

            // Each active crew owns its own stage and remaining time.
            // Reverse traversal tolerates returns that remove completed trips.
            for (int i=ActiveJourneys.Count-1;i>=0;i--)
            {
                var journey=ActiveJourneys[i];
                journey.SecondsRemaining=ExpeditionJourneyTiming.Advance(
                    journey.SecondsRemaining,gameDelta);
                if (!ExpeditionJourneyTiming.Finished(journey.SecondsRemaining)) continue;

                if (journey.Phase==0)
                {
                    Mission mission=default;
                    foreach (var candidate in MissionCatalog.All())
                        if (candidate.Name==journey.MissionName) { mission=candidate; break; }

                    if (mission.Junction!=Junction.None && !journey.HasSuggestion)
                    {
                        // Advice is optional. A real AOS commander never waits
                        // forever for a player who has left the crystal ball.
                        journey.Phase=1;
                        journey.SecondsRemaining=ExpeditionJourneyTiming.AdviceSeconds;
                        LastReport=Loc.F(
                            "Отряд «{0}» у развилки. Можно дать совет; без него старший решит сам.",
                            Loc.Name(journey.Commander));
                        Log(LastReport);
                        SaveExpeditionProgress();
                        continue;
                    }
                }

                if (journey.Phase==0 || journey.Phase==1)
                {
                    // The commander chooses and battle resolves at the site,
                    // regardless of whether the player sent any advice.
                    if (!ResolveEncounter(journey)) continue;
                    journey.Phase=2;
                    journey.SecondsRemaining=ExpeditionJourneyTiming.ReturnSeconds;
                    LastReport=Loc.F("Отряд «{0}» завершил событие и возвращается.",
                        Loc.Name(journey.Commander));
                    Log(LastReport);
                    SaveExpeditionProgress();
                    continue;
                }

                if (journey.Phase==2)
                {
                    // Physical homecoming modifies the player's roster,
                    // pockets, crypt inventory and replay archive. Never
                    // apply it to the wrong scene or without the inventory.
                    if (!ExpeditionJourneyTiming.CanSettle(canDeliver,
                        Inventory.PlayerInventory.Instance!=null,journey.Recalled))
                        continue;
                    // Older saves without the encounter result must resolve
                    // it once before return; never award an unplayed victory.
                    if (!journey.Recalled && !journey.EncounterResolved)
                    {
                        if (!ResolveEncounter(journey)) continue;
                        journey.SecondsRemaining=ExpeditionJourneyTiming.ReturnSeconds;
                        SaveExpeditionProgress();
                        continue;
                    }
                    // Do not discard the only state of a journey that
                    // cannot settle. A missing roster member must not turn
                    // into an unrecorded permanent disappearance.
                    if (!CanCompleteJourney(journey)) continue;
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
                Mission mission=default;
                foreach(var candidate in MissionCatalog.All())
                    if(candidate.Name==journey.MissionName) { mission=candidate; break; }
                if (offer.HasValue && !JunctionCatalog.Options(mission.Junction).Contains(offer.Value))
                    return false;
                journey.HasSuggestion=offer.HasValue;
                journey.SuggestedAction=offer.HasValue ? (int)offer.Value : 0;
                if (!ResolveEncounter(journey)) return false;
                journey.Phase=2;
                journey.SecondsRemaining=ReturnSeconds;
                LastReport=Loc.F("Отряд «{0}» завершил событие и возвращается.",
                    Loc.Name(commanderName));
                SaveExpeditionProgress();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Real AOS event is settled AT THE LOCATION, prior to homeward travel.
        /// The recorded outcome is immutable in gameplay and persists through
        /// SaveGame, so loading during return cannot reroll casualties or loot.
        /// Nothing is awarded or brought home until CompleteJourney().
        /// </summary>
        private bool ResolveEncounter(SavedAutoExpedition journey)
        {
            if (journey.EncounterResolved) return true;
            Mission mission=default;
            bool found=false;
            foreach(var candidate in MissionCatalog.All())
                if(candidate.Name==journey.MissionName)
                { mission=candidate; found=true; break; }
            if(!found) return false;
            var away=new List<SquadRoster.Member>();
            foreach(var name in journey.Party)
            {
                if(!SquadRoster.TryGet(name,out var member) || !member.IsAway)
                    return false;
                member.IsCommander=member.Name==journey.Commander;
                away.Add(member);
            }
            if (away.Count==0 || !journey.Party.Contains(journey.Commander))
                return false;
            AOS.MissionAction? suggested=journey.HasSuggestion
                ? (AOS.MissionAction?)((AOS.MissionAction)journey.SuggestedAction) : null;
            AOS.MissionAction? chosen=mission.Junction==Junction.None
                ? null : Decide(mission,away,suggested);
            // The approved caravan rules allow taking goods while sparing
            // people without bloodshed. Taking everything or captives
            // risks the hired guards in an actual AOS fight.
            bool noFight=chosen==AOS.MissionAction.LetThemPass ||
                chosen==AOS.MissionAction.TakeGoodsSparePeople;
            var fight=noFight
                ? new Expedition.Outcome(Names(away),0,0)
                : Expedition.Fight(away, mission.Guards);

            var events=new ExpeditionReplayData(Loc.Name(mission.Name),
                Loc.Name(journey.Commander),journey.Party);
            if (fight.Record!=null && fight.Record.Events!=null)
                foreach(var ev in fight.Record.Events)
                {
                    if(ev==null) continue;
                    string actor=string.IsNullOrEmpty(ev.ActorName)
                        ? Loc.T("Неизвестный воин") : Loc.Name(ev.ActorName);
                    events.Append(ExpeditionReplayKind.CombatAction,actor,
                        Retelling.Describe(ev),ev.TargetId,ev.DamageDealt,ev.HealDealt,ev.Round);
                }
            // Commit all fields only after the resolver returned successfully.
            journey.Survivors=new List<string>(fight.Survivors);
            journey.SurvivorHealth=CopyHealth(fight.Health);
            journey.TakenCoins=fight.Taken;
            journey.FoesFallen=fight.FoesFallen;
            journey.EncounterFought=!noFight;
            journey.BattleWon=fight.Record!=null && fight.Record.Winner=="Squad";
            journey.HasChosenAction=chosen.HasValue;
            journey.ChosenAction=chosen.HasValue ? (int)chosen.Value : 0;
            journey.BattleSummary=Retelling.Tell(fight.Record);
            journey.EncounterTrace=events;
            journey.EncounterResolved=true;
            return true;
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
                SaveExpeditionProgress();
                return true;
            }
            return false;
        }

        private static bool CanCompleteJourney(SavedAutoExpedition journey)
        {
            if (journey==null || journey.Party==null || journey.Party.Count==0)
                return false;
            bool exists=false;
            foreach(var mission in MissionCatalog.All())
                if(mission.Name==journey.MissionName) { exists=true; break; }
            if(!exists || !journey.Party.Contains(journey.Commander))
                return false;
            foreach(var name in journey.Party)
                if(!SquadRoster.TryGet(name,out var member) || !member.IsAway)
                    return false;
            // The crypt must be capable of accepting money and physical
            // cargo before the journey is removed from durable state.
            return journey.Recalled || Inventory.PlayerInventory.Instance!=null;
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
                SaveExpeditionProgress();
                return;
            }
            ResolveReturned(found,journey.Commander,away,
                new List<string>(journey.Party),journey);
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

            // Validate everything BEFORE mutating SquadRoster. The old
            // SendAway silently skipped unavailable soldiers, making the
            // on-screen party differ from the group actually sent.
            var selected=new HashSet<string>();
            int maxSize=System.Math.Min(mission.Squad,
                Leadership.SquadSize(commander.Leadership));
            if (companions!=null)
                foreach (var name in companions)
                {
                    if (string.IsNullOrEmpty(name) || name==commanderName ||
                        !selected.Add(name) ||
                        !SquadRoster.TryGet(name,out var member) ||
                        !SquadRoster.CanGo(member))
                        return Loc.T("Неверный состав: один из спутников недоступен или указан повторно.");
                }
            if (selected.Count+1>maxSize)
                return Loc.T("Старший не может вести столько спутников.");

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
            // The roster already marks these real characters as travelling.
            // Hide their scene objects too; otherwise one warrior can fight
            // beside the player in the crypt while simultaneously on a raid.
            foreach (var warrior in Squad())
                if (party.Contains(warrior.DisplayName))
                    warrior.gameObject.SetActive(false);
            Sent++;
            ActiveJourneys.Add(new SavedAutoExpedition
            {
                MissionName=mission.Name, Commander=commanderName,
                JourneyId=System.Guid.NewGuid().ToString("N"),
                Party=new List<string>(party), Phase=0,
                SecondsRemaining=OutboundSeconds,
                HasSuggestion=suggestion.HasValue,
                SuggestedAction=suggestion.HasValue ? (int)suggestion.Value : 0
            });
            LastReport=Loc.F("Отряд «{0}» отправлен к месту «{1}». Отряд в пути.",
                Loc.Name(commanderName),Loc.Name(mission.Name));
            SaveExpeditionProgress();
            return LastReport;
        }

        private void ResolveReturned(Mission mission, string commanderName,
            List<SquadRoster.Member> away, List<string> party,
            SavedAutoExpedition journey)
        {
            // Use the event already decided and simulated at the destination.
            // Re-running AOS here would change the past on every load.
            if(!journey.EncounterResolved) return;
            AOS.MissionAction? chosen=journey.HasChosenAction
                ? (AOS.MissionAction?)((AOS.MissionAction)journey.ChosenAction) : null;
            var survivors=journey.Survivors ?? new List<string>();
            bool secured=ExpeditionObjectiveRules.Secured(
                journey.EncounterFought,journey.BattleWon,survivors.Count);

            // The same survivors return with their real AOS injuries.
            // Legacy journeys without wound records keep their old health.
            if(journey.SurvivorHealth!=null)
                foreach(var condition in journey.SurvivorHealth)
                    if(condition!=null && survivors.Contains(condition.Name))
                        SquadRoster.ApplyExpeditionHealth(condition.Name,
                            condition.Fraction);

            // Состав меняется до отчёта: отчёт рассказывает о том, что уже
            // случилось, а не назначает это.
            SquadRoster.ComeBack(party, survivors);

            LastReport = Report(mission, away, survivors, commanderName);
            var wounded=new List<string>();
            if(journey.SurvivorHealth!=null)
                foreach(var condition in journey.SurvivorHealth)
                    if(condition!=null && condition.Fraction>0f &&
                        condition.Fraction<0.99f && survivors.Contains(condition.Name))
                        wounded.Add(Loc.Name(condition.Name));
            if(wounded.Count>0)
                LastReport += Loc.F(" Раны после похода: {0}.",string.Join(", ",wounded));

            // Что там было. До этого отчёт говорил, кто вернулся,
            // и молчал о том, почему остальные не вернулись.
            string told = journey.BattleSummary;
            if (!string.IsNullOrEmpty(told)) LastReport += " " + told;

            if (chosen.HasValue && secured)
            {
                LastReport += " " + JunctionCatalog.Told(chosen.Value);
                Judge(away, survivors, chosen.Value);
            }
            else if (chosen.HasValue)
                LastReport += " " + Loc.T("Замысел командира не выполнен: охрана остановила отряд.");
            if (survivors.Count>0 && !secured)
                LastReport += " " + Loc.T("Отступили с пустыми руками, цель не достигнута.");

            // The voice of Memory now has durable, observed expedition
            // experience. Dead characters cannot remember their own return.
            foreach(var member in away)
            {
                if(!survivors.Contains(member.Name)) continue;
                string experience=null;
                if(!secured)
                    experience="ExpeditionRepelled";
                else if(chosen==AOS.MissionAction.LetThemPass)
                    experience="ExpeditionSparedCaravan";
                else if(chosen==AOS.MissionAction.TakeGoodsSparePeople)
                    experience="ExpeditionTookGoods";
                else if(chosen==AOS.MissionAction.TakeEverything)
                    experience="ExpeditionRaidedCaravan";
                else if(chosen==AOS.MissionAction.TakePeople)
                    experience="ExpeditionCaptureAttempt"; // no physical prisoner yet
                if(experience!=null)
                {
                    var feeling=experience=="ExpeditionRepelled" ? AOS.EmotionType.Fear
                        : member.Moral==MoralType.Pious
                            ? (chosen==AOS.MissionAction.LetThemPass
                                ? AOS.EmotionType.Joy : AOS.EmotionType.Disgust)
                            : member.Moral==MoralType.Vicious
                                ? (chosen==AOS.MissionAction.LetThemPass
                                    ? AOS.EmotionType.Disgust : AOS.EmotionType.Joy)
                                : AOS.EmotionType.Calm;
                    ExpeditionMemoryLedger.Remember(journey.JourneyId,member.Name,
                        mission.Name,experience,feeling,1f);
                }
                if(survivors.Count<away.Count)
                    ExpeditionMemoryLedger.Remember(journey.JourneyId,member.Name,
                        mission.Name,"ExpeditionLostCompanion",
                        AOS.EmotionType.Sadness,0.9f);
            }

            // Ушедших видно в склепе — сцена догоняет состав: не вернувшихся
            // в ней больше нет, вернувшимся должны.
            Settle(party, survivors);

            var shares = new List<ExpeditionLootShare>();
            int coinsForLord = 0;
            if (survivors.Count > 0)
            {
                if(secured) TakeSpoils(mission, chosen, journey);

                // Добыча — в кошель Греховода, числом (монеты — то, чем он
                // владеет и что тратит; решение автора 30 сентября). До
                // 10 октября она шла только в Казну, а Казны в склепе демо
                // нет: ограбленный обоз не приносил ни монеты, и «хочу игру,
                // где нужно грабить караваны» (автор) кончалось пустыми
                // руками. Правило «серебро закроет долг разом» записано
                // в 39-PLACES §2.1; Казна была нужна, пока платить было
                // нечем, — теперь кошель есть и плата лично тоже.
                // Survivors alone do not prove the wagon or guarded
                // cargo was secured. No magically recovered bags after defeat.
                int coin=ExpeditionObjectiveRules.DeliveredCoins(secured,
                    journey.TakenCoins,Carried(mission, chosen));
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
                if (secured && record != null)
                    Log(Loc.F("С вылазки привезли: {0} — «{1}». Граммофон в склепе.", Core.Records.MetalWord(record), Loc.T(record.Title)));
            }

            LastReplay = BuildReplay(mission, commanderName, party, chosen,
                journey, survivors, shares, coinsForLord, secured);
            ExpeditionReplayArchive.Add(LastReplay);

            // Отряд вернулся — точка, к которой игрок захочет вернуться
            // сам, и единственная, которую он не выбирает. В режиме
            // обязательств она же и единственная возможная.
            SaveExpeditionProgress();

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
            SavedAutoExpedition outcome, List<string> survivors,
            List<ExpeditionLootShare> shares, int coinsForLord, bool secured)
        {
            var replay = new ExpeditionReplayData(Loc.Name(mission.Name),Loc.Name(leader),party);
            replay.Append(ExpeditionReplayKind.Departure,Loc.Name(leader),
                Loc.T("Отряд вышел из склепа."));
            replay.Append(ExpeditionReplayKind.March,Loc.Name(leader),
                Loc.F("Отряд направился к месту «{0}».",Loc.Name(mission.Name)));

            if(chosen.HasValue)
                replay.Append(ExpeditionReplayKind.CommandDecision,Loc.Name(leader),
                    JunctionCatalog.Told(chosen.Value));
            if(outcome.EncounterTrace!=null && outcome.EncounterTrace.Frames!=null)
                foreach(var frame in outcome.EncounterTrace.Frames)
                    if(frame!=null)
                        replay.Append(frame.Kind,frame.Actor,frame.Description,frame.Target,
                            frame.Damage,frame.Healing,frame.Round);

            if(!outcome.EncounterFought)
                replay.Append(ExpeditionReplayKind.CommandDecision,Loc.Name(leader),
                    chosen==AOS.MissionAction.TakeGoodsSparePeople
                        ? Loc.T("Обоз разгрузили без боя и отпустили возчиков.")
                        : Loc.T("Столкновения не было: командир разрешил проход."));

            if(survivors.Count>0 && secured)
            {
                replay.Append(ExpeditionReplayKind.Loot,Loc.Name(leader),
                    Loc.F("С павших противников взяли монет: {0}.",outcome.TakenCoins));
                foreach(var taken in shares)
                    replay.Append(ExpeditionReplayKind.Loot,Loc.Name(taken.Name),
                        Loc.F("Оставил себе {0} монет.",taken.Coins));
                replay.Append(ExpeditionReplayKind.Loot,Loc.T("Отряд"),
                    Loc.F("Общая добыча, вернувшаяся в казну: {0} монет.",coinsForLord));
            }
            else if(survivors.Count>0)
                replay.Append(ExpeditionReplayKind.Loot,"",
                    Loc.T("Охрана удержала груз. Отряд отступил без добычи."));
            else
                replay.Append(ExpeditionReplayKind.Loot,"",Loc.T("Отряд погиб; добыча осталась на месте."));

            var returnNote=survivors.Count>0
                ? Loc.F("Вернулись: {0}.",string.Join(", ",survivors))
                : Loc.T("К склепу не вернулся никто.");
            var wounded=new List<string>();
            if(outcome.SurvivorHealth!=null)
                foreach(var condition in outcome.SurvivorHealth)
                    if(condition!=null && condition.Fraction>0f &&
                       condition.Fraction<0.99f && survivors.Contains(condition.Name))
                        wounded.Add(Loc.Name(condition.Name));
            if(wounded.Count>0)
                returnNote += Loc.F(" Ранены: {0}.",string.Join(", ",wounded));
            replay.Append(ExpeditionReplayKind.Return,Loc.Name(leader),returnNote);
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

            foreach (var w in Object.FindObjectsByType<Warrior>(
                FindObjectsInactive.Include,FindObjectsSortMode.InstanceID))
            {
                if (w==null || w is SinbinderPlayer || w.Team!=Team.Player ||
                    !party.Contains(w.DisplayName)) continue;
                if (!stay.Contains(w.DisplayName)) { gone.Add(w); continue; }
                if (!SquadRoster.TryGet(w.DisplayName, out var m)) continue;

                w.UnpaidMissions = m.UnpaidMissions;
                w.ChangeLoyalty(m.Loyalty - w.Loyalty);
                w.SetHealthFraction(ExpeditionHealthRules.RestoreFraction(m.HealthFraction));
                w.gameObject.SetActive(true);
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
                    ExpeditionSite = mission.Name,
                    HasTreasure = mission.Prize != Prize.None,
                    HasGuiltyParty = false,
                    RecentMemories = ExpeditionMemoryLedger.ForWarrior(boss.Name, mission.Name),
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
        private void TakeSpoils(Mission mission, AOS.MissionAction? chosen,
            SavedAutoExpedition journey)
        {
            if (chosen == AOS.MissionAction.TakePeople)
                Log(Loc.T("Командир выбрал захват людей. Учёт пленников пока отсутствует: доступные ресурсы склепа не пополнены."));
            if (chosen == AOS.MissionAction.LetThemPass) return;

            // Taking ordinary cargo must not invent a soul encounter.
            if (mission.Spoils == Spoils.Souls)
                Log(Loc.T("Души остались на месте: в отряде нет собирателя душ."));

            if (mission.Spoils == Spoils.Upgrade)
                Log(Loc.T("В отряде нет искателя секретов: редкую находку не обнаружили."));

            if (mission.Spoils == Spoils.Shell)
            {
                // An actual defeated body exists in AOS only if at least one
                // hostile shell fell. Never mint an arbitrary body from
                // a label, peaceful encounter, retreat, or empty battlefield.
                if(journey==null || journey.FoesFallen<=0)
                {
                    Log(Loc.T("Пригодной оболочки среди павших противников не нашли."));
                    return;
                }
                var inventory=Inventory.PlayerInventory.Instance;
                if(inventory==null)
                {
                    Log(Loc.T("Нет доступной сумы: останки не были доставлены."));
                    return;
                }
                var body=new Inventory.InventoryItem(
                    Loc.N("Обычная оболочка с затопленной каменоломни"),
                    Loc.N("Останки одного реально павшего противника. Душа не собрана."),
                    Inventory.ItemType.Shell);
                if(inventory.AddItem(body))
                    Log(Loc.T("В суму доставлена одна обычная оболочка. Душа не собрана."));
                else
                    Log(Loc.T("В суме не хватило места: оболочка не доставлена."));
            }
        }

        /// <summary>
        /// Saving a virtual personal expedition calls
        /// PersonalExpedition.BeforeSaveOrLoad(), which ends that active
        /// map. Remote automatic events must never eject the player from
        /// a personally controlled expedition just to write a quicksave.
        /// Their in-memory journey is saved on the next safe save.
        /// </summary>
        private static void SaveExpeditionProgress()
        {
            if (!PersonalExpedition.Active &&
                Inventory.PlayerInventory.Instance != null)
                Core.SaveSystem.AutoSave();
        }

        /// <summary>Забыть добычу при новой игре.</summary>
        public static void Forget()
        {
            Upgrades = 0;
            _lastWorldReport="";
            _lastWorldReplay=null;
            ActiveJourneys.Clear();
            ExpeditionMemoryLedger.Forget();
            CryptUpgrades.Forget();
        }

        private static void Log(string line)
            => Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
    }
}
