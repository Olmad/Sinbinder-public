// Assets/Scripts/Gameplay/FallenBag.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Inventory;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Мешок павшего (облачная, 26.10, ответ 6/7; автор — «да»). Свой пал — на месте гибели
    /// «Мешок {кого}»: надетое, карман и памятная вещь. Греховод поднимает его на F.
    ///
    /// Не поднял сам — через несколько секунд решают свои, кто рядом, и решают душой,
    /// а не правилом (отношение к павшему — из памяти, вычисляется, не хранится):
    /// * брат или друг павшего — несёт вещи Греховоду;
    /// * жадный, что с павшим не ладил, — берёт себе (в карман и на себя);
    /// * благочестивый — не трогает: «у мёртвых не берут».
    /// Памятную вещь (лопата Марги, перо Каргана, свеча Вейна) свои не берут никогда —
    /// только Греховод; с ним она уходит в склеп. Знамя «Крыс» — своя история (<see cref="RatBanner"/>).
    /// </summary>
    public class FallenBag : MonoBehaviour
    {
        private const float Reach = 1.6f;
        private const float WaitForHero = 6f;
        private const float Near = 8f;

        private static readonly List<FallenBag> Live = new();

        private string _owner = "", _ownerId = "";
        private readonly List<InventoryItem> _items = new();
        private int _gold;
        private InventoryItem _memento;
        private float _since;
        private bool _decided;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Live.Clear();

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        // ---------------------------------------------------------- появление

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindFirstObjectByType<Watcher>() != null) return;
            var go = new GameObject("Мешки павших");
            DontDestroyOnLoad(go);
            go.AddComponent<Watcher>();
        }

        /// <summary>Слушает гибели в любой сцене с боем — как <see cref="SignatureDeaths"/>.</summary>
        private class Watcher : MonoBehaviour
        {
            private CombatManager _subscribed;

            void Update()
            {
                var combat = CombatManager.Instance;
                if (combat == null || combat == _subscribed) return;
                if (_subscribed != null) _subscribed.OnAnyDeath -= OnDeath;
                combat.OnAnyDeath += OnDeath;
                _subscribed = combat;
            }

            void OnDestroy()
            {
                if (_subscribed != null) _subscribed.OnAnyDeath -= OnDeath;
            }
        }

        private static void OnDeath(Damageable killed, GameObject killer)
        {
            var w = killed != null ? killed.Warrior : null;
            if (w == null || w is SinbinderPlayer || w.Team != Team.Player) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = Loc.F("Мешок {0}", w.DisplayName);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = w.transform.position + w.transform.right * 0.6f + Vector3.up * 0.14f;
            go.transform.localScale = new Vector3(0.38f, 0.28f, 0.32f);
            var r = go.GetComponent<Renderer>();
            r.material.color = new Color(0.30f, 0.23f, 0.15f);   // мешковина

            var bag = go.AddComponent<FallenBag>();
            bag._owner = w.DisplayName;
            bag._ownerId = w.Id;
            bag._since = Time.time;
            foreach (GearSlot slot in System.Enum.GetValues(typeof(GearSlot)))
            {
                if (slot == GearSlot.None) continue;
                var item = w.Worn(slot);
                if (item != null) bag._items.Add(item);
            }
            w.TakeAll();
            bag._gold = w.EmptyPocket();
            bag._memento = Memento(w.DisplayName);
        }

        /// <summary>Памятная вещь по имени — та, что в его примете.</summary>
        private static InventoryItem Memento(string name)
        {
            string title = null, line = null;
            if (name.Contains("Марга")) { title = Loc.N("Лопата Марги"); line = Loc.N("Он копал ею везде, где вставал лагерь."); }
            else if (name.Contains("Карган")) { title = Loc.N("Воронье перо"); line = Loc.N("Ворон прибился к нему ещё при жизни."); }
            else if (name.Contains("Вейн")) { title = Loc.N("Огарок свечи Вейна"); line = Loc.N("Он читал при ней, пока лагерь спал."); }
            return title == null ? null : new InventoryItem(title, line, ItemType.Artifact);
        }

        // ---------------------------------------------------------- Греховод

        /// <summary>Все мешки, до которых достаёт рука: F — тому, на который смотрят (UI.GearPanel).</summary>
        public static IEnumerable<FallenBag> InReach()
        {
            if (!SinbinderPlayer.Exists) yield break;
            foreach (var b in Live)
                if (b != null && CampFocus.GroundDistance(SinbinderPlayer.Where, b.transform.position) <= Reach)
                    yield return b;
        }

        public static FallenBag Nearest()
        {
            if (!SinbinderPlayer.Exists) return null;
            foreach (var b in Live)
                if (b != null && CampFocus.GroundDistance(SinbinderPlayer.Where, b.transform.position) <= Reach)
                    return b;
            return null;
        }

        public string Prompt => Loc.F("F — поднять мешок {0}", Loc.Name(_owner));

        /// <summary>F: всё — Греховоду, и памятная вещь тоже.</summary>
        public void Take()
        {
            var bag = PlayerInventory.Instance;
            if (bag != null)
            {
                foreach (var item in _items) bag.AddItem(item);
                if (_memento != null) bag.AddItem(_memento);
                if (_gold > 0) bag.AddGold(_gold);
            }
            Log(_memento != null
                ? Loc.F("Вы подняли мешок {0}. «{1}» — с вами.", Loc.Name(_owner), Loc.T(_memento.Name))
                : Loc.F("Вы подняли мешок {0}.", Loc.Name(_owner)));
            Destroy(gameObject);
        }

        // ---------------------------------------------------------- свои

        void Update()
        {
            if (_decided || Time.time - _since < WaitForHero) return;
            _decided = true;

            var relations = new RelationshipSystem(AOS.MemoryProcessor.Instance);
            Warrior who = null;
            float best = Near;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w == null || w.IsDead || w is SinbinderPlayer || w.Team != Team.Player || w.Soul == null) continue;
                float d = CampFocus.GroundDistance(w.transform.position, transform.position);
                if (d < best) { best = d; who = w; }
            }
            if (who == null) return;

            float feel = relations.GetRelationship(who.Id, _ownerId);
            bool brother = (who.DisplayName.Contains("Гурт") && _owner.Contains("Хорь"))
                        || (who.DisplayName.Contains("Хорь") && _owner.Contains("Гурт"));

            if (brother || feel > 20f)
            {
                // Несёт Греховоду — всё, кроме памятной: её свои не трогают, она ждёт его.
                var bag = PlayerInventory.Instance;
                if (bag != null)
                {
                    foreach (var item in _items) bag.AddItem(item);
                    if (_gold > 0) bag.AddGold(_gold);
                }
                _items.Clear(); _gold = 0;
                Log(Loc.F("{0} несёт вам вещи {1}.", Loc.Name(who.DisplayName), Loc.Name(_owner)));
            }
            else if (who.Soul.Sin == SinType.Greed && feel < 0f)
            {
                who.Pocket(_gold);
                foreach (var item in _items) who.Give(item);
                _items.Clear(); _gold = 0;
                Log(Loc.F("{0} обобрал {1}: не ладили.", Loc.Name(who.DisplayName), Loc.Name(_owner)));
            }
            else if (who.Soul.Moral == MoralType.Pious)
            {
                Log(Loc.F("{0} не тронул мешок {1}: у мёртвых не берут.", Loc.Name(who.DisplayName), Loc.Name(_owner)));
            }

            // Пусто и без памятной — мешка больше нет; памятная лежит, пока Греховод не поднимет.
            if (_items.Count == 0 && _gold == 0 && _memento == null) Destroy(gameObject);
        }

        private static void Log(string line) => Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
    }
}
