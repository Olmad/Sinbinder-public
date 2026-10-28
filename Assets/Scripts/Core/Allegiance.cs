// Assets/Scripts/Core/Allegiance.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Core
{
    /// <summary>
    /// Верность воина — к каждому Греховоду отдельно (54-TUG.md, «Перетягивание каната»;
    /// автор, 27.10: «на карте все воины без стороны, они подчиняются тому, кто отдал приказ,
    /// и смысл — в хороших отношениях с ними, чтобы они слушались»).
    ///
    /// В демо Греховод один, и приказ не называет, чей он: верность там — та, что в душе
    /// (<c>SoulData.Loyalty</c>), и здесь ничего не меняется. Приказ с именем Греховода
    /// (<c>PendingCommand.Lord</c>) голосует верностью к нему: кого он расположил к себе —
    /// тот и слушается. Ничего случайного: начальная верность к незнакомому Греховоду —
    /// от души воина, дальше её двигают поступки (<see cref="Change"/>).
    /// </summary>
    public static class Allegiance
    {
        /// <summary>
        /// К незнакомому Греховоду — доля верности души: чужому верят меньше. Было 0,45 — и приказ чужого
        /// проигрывал собственной воле воина почти всегда (длинная партия 28.10: 299 отказов из 305).
        /// </summary>
        public const float StrangerShare = 0.75f;

        private static readonly Dictionary<(string warrior, string lord), float> Toward = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Toward.Clear();

        /// <summary>Новая партия — все верности с нуля.</summary>
        public static void Forget() => Toward.Clear();

        /// <summary>
        /// Верность воина к этому Греховоду; −1 — Греховод не назван (демо): решает душа.
        /// </summary>
        public static float Of(string warriorId, float soulLoyalty, string lord)
        {
            if (string.IsNullOrEmpty(lord) || string.IsNullOrEmpty(warriorId)) return -1f;
            return Toward.TryGetValue((warriorId, lord), out var v) ? v : soulLoyalty * StrangerShare;
        }

        /// <summary>Сдвинуть верность к Греховоду (поступком: заплатил, спас, бросил, ударил). 0…100.</summary>
        /// <summary>Знает ли воин этого Греховода — была ли уже между ними хоть одна сделка.</summary>
        public static bool Knows(string warriorId, string lord)
            => !string.IsNullOrEmpty(lord) && !string.IsNullOrEmpty(warriorId) && Toward.ContainsKey((warriorId, lord));

        public static float Change(string warriorId, float soulLoyalty, string lord, float delta)
        {
            if (string.IsNullOrEmpty(lord) || string.IsNullOrEmpty(warriorId)) return -1f;
            float now = Mathf.Clamp(Of(warriorId, soulLoyalty, lord) + delta, 0f, 100f);
            Toward[(warriorId, lord)] = now;
            return now;
        }

        /// <summary>К кому этот воин верней всех из названных — для «кого бить»; null — ни к кому.</summary>
        public static string Dearest(string warriorId)
        {
            string best = null;
            float top = -1f;
            foreach (var kv in Toward)
                if (kv.Key.warrior == warriorId && kv.Value > top) { top = kv.Value; best = kv.Key.lord; }
            return best;
        }
    }
}
