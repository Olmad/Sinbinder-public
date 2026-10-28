// Assets/Scripts/Gameplay/Favor.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Чем расположить ничьего — «Перетягивание», 54-TUG.md (автор, 28.10: «как поднять отношения
    /// с воинами, чтобы поднять шанс послушания? это тоже должна быть интересная механика»;
    /// таблица по грехам — «да, но смотря как это будет механически работать»).
    ///
    /// Верность к Греховоду (<see cref="Allegiance"/>) двигают поступки, и каждый грех ценит своё:
    ///
    /// * **приказ по душе** — гневному в бой, унылому недалеко, гордому — ему одному, первым;
    /// * **спас** — Греховод или его человек убил того, кто бил ничьего;
    /// * **победа под твоим началом** — убил врага, исполняя твой приказ: гордому — слава;
    /// * **подарок** — монета из кошеля партии (<see cref="Purse"/>): жадному дороже всех; завистливый
    ///   рядом, кому не дали, — обижен. Второй подарок тому же стоит вдвое меньше первого.
    ///
    /// Чисел игроку нет: заметное изменение воин говорит вслух, а как он к тебе — словом
    /// в подсказке при наведении (<see cref="Attitude"/>). Кошель — число: им Греховод владеет.
    /// </summary>
    public static class Favor
    {
        /// <summary>Монеты Греховода игрока в этой партии — свои, не лагерные.</summary>
        public static int Purse => PurseOf(Lords.Player);

        /// <summary>Кошель Греховода партии; у каждого — свой, в начале одинаковый.</summary>
        public static int PurseOf(string lord)
            => string.IsNullOrEmpty(lord) ? 0 : Purses.TryGetValue(lord, out var n) ? n : _start;

        private static readonly Dictionary<string, int> Purses = new();

        /// <summary>Добавить монет Греховоду (поднял кошель павшего).</summary>
        public static void AddCoins(string lord, int coins)
        {
            if (string.IsNullOrEmpty(lord) || coins <= 0) return;
            Purses[lord] = PurseOf(lord) + coins;
        }

        /// <summary>Отдать весь кошель Греховода (пал — кошель на землю). Возвращает, сколько было.</summary>
        public static int EmptyPurse(string lord)
        {
            int n = PurseOf(lord);
            if (!string.IsNullOrEmpty(lord)) Purses[lord] = 0;
            return n;
        }
        private static int _start;

        public const int StartPurse = 20;
        private const float GiftReach = 2.5f;
        private const float Witness = 8f;
        private const float Saved = 12f;

        private static readonly Dictionary<string, int> Gifts = new();
        private static readonly Dictionary<string, float> GiftedAt = new();
        private static readonly Dictionary<string, float> SaidAt = new();
        private static readonly Dictionary<(string lord, float at), int> Groups = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Begin(0);

        public static void Begin(int purse)
        {
            _start = purse;
            Purses.Clear();
            Gifts.Clear();
            GiftedAt.Clear();
            SaidAt.Clear();
            Groups.Clear();
        }

        // ---------------------------------------------------------- приказ

        /// <summary>Приказ отдан: сколько человек получили его разом — гордому важно, один ли он.</summary>
        public static void Issued(string lord, float at)
        {
            if (string.IsNullOrEmpty(lord)) return;
            Groups.TryGetValue((lord, at), out var n);
            Groups[(lord, at)] = n + 1;
        }

        /// <summary>Послушал: привычка слушать — всем; по душе ли приказ — по греху.</summary>
        public static void Obeyed(Warrior w, PendingCommand cmd)
        {
            if (w == null || w.Soul == null || string.IsNullOrEmpty(cmd.Lord)) return;
            float d = Hostility.ObeyHabit;
            string line = null;
            Groups.TryGetValue((cmd.Lord, cmd.IssuedAt), out var group);
            float far = CampFocus.GroundDistance(w.transform.position, cmd.Point);
            bool fight = cmd.Kind == CommandKind.Attack || cmd.Kind == CommandKind.AttackMove;
            bool back = cmd.Kind == CommandKind.FallBack || cmd.Kind == CommandKind.Hold;

            switch (w.Soul.Sin)
            {
                case SinType.Wrath when fight: d += 4f; line = Loc.T("Наконец-то дело!"); break;
                case SinType.Wrath when back: d -= 3f; line = Loc.T("Опять назад?"); break;
                case SinType.Sloth when far > 40f: d -= 4f; line = Loc.T("Опять идти… и далеко."); break;
                case SinType.Sloth when far < 15f: d += 2f; break;
                case SinType.Pride when group == 1: d += 4f; line = Loc.T("Я поведу."); break;
                case SinType.Pride when group >= 6: d -= 2f; line = Loc.T("Я вам не один из толпы, владыка."); break;
            }
            Change(w, cmd.Lord, d, line);
        }

        // ---------------------------------------------------------- бой

        /// <summary>Убит: кто его бил — спасён; убил по приказу — его победа, гордому слава.</summary>
        public static void Killed(Warrior dead, Warrior killer)
        {
            if (!Lords.Active || dead == null || killer == null) return;
            string lord = Lords.Of(killer) ?? (killer.HasCommand ? killer.Command.Lord : null);
            if (string.IsNullOrEmpty(lord)) return;

            // Спас: ничьи, кого бил убитый, — обязаны.
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w == null || w.IsDead || w.Team != Team.Neutral || w == killer) continue;
                if (!Hostility.StruckRecently(w, dead)) continue;
                float d = w.Soul != null && w.Soul.Sin == SinType.Wrath ? Saved * 0.6f
                        : w.Soul != null && w.Soul.Sin == SinType.Pride ? Saved * 0.5f : Saved;
                Change(w, lord, d, w.Soul != null && w.Soul.Sin == SinType.Wrath
                    ? Loc.T("Он был мой. Но спасибо, владыка.")
                    : Loc.T("Вы меня вытащили, владыка. Запомню."));
            }

            // Победа под началом Греховода.
            if (killer.Team != Team.Neutral || killer.Soul == null) return;
            switch (killer.Soul.Sin)
            {
                case SinType.Pride: Change(killer, lord, 6f, Loc.T("Видели, владыка? Это был я.")); Envy(killer, lord, -2f); break;
                case SinType.Wrath: Change(killer, lord, 3f, null); break;
                default: Change(killer, lord, 1f, null); break;
            }
        }

        // ---------------------------------------------------------- подарок

        /// <summary>Ничьи, кому можно дать монету: рядом, живые, монеты есть.</summary>
        public static IEnumerable<Warrior> Takers()
        {
            if (!Lords.Active || Purse <= 0 || !SinbinderPlayer.Exists) yield break;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                if (w != null && !w.IsDead && w.Team == Team.Neutral
                    && CampFocus.GroundDistance(SinbinderPlayer.Where, w.transform.position) <= GiftReach)
                    yield return w;
        }

        public static string GiftPrompt(Warrior w)
            => Loc.F("F — дать монету: {0} (в кошеле {1})", Loc.Name(w.DisplayName), Purse);

        /// <summary>Дать монету от игрока.</summary>
        public static void Give(Warrior w) => Give(w, Lords.Player);

        /// <summary>Дать монету от Греховода. Каждый грех берёт по-своему; завистливые рядом, кому не дали, — обижены.</summary>
        public static void Give(Warrior w, string lord)
        {
            if (w == null || w.Soul == null || !Lords.Active || PurseOf(lord) <= 0) return;
            Purses[lord] = PurseOf(lord) - 1;
            Gifts.TryGetValue(w.Id, out var n);
            Gifts[w.Id] = n + 1;
            GiftedAt[w.Id] = Time.time;
            float worth = 1f / (1 << Mathf.Min(n, 4));

            var (gain, line) = w.Soul.Sin switch
            {
                SinType.Greed => (12f, Loc.T("Вот это разговор, владыка.")),
                SinType.Envy => (6f, Loc.T("Наконец и мне.")),
                SinType.Lust => (6f, Loc.T("Это мне? Ох, владыка.")),
                SinType.Sloth => (4f, Loc.T("Спасибо… Можно я присяду?")),
                SinType.Gluttony => (3f, Loc.T("Монета — хорошо. Хлеб был бы лучше.")),
                SinType.Pride => (1f, Loc.T("Я не нищий, владыка. Но возьму.")),
                _ => (2f, Loc.T("Хм. Ладно.")),
            };
            Change(w, lord, gain * worth, line, force: true);
            Envy(w, lord, -3f);
        }

        /// <summary>Завистливые рядом с тем, кого выделили, и кому не дали за минуту, — обижены.</summary>
        private static void Envy(Warrior favoured, string lord, float hurt)
        {
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w == null || w == favoured || w.IsDead || w.Team != Team.Neutral || w.Soul == null) continue;
                if (w.Soul.Sin != SinType.Envy) continue;
                if (CampFocus.GroundDistance(w.transform.position, favoured.transform.position) > Witness) continue;
                if (GiftedAt.TryGetValue(w.Id, out var at) && Time.time - at < 60f) continue;
                Change(w, lord, hurt, Loc.F("{0} — а мне?", Loc.Name(favoured.DisplayName)));
            }
        }

        // ---------------------------------------------------------- общее

        private static void Change(Warrior w, string lord, float delta, string line, bool force = false)
        {
            if (w == null || string.IsNullOrEmpty(lord) || Mathf.Approximately(delta, 0f)) return;
            Allegiance.Change(w.Id, w.Loyalty, lord, delta);
            if (string.IsNullOrEmpty(line) || lord != Lords.Player) return;
            // Вслух — не чаще раза в шесть секунд: иначе в бою лагерь заговорит хором.
            if (!force && SaidAt.TryGetValue(w.Id, out var at) && Time.time - at < 6f) return;
            SaidAt[w.Id] = Time.time;
            UI.SpeechBubbles.Say(w, line, 3.5f);
        }

        /// <summary>Как ничей относится к игроку — словом, без числа (подсказка при наведении).</summary>
        public static string Attitude(Warrior w)
        {
            if (w == null || !Lords.Active) return "";
            float v = Allegiance.Of(w.Id, w.Loyalty, Lords.Player);
            if (v <= Hostility.Despised) return Loc.T("К вам: враждебен");
            if (v < 25f) return Loc.T("К вам: сторонится");
            if (v < 40f) return Loc.T("К вам: присматривается");
            if (v < 55f) return Loc.T("К вам: прислушивается");
            if (v < 75f) return Loc.T("К вам: готов слушать");
            return Loc.T("К вам: предан");
        }
    }
}
