// Assets/Scripts/Gameplay/Champions.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Титулованные и легенды «Перетягивания» (54-TUG.md, «D: титулованные и легенды — места»; автор:
    /// «титулованные воины и легенды — в равных местах относительно игроков, честно для всех сторон»).
    ///
    /// * <b>Титулованный</b> — по одному на сторону: от каждого склепа к середине карты, повёрнуто на одинаковый
    ///   угол и на одинаковом расстоянии. На двоих это выходит зеркалом через середину, на четверых — поворотом:
    ///   у каждой стороны свой, и до него всем одинаково идти. Душа старше и сильнее, титул — с рождения.
    /// * <b>Легенда</b> — одна, в середине карты: до неё всем одинаково, за неё спорят.
    ///
    /// Ничьи, как жители: верность к ним добывают тем же (Favor), только гордые — не всякому. Без жребия: кто
    /// это будет — от номера партии.
    /// </summary>
    public class Champions : MonoBehaviour
    {
        private const float Reach = 46f;      // от склепа до титулованного
        private const float Turn = 32f;       // поворот от направления на середину, градусы

        private static readonly string[] Titles =
        {
            Loc.N("Ломатель Щитов"), Loc.N("Последний с Заставы"), Loc.N("Ночной Волк"), Loc.N("Сжёгший Знамя"),
            Loc.N("Неспящий"), Loc.N("Тот, кто вернулся"), Loc.N("Кровник Ордена"), Loc.N("Бродячий Клинок"),
        };

        private static readonly string[] Legends =
        {
            Loc.N("Безымянный Князь"), Loc.N("Старая Ведьма Болот"), Loc.N("Первый Греховод"), Loc.N("Мать Воронов"),
        };

        private MapData _map;
        public readonly List<Warrior> Titled = new();
        public Warrior Legend { get; private set; }

        public void Configure(MapData map) => _map = map;

        // После выпечки навмеша: встают на него.
        void Start()
        {
            if (_map == null || _map.Crypts.Count == 0) return;
            var home = gameObject.AddComponent<Residents>();
            home.Configure("village", 6f, 0, 0);
            int m = Mathf.Abs(MapLoader.Match);
            var centre = Vector3.zero;

            for (int i = 0; i < _map.Crypts.Count; i++)
            {
                var c = new Vector3(_map.Crypts[i].X, 0f, _map.Crypts[i].Z);
                var to = centre - c;
                to.y = 0f;
                if (to.sqrMagnitude < 1f) to = Vector3.forward;
                var at = c + Quaternion.Euler(0f, Turn, 0f) * to.normalized * Mathf.Min(Reach, to.magnitude * 0.6f);
                string title = Loc.T(Titles[(m + i) % Titles.Length]);
                var w = home.Champion(m * 101 + i * 13 + 7, at, 88f, 3, title);
                if (w == null) continue;
                // Дар — один на всех титулованных партии: у каждой стороны свой человек, но равный.
                Gift.Give(w, (Gift.Kind)(m % Gift.Titled));
                Titled.Add(w);
                Camp(at, i);
                Debug.Log($"[ТИТУЛ] сторона {i + 1}: {w.DisplayName} «{title}», дар — {w.GetComponent<Gift>()?.Word} у ({at.x:0}, {at.z:0}), до склепа {CampFocus.GroundDistance(at, c):0} м");
            }

            // Легенда — в середине, если там не склеп.
            bool free = true;
            foreach (var c in _map.Crypts) if (CampFocus.GroundDistance(new Vector3(c.X, 0f, c.Z), centre) < 30f) free = false;
            if (free)
            {
                string title = Loc.T(Legends[m % Legends.Length]);
                Legend = home.Champion(m * 211 + 3, centre, 110f, 4, title);
                if (Legend != null)
                {
                    Gift.Give(Legend, Gift.Kind.Legend);
                    Legend.transform.localScale *= 1.12f;
                    Camp(centre, 9);
                    Debug.Log($"[ТИТУЛ] легенда: {Legend.DisplayName} «{title}» в середине");
                }
            }
        }

        void OnEnable() => Invoke(nameof(Announce), 20f);

        /// <summary>Журнал: кто титулованные и чем сильны — игрок знает, за кем идти.</summary>
        private void Announce()
        {
            var log = Object.FindFirstObjectByType<UI.BattleLogUI>();
            if (log == null) return;
            if (Titled.Count > 0 && Titled[0] != null && Titled[0].TryGetComponent<Gift>(out var g))
                log.Write(Loc.F("Говорят, у каждого склепа объявился свой {0}: {1}.", g.Word, g.What));
            if (Legend != null)
                log.Write(Loc.T("И ещё говорят: в середине земли сидит легенда. Кому она верна — к тому тянутся и другие."));
        }

        /// <summary>Свой костёр у титулованного: видно, где он, и за чем идти.</summary>
        private void Camp(Vector3 at, int salt)
        {
            var p = MapLoader.Prefab("Campfire");
            if (p == null) return;
            var go = Instantiate(p, transform);
            go.transform.SetPositionAndRotation(at + Quaternion.Euler(0f, salt * 47f, 0f) * Vector3.forward * 1.8f, p.transform.rotation);
            go.transform.localScale = p.transform.localScale;
        }
    }
}
