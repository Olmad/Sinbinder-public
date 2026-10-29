// Assets/Scripts/Gameplay/IzbaInterior.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Изба изнутри (автор, 29.10: «в избу нужна возможность входить, а значит и внутренний интерьер
    /// (интерактивный)… разнообразный»; «может, в некоторых пусть спавнится одна банка?»). Изба — две модели
    /// (village.py: IzbaWalls, IzbaRoof): Греховод внутри — крыша гаснет, как у палаток, и сверху видно убранство.
    ///
    /// Убранство — своё в каждой избе, по номеру избы, без жребия: печь — всегда (в углу у двери или у задней
    /// стены, зеркально), и три–четыре вещи из семи на своих местах — стол с лавками, полати, полка с крынками,
    /// прялка, колыбель, сундук, бочка. В каждой третьей — банка для душ на полке или на столе.
    /// </summary>
    public class IzbaInterior : MonoBehaviour
    {
        private const float W = 5.0f, D = 4.0f;   // сруб изнутри — village.py: izba
        private readonly List<Renderer> _roof = new();
        private int _stamp;

        public void Configure(int stamp, GameObject roof)
        {
            _stamp = Mathf.Abs(stamp);
            if (roof != null) _roof.AddRange(roof.GetComponentsInChildren<Renderer>());
            Furnish();
        }

        void Update()
        {
            if (!SinbinderPlayer.Exists || _roof.Count == 0) return;
            var local = transform.InverseTransformPoint(SinbinderPlayer.Where);
            bool inside = Mathf.Abs(local.x) < W * 0.5f && Mathf.Abs(local.z) < D * 0.5f + 0.3f;
            foreach (var r in _roof) if (r != null && r.enabled == inside) r.enabled = !inside;
        }

        /// <summary>Расстановка: оси избы — x вдоль длины, z к двери (дверь — +z).</summary>
        private void Furnish()
        {
            float m = _stamp % 2 == 0 ? 1f : -1f;   // зеркально — у половины изб печь справа
            Put("Pechka", new Vector3(-1.45f * m, 0.33f, -0.95f), 180f, IzbaThing.Kind.Stove);

            // Места вдоль стен — каждой вещи своё; какие вещи — от номера избы.
            var pool = new (string prop, Vector3 at, float yaw, IzbaThing.Kind kind)[]
            {
                ("IzbaTable", new Vector3(0.9f * m, 0.33f, 0.2f), 90f, IzbaThing.Kind.Table),
                ("IzbaBed", new Vector3(1.4f * m, 0.33f, -1.35f), 0f, IzbaThing.Kind.Bed),
                ("PotShelf", new Vector3(2.25f * m, 0.33f, 0.9f), 90f * m, IzbaThing.Kind.Pots),
                ("Spinner", new Vector3(-0.2f * m, 0.33f, -1.6f), 0f, IzbaThing.Kind.Spinner),
                ("Cradle", new Vector3(0.0f, 0.33f, -0.6f), 0f, IzbaThing.Kind.Cradle),
                ("Chest", new Vector3(-1.9f * m, 0.33f, 1.4f), 90f * m, IzbaThing.Kind.Chest),
                ("Barrel", new Vector3(1.95f * m, 0.33f, 1.55f), 0f, IzbaThing.Kind.None),
            };
            int count = 3 + _stamp / 7 % 2;
            int start = _stamp / 3 % pool.Length;
            var used = new List<Vector3>();
            for (int k = 0, i = start; k < count; k++, i = (i + 2 + _stamp % 3) % pool.Length)
            {
                var (prop, at, yaw, kind) = pool[i];
                bool clash = false;
                foreach (var u in used) if ((u - at).sqrMagnitude < 1.2f) clash = true;
                if (clash) continue;
                used.Add(at);
                Put(prop, at, yaw, kind);
            }

            // Банка для душ — в каждой третьей избе (автор: «в некоторых могут спавниться одна банка»).
            if (_stamp % 3 == 0) Put("SoulJar", new Vector3(0.9f * m, 1.12f, 0.2f), 0f, IzbaThing.Kind.Jar);
        }

        private void Put(string prop, Vector3 local, float yaw, IzbaThing.Kind kind)
        {
            var p = MapLoader.Prefab(prop) ?? Resources.Load<GameObject>("Props/" + prop);
            if (p == null) return;
            var go = Instantiate(p, transform);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * p.transform.rotation;
            go.transform.localScale = p.transform.localScale;
            if (kind != IzbaThing.Kind.None) go.AddComponent<IzbaThing>().Configure(kind, _stamp);
        }
    }

    /// <summary>
    /// Вещь в избе под F: печь — погреться, стол — хлеба отломить, крынки — заглянуть (в иной — монета, один раз),
    /// сундук — открыть (раз), банка — в сумку, полати, прялка, колыбель — слово. Что внутри — от номера избы.
    /// </summary>
    public class IzbaThing : MonoBehaviour
    {
        public enum Kind { None, Stove, Table, Bed, Pots, Spinner, Cradle, Chest, Jar }

        private static readonly List<IzbaThing> Live = new();
        private const float Reach = 1.8f;
        private Kind _kind;
        private int _stamp;
        private bool _used;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Live.Clear();

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        public void Configure(Kind kind, int stamp) { _kind = kind; _stamp = stamp; }

        public static IEnumerable<IzbaThing> InReach()
        {
            if (!SinbinderPlayer.Exists) yield break;
            foreach (var t in Live)
                if (t != null && CampFocus.GroundDistance(SinbinderPlayer.Where, t.transform.position) <= Reach) yield return t;
        }

        public string Prompt => _kind switch
        {
            Kind.Stove => Loc.T("F — погреться у печи"),
            Kind.Table => Loc.T("F — отломить хлеба"),
            Kind.Bed => Loc.T("F — потрогать тюфяк"),
            Kind.Pots => _used ? Loc.T("F — крынки (пусто)") : Loc.T("F — заглянуть в крынки"),
            Kind.Spinner => Loc.T("F — тронуть кудель"),
            Kind.Cradle => Loc.T("F — качнуть колыбель"),
            Kind.Chest => _used ? Loc.T("F — сундук (пуст)") : Loc.T("F — открыть сундук"),
            Kind.Jar => Loc.T("F — взять банку для душ"),
            _ => "",
        };

        public void Use()
        {
            var hero = SinbinderPlayer.Instance;
            string line;
            switch (_kind)
            {
                case Kind.Stove: line = Loc.T("Тёплая ещё. Кто-то топил утром."); break;
                case Kind.Table: line = Loc.T("Хлеб чёрствый, но хлеб."); break;
                case Kind.Bed: line = Loc.T("Ещё хранит чужое тепло."); break;
                case Kind.Spinner: line = Loc.T("Кудель брошена на середине."); break;
                case Kind.Cradle: line = Loc.T("Пустая. Давно пустая."); break;
                case Kind.Pots:
                    if (!_used && _stamp % 4 == 1 && Lords.Active) { Favor.AddCoins(Lords.Player, 2); line = Loc.T("В крынке — две монеты. Спрятали от Ордена."); }
                    else line = _used ? Loc.T("Пусто.") : Loc.T("Мука да прошлогодние грибы.");
                    _used = true;
                    break;
                case Kind.Chest:
                    if (!_used && _stamp % 2 == 0 && Lords.Active) { Favor.AddCoins(Lords.Player, 3); line = Loc.T("На дне — три монеты, завёрнутые в тряпицу."); }
                    else line = _used ? Loc.T("Пуст.") : Loc.T("Тряпьё и старые лапти.");
                    _used = true;
                    break;
                case Kind.Jar:
                    if (Satchel.AddJar()) { line = Loc.T("Банка для душ. Целая."); Destroy(gameObject); }
                    else line = Loc.T("Сумка полна — банку некуда.");
                    break;
                default: return;
            }
            if (hero != null) UI.SpeechBubbles.Say(hero, line);
        }
    }
}
