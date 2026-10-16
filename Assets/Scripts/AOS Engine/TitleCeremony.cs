// Assets/Scripts/AOS Engine/TitleCeremony.cs
// Перевод: текст через Loc
using System.Collections;
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    public static class TitleCeremony
    {
        public static void Start(Warrior warrior, string title, bool isLegendary)
        {
            // Враг получает имя по тем же правилам, что свои (автор, 16 октября:
            // «пусть получают… игрок заметит, но не будет вдаваться в подробности,
            // потому что бой, но при этом мельком почувствует глубину»).
            // Мельком — значит без церемонии: она ставит мир на паузу, наезжает
            // камерой, и кричит в ней «Отряд». Над врагом это остановило бы бой
            // ради чужого праздника.
            if (warrior != null && warrior.Team == Team.Enemy)
            {
                Glimpse(warrior, title);
                return;
            }

            var ceremony = Object.FindFirstObjectByType<TitleCeremonyBehaviour>();
            if (ceremony == null)
            {
                Debug.LogWarning("[TITLE] TitleCeremonyBehaviour не найден на сцене!");
                return;
            }
            ceremony.Enqueue(warrior, title, isLegendary);
        }

        /// <summary>Как далеко соратник слышит, что его товарищ заслужил имя, метров.</summary>
        private const float Earshot = 12f;

        /// <summary>
        /// Имя врага — так, как его заметят посреди боя: строка в журнале,
        /// а над головой соратника — крик, тот же, что кричит отряд своим
        /// («Убийца Мертвецов! Готфрид!»). Кричать некому — молчит: сам себе
        /// имени не кричат. Врага в тумане не видно, и о нём ничего не пишется —
        /// имя он всё равно носит, и душа его запомнит.
        /// </summary>
        private static void Glimpse(Warrior warrior, string title)
        {
            if (FogOfWar.Hides(warrior)) return;

            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(
                Loc.F("Враг заслужил имя: {0}.", Loc.Name(warrior.ShownName)));

            Warrior near = null;
            float best = Earshot * Earshot;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
            {
                if (w == null || w == warrior || w.IsDead || w.Team != warrior.Team) continue;
                float d = (w.transform.position - warrior.transform.position).sqrMagnitude;
                if (d < best) { best = d; near = w; }
            }

            if (near != null) UI.SpeechBubbles.Say(near, TitleWords.Shout(warrior, title, false));
        }
    }
}
