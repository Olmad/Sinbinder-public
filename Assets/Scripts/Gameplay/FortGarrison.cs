// A fort's independent garrison; neutral townsfolk are never targeted by default.
using UnityEngine;
using UnityEngine.AI;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    public sealed class FortGarrison : MonoBehaviour
    {
        private int _guards, _archers;
        private float _half, _walk;
        public void Configure(int guards,int archers,float half,float walk)
        { _guards=guards; _archers=archers;_half=half;_walk=walk; }

        // GroundNavMesh bakes in Start at execution order -900. These sentries spawn later.
        void Start()
        {
            for(int i=0;i<_guards;i++)
            {
                float t=(_guards==1?.5f:i/(float)(_guards-1));
                Make("Страж форта",new Vector3((t-.5f)*_half,0f,_half*.20f),false,i);
            }
            for(int i=0;i<_archers;i++)
            {
                float t=(_archers==1?.5f:i/(float)(_archers-1));
                Make("Стрелок форта",new Vector3((t-.5f)*(_half*1.3f),_walk+.13f,_half-2.5f),true,20+i);
            }
        }
        void Make(string label,Vector3 local,bool ranged,int index)
        {
            var go=new GameObject(label+" "+(index+1));
            go.transform.SetParent(transform,false);
            go.transform.localPosition=local;
            go.transform.localRotation=Quaternion.Euler(0f,0f,0f);
            var warrior=go.AddComponent<Warrior>();
            var soul=new SoulData(label,SinType.Pride,MoralType.Neutral,2,65f);
            if(ranged)soul.SetTrade(Trade.Archer);   // стрелок — лучник: настоящая стрела (AutoAttack), не луч
            warrior.Initialize(soul,ShellType.Skeleton,new RelationshipSystem(AOS.MemoryProcessor.Instance),false,Team.Neutral);
            WarriorRig.Attach(go,3.2f);
            WarriorLook.Build(go,ShellType.Skeleton,1.65f,1f,1f);
            var auto=go.GetComponent<AutoAttack>();if(auto!=null)auto.enabled=false;
            var agent=go.GetComponent<NavMeshAgent>();if(agent!=null)agent.enabled=false;
            go.AddComponent<FortSentry>().Configure(transform.position,_half+(ranged?25f:8f),ranged);
        }
    }

    public sealed class FortSentry : MonoBehaviour
    {
        private Vector3 _centre;
        private float _radius;
        private bool _ranged;
        private float _next;
        private Damageable _self;
        public void Configure(Vector3 centre,float radius,bool ranged)
        { _centre=centre;_radius=radius;_ranged=ranged; }
        void Awake() { _self=GetComponent<Damageable>(); }
        void Update()
        {
            if(Time.deltaTime<=0f || (_self!=null && _self.IsDead) || Time.time<_next)return;
            _next=Time.time+(_ranged?1.7f:1.15f);
            float range=_ranged?27f:3f;
            Damageable candidate=null;float best=range*range;
            foreach(var w in FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if(w==null || w.gameObject==gameObject || w.IsDead)continue;
                // No attack on neutral inhabitants or untouched wandering civilians.
                if(w.Team!=Team.Player && !w.IsCommander && !w.HasCommand)continue;
                if(Vector3.Distance(_centre,w.transform.position)>_radius)continue;
                var d=w.GetComponent<Damageable>();if(d==null || d.IsDead)continue;
                float sq=(w.transform.position-transform.position).sqrMagnitude;
                if(sq<best){best=sq;candidate=d;}
            }
            if(candidate==null)return;
            Vector3 origin=transform.position+Vector3.up*1.25f;
            Vector3 dest=candidate.transform.position+Vector3.up*.85f;
            var vec=dest-origin;
            if(vec.sqrMagnitude < .1f) return;
            float nearest=float.PositiveInfinity;
            Damageable hitActor=null;
            bool stopped=false;
            foreach(var hit in Physics.RaycastAll(origin,vec.normalized,vec.magnitude+.3f,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.collider.transform.IsChildOf(transform)) continue;
                if(hit.distance>=nearest) continue;
                nearest=hit.distance;
                hitActor=hit.collider.GetComponentInParent<Damageable>();
                stopped=true;
            }
            if(stopped && hitActor!=candidate) return;
            var horizontal=new Vector3(vec.x,0f,vec.z);
            if(horizontal.sqrMagnitude>.01f) transform.rotation=Quaternion.LookRotation(horizontal);
            // Стрелок — настоящей стрелой (AutoAttack: дуга, урон по прилёту; автор 25.11: «нам нужны лучники!»); луч — только если лука нет.
            if(_ranged && TryGetComponent<AutoAttack>(out var bow)){bow.ForceAttack(candidate);return;}
            candidate.TakeDamage(_ranged?7f:5f,gameObject);
            if(_ranged) Trace(origin,dest);
        }
        private static void Trace(Vector3 start,Vector3 end)
        {
            var streak = new GameObject("Выстрел со стены");
            var line = streak.AddComponent<LineRenderer>();
            line.positionCount=2;
            line.SetPosition(0,start);line.SetPosition(1,end);
            line.startWidth=.09f;line.endWidth=.018f;
            var shader=Shader.Find("Sprites/Default");
            if(shader!=null)
            {
                var material=new Material(shader);
                material.color=new Color(1f,.78f,.31f);
                line.sharedMaterial=material;
                streak.AddComponent<FortTracerCleanup>().Material=material;
            }
            Object.Destroy(streak,.16f);
        }
    }
    public sealed class FortTracerCleanup : MonoBehaviour
    {
        public Material Material;
        void OnDestroy(){ if(Material!=null) Destroy(Material); }
    }
}
