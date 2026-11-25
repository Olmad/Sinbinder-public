// PERSONAL EXPEDITIONS: real, navigable caravan and chapel battlefields.
// The regular SinbinderPlayer, Tab/V, AOS warriors, loot bags and soul
// harvesting are reused rather than replaced with scripted "victory".
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.SceneManagement;
using Sinbinder.Core;
using Sinbinder.Crypt;

namespace Sinbinder.Gameplay
{
    [Serializable]
    public sealed class SavedPersonalExpedition
    {
        public string Id;
        public int EnemiesRemaining;
        public bool CargoTaken;
        public bool CaravanPassed;
        public bool CaravanAssaulted;
        public bool SecretTaken;
        public int Visits;
        // Время мира (Sorties): когда ушли отсюда и когда зачистили. Нет в старых сохранениях — «ещё не уходили».
        public int LeftAt = -1;
        public int ClearedAt = -1;
        /// <summary>0 — свои хозяева места; 1 — дозор Ордена занял разграбленную часовню.</summary>
        public int Occupant;
    }

    public sealed class PersonalExpedition : MonoBehaviour
    {
        private sealed class Traveller
        {
            public Warrior Warrior;
            public Vector3 Home;
        }

        private static PersonalExpedition _active;
        private static readonly Dictionary<string, SavedPersonalExpedition> Progress = new();

        public static bool Active => _active != null;
        internal static PersonalExpedition DialogueVisit => _active != null && !_active._exiting ? _active : null;

        /// <summary>Ушёл ли воин на эту вылазку (Греховод — тоже). Не ушёл — его на карте быть не может.</summary>
        public static bool Travels(Warrior w)
            => _active != null && w != null && _active._travellers.Exists(t => t.Warrior == w);
        public static string CurrentMapKey => _active != null && _active._plan != null
            ? _active._plan.Id : SceneManager.GetActiveScene().name;

        /// <summary>
        /// Отряд без Греховода (MissionBoard) убил здесь стражей — на личной карте их столько же
        /// меньше; все пали — место зачищено с этого похода (Sorties), не разграблено.
        /// </summary>
        public static void GuardsFellElsewhere(string placeId,int fallen)
        {
            if(fallen<=0 || string.IsNullOrEmpty(placeId) || !Progress.TryGetValue(placeId,out var state)) return;
            state.EnemiesRemaining=Mathf.Max(0,state.EnemiesRemaining-fallen);
            if(state.EnemiesRemaining==0 && state.ClearedAt<0) state.ClearedAt=Sorties.Count;
        }

        /// <summary>
        /// Весть дошла до склепа (план 69 §7 п.2): живой, убивший нашего, уходит искать Греховода
        /// сам — на его месте стражей на одного меньше. <paramref name="from"/> — места, откуда
        /// весть принесли. Вернёт строки слуха для склепа.
        /// </summary>
        public static List<string> SendHunters(params string[] from)
        {
            var lines=new List<string>();
            foreach(var (s,left) in Survivors.GoHunting(from))
            {
                GuardsFellElsewhere(left,1);
                lines.Add(Survivors.Rumour(s));
                Debug.Log($"[ВЫЖИВШИЕ] охотник: {s.Name} ушёл с «{left}» искать Греховода (убил: {s.Killed})");
            }
            return lines;
        }

        public static List<SavedPersonalExpedition> SnapshotProgress()
        {
            var list = new List<SavedPersonalExpedition>();
            foreach (var state in Progress.Values)
                list.Add(new SavedPersonalExpedition
                { Id=state.Id, EnemiesRemaining=state.EnemiesRemaining,
                  CargoTaken=state.CargoTaken, CaravanPassed=state.CaravanPassed,
                  CaravanAssaulted=state.CaravanAssaulted,
                  SecretTaken=state.SecretTaken,
                  Visits=state.Visits,
                  LeftAt=state.LeftAt, ClearedAt=state.ClearedAt, Occupant=state.Occupant });
            return list;
        }

        public static void RestoreProgress(List<SavedPersonalExpedition> saved)
        {
            // Restore is called after SaveSystem has installed the saved
            // squad. Normal Exit() would immediately overwrite it with
            // stale scene warriors, resurrecting dead squadmates or losing
            // items. End the virtual map without reporting or settling.
            if (Active) _active.Exit(commitResult:false);
            Progress.Clear();
            if (saved == null) return;
            foreach (var state in saved)
                if (state != null && !string.IsNullOrEmpty(state.Id))
                    Progress[state.Id] = state;
        }

        public static void Forget()
        {
            if (Active) _active.Exit();
            Progress.Clear();
        }

        public static void BeforeSaveOrLoad()
        {
            // A live expedition contains transient NavMesh and enemies.
            // Return the player to the crypt before snapshotting a scene,
            // but do not lose real collected loot or fallen warrior state.
            if (Active) _active.Exit();
        }

