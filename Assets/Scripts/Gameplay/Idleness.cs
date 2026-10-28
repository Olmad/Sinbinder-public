// Assets/Scripts/Gameplay/Idleness.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Армия без дела — не копится вечно («Перетягивание»; автор, 28.10: «можно ли двум игрокам набрать
    /// воинов и просто ждать волны — бесконечно собирая армию?» — «да», три защиты). Верный, у кого больше
    /// минуты нет ни приказа, ни врага рядом, остывает — по греху:
    ///
    /// * унылому без дела хорошо — не остывает;
    /// * гневному невмоготу — остывает втрое быстрее, просит дела;
    /// * жадный хочет монету в минуту из кошеля Греховода; нет монеты — остывает сильно;
    /// * прочие — понемногу.
    ///
    /// Держать большую армию можно, только кормя её делом и монетой: экспедициями и боем.
    /// </summary>
    public class Idleness : MonoBehaviour
    {
        private const float Tick = 10f;
        private const float Bored = 60f;
        private const float PayEvery = 60f;

        private readonly Dictionary<string, float> _busyAt = new();
        private readonly Dictionary<string, float> _paidAt = new();
        private float _next;

        void Update()
        {
            if (TugMatch.Over || Time.time < _next) return;
            _next = Time.time + Tick;
            var manager = CombatManager.Instance;

            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w == null || w.IsDead || w.Team != Team.Neutral || w.Soul == null || RivalLord.LordOf(w) != null) continue;
                string lord = Allegiance.Dearest(w.Id);
                if (lord == null || Allegiance.Of(w.Id, w.Loyalty, lord) < Hostility.Follows) continue;

                bool busy = w.HasCommand || (manager != null && manager.GetClosestEnemy(w.transform.position, 12f, w.gameObject) != null);
                if (busy || !_busyAt.ContainsKey(w.Id)) { _busyAt[w.Id] = Time.time; if (busy) continue; }
                if (Time.time - _busyAt[w.Id] < Bored) continue;

                switch (w.Soul.Sin)
                {
                    case SinType.Sloth:
                        break;
                    case SinType.Greed:
                        if (_paidAt.TryGetValue(w.Id, out var paid) && Time.time - paid < PayEvery) break;
                        if (Favor.Spend(lord, 1)) _paidAt[w.Id] = Time.time;
                        else Favor.Nudge(w, lord, -3f, Loc.T("Без платы не стою, владыка."));
                        break;
                    case SinType.Wrath:
                        Favor.Nudge(w, lord, -1.5f, Loc.T("Скучно, владыка. Дайте дело."));
                        break;
                    default:
                        Favor.Nudge(w, lord, -0.5f, null);
                        break;
                }
            }
        }
    }
}
