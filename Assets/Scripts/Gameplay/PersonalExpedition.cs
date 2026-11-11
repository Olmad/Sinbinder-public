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
        public bool SecretTaken;
        public int Visits;
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
        public static string CurrentMapKey => _active != null && _active._plan != null
            ? _active._plan.Id : SceneManager.GetActiveScene().name;

        public static List<SavedPersonalExpedition> SnapshotProgress()
        {
            var list = new List<SavedPersonalExpedition>();
            foreach (var state in Progress.Values)
                list.Add(new SavedPersonalExpedition
                { Id=state.Id, EnemiesRemaining=state.EnemiesRemaining,
                  CargoTaken=state.CargoTaken, SecretTaken=state.SecretTaken,
                  Visits=state.Visits });
            return list;
        }

        public static void RestoreProgress(List<SavedPersonalExpedition> saved)
        {
            if (Active) _active.Exit();
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

            var travellers = new List<Traveller>
            {
                new Traveller { Warrior=SinbinderPlayer.Instance, Home=SinbinderPlayer.Where }
            };

            // Bring THE SAME living AOS soldiers from the crypt. A paper
            // roster record is not an interactive character, and silently
            // replacing one would erase its real memories and equipment.
            if (party != null)
            {
                var seen = new HashSet<string>();
                foreach (string name in party)
                {
                    if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;
                    // Recheck the selected roster at the moment of departure.
                    // UI was opened earlier, and a soldier might now be away,
                    // unavailable or on another mission. Never teleport them.
                    if (!SquadRoster.TryGet(name, out var member) ||
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
                            w.Team == Team.Player && w.DisplayName == name)
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
            var camera=Camera.main != null ? Camera.main.GetComponent<RTS_Camera>() : null;
            visit._returnTactical=camera!=null && !camera.FirstPersonNow;
            camera?.SetView(RTS_Camera.CameraView.FirstPerson);
            visit.enabled = true;
            visit.Announce(Loc.F("Экспедиция «{0}»: доберитесь до отмеченной цели и вернитесь к бирюзовому знаку.",
                Loc.T(plan.Title)));
            return true;
        }

        private ExpeditionMapPlan _plan;
        private Mission _mission;
        private GameObject _mapRoot;
        private SavedPersonalExpedition _state;
        private readonly List<Traveller> _travellers=new();
        private readonly List<Warrior> _guards=new();
        private bool _exiting, _returnTactical;
        private string _notice="";
        private float _noticeUntil;

        private static void Warp(Warrior character, Vector3 point)
        {
            if (character == null) return;
            var agent=character.GetComponent<NavMeshAgent>();
            if (agent!=null && agent.enabled && agent.isOnNavMesh && agent.Warp(point)) return;
            if (agent!=null && agent.enabled) agent.enabled=false;
            character.transform.position=point;
            if (agent!=null) agent.enabled=true;
        }

        private void SpawnGuards()
        {
            int remaining=Mathf.Clamp(_state.EnemiesRemaining,0,8);
            var sceneSetup=UnityEngine.Object.FindFirstObjectByType<AOS.AOSSceneSetup>();
            var relations=new RelationshipSystem(AOS.MemoryProcessor.Instance);
            for(int i=0;i<remaining;i++)
            {
                Vector3 at=ExpeditionMapBuilder.Point(_plan,(i%2==0?-1f:1f)*(5f+i*2f),
                    _plan.GoalZ-13f-(i/2)*5f);
                var go=new GameObject(_plan.Kind==ExpeditionMapKind.SaltRoad
                    ? "Страж соляного обоза" : "Покойник при часовне");
                go.transform.SetParent(_mapRoot.transform,true);
                go.transform.position=at;
                var w=go.AddComponent<Warrior>();
                var shell=_plan.Kind==ExpeditionMapKind.SaltRoad
                    ? ShellType.Living : ShellType.Skeleton;
                w.Initialize(new SoulData("Страж экспедиции "+(i+1),
                        (SinType)(i%7),MoralType.Vicious,1,55f),shell,relations,false,Team.Enemy);
                WarriorRig.Attach(go);
                WarriorLook.Build(go,shell,1.65f,1f,1f);
                sceneSetup?.SetupWarrior(go);
                _guards.Add(w);
            }
        }

        private int EnemiesLeft()
        {
            int count=0;
            foreach(var w in _guards) if(w!=null && !w.IsDead) count++;
            return count;
        }

        private Vector3 Gate => ExpeditionMapBuilder.Point(_plan,0f,_plan.EntranceZ);
        private Vector3 Objective => ExpeditionMapBuilder.Point(_plan,0f,_plan.GoalZ);
        private static float Dist(Vector3 a,Vector3 b)
        { a.y=0f; b.y=0f; return Vector3.Distance(a,b); }

        private void Update()
        {
            if (_exiting || !SinbinderPlayer.Exists) return;
            if (Core.GamePauseController.Instance != null &&
                Core.GamePauseController.Instance.IsPaused) return;
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (Dist(SinbinderPlayer.Where,Gate)<4f)
                {
                    Exit();
                    return;
                }
                if (Dist(SinbinderPlayer.Where,Objective)<4f &&
                    FallenBag.Near()==null)
                    CollectObjective();
            }
        }

        private void CollectObjective()
        {
            if(EnemiesLeft()>0)
            {
                Announce(Loc.T("Охрана не дала добраться до добычи. Сначала разберитесь с ней."));
                return;
            }
            if (_plan.Kind==ExpeditionMapKind.SaltRoad)
            {
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
                ? (_state.CargoTaken ? Loc.T("Серебро уже забрано") : Loc.T("Найти обоз и забрать серебро"))
                : (_state.SecretTaken ? Loc.T("Тайник изучен") : Loc.T("Осмотреть алтарь часовни"));
            string prompt=Dist(SinbinderPlayer.Where,Gate)<4f ? Loc.T("F — вернуться в склеп")
                : Dist(SinbinderPlayer.Where,Objective)<4f ? Loc.T("F — исследовать цель")
                : Loc.T("Следуйте к янтарному огню; бирюзовый огонь — путь назад.");
            string notice=Time.unscaledTime<_noticeUntil ? "\n"+_notice : "";
            GUI.Box(new Rect(14f,Screen.height-138f,Mathf.Min(560f,Screen.width-28f),125f),
                Loc.T(_plan.Title)+"\n"+state+Loc.F(" · живых стражей: {0}",EnemiesLeft())+
                "\n"+prompt+notice,style);
        }

        public void Exit()
        {
            if(_exiting) return;
            _exiting=true;
            _state.EnemiesRemaining=EnemiesLeft();
            // No duplicate reward by returning to the same expedition.
            foreach(var member in _travellers)
                if(member.Warrior!=null && !member.Warrior.IsDead)
                {
                    member.Warrior.ClearCommand();
                    Warp(member.Warrior,member.Home);
                }
            _active=null;
            FallenBag.OnMapLoaded(SceneManager.GetActiveScene().name);
            var surface=_mapRoot!=null ? _mapRoot.GetComponent<NavMeshSurface>() : null;
            surface?.RemoveData();
            if(_mapRoot!=null) UnityEngine.Object.Destroy(_mapRoot);
            _mapRoot=null;

            var camera=Camera.main!=null ? Camera.main.GetComponent<RTS_Camera>() : null;
            if(camera!=null) camera.SetView(_returnTactical
                ? RTS_Camera.CameraView.Tactical : RTS_Camera.CameraView.FirstPerson);

            // All real surviving companions keep their original AOS soul,
            // carried gear and pocket; dead companions no longer appear.
            SquadRoster.Remember(MissionBoard.Squad());
            CryptMap.Board?.RecordPersonal(_mission,_state.CargoTaken||_state.SecretTaken);
            UnityEngine.Object.Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if(!_exiting && _active==this) Exit();
        }
    }
}