        public static bool Enter(Mission mission, ICollection<string> party, out string error)
        {
            error = "";
            var plan = ExpeditionMapPlan.ForName(mission.Name);
            if (plan == null)
            {
                error = Loc.T("Для этой экспедиции ещё нет проходимой карты.");
                return false;
            }
            if (Active)
            {
                error = Loc.T("Вы уже на экспедиции.");
                return false;
            }
            if (!SinbinderPlayer.Exists || SinbinderPlayer.Instance.IsDead)
            {
                error = Loc.T("Греховод должен лично присутствовать.");
                return false;
            }
            // Разговор в склепе (Карган о потерях после вылазки) держит камеру над склепом:
            // уйти посреди него — и Греховод на карте, а камера и полосы остались здесь (кадр 23.11).
            if (UI.Letterbox.CanSkip || UI.TalkView.Shown)
            {
                error = Loc.T("Сейчас говорят. Дослушайте — потом в путь.");
                return false;
            }

            var travellers = new List<Traveller>
            {
                new Traveller { Warrior=SinbinderPlayer.Instance, Home=SinbinderPlayer.Where }
            };

            // Bring THE SAME living AOS soldiers from the crypt. A paper
            // roster record is not an interactive character, and silently
            // replacing one would erase its real memories and equipment.
            if (party != null)
            {
                var scene = SceneManager.GetActiveScene();
                var seen = new HashSet<string>();
                foreach (string name in party)
                {
                    if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;
                    // Recheck the selected roster at the moment of departure.
                    // UI was opened earlier, and a soldier might now be away,
                    // unavailable or on another mission. Never teleport them.
                    if (!SquadRoster.HasUniqueName(name) || !SquadRoster.TryGet(name, out var member) ||
                        !SquadRoster.CanGo(member))
                    {
                        error = Loc.F("Воин {0} сейчас не может выйти в экспедицию.", Loc.Name(name));
                        return false;
                    }
                    if (seen.Count + 1 > Mathf.Max(1, mission.Squad))
                    {
                        error = Loc.T("Отряд больше допуска для этой экспедиции.");
                        return false;
                    }
                    Warrior found = null;
                    foreach (var w in UnityEngine.Object.FindObjectsByType<Warrior>(
                                 FindObjectsSortMode.None))
                        if (w != null && !(w is SinbinderPlayer) && !w.IsDead &&
                            w.Team == Team.Player && w.gameObject.scene == scene &&
                            (string.IsNullOrEmpty(member.WarriorId) ? w.DisplayName == name : w.Id == member.WarriorId))
                        { found = w; break; }
                    if (found == null)
                    {
                        error = Loc.F("Воин {0} сейчас не находится в склепе.", Loc.Name(name));
                        return false;
                    }
                    travellers.Add(new Traveller { Warrior=found, Home=found.transform.position });
                }
            }

            var obj = new GameObject("Личная экспедиция · " + plan.Title);
            var visit = obj.AddComponent<PersonalExpedition>();
            visit.enabled = false; // cannot tick before initialization
            visit._plan = plan;
            visit._mission = mission;
            visit._travellers.AddRange(travellers);
            if (!Progress.TryGetValue(plan.Id, out visit._state))
            {
                visit._state = new SavedPersonalExpedition
                { Id=plan.Id, EnemiesRemaining=Mathf.Max(0,mission.Guards) };
                Progress[plan.Id] = visit._state;
            }

            // Legacy personal-map saves predate the explicit assault flag.
            // If some caravan guards were already killed on an earlier
            // visit, this is evidence of fighting, not a fresh peaceful
            // encounter. Do not let an old save erase that consequence.
            if(plan.Kind==ExpeditionMapKind.SaltRoad &&
               visit._state.EnemiesRemaining<mission.Guards &&
               !visit._state.CaravanPassed && !visit._state.CargoTaken)
                visit._state.CaravanAssaulted=true;

            // Место не обновляется само (Survivors.OnReturn): та же ночь — те же раны;
            // разграбленную часовню через три похода занимает дозор Ордена — и журнал говорит почему.
            visit._return = Survivors.OnReturn(visit._state.LeftAt, visit._state.ClearedAt,
                visit._state.SecretTaken, visit._state.EnemiesRemaining, Sorties.Count);
            if (visit._return == Return.Reoccupied && plan.Kind == ExpeditionMapKind.Chapel)
            {
                visit._state.EnemiesRemaining = 2;
                visit._state.Occupant = 1;
                visit._state.ClearedAt = -1;
                visit._why.Add(Survivors.Reoccupied());
                // С дозором — тот, кто знает Греховода в лицо: охранник разграбленного обоза (п.2).
                Survivors.Guide(plan.Id);
            }
            // Бежавший живой возвращается не один — если с прошлого раза прошло время.
            foreach (var fled in Survivors.BringFriends(Survivors.At(plan.Id), visit._return))
            {
                if (visit._state.EnemiesRemaining < 4) visit._state.EnemiesRemaining++;
                visit._why.Add(Survivors.Friends(fled));
            }
            // Кто ищет Греховода сам — приходит туда, куда пошёл он (п.2). Слух о нём был в склепе;
            // почему он здесь, скажет журнал при входе (Survivors.Why).
            foreach (var hunter in Survivors.JoinHunt(plan.Id))
                if (visit._state.EnemiesRemaining < 8) visit._state.EnemiesRemaining++;

            visit._mapRoot = ExpeditionMapBuilder.Build(plan);
            if (visit._mapRoot == null)
            {
                UnityEngine.Object.Destroy(obj);
                error = Loc.T("Не удалось построить карту экспедиции.");
                return false;
            }
            // These markers are part of the physical map. One is the exit,
            // the other is the interactive objective with real rewards.
            ExpeditionMapBuilder.Mark(visit._mapRoot.transform, "Возвращение",
                new Vector3(0f,0f,plan.EntranceZ),new Color(0.2f,0.8f,0.75f));
            ExpeditionMapBuilder.Mark(visit._mapRoot.transform, "Цель экспедиции",
                new Vector3(0f,0f,plan.GoalZ),new Color(0.9f,0.65f,0.25f));
            visit._state.Visits++;

            // Туман войны — над этой картой, а не над склепом (иначе вся карта «не разведана»:
            // чёрная, и стражи спрятаны вместе с коллайдерами — не щёлкнуть, не приказать).
            // Дорога от знака до цели отряду известна — серым, как свой лагерь:
            // земля и вещи видны, враги в сером прячутся, пока их не увидят.
            FogOfWar.Cover(new Bounds(ExpeditionMapBuilder.Point(plan, 0f, 0f), new Vector3(plan.Width, 1f, plan.Depth)));
            for (float z = plan.EntranceZ; z <= plan.GoalZ; z += 6f)
                FogOfWar.Reveal(ExpeditionMapBuilder.Point(plan, 0f, z), 9f);
            FogOfWar.Reveal(ExpeditionMapBuilder.Point(plan, 0f, plan.GoalZ), 12f);

            // Change virtual map BEFORE any combat death creates a bag.
            _active = visit;
            FallenBag.OnMapLoaded(plan.Id);
            for(int i=0;i<visit._travellers.Count;i++)
            {
                var member=visit._travellers[i];
                Vector3 spawn=ExpeditionMapBuilder.Point(plan,(i%3-1)*2.5f,plan.EntranceZ+4f+i/3*3f);
                Warp(member.Warrior,spawn);
                member.Warrior.ClearCommand();
            }

            visit.SpawnGuards();
            visit._night = Night.Fall(visit._mapRoot.transform);
            var camera=Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
            visit._returnTactical=camera!=null && !camera.FirstPersonNow;
            camera?.SetView(RTS_Camera.CameraView.FirstPerson);
            // Глаза — на цель, а не в землю: из вида сверху SetView оставлял наклон
            // тактической камеры (70°), и первый кадр карты был чёрной землёй под ногами (20.11).
            if (camera != null)
            {
                var ahead = ExpeditionMapBuilder.Point(plan, 0f, plan.GoalZ) - SinbinderPlayer.Where;
                ahead.y = 0f;
                if (ahead.sqrMagnitude > 1e-4f) camera.Yaw = Quaternion.LookRotation(ahead).eulerAngles.y;
                camera.Pitch = 0f;
            }
            visit.enabled = true;
            visit.Announce(Loc.F("Экспедиция «{0}»: доберитесь до отмеченной цели и вернитесь к бирюзовому знаку.",
                Loc.T(plan.Title)));
            // Перемены места журнал при входе не объявляет (автор, 23.11: «пусть не журнал говорит
            // об изменениях, а меню экспедиций»): их читают в карточке у шара (KnownAbout), а здесь
            // стражи узнают Греховода сами. Объяснения — только в журнал разработчика.
            foreach(var line in visit._why) Debug.Log("[ВЫЖИВШИЕ] "+line);
            return true;
        }

