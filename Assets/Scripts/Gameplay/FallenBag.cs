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
            if (_items.Count == 0 && _gold == 0 && _memento == null) Destroy(gameObject);
        }

        private static void Log(string line) => Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
    }
}
