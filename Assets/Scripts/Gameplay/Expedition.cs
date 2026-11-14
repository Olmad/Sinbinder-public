// Assets/Scripts/Gameplay/Expedition.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.AOS;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Чем кончилась вылазка отряда, ушедшего на доле 2.
    ///
    /// Раньше это была таблица (<c>Homecoming.Returned</c>): сценарист
    /// заранее решил, что Жадность возвращает четверых, а Уныние одного.
    /// Правило пролога §2 говорит обратное: ни одна постановочная сцена
    /// не имеет права показать то, чего движок не мог бы решить сам.
    ///
    /// Стенд проверил, умеет ли движок то же самое (docs/12-BALANCE.md,
    /// «Вылазка»). Умеет — и на равном бою различает командиров лучше
    /// таблицы: разброс 2.07 из пяти. Но порядок у них разный, и совпадало
    /// только Уныние: таблица делала Жадность лучшим исходом, движок —
    /// одним из худших, потому что собиратель отвлекается на добычу.
    ///
    /// Выбран второй из трёх выходов, записанных в разборе: считать
    /// эпилог боем и переписать объяснения под то, что движок правда
    /// делает. Здесь этот бой и проводится.
    ///
    /// <b>Повторяемость.</b> Ни одного случайного числа: состав известен,
    /// противник строится по счёту, <see cref="AutoBattleResolver"/>
    /// кубиков не бросает. Один и тот же выбор игрока даёт один и тот же
    /// исход — иначе объяснение «он свернул за блеском» было бы враньём
    /// через раз.
    /// </summary>
    [System.Serializable]
    public sealed class ExpeditionSurvivorHealth
    {
        public string Name;
        public float Fraction;
    }

    public static class Expedition
    {
        /// <summary>
        /// Чем кончилось: кто вернулся и сколько сняли с павших чужих.
        ///
        /// Добыча считается <see cref="BodyWorth"/> — тем же, чем её
        /// считает бой на сцене. Пока вылазка платила «врагов × монету»,
        /// в игре было две правды о мертвеце: пятнадцать монет за Ловчего
        /// на поле и плоские шесть за «Чужого» на карте.
        /// </summary>
        public readonly struct Outcome
        {
            public readonly List<string> Survivors;
            public readonly int Taken;
            public readonly int FoesFallen;

            /// <summary>
            /// Что там происходило. Заполняется движком, а пересказывает
            /// её Crypt.Retelling. До этого запись боя не работала ни
            /// разу: резолвер умел её вести, но никто её не заводил.
            /// </summary>
            public readonly AOS.BattleRecord Record;
            // Wounds are part of real simulated outcome, not a fresh roll on return.
            public readonly List<ExpeditionSurvivorHealth> Health;

            public Outcome(List<string> survivors, int taken, int foesFallen,
                           AOS.BattleRecord record = null,
                           List<ExpeditionSurvivorHealth> health = null)
            {
                Survivors = survivors;
                Taken = taken;
                FoesFallen = foesFallen;
                Record = record;
                Health = health;
            }
        }

        /// <summary>
        /// Провести вылазку и вернуть имена выживших в порядке отряда.
        /// Пустой список — не вернулся никто.
        /// </summary>
        public static List<string> Resolve(List<SquadRoster.Member> away)
            => Resolve(away, away != null ? away.Count : 0);

        /// <summary>
        /// То же, но противник задан отдельно.
        ///
        /// В прологе он был вровень по числу ушедших: там точка одна,
        /// и выбирать не из чего. На карте склепа точки разной тяжести,
        /// и «их там больше» обязано что-то значить — иначе выбор точки
        /// это выбор названия.
        /// </summary>
        public static List<string> Resolve(List<SquadRoster.Member> away, int foeCount)
            => Fight(away, foeCount).Survivors;

        /// <summary>
        /// One genuine offscreen AOS encounter. Every StepRound executes
        /// one round against CURRENT health, team allegiance and survivor
        /// lists. A checkpoint reconstructs this state without replaying
        /// earlier rounds or re-deciding earlier choices.
        /// </summary>
        public sealed class CombatEncounter : System.IDisposable
        {
            private GameObject _holder;
            private readonly List<Warrior> _originalSquad = new List<Warrior>();
            private readonly List<Warrior> _originalFoes = new List<Warrior>();
            private readonly List<Warrior> _squad = new List<Warrior>();
            private readonly List<Warrior> _foes = new List<Warrior>();
            private readonly List<string> _names = new List<string>();
            private AutoBattleResolver.Session _session;
            private BattleRecord _record;

            public bool IsComplete => _session != null && _session.IsComplete;
            public int CompletedRounds => _session != null ? _session.CompletedRounds : 0;
            public BattleRecord Record => _record;

            public CombatEncounter(List<SquadRoster.Member> away, int foeCount,
                                   ExpeditionBattleCheckpoint checkpoint = null)
            {
                if(away==null || away.Count==0)
                    throw new System.ArgumentException("Combat requires outgoing warriors.");
                int count=Mathf.Max(foeCount,1);
                if(checkpoint!=null && !ExpeditionBattleCheckpoint.IsValid(
                    checkpoint,away.Count,count,AutoBattleResolver.MaxTurns))
                    throw new System.ArgumentException("Invalid AOS round checkpoint.");

                _holder=new GameObject("Вылазка");
                _holder.SetActive(false);
                try
                {
                    var relations=new RelationshipSystem(MemoryProcessor.Instance);
                    Warrior commander=null;
                    foreach(var member in away)
                    {
                        var w=Summon(_holder,member,relations);
                        _originalSquad.Add(w);
                        _squad.Add(w);
                        _names.Add(member.Name);
                        if(member.IsCommander) commander=w;
                    }
                    for(int i=0;i<count;i++)
                    {
                        var sin=(SinType)(i%7);
                        var foe=Make(_holder,"Чужой "+(i+1),sin,MoralType.Vicious,
                            40f+i*5f,50f,false,Team.Enemy,relations);
                        _originalFoes.Add(foe);
                        _foes.Add(foe);
                    }
                    _record=new BattleRecord();
                    int rounds=0;
                    if(checkpoint!=null)
                    {
                        Restore(checkpoint);
                        rounds=checkpoint.CompletedRounds;
                    }
                    var strategy=commander!=null && commander.Soul!=null
                        ? SquadOrders.FromSin(commander.Soul.Sin)
                        : SquadStrategy.Balanced;
                    _session=new AutoBattleResolver.Session(commander,_squad,
                        _foes,strategy,_record,rounds);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            private static BattleRecord CopyRecord(BattleRecord source)
            {
                var record=new BattleRecord();
                if(source==null || source.Events==null) return record;
                record.Winner=source.Winner;
                record.TotalTurns=source.TotalTurns;
                foreach(var ev in source.Events)
                {
                    if(ev==null) throw new System.ArgumentException("Null AOS event.");
                    record.AddEvent(new BattleEvent
                    {
                        ActorId=ev.ActorId, ActorName=ev.ActorName,
                        ActorGender=ev.ActorGender, TargetId=ev.TargetId,
                        TargetName=ev.TargetName, Action=ev.Action,
                        DamageDealt=ev.DamageDealt, HealDealt=ev.HealDealt,
                        Resolved=ev.Resolved, TargetKilled=ev.TargetKilled,
                        TargetHealthAfter=ev.TargetHealthAfter,Round=ev.Round
                    });
                }
                return record;
            }

            private void Restore(ExpeditionBattleCheckpoint state)
            {
                // Match source identities by original roster/order, never by
                // mutable current team or newly converted enemy allegiance.
                var all=new List<Warrior>(_originalSquad);
                all.AddRange(_originalFoes);
                for(int i=0;i<all.Count;i++)
                {
                    var actor=state.Actors[i];
                    bool original=i<_originalSquad.Count;
                    if(actor.OriginalParty!=original ||
                       actor.Name!=all[i].DisplayName)
                        throw new System.ArgumentException(
                            "AOS checkpoint actor order differs from original roster.");
                    all[i].RestoreAutoExpeditionSnapshot(actor.Id,
                        actor.HealthFraction,actor.Dead,(Team)actor.Team);
                }

                _squad.Clear();_foes.Clear();
                for(int index=0;index<all.Count;index++)
                {
                    var actor=state.Actors[index];
                    if(actor.SquadIndex>=0)
                    {
                        while(_squad.Count<=actor.SquadIndex) _squad.Add(null);
                        _squad[actor.SquadIndex]=all[index];
                    }
                    if(actor.EnemyIndex>=0)
                    {
                        while(_foes.Count<=actor.EnemyIndex) _foes.Add(null);
                        _foes[actor.EnemyIndex]=all[index];
                    }
                }
                _record=CopyRecord(state.Record);
            }

            public bool StepRound()
            {
                if(_session==null) return false;
                // An offscreen expedition is NOT located in the player's
                // currently loaded Crypt/forest scene. No fictional site tags:
                // MissionCatalog does not yet author them. AOS perks should
                // only see known offscreen facts during the executed round.
                using(Core.LocationManager.ForOffscreenSite())
                    return _session.StepRound();
            }

            public ExpeditionBattleCheckpoint Snapshot()
            {
                if(_session==null || _session.IsFaulted)
                    throw new System.InvalidOperationException(
                        "Cannot save a failed or uninitialized AOS round.");
                var state=new ExpeditionBattleCheckpoint
                {
                    CompletedRounds=_session.CompletedRounds,
                    Record=CopyRecord(_record)
                };
                var all=new List<Warrior>(_originalSquad);
                all.AddRange(_originalFoes);
                for(int i=0;i<all.Count;i++)
                {
                    var w=all[i];
                    state.Actors.Add(new ExpeditionBattleActor
                    {
                        Name=w.DisplayName,Id=w.Id,
                        OriginalParty=i<_originalSquad.Count,
                        Team=(int)w.Team,Dead=w.IsDead,
                        // Keep sub-one-percent real HP unchanged at each
                        // round boundary. The older roster conversion has
                        // a protective 1% floor intended for legacy saves;
                        // applying that floor every turn would heal actors.
                        HealthFraction=w.IsDead ? 0f :
                            (w.MaxHP>0f
                                ? System.Math.Max(float.Epsilon,
                                    System.Math.Min(1f,w.HP/w.MaxHP))
                                : 1f),
                        SquadIndex=_squad.IndexOf(w),
                        EnemyIndex=_foes.IndexOf(w)
                    });
                }
                if(!ExpeditionBattleCheckpoint.IsValid(state,_originalSquad.Count,
                    _originalFoes.Count,AutoBattleResolver.MaxTurns))
                    throw new System.InvalidOperationException(
                        "AOS round did not produce a valid checkpoint.");
                return state;
            }

            public Outcome Outcome()
            {
                if(!IsComplete)
                    throw new System.InvalidOperationException(
                        "Cannot award expedition outcome before AOS has finished.");
                var survivors=new List<string>();
                var health=new List<ExpeditionSurvivorHealth>();
                for(int i=0;i<_originalSquad.Count;i++)
                {
                    var warrior=_originalSquad[i];
                    if(warrior.IsDead) continue;
                    survivors.Add(_names[i]);
                    health.Add(new ExpeditionSurvivorHealth
                    {
                        Name=_names[i],
                        Fraction=ExpeditionHealthRules.Capture(warrior.HP,warrior.MaxHP)
                    });
                }
                int taken=0,fallen=0;
                // The original enemy identity stays an enemy-origin corpse
                // even if AOS temporarily persuaded it to switch sides.
                foreach(var foe in _originalFoes)
                    if(foe.IsDead)
                    {
                        fallen++;
                        taken+=BodyWorth.Gold(foe.Shell,foe.Soul);
                    }
                return new Outcome(survivors,taken,fallen,CopyRecord(_record),health);
            }

            public void Dispose()
            {
                if(_holder==null) return;
                if(Application.isPlaying) Object.Destroy(_holder);
                else Object.DestroyImmediate(_holder);
                _holder=null;
            }
        }

        /// <summary>
        /// Legacy synchronous expedition uses exactly the same live
        /// round-step code. One single source of combat rules is preserved.
        /// </summary>
        public static Outcome Fight(List<SquadRoster.Member> away, int foeCount)
        {
            if(away==null || away.Count==0)
                return new Outcome(new List<string>(),0,0,new BattleRecord());
            using(var combat=new CombatEncounter(away,foeCount))
            {
                // Existing story encounters still have synchronous contract;
                // the mission board can advance a CombatEncounter over time.
                int previousGold=AutoBattleResolver.SquadGold;
                try
                {
                    AutoBattleResolver.SquadGold=0;
                    while(combat.StepRound()) { }
                    return combat.Outcome();
                }
                finally { AutoBattleResolver.SquadGold=previousGold; }
            }
        }

        /// <summary>
        /// Собрать временного воина по записи отряда.
        ///
        /// Нужно тем, кому воин требуется на один вопрос, а не на бой:
        /// развилка спрашивает командира, а спросить может только
        /// движок решений, и ему нужен настоящий воин. Одна правда
        /// о том, как запись отряда становится воином, — здесь.
        /// Держатель обязан быть выключен: это счёт, а не сцена.
        /// </summary>
        public static Warrior Summon(GameObject holder, SquadRoster.Member m,
                                     RelationshipSystem relations)
        {
            // The roster already persists the warrior's generated life
            // story and narrative perks. Temporary AOS projections must
            // receive that same testimony, not an invented empty soul.
            var w = Make(holder, m.Name, m.Sin, m.Moral, m.Intensity,
                         m.Loyalty, m.IsCommander, Team.Player, relations,
                         m.Gender, m.Shell, m.Seed);
            w.UnpaidMissions = m.UnpaidMissions;
            // Previous battles do not heal themselves when simulations use
            // new temporary objects; keep the actual survivor's injury.
            w.SetHealthFraction(ExpeditionHealthRules.RestoreFraction(m.HealthFraction));

            // Надетое и карман спорят в голове (искушение, Жадность) —
            // спрошенный на развилке обязан решать с ними же.
            if (m.Gear != null)
                foreach (var item in m.Gear) w.Give(item);
            w.Pocket(m.Pocket);
            return w;
        }

        /// <summary>
        /// Воин для счёта. Тело — его собственное (<see cref="SquadRoster.Member.Shell"/>):
        /// вылазка — настоящий бой, и зомби в нём держит удар дольше
        /// призрака. Чужие — скелеты, как были.
        /// </summary>
        private static Warrior Make(GameObject holder, string name, SinType sin,
            MoralType moral, float intensity, float loyalty, bool isCommander,
            Team team, RelationshipSystem relations, Gender gender = Gender.Male,
            ShellType shell = ShellType.Skeleton, MemorySeed memory = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(holder.transform);

            var w = go.AddComponent<Warrior>();
            w.Initialize(new SoulData(name, sin, moral, 1, intensity, memory, gender),
                         shell, relations, isCommander, team);
            // Верность задаётся сдвигом от полусотни: своего сеттера
            // у неё нет, и заводить его ради счётной вылазки незачем.
            w.ChangeLoyalty(loyalty - w.Loyalty);

            return w;
        }
    }
}