        /// <summary>
        /// Что известно о месте — для карточки экспедиции у шара. Имена — только встреченных
        /// (автор, 23.11: «я имел в виду конкретные имена. А узнать, что там по слухам Орден, — более
        /// чем логично»): что сделали мы сами, кого видели, и слух о новых хозяевах — без имён.
        /// Без чисел: их игрок не видит (MissionCatalog).
        /// </summary>
        public static List<string> KnownAbout(ExpeditionMapPlan plan)
        {
            var lines=new List<string>();
            if(plan==null || string.IsNullOrEmpty(plan.Id)) return lines;
            string placeId=plan.Id;
            Progress.TryGetValue(placeId,out var state);
            var known=Survivors.KnownAt(placeId);
            bool been=state!=null && state.Visits>0;
            if(!been && known.Count==0) return lines;
            if(been)
            {
                bool tonight=state.LeftAt>=0 && Sorties.Count<=state.LeftAt;
                lines.Add(tonight ? Loc.T("Вы ушли отсюда этой ночью: стража не успела залечить раны.")
                                  : Loc.T("Вы здесь уже были; с тех пор прошло время."));
                if(state.CargoTaken && !state.CaravanPassed) lines.Add(Loc.T("Обоз разграблен — охрана ушла на дорогу."));
                else if(state.CaravanPassed) lines.Add(Loc.T("Обоз вы пропустили с миром."));
                if(state.SecretTaken) lines.Add(Loc.T("Тайник взят."));
            }
            if(known.Exists(s => s.Reason=="order"))
                lines.Add(Loc.T("Место стережёт дозор Ордена: слух о разграблении дошёл до них."));
            if(known.Count>0)
                foreach(var s in known) lines.Add(Survivors.Known(s));
            else if(been && state.EnemiesRemaining==0)
                lines.Add(Loc.T("Когда вы уходили, стражи там не осталось."));
            // Новые хозяева — по слуху, без имён: их ещё никто не видел (Survivors.OnReturn).
            if(been && plan.Kind==ExpeditionMapKind.Chapel && state.Occupant!=1 &&
               Survivors.OnReturn(state.LeftAt,state.ClearedAt,state.SecretTaken,state.EnemiesRemaining,Sorties.Count)==Return.Reoccupied)
                lines.Add(Loc.T("По слухам, разграбленную часовню занял дозор Ордена."));
            return lines;
        }

        private ExpeditionMapPlan _plan;
        private Mission _mission;
        private GameObject _mapRoot;
        private SavedPersonalExpedition _state;
        private readonly List<Traveller> _travellers=new();
        private readonly List<Warrior> _guards=new();
        private bool _exiting, _returnTactical, _caravanChoiceOpen;
        private string _notice="";
        private float _noticeUntil;
        private Night _night;

        /// <summary>
        /// Лунная ночь над картой. Карта строится внутри сцены склепа и наследовала его
        /// подземный свет: ровный полумрак 0.11 и густой туман. 20.11 кадры из глаз
        /// Греховода были чёрными — видны только небо с луной и подписи. Под открытым
        /// небом светит луна: свет склепа запоминается на входе и возвращается на выходе.
        /// </summary>
        private sealed class Night
        {
            private UnityEngine.Rendering.AmbientMode _mode;
            private Color _ambient;
            private bool _fog;
            private FogMode _fogMode;
            private Color _fogColor;
            private float _fogDensity;
            private Light _sun;

            public static Night Fall(Transform map)
            {
                var night = new Night
                {
                    _mode = RenderSettings.ambientMode, _ambient = RenderSettings.ambientLight,
                    _fog = RenderSettings.fog, _fogMode = RenderSettings.fogMode,
                    _fogColor = RenderSettings.fogColor, _fogDensity = RenderSettings.fogDensity,
                    _sun = RenderSettings.sun,
                };
                var go = new GameObject("Луна");
                go.transform.SetParent(map, false);
                go.transform.rotation = Quaternion.Euler(38f, -30f, 0f);
                var moon = go.AddComponent<Light>();
                moon.type = LightType.Directional;
                moon.color = new Color(0.62f, 0.70f, 0.90f);
                moon.intensity = 0.55f;
                moon.shadows = LightShadows.Soft;
                moon.shadowStrength = 0.7f;
                RenderSettings.sun = moon;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.20f, 0.22f, 0.28f);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = new Color(0.07f, 0.08f, 0.11f);
                RenderSettings.fogDensity = 0.012f;
                return night;
            }

