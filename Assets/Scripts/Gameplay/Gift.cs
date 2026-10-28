// Assets/Scripts/Gameplay/Gift.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Дар титулованного и легенды «Перетягивания» (автор, 28.10: «у титулованных и тем более легенд есть
    /// особенные навыки?»). Титул берётся из книги титулов (TitleDatabase) — имя, реплики, наряд, — а дар
    /// делает его делом:
    ///
    /// * <b>Убийца Охотников</b> — по охотникам Ордена бьёт в полтора раза сильнее;
    /// * <b>Берсерк</b> — раненый (меньше половины тела) бьёт на сорок процентов сильнее;
    /// * <b>Щит Отряда</b> — верным его Греховода рядом (5 м) удары слабее на пятую часть;
    /// * <b>Наставник</b> — верные его Греховода рядом (8 м) теплеют к нему понемногу;
    /// * <b>Каратель</b> — по Греховодам бьёт в полтора раза сильнее;
    /// * <b>Легенда</b> — крепче (удары по ней — на четверть слабее), и ничьи вокруг (15 м) тянутся к тому
    ///   Греховоду, кому она верна.
    ///
    /// Чей — тот, к кому он верен (Allegiance.Dearest, не ниже «следует»): ничейный титулованный дара своим
    /// не даёт — его ещё надо перетянуть. Цифр игроку не видно: дар назван словами.
    /// </summary>
    public class Gift : MonoBehaviour
    {
        public enum Kind { HunterSlayer, Berserk, Shield, Mentor, Punisher, Legend }

        /// <summary>Сколько даров у титулованных (без легенды): номер партии выбирает из них.</summary>
        public const int Titled = 5;

        private static readonly List<Gift> Live = new();
        private Kind _kind;
        private Warrior _self;
        private float _next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Live.Clear();

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        /// <summary>Наделить даром и титулом из книги титулов.</summary>
        public static Gift Give(Warrior w, Kind kind)
        {
            if (w == null) return null;
            var g = w.gameObject.AddComponent<Gift>();
            g._kind = kind;
            g._self = w;
            if (kind == Kind.Legend)
            {
                if (w.Reputation != null)
                {
                    w.Reputation.LegendaryUnlocked = true;
                    w.Reputation.CurrentLegendaryTitle = $"{(w.Soul != null ? w.Soul.EarnedTitle : "")} {w.DisplayName}";   // ключ: имя в записи, по-русски (как TitleManager.Bestow)
                }
            }
            else AOS.TitleManager.Bestow(w, g.Word);
            return g;
        }

        /// <summary>Титул словом (из книги титулов).</summary>
        public string Word => _kind switch
        {
            Kind.HunterSlayer => Loc.T("Убийца Охотников"),
            Kind.Berserk => Loc.T("Берсерк"),
            Kind.Shield => Loc.T("Щит Отряда"),
            Kind.Mentor => _self != null ? Grammar.Pick(_self.Gender, Loc.T("Наставник"), Loc.T("Наставница")) : Loc.T("Наставник"),
            Kind.Punisher => _self != null ? Grammar.Pick(_self.Gender, Loc.T("Каратель"), Loc.T("Карательница")) : Loc.T("Каратель"),
            _ => Loc.T("легенда"),
        };

        /// <summary>Дар словами — для журнала и подсказки.</summary>
        public string What => _kind switch
        {
            Kind.HunterSlayer => Loc.T("охотников Ордена рубит, как никто"),
            Kind.Berserk => Loc.T("раненый — страшнее, чем целый"),
            Kind.Shield => Loc.T("рядом с ним своих бьют слабее"),
            Kind.Mentor => Loc.T("рядом с ним свои крепнут в верности"),
            Kind.Punisher => Loc.T("Греховодам от него достаётся вдвойне"),
            _ => Loc.T("крепка, и к её владыке тянутся другие"),
        };

        /// <summary>Чей дар сейчас: Греховод, к кому верен; ничей — null.</summary>
        public string Lord
        {
            get
            {
                if (_self == null || _self.IsDead) return null;
                string lord = Allegiance.Dearest(_self.Id);
                return lord != null && Allegiance.Of(_self.Id, _self.Loyalty, lord) >= Hostility.Follows ? lord : null;
            }
        }

        /// <summary>Удар с поправкой на дары: того, кто бьёт, и тех, кто рядом с ударенным (Damageable.TakeDamage).</summary>
        public static float Modify(float damage, GameObject attacker, Warrior victim)
        {
            if (Live.Count == 0 || damage <= 0f) return damage;
            if (attacker != null && attacker.TryGetComponent<Gift>(out var a) && a._self != null && !a._self.IsDead)
            {
                switch (a._kind)
                {
                    case Kind.HunterSlayer:
                        if (victim != null && victim.Team == Team.Enemy) damage *= 1.5f;
                        break;
                    case Kind.Berserk:
                        var body = a.GetComponent<Damageable>();
                        if (body != null && body.HP < body.MaxHP * 0.5f) damage *= 1.4f;
                        break;
                    case Kind.Punisher:
                        if (victim != null && Lords.Of(victim) != null) damage *= 1.5f;
                        break;
                }
            }
            if (victim == null) return damage;
            if (victim.TryGetComponent<Gift>(out var v) && v._kind == Kind.Legend) damage *= 0.75f;

            // Щит рядом: свои его Греховода получают слабее.
            string mine = Allegiance.Dearest(victim.Id);
            if (mine != null)
                foreach (var g in Live)
                    if (g != null && g._kind == Kind.Shield && g._self != victim && g.Lord == mine
                        && CampFocus.GroundDistance(g.transform.position, victim.transform.position) <= 5f)
                    { damage *= 0.8f; break; }
            return damage;
        }

        // Наставник и легенда — понемногу, раз в десять секунд.
        void Update()
        {
            if (_kind != Kind.Mentor && _kind != Kind.Legend) return;
            if (TugMatch.Over || Time.time < _next) return;
            _next = Time.time + 10f;
            string lord = Lord;
            if (lord == null) return;
            float reach = _kind == Kind.Legend ? 15f : 8f;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w == null || w == _self || w.IsDead || w.Team != Team.Neutral) continue;
                if (CampFocus.GroundDistance(w.transform.position, transform.position) > reach) continue;
                if (_kind == Kind.Mentor && Allegiance.Dearest(w.Id) != lord) continue;
                Favor.Nudge(w, lord, _kind == Kind.Legend ? 0.4f : 0.6f, null);
            }
        }
    }
}
