// Assets/Scripts/Gameplay/LordPurse.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Кошель павшего Греховода «Перетягивания» (автор, 28.10: «при гибели Греховод
    /// восстанавливается на троне через минуту, но при смерти не оставляет свою душу, только весь
    /// инвентарь»). Лежит, где он пал; поднимает любой Греховод — игрок по F, ИИ — проходя рядом:
    /// монеты — в его кошель партии (<see cref="Favor"/>).
    /// </summary>
    public class LordPurse : MonoBehaviour
    {
        private static readonly List<LordPurse> Live = new();
        private const float Reach = 2.2f;

        private int _coins;
        private string _whose = "";

        public int Coins => _coins;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Live.Clear();

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        public static IReadOnlyList<LordPurse> All => Live;

        /// <summary>Уронить кошель: мешковина у ног павшего. Пустой — не роняется.</summary>
        public static LordPurse Drop(Vector3 at, int coins, string whose)
        {
            if (coins <= 0) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = Loc.F("Кошель — {0}", whose);
            Destroy(go.GetComponent<Collider>());
            go.transform.position = at + Vector3.up * 0.14f;
            go.transform.localScale = new Vector3(0.36f, 0.26f, 0.30f);
            go.GetComponent<Renderer>().material.color = new Color(0.34f, 0.25f, 0.13f);
            var purse = go.AddComponent<LordPurse>();
            purse._coins = coins;
            purse._whose = whose;
            return purse;
        }

        /// <summary>Кошели, до которых достаёт рука Греховода игрока.</summary>
        public static IEnumerable<LordPurse> InReach()
        {
            if (!SinbinderPlayer.Exists || SinbinderPlayer.Instance.IsDead) yield break;
            foreach (var p in Live)
                if (p != null && CampFocus.GroundDistance(SinbinderPlayer.Where, p.transform.position) <= Reach)
                    yield return p;
        }

        /// <summary>Тот, кто рядом, — у ног этого тела (ИИ поднимает, проходя).</summary>
        public static LordPurse Near(Vector3 at)
        {
            foreach (var p in Live)
                if (p != null && CampFocus.GroundDistance(at, p.transform.position) <= Reach) return p;
            return null;
        }

        public string Prompt => Loc.F("F — подобрать кошель ({0}): {1} мон.", _whose, _coins);

        /// <summary>Поднять: монеты — Греховоду, кошель исчезает.</summary>
        public void Take(string lord)
        {
            if (string.IsNullOrEmpty(lord) || _coins <= 0) return;
            Favor.AddCoins(lord, _coins);
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(Loc.F("{0} поднял кошель: {1} мон.", lord, _coins));
            _coins = 0;
            Destroy(gameObject);
        }
    }
}