            /// <summary>Свет склепа — назад. Луна уходит вместе с картой.</summary>
            public void Lift()
            {
                RenderSettings.ambientMode = _mode;
                RenderSettings.ambientLight = _ambient;
                RenderSettings.fog = _fog;
                RenderSettings.fogMode = _fogMode;
                RenderSettings.fogColor = _fogColor;
                RenderSettings.fogDensity = _fogDensity;
                RenderSettings.sun = _sun;
            }
        }

        private static void Warp(Warrior character, Vector3 point)
        {
            if (character == null) return;
            var agent=character.GetComponent<NavMeshAgent>();
            // When the target surface is already baked, a normal warp keeps
            // the agent active and preserves its movement capabilities.
            if (agent!=null && agent.enabled && agent.isOnNavMesh &&
                agent.Warp(point)) return;

            bool wasEnabled=agent!=null && agent.enabled;
            if (wasEnabled) agent.enabled=false;
            if (NavMesh.SamplePosition(point, out var safe, 3f, NavMesh.AllAreas))
            {
                character.transform.position=safe.position;
                if (wasEnabled) agent.enabled=true;
            }
            else
            {
                // Never re-enable an agent at an unbaked position: Unity
                // would report "Failed to create agent ... close to NavMesh"
                // and subsequent AOS movement would silently stop working.
                character.transform.position=point;
                Debug.LogWarning("[EXPEDITION] No NavMesh near return/entry: " +
                                 character.DisplayName);
            }
        }

        private void SpawnGuards()
        {
            int remaining=Mathf.Clamp(_state.EnemiesRemaining,0,8);
            var sceneSetup=UnityEngine.Object.FindFirstObjectByType<AOS.AOSSceneSetup>();
            var relations=new RelationshipSystem(AOS.MemoryProcessor.Instance);
            bool order=_state.Occupant==1;
            bool undead=!order && _plan.Kind!=ExpeditionMapKind.SaltRoad;
            // Выжившие прошлых встреч стоят первыми — с прежним именем, грехом и ранами (Survivors).
            var known=Survivors.At(_plan.Id);
            if(_return==Return.SameNight && known.Count>0) _why.Insert(0,Survivors.SameNight());
            // Имена отряда — тоже заняты: поднятый охотник пролога в отряде и страж с тем же именем
            // читались бы одним человеком.
            var taken=new List<string>();
            foreach(var m in SquadRoster.Members) if(!string.IsNullOrEmpty(m.Name)) taken.Add(m.Name);
            for(int i=0;i<remaining;i++)
            {
                Vector3 at=ExpeditionMapBuilder.Point(_plan,(i%2==0?-1f:1f)*(5f+i*2f),
                    _plan.GoalZ-13f-(i/2)*5f);
                var was=i<known.Count ? known[i] : null;
                // Охотник пришёл издалека и живым (п.2): место не делает его покойником, а обоз,
                // который ещё не тронули, не делает мирным — он пришёл за Греховодом.
                bool hunter=was!=null && was.Reason==Survivors.Hunt;
                bool bones=undead && !hunter;
                var go=new GameObject(hunter ? "Ищет Греховода" : order ? "Дозор Ордена" : _plan.Kind==ExpeditionMapKind.SaltRoad
                    ? "Страж соляного обоза" : "Покойник при часовне");
                go.transform.SetParent(_mapRoot.transform,true);
                go.transform.position=at;
                var w=go.AddComponent<Warrior>();
                var shell=bones ? ShellType.Skeleton : ShellType.Living;
                // A caravan isn't a battle until the player attacks or
                // explicitly orders an assault. Chapel defenders remain
                // hostile from the outset. State persists across visits.
                Team disposition=!hunter && _plan.Kind==ExpeditionMapKind.SaltRoad &&
                    !_state.CaravanAssaulted && !_state.CaravanPassed
                    ? Team.Neutral : Team.Enemy;
                string name=was!=null ? was.Name : Survivors.NameFor(order ? "order" : undead ? "bound" : "caravan",taken);
                taken.Add(name);
                var sin=was!=null ? (SinType)was.Sin : (SinType)(i%7);
                // Страж Ордена — с чином, как охотник пролога: «Готфрид Инквизитор»; чин помнит запись.
                var rank=was!=null && was.Trade!=0 ? (Trade)was.Trade : order ? Survivors.OrderRank(i) : Trade.None;
                var soul=new SoulData(name,sin,MoralType.Vicious,1,55f);
                if(rank!=Trade.None) soul.SetTrade(rank);
                // Прежняя личность — прежний номер: память AOS наших о нём снова находит адресата.
                w.Initialize(soul,shell,relations,false,disposition,
                    was!=null && !string.IsNullOrEmpty(was.Id) ? was.Id : null);
                WarriorRig.Attach(go);
                WarriorLook.Build(go,shell,1.65f,1f,1f);
                sceneSetup?.SetupWarrior(go);
                _guards.Add(w);
                var record=was ?? new SavedSurvivor{Name=name,Sin=(int)sin,Undead=bones};
                record.Undead=bones;
                record.Trade=(int)rank;
                if(record.Reason!="guide" && !hunter) record.Reason=order ? "order" : undead ? "bound" : "caravan";
                record.Id=w.Id;
                _met[w]=record;
                if(was!=null)
                {
                    // Покойник не лечится: кости с прошлой встречи не срослись.
                    // Живого залечило время — если оно прошло: той же ночью раны те же.
                    // Охотник шёл издалека — время у него прошло, что бы ни было с этим местом.
                    if(was.Health<0.999f && (bones || !hunter && _return==Return.SameNight))
                        go.GetComponent<Damageable>()?.Wound(was.Health);
                    _why.Add(Survivors.Why(was,w.MaxHP));
                    Debug.Log("[ВЫЖИВШИЕ] "+Survivors.Why(was,w.MaxHP));
                }
            }
        }

