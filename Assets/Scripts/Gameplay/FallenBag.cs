// Assets/Scripts/Gameplay/FallenBag.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        private string _homeScene = "";
        private FallenBagRetention _retention = new FallenBagRetention();
        private bool _onMap = true, _retired;
        private TextMesh _visibleLabel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Live.Clear();

        void OnEnable()
        {
            if (!Live.Contains(this)) Live.Add(this);
            SceneManager.activeSceneChanged += MapChanged;
        }

        void OnDisable()
        {
            SceneManager.activeSceneChanged -= MapChanged;
            Live.Remove(this);
        }

        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= MapChanged;
            Live.Remove(this);
        }

        private void MapChanged(Scene previous, Scene next) => SetMap(next.name);

        private void SetMap(string name)
        {
            if (_retired || string.IsNullOrEmpty(_homeScene)) return;
            _retention.ChangeMap(name == _homeScene, Time.unscaledTime);
            if (_retention.Expired) { Retire(); return; }
            _onMap = _retention.OnHomeMap;
            foreach (var r in GetComponentsInChildren<Renderer>())
                r.enabled = _onMap;
        }

        private void Retire()
        {
            if (_retired) return;
            _retired = true;
            _onMap = false;
            foreach (var r in GetComponentsInChildren<Renderer>())
                r.enabled = false;
            Live.Remove(this); // no interaction while Unity's Destroy is deferred
            Destroy(gameObject);
        }

        private static FallenBag MakePhysical(string owner, Vector3 position, string scene)
        {
            // A visible sack, not just an invisible loot record. No collider:
            // F selection is handled by GearPanel and still respects reach.
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = Loc.F("Мешок {0}", Loc.Name(owner));
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.48f, 0.32f, 0.40f);
            go.GetComponent<Renderer>().material.color = new Color(0.38f, 0.27f, 0.16f);

            var knot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            knot.name = "Горловина мешка";
            Object.Destroy(knot.GetComponent<Collider>());
            knot.transform.SetParent(go.transform, false);
            knot.transform.localPosition = new Vector3(0f, 0.64f, 0f);
            knot.transform.localScale = new Vector3(0.55f, 0.38f, 0.55f);
            knot.GetComponent<Renderer>().material.color = new Color(0.48f, 0.36f, 0.20f);

            var marker = new GameObject("Надпись над мешком");
            marker.transform.SetParent(go.transform, false);
            marker.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            // Counteract the scaled sack root so the label remains legible.
            marker.transform.localScale = new Vector3(
                1f / go.transform.localScale.x,
                1f / go.transform.localScale.y,
                1f / go.transform.localScale.z);
            var label = marker.AddComponent<TextMesh>();
            label.text = Loc.T("Мешок");
            label.fontSize = 40;
            label.characterSize = 0.12f;
            label.color = new Color(1f, 0.91f, 0.68f);
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;

            var bag = go.AddComponent<FallenBag>();
            bag._owner = owner;
            bag._homeScene = scene;
            bag._visibleLabel = label;
            Object.DontDestroyOnLoad(go);
            return bag;
        }

        /// <summary>Discard the last game's bags when a genuine new game starts.</summary>
        public static void Forget()
        {
            foreach (var b in Live.ToArray())
                if (b != null) b.Retire();
            Live.Clear();
        }

        /// <summary>
        /// Snapshot exact uncollected contents across maps and save/load.
        /// Time expires only while the player is *away* from the map.
        /// </summary>
        public static List<SavedFallenBag> Snapshot()
        {
            var saved = new List<SavedFallenBag>();
            foreach (var b in Live)
            {
                if (b == null || b._retired || b._retention.Expired) continue;
                float left = b._retention.Remaining(Time.unscaledTime);
                if (!b._retention.OnHomeMap && left <= 0f) continue;
                if (b._items.Count == 0 && b._memento == null && b._gold == 0)
                    continue;
                saved.Add(new SavedFallenBag
                {
                    Scene = b._homeScene,
                    Position = b.transform.position,
                    Owner = b._owner,
                    OwnerId = b._ownerId,
                    Items = new List<InventoryItem>(b._items),
                    Memento = b._memento,
                    Gold = b._gold,
                    Decided = b._decided,
                    Away = !b._retention.OnHomeMap,
                    RemainingAway = left
                });
            }
            return saved;
        }

        /// <summary>Restore the physical bag, not merely its bookkeeping.</summary>
        public static void Restore(List<SavedFallenBag> saved, string targetScene)
        {
            Forget();
            if (saved == null) return; // older save file has no bag field
            foreach (var record in saved)
            {
                if (record == null || string.IsNullOrEmpty(record.Scene) ||
                    record.Items == null && record.Gold <= 0 && record.Memento == null)
                    continue;
                if (record.Away && record.RemainingAway <= 0f) continue;
                var b = MakePhysical(record.Owner ?? "", record.Position, record.Scene);
                b._ownerId = record.OwnerId ?? "";
                b._gold = Mathf.Max(0, record.Gold);
                b._memento = record.Memento;
                b._decided = record.Decided;
                b._since = Time.time;
                if (record.Items != null) b._items.AddRange(record.Items);
                b._retention = FallenBagRetention.FromSave(!record.Away,
                    record.RemainingAway, Time.unscaledTime);
                b.SetMap(targetScene);
            }
        }

        // ---------------------------------------------------------- появление

        public static void CreateFor(Damageable killed, HarvestableBody bodyLoot)
        {
            var w = killed != null ? killed.Warrior : null;
            if (w == null || w is SinbinderPlayer) return;

            // Owned gear and actual pocket coins are distinct from the
            // generated harvestable loot on a corpse. Never duplicate gold.
            bool allied = w.Team == Team.Player;
            InventoryItem keepsake = allied ? Memento(w.DisplayName) : null;
            // Generated treasure belongs to the existing corpse system.
            // If automatic distribution is enabled, leave it there for
            // LootChain. Otherwise transfer that value once to the bag.
            bool collectCorpseLoot = !allied && !LootChain.Enabled && bodyLoot != null;
            int extraGold = collectCorpseLoot ? bodyLoot.GoldValue : 0;
            string trophy = collectCorpseLoot && bodyLoot.HasEquipment
                ? bodyLoot.EquipmentName : null;
            if (w.Carried.Count == 0 && w.PocketGold <= 0 && keepsake == null &&
                extraGold <= 0 && string.IsNullOrEmpty(trophy))
                return;

            bool ownedEquipment = w.Carried.Count > 0;
            Vector3 where = w.transform.position +
                w.transform.right * 0.6f + Vector3.up * 0.16f;
            var bag = MakePhysical(w.DisplayName, where,
                SceneManager.GetActiveScene().name);
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
            bag._memento = keepsake;
            if (collectCorpseLoot)
            {
                // Both Collect methods zero the generated body payload
                // to prevent a second reward from Remains / LootChain.
                bag._gold = checked(bag._gold + bodyLoot.CollectGold());
                string acquired = bodyLoot.CollectEquipment();
                if (!string.IsNullOrEmpty(acquired) && !ownedEquipment)
                    bag._items.Add(LootChain.Trophy(acquired));
            }
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
                if (b != null && b._onMap && !b._retired &&
                    CampFocus.GroundDistance(SinbinderPlayer.Where, b.transform.position) <= Reach)
                    yield return b;
        }

        public static FallenBag Nearest()
        {
            if (!SinbinderPlayer.Exists) return null;
            foreach (var b in Live)
                if (b != null && b._onMap && !b._retired &&
                    CampFocus.GroundDistance(SinbinderPlayer.Where, b.transform.position) <= Reach)
                    return b;
            return null;
        }

        public string Prompt => Loc.F("F — поднять мешок {0}", Loc.Name(_owner));

        /// <summary>F: всё — Греховоду, и памятная вещь тоже.</summary>
        public void Take()
        {
            if (_retired || !_onMap) return;
            var inventory = PlayerInventory.Instance;
            if (inventory == null)
            {
                Log(Loc.T("Сейчас некому забрать содержимое мешка."));
                return;
            }

            // An unaccepted item stays in the physical bag; a full
            // inventory is not an excuse to delete someone's equipment.
            int received = FallenBagTransfer.Move(_items, inventory.AddItem);
            if (_memento != null && inventory.AddItem(_memento))
            {
                received++;
                _memento = null;
            }
            if (_gold > 0)
            {
                inventory.AddGold(_gold);
                _gold = 0;
            }

            if (_items.Count > 0 || _memento != null)
            {
                Log(Loc.F("Мешок {0}: места не хватило, оставшиеся вещи на земле.",
                    Loc.Name(_owner)));
                return;
            }
            Log(Loc.F("Мешок {0} собран.", Loc.Name(_owner)));
            Retire();
        }

        // ---------------------------------------------------------- свои

        void Update()
        {
            if (_retired) return;
            _retention.Tick(Time.unscaledTime);
            if (_retention.Expired) { Retire(); return; }
            if (!_onMap) return; // not visible or interactable on other maps

            // Keep the label facing both first-person and tactical cameras.
            if (_visibleLabel != null && Camera.main != null)
                _visibleLabel.transform.rotation = Camera.main.transform.rotation;

            if (_decided || Time.time - _since < WaitForHero) return;
            // A bag must not vanish in view because an NPC silently looted it.
            if (SinbinderPlayer.Exists &&
                CampFocus.GroundDistance(SinbinderPlayer.Where, transform.position) < 10f)
                return;
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
                    FallenBagTransfer.Move(_items, bag.AddItem);
                    if (_gold > 0) { bag.AddGold(_gold); _gold = 0; }
                }
                Log(Loc.F("{0} несёт вам вещи {1}.", Loc.Name(who.DisplayName), Loc.Name(_owner)));
            }
            else if (who.Soul.Sin == SinType.Greed && feel < 0f)
            {
                who.Pocket(_gold);
                _gold = 0;
                SquadRoster.SyncPocket(who);
                FallenBagTransfer.Move(_items, who.Give);
                Log(Loc.F("{0} обобрал {1}: не ладили.", Loc.Name(who.DisplayName), Loc.Name(_owner)));
            }
            else if (who.Soul.Moral == MoralType.Pious)
            {
                Log(Loc.F("{0} не тронул мешок {1}: у мёртвых не берут.", Loc.Name(who.DisplayName), Loc.Name(_owner)));
            }

            // Пусто и без памятной — мешка больше нет; памятная лежит, пока Греховод не поднимет.
            if (_items.Count == 0 && _gold == 0 && _memento == null) Retire();
        }

        private static void Log(string line) => Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
    }
}