        // ── Выжившие помнят (Survivors, план 69 §7) ──
        private readonly Dictionary<Warrior,SavedSurvivor> _met=new();
        private readonly HashSet<Warrior> _greeted=new();
        private readonly List<string> _why=new();
        private Return _return;
        private CombatManager _combat;
        private float _nextLook;

        void Update()
        {
            if(_exiting || _plan==null) return;
            if(_combat==null && CombatManager.Instance!=null)
            { _combat=CombatManager.Instance; _combat.OnAnyDeath+=OnDeath; }
            if(Time.time<_nextLook) return;
            _nextLook=Time.time+0.5f;
            var hero=SinbinderPlayer.Exists ? SinbinderPlayer.Instance : null;
            foreach(var g in _guards)
            {
                if(g==null || g.IsDead || !_met.TryGetValue(g,out var rec)) continue;
                var mind=g.GetComponent<AOS.AOSWarriorWrapper>();
                if(mind!=null && mind.LastDecisionDetail.Action.ToString()=="Flee") rec.Fled=true;
                // Узнал — один раз за поход, и только тот, кто уже встречал Греховода.
                // Личный счёт — раньше общего «опять ты»: кто именно убил, кто именно ранил.
                if(!_greeted.Contains(g) && rec.Grudges!=null && rec.Grudges.Count>0 && CallOut(g,rec)) { _greeted.Add(g); continue; }
                if(hero!=null && rec.Meetings>0 && !_greeted.Contains(g) && Dist(g.transform.position,hero.transform.position)<14f)
                {
                    _greeted.Add(g);
                    string line=Survivors.Greeting(rec);
                    UI.SpeechBubbles.Say(g,line,5f);
                    UnityEngine.Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(Loc.F("{0}: «{1}»",Loc.Name(g.DisplayName),line));
                    Debug.Log("[ВЫЖИВШИЕ] узнал: "+g.DisplayName+" — "+line);
                }
            }
        }

        /// <summary>Кто видел смерть и запомнил убийцу — тот же радиус, что у свидетелей AOS (AOSEventHub).</summary>
        private const float Witness=15f;
        /// <summary>С какого расстояния узнают того, с кем счёт.</summary>
        private const float Recognize=20f;
        private readonly HashSet<Warrior> _spoke=new();
        private float _lastCallOut=-99f;

        /// <summary>
        /// Только что прозвучал личный счёт — перебранка отряда (DialogueTrigger) ждёт:
        /// кадр 23.11 — «Косой Ждан! Прохор Немытый лежит из-за тебя» тонул в чужом разговоре.
        /// </summary>
        public static bool HoldBanter => _active!=null && !_active._exiting && Time.time-_active._lastCallOut<8f;

        private void OnDeath(Damageable killed,GameObject killer)
        {
            var dead=killed!=null ? killed.Warrior : null;
            if(dead==null || killer==null) return;
            var by=killer.GetComponentInParent<Warrior>();
            if(by==null) return;
            // Пал страж от руки нашего — товарищи рядом запомнили, кто именно (и Греховода тоже).
            if(_met.TryGetValue(dead,out var fallen))
            {
                if(by.Team!=Team.Player) return;
                foreach(var kv in _met)
                    if(kv.Key!=null && kv.Key!=dead && !kv.Key.IsDead &&
                       Dist(kv.Key.transform.position,dead.transform.position)<Witness)
                        Survivors.Grudge(kv.Value,by.DisplayName,fallen.Name,false);
                return;
            }
            // Греховод пал — это конец игры, а не строка в памяти врага.
            if(dead.Team!=Team.Player || dead is SinbinderPlayer || !_met.TryGetValue(by,out var rec)) return;
            rec.Killed=string.IsNullOrEmpty(rec.Killed) ? Loc.Name(dead.DisplayName) : rec.Killed+", "+Loc.Name(dead.DisplayName);
            // И сам павший помнит, кто его убил: поднимет его некромант — узнает убийцу (MurderedMe).
            Survivors.Grudge(rec,dead.DisplayName,dead.DisplayName,true);
            // Наши рядом видели, кто именно убил. Греховод — сам игрок: за него помнит игрок.
            foreach(var w in Squad())
                if(w!=dead && Dist(w.transform.position,dead.transform.position)<Witness)
                    Survivors.Grudge(rec,w.DisplayName,dead.DisplayName,true);
        }

        /// <summary>Удар в личный счёт: наш ранил стража или страж — нашего (Греховода не считаем).</summary>
        private void NoteHit(Warrior victim,GameObject attacker)
        {
            var by=attacker.GetComponentInParent<Warrior>();
            if(by==null || by is SinbinderPlayer || victim is SinbinderPlayer) return;
            if(_met.TryGetValue(victim,out var hurt) && by.Team==Team.Player)
                Survivors.Grudge(hurt,by.DisplayName,"",false);
            else if(victim.Team==Team.Player && _met.TryGetValue(by,out var striker))
                Survivors.Grudge(striker,victim.DisplayName,"",true);
        }

        private static List<Warrior> Squad()
        {
            var list=new List<Warrior>();
            foreach(var w in UnityEngine.Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                if(w!=null && !w.IsDead && w.Team==Team.Player && !(w is SinbinderPlayer)) list.Add(w);
            return list;
        }

        /// <summary>
        /// Встреча со своим обидчиком: стражник окликает нашего, который убил его товарища
        /// или ранил его, — или наш окликает стражника. Убийство важнее раны. Память AOS
        /// получает адресата: в бою говоривший тянется к тому, с кем счёт (AOSWarriorWrapper.Grudge).
        /// </summary>
        private bool CallOut(Warrior guard,SavedSurvivor rec)
        {
            SavedGrudge best=null; Warrior other=null; Warrior hero=SinbinderPlayer.Exists ? SinbinderPlayer.Instance : null;
            var squad=Squad();
            if(hero!=null && !hero.IsDead) squad.Add(hero);
            foreach(var g in rec.Grudges)
            {
                if(g==null) continue;
                if(best!=null && !string.IsNullOrEmpty(best.Victim)) break;
                var w=squad.Find(x => x.DisplayName==g.Who);
                if(w==null || Dist(w.transform.position,guard.transform.position)>Recognize) continue;
                if(g.Ours && (w is SinbinderPlayer || _spoke.Contains(w))) continue;
                if(best==null || (string.IsNullOrEmpty(best.Victim) && !string.IsNullOrEmpty(g.Victim))) { best=g; other=w; }
            }
            if(best==null) return false;
            bool kill=!string.IsNullOrEmpty(best.Victim);
            bool me=kill && best.Ours && best.Victim==best.Who;   // поднятый узнал своего убийцу
            var speaker=best.Ours ? other : guard;
            var target=best.Ours ? guard : other;
            string line=me ? Survivors.Murderer(target.DisplayName)
                : kill ? Survivors.CallOut(target.DisplayName,best.Victim) : Survivors.Scar(target.DisplayName);
            UI.SpeechBubbles.Say(speaker,line,5f);
            UnityEngine.Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(Loc.F("{0}: «{1}»",Loc.Name(speaker.DisplayName),line));
            AOS.MemoryProcessor.Instance?.CreateMemory(speaker,me ? "MurderedMe" : kill ? "EnemyKilledAlly" : "WoundedMe",target.Id,
                AOS.EmotionType.Anger,kill ? 0.8f : 0.5f);
            _spoke.Add(speaker);
            _lastCallOut=Time.time;
            Debug.Log("[ВЫЖИВШИЕ] счёт: "+speaker.DisplayName+" → "+target.DisplayName+" — "+line);
            return true;
        }

        /// <summary>Итог встречи в память: кто из стражи жив — запомнен, павшие забыты.</summary>
        private void SettleSurvivors()
        {
            var alive=new List<SavedSurvivor>();
            foreach(var g in _guards)
            {
                if(g==null || g.IsDead || !_met.TryGetValue(g,out var rec)) continue;
                rec.Health=g.MaxHP>0f ? Mathf.Clamp01(g.HP/g.MaxHP) : 1f;
                rec.Meetings++;
                rec.SeenAt=_plan.Id;   // видели его здесь — меню назовёт его здесь
                alive.Add(rec);
            }
            // Обоз разграблен — охранять больше нечего: выжившая охрана уходит на дорогу
            // (оттуда её возьмёт дозор Ордена — п.2), а на месте обоза пусто.
            if(_plan.Kind==ExpeditionMapKind.SaltRoad && _state!=null && _state.CargoTaken && !_state.CaravanPassed && alive.Count>0)
            {
                Survivors.Settle(_plan.Id,null);
                Survivors.Wander(alive);
                _state.EnemiesRemaining=0;
                foreach(var s in alive) Debug.Log($"[ВЫЖИВШИЕ] {s.Name}: обоз разграблен — ушёл на дорогу");
                return;
            }
            Survivors.Settle(_plan.Id,alive);
            foreach(var s in alive)
                Debug.Log($"[ВЫЖИВШИЕ] запомнен: {s.Name} при «{_plan.Title}», здоровье {s.Health:P0}, встреч {s.Meetings}"
                    +(string.IsNullOrEmpty(s.Killed) ? "" : ", убил: "+s.Killed)+(s.Fled ? ", бежал" : ""));
        }

        private int EnemiesLeft()
        {
            int count=0;
            foreach(var w in _guards) if(w!=null && !w.IsDead) count++;
            return count;
        }

        private Vector3 Gate => ExpeditionMapBuilder.Point(_plan,0f,_plan.EntranceZ);
        private Vector3 CaravanDecisionPoint => ExpeditionMapBuilder.Point(_plan,0f,_plan.EntranceZ+19f);
        private Vector3 Objective => ExpeditionMapBuilder.Point(_plan,0f,_plan.GoalZ);
        private static float Dist(Vector3 a,Vector3 b)
        { a.y=0f; b.y=0f; return Vector3.Distance(a,b); }

        /// <summary>
        /// The central F-interaction selector owns mission objectives too.
        /// Never read F in a second Update: GearPanel may otherwise take a
        /// corpse bag and award cargo in the same frame, or end the mission
        /// while the player is interacting with another object.
        /// </summary>
        public static void OfferInteractions(System.Action<float,string,System.Action> offer)
        {
            if (_active == null || _active._exiting || !SinbinderPlayer.Exists ||
                offer == null) return;
            var expedition = _active;
            if (Dist(SinbinderPlayer.Where,expedition.Gate)<4f)
                offer(Gaze.Miss(expedition.Gate+Vector3.up*1.4f,1.5f),
                    Loc.T("F — вернуться в склеп"), expedition.Exit);
            if (expedition._plan.Kind==ExpeditionMapKind.SaltRoad &&
                !expedition._state.CaravanPassed &&
                !expedition._state.CargoTaken &&
                !expedition._state.CaravanAssaulted &&
                Dist(SinbinderPlayer.Where,expedition.CaravanDecisionPoint)<6f)
                offer(Gaze.Miss(expedition.CaravanDecisionPoint+Vector3.up*1.4f,1.5f),
                    Loc.T("F — решить судьбу обоза"),expedition.OpenCaravanChoice);
            if (Dist(SinbinderPlayer.Where,expedition.Objective)<4f)
                offer(Gaze.Miss(expedition.Objective+Vector3.up*1.4f,1.5f),
                    Loc.T("F — исследовать цель"), expedition.CollectObjective);
        }

        private void OpenCaravanChoice()
        {
            if (_exiting || _state.CaravanPassed || _state.CargoTaken ||
                _state.CaravanAssaulted) return;
            _caravanChoiceOpen=true;
        }

        private void ChooseCaravan(bool attack)
        {
            if (!_caravanChoiceOpen || _exiting || _state.CaravanPassed ||
                _state.CargoTaken) return;
            _caravanChoiceOpen=false;
            if (attack)
            {
                ProvokeCaravan();
                Announce(Loc.T("Греховод приказал напасть. Охрана защищает обоз настоящим боем."));
                return;
            }
            // LetThemPass is a real irreversible outcome, not a victory
            // with invisible guards or an award of cargo on next visit.
            _state.CaravanPassed=true;
            _state.CargoTaken=true;
            _state.EnemiesRemaining=0;
            foreach (var guard in _guards)
                if (guard!=null) UnityEngine.Object.Destroy(guard.gameObject);
            _guards.Clear();
            Announce(Loc.T("Греховод пропустил обоз. Возчики увезли серебро; добычи не будет."));
        }

        /// <summary>
        /// Convert caravan guards into real combat enemies. A single cause
        /// (either a player's explicit assault or their first hostile hit)
        /// updates both Warrior.Team and CombatManager's live registries.
        /// This is not a scripted defeat, a fake fight or a new AI faction.
        /// </summary>
        private void ProvokeCaravan()
        {
            if(_exiting || _plan==null ||
               _plan.Kind!=ExpeditionMapKind.SaltRoad ||
               _state==null || _state.CaravanPassed ||
               _state.CargoTaken || _state.CaravanAssaulted) return;

            _state.CaravanAssaulted=true;
            _caravanChoiceOpen=false;
            foreach(var guard in _guards)
            {
                if(guard==null || guard.IsDead) continue;
                guard.Team=Team.Enemy;
                var body=guard.GetComponent<Damageable>();
                if(body!=null && CombatManager.Instance!=null)
                    CombatManager.Instance.UpdateTeam(body,Team.Enemy);
            }
        }

        /// <summary>
        /// A direct attack on a neutral caravan guard is an irreversible
        /// attack decision. Called by the real Damageable pipeline, before
        /// the blow lands, so other guards can respond immediately.
        /// No other map/faction is affected.
        /// </summary>
        public static void OnGuardAttacked(Warrior victim, GameObject attacker)
        {
            var expedition=_active;
            if(expedition!=null && victim!=null && attacker!=null && !expedition._exiting) expedition.NoteHit(victim,attacker);
            if(expedition==null || victim==null || attacker==null ||
               expedition._plan==null ||
               expedition._plan.Kind!=ExpeditionMapKind.SaltRoad ||
               expedition._state==null || expedition._state.CaravanAssaulted ||
               expedition._state.CaravanPassed || expedition._state.CargoTaken ||
               !expedition._guards.Contains(victim)) return;

            var assailant=attacker.GetComponentInParent<Warrior>();
            if(assailant==null || assailant.Team!=Team.Player) return;
            expedition.ProvokeCaravan();
            expedition.Announce(Loc.T(
                "Вы напали на стража. Охрана обоза стала враждебной, мирный исход закрыт."));
        }

        private void CollectObjective()
        {
            if(_plan.Kind==ExpeditionMapKind.SaltRoad &&
               !_state.CaravanPassed && !_state.CargoTaken &&
               !_state.CaravanAssaulted)
            {
                OpenCaravanChoice();
                return;
            }
            if(EnemiesLeft()>0)
            {
                Announce(Loc.T("Охрана не дала добраться до добычи. Сначала разберитесь с ней."));
                return;
            }
            if (_plan.Kind==ExpeditionMapKind.SaltRoad)
            {
                if(_state.CaravanPassed)
                { Announce(Loc.T("Обоз ушёл по дороге. Серебро больше недоступно.")); return; }
                if(_state.CargoTaken)
                { Announce(Loc.T("Обоз уже разгружен.")); return; }
                var inventory=Inventory.PlayerInventory.Instance;
                if(inventory==null)
                { Announce(Loc.T("Ваш кошель недоступен. Добыча остаётся на месте.")); return; }
                int coins=MissionCatalog.Coin(_mission.Prize);
                inventory.AddGold(coins);
                _state.CargoTaken=true;
                Announce(Loc.F("Вы забрали серебро обоза: {0} монет.",coins));
            }
            else
            {
                if(_state.SecretTaken)
                { Announce(Loc.T("Тайник часовни уже пуст.")); return; }
                var inventory=Inventory.PlayerInventory.Instance;
                if(inventory==null) { Announce(Loc.T("В вашей суме нет места для находки.")); return; }
                var item=new Inventory.InventoryItem(Loc.N("Запечатанное письмо из часовни"),
                    Loc.N("Скрыто под алтарём. Отряд без искателя секретов его бы не обнаружил."),
                    Inventory.ItemType.Artifact);
                if(!inventory.AddItem(item))
                { Announce(Loc.T("Освободите место для находки.")); return; }
                _state.SecretTaken=true;
                Announce(Loc.T("Под алтарём обнаружено запечатанное письмо. Оно в вашей суме."));
            }
        }

        private void Announce(string value)
        {
            _notice=value;
            _noticeUntil=Time.unscaledTime+8f;
            UnityEngine.Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(value);
        }

        private void OnGUI()
        {
            if (_exiting || _plan==null || !SinbinderPlayer.Exists) return;
            var style=new GUIStyle(GUI.skin.box){alignment=TextAnchor.UpperLeft,wordWrap=true,fontSize=15};
            string state=_plan.Kind==ExpeditionMapKind.SaltRoad
                ? (_state.CaravanPassed ? Loc.T("Обоз пропущен, серебро увезли")
                    : _state.CargoTaken ? Loc.T("Серебро уже забрано")
                    : _state.CaravanAssaulted
                        ? Loc.T("Вы напали на обоз: охрана теперь враждебна")
                        : Loc.T("Обоз не тронут. Охрана нейтральна, пока вы не нападёте"))
                : (_state.SecretTaken ? Loc.T("Тайник изучен") : Loc.T("Осмотреть алтарь часовни"));
            string prompt=Dist(SinbinderPlayer.Where,Gate)<4f ? Loc.T("F — вернуться в склеп")
                : _plan.Kind==ExpeditionMapKind.SaltRoad && !_state.CargoTaken &&
                  !_state.CaravanAssaulted &&
                  Dist(SinbinderPlayer.Where,CaravanDecisionPoint)<6f
                    ? Loc.T("F — решить судьбу обоза")
                : Dist(SinbinderPlayer.Where,Objective)<4f ? Loc.T("F — исследовать цель")
                : Loc.T("Следуйте к янтарному огню; бирюзовый огонь — путь назад.");
            string notice=Time.unscaledTime<_noticeUntil ? "\n"+_notice : "";
            if (_caravanChoiceOpen)
            {
                float width=Mathf.Min(460f,Screen.width-24f);
                var area=new Rect((Screen.width-width)*0.5f,
                    Mathf.Max(8f,Screen.height*0.25f),width,178f);
                GUI.Box(area,Loc.T("Соляной обоз. Возчики безоружны. Охрана готова защищать груз."));
                if(GUI.Button(new Rect(area.x+12f,area.y+52f,width-24f,34f),
                    Loc.T("Напасть на охрану и забрать серебро")))
                    ChooseCaravan(true);
                if(GUI.Button(new Rect(area.x+12f,area.y+94f,width-24f,34f),
                    Loc.T("Пропустить обоз без добычи")))
                    ChooseCaravan(false);
                if(GUI.Button(new Rect(area.x+12f,area.y+136f,width-24f,30f),
                    Loc.T("Пока не решать")))
                    _caravanChoiceOpen=false;
            }
            GUI.Box(new Rect(14f,Screen.height-138f,Mathf.Min(560f,Screen.width-28f),125f),
                Loc.T(_plan.Title)+"\n"+state+Loc.F(" · живых стражей: {0}",EnemiesLeft())+
                "\n"+prompt+notice,style);
        }

        public void Exit() => Exit(commitResult:true);

        /// <summary>
        /// commitResult:false is exclusively for SaveSystem.Restore.
        /// Never record a mission, hand out rewards or sync the old
        /// runtime roster over newly restored save data.
        /// </summary>
        private void Exit(bool commitResult)
        {
            if(_exiting) return;
            _exiting=true;
            _caravanChoiceOpen=false;
            if (commitResult && _state != null)
                _state.EnemiesRemaining=EnemiesLeft();
            if (commitResult && _plan != null) SettleSurvivors();
            if (_combat != null) { _combat.OnAnyDeath -= OnDeath; _combat = null; }
            // No duplicate reward by returning to the same expedition.
            foreach(var member in _travellers)
                if(member.Warrior!=null && !member.Warrior.IsDead)
                {
                    member.Warrior.ClearCommand();
                    Warp(member.Warrior,member.Home);
                }
            // Страховка: свой, который не уходил, но оказался на карте (навык, толчок),
            // не остаётся на убранной карте — встаёт в склепе рядом с местом Греховода.
            if (_plan != null && _travellers.Count > 0)
            {
                var centre = ExpeditionMapBuilder.Point(_plan, 0f, 0f);
                float reach = Mathf.Max(_plan.Width, _plan.Depth);
                int n = 0;
                foreach (var w in UnityEngine.Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                {
                    if (w == null || w.IsDead || w.Team != Team.Player || Travels(w) || Dist(w.transform.position, centre) > reach) continue;
                    w.ClearCommand();
                    Warp(w, _travellers[0].Home + new Vector3(1.5f + n++ * 1.2f, 0f, 1.5f));
                    Debug.LogWarning("[EXPEDITION] не уходил, но был на карте — возвращён в склеп: " + w.DisplayName);
                }
            }
            _active=null;
            FallenBag.OnMapLoaded(SceneManager.GetActiveScene().name);
            FogOfWar.Uncover();
            _night?.Lift();
            _night=null;
            var surface=_mapRoot!=null ? _mapRoot.GetComponent<NavMeshSurface>() : null;
            surface?.RemoveData();
            if (_mapRoot != null)
            {
                _mapRoot.SetActive(false);
                UnityEngine.Object.Destroy(_mapRoot);
            }
            _mapRoot=null;

            var camera=Camera.main!=null ? Camera.main.GetComponent<RTS_Camera>() : null;
            if(camera!=null) camera.SetView(_returnTactical
                ? RTS_Camera.CameraView.Tactical : RTS_Camera.CameraView.FirstPerson);

            // A normal expedition produces a report and synchronises living
            // warriors. Restore must NOT write stale state into the freshly
            // loaded save snapshot or award an unearned mission result.
            if (commitResult)
            {
                SquadRoster.RefreshFromScene(MissionBoard.Squad());
                // The separate TestChamber scene owns a real MissionBoard,
                // but has no CryptMap component. Preserve its authentic
                // expedition report rather than discarding the visit.
                var board = CryptMap.Board ??
                    UnityEngine.Object.FindFirstObjectByType<MissionBoard>();
                board?.RecordPersonal(_mission,
                    _state != null && ((_state.CargoTaken && !_state.CaravanPassed) || _state.SecretTaken));
                if (board == null) Sorties.Tick(); // возвращение — шаг времени мира и без доски
                if (_state != null)
                {
                    _state.LeftAt = Sorties.Count;
                    if (_state.EnemiesRemaining == 0 && _state.ClearedAt < 0) _state.ClearedAt = Sorties.Count;
                }
                // Греховод в склепе — и весть с ним: кто убил нашего и жив, теперь ищет его сам (п.2).
                // Отсюда — и с дороги: охрана разграбленного обоза ушла туда в этот же выход.
                foreach (var rumour in SendHunters(_plan != null ? _plan.Id : "", Survivors.Road))
                {
                    if (board != null) board.Hear(rumour);
                    else UnityEngine.Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(rumour);
                }
            }
            UnityEngine.Object.Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if(!_exiting && _active==this) Exit();
        }
    }
}
