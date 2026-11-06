// Assets/Scripts/Gameplay/SoulCourtReasons.cs
// Перевод: текст через Loc
using UnityEngine;
using Sinbinder.AOS;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Почему судья голосует так (автор, 06.11: «нет пояснений, при чём тут тот или иной
    /// судья. Почему Чревоугодие проголосовало за то, чтобы уйти»). До этого судья, у кого
    /// своей строки не было, говорил «Послушай владыку» — и при чём тут Чревоугодие, не знал
    /// никто.
    ///
    /// Причина — та же, что в коде его модуля (AOS Engine/Modules): судья называет, что
    /// в душе (грех или добродетель), и как это тянет в этот миг. Голос судьи — разница двух
    /// чисел: что модуль дал приказу и что — сопернику приказа. Говорит о том, что громче:
    /// у Верности это приказ («велено»), у Чревоугодия — драка («сытому она ни к чему»).
    /// Местоимения — мужским родом, женский даёт <see cref="Grammar.For"/>; прилагательные
    /// и глаголы с родом — парами.
    /// </summary>
    public static class SoulCourtReasons
    {
        public static string Reason(string module, float obey, float rival, ActionType rivalAction,
                                    DecisionContext c, Warrior w)
        {
            if (w == null || c == null) return null;
            bool aboutOrder = Mathf.Abs(obey) > Mathf.Abs(rival);
            string s = aboutOrder ? Order(module, obey > 0f, c, w)
                                  : Pull(module, Kind(rivalAction), rival > 0f, c, w);
            return string.IsNullOrEmpty(s) ? Fallback(Kind(rivalAction), rival > 0f || obey < 0f, w)
                                           : Grammar.For(w.Gender, s);
        }

        /// <summary>
        /// Почему судья воздерживается — одной строкой, по его сути (автор, 06.11: «в обучении
        /// показать и представить недействующих судей»). Звучит в полном суде, во сне: потом
        /// короткие суды метят воздержавшихся молча, и игрок уже знает, кто они и почему молчат.
        /// </summary>
        public static string Idle(string module, Warrior w)
        {
            if (w == null) return Loc.T("Воздерживаюсь.");
            switch (module)
            {
                case "Loyalty":  return P(w, Loc.T("Воздерживаюсь: владыке он ни предан, ни чужд."),
                                             Loc.T("Воздерживаюсь: владыке она ни предана, ни чужда."));
                case "Fear":     return Grammar.For(w.Gender, Loc.T("Воздерживаюсь: его жизни этот приказ не грозит."));
                case "Memory":   return Loc.T("Воздерживаюсь: обид и долгов тут нет.");
                case "Morality": return Loc.T("Воздерживаюсь: ни добра, ни зла тут нет.");
                case "Virtue":   return Loc.T("Воздерживаюсь: добрые и грешные черты тут не спорят.");
                case "Patience": return Loc.T("Воздерживаюсь: ждать тут нечего.");
                case "Greed":    return Loc.T("Воздерживаюсь: делить тут нечего.");
                case "Pride":    return Grammar.For(w.Gender, Loc.T("Воздерживаюсь: честь его тут не задета."));
                case "Wrath":    return Sin(w, SinType.Wrath) < 0f ? Loc.T("Воздерживаюсь: гасить тут нечего.")
                                                                   : Loc.T("Воздерживаюсь: мстить тут некому.");
                case "Envy":     return Loc.T("Воздерживаюсь: сравнивать тут не с кем.");
                case "Lust":     return Loc.T("Воздерживаюсь: соблазна тут нет.");
                case "Gluttony": return Loc.T("Воздерживаюсь: о брюхе тут речи нет.");
                case "Sloth":    return Loc.T("Воздерживаюсь: труд тут невелик.");
                default:         return Loc.T("Воздерживаюсь.");
            }
        }

        // ── что в душе ──
        private static float Sin(Warrior w, SinType s) => w.Soul != null ? w.Soul.Get(s) : 0f;
        private static bool Pious(Warrior w) => w.Soul != null && w.Soul.Moral == MoralType.Pious;
        private static bool Vicious(Warrior w) => w.Soul != null && w.Soul.Moral == MoralType.Vicious;
        private static bool Hurt(DecisionContext c) => c.MaxHP > 0f && c.CurrentHP < c.MaxHP * 0.4f;
        // Пары — уже переведённые: литералы в Loc.T у места вызова, их собирает Tools/lang/catalog.py.
        private static string P(Warrior w, string he, string she) => Grammar.Pick(w.Gender, he, she);

        /// <summary>Соперник приказа — по смыслу: умения боя — та же драка.</summary>
        private static ActionType Kind(ActionType a)
        {
            switch (a)
            {
                case ActionType.PowerStrike:
                case ActionType.Berserk:
                case ActionType.DuelChallenge:
                case ActionType.CounterAttack:
                case ActionType.LastStand:
                case ActionType.TakeBait:
                    return ActionType.Attack;
                default:
                    return a;
            }
        }

        /// <summary>Голос решил сам приказ: <paramref name="likes"/> — модуль дал ему плюс.</summary>
        private static string Order(string module, bool likes, DecisionContext c, Warrior w)
        {
            switch (module)
            {
                case "Loyalty":
                    if (likes) return c.CommandVolume < 0.6f
                        ? P(w, Loc.T("Приказ еле слышен — но в нём верность, и он расслышал."), Loc.T("Приказ еле слышен — но в ней верность, и она расслышала."))
                        : Loc.T("В нём верность владыке. А владыка велел.");
                    return Loc.T("Бросить бой посреди схватки — это не верность, а бегство.");
                case "Morality":
                    return likes ? Loc.T("Душа его благочестива. Велено — исполни.")
                                 : Loc.T("Душа его порочна. Чужая воля ему поперёк горла.");
                case "Virtue":
                    // Добродетель считает среднее по семи чертам, компас — натура души: они
                    // могут расходиться (прогон 06.11: «он благочестив» — и тут же «грешник»).
                    // Тогда реплика называет расхождение сама.
                    bool pious = w.Soul != null && w.Soul.Moral == MoralType.Pious;
                    if (likes) return Vicious(w)
                        ? P(w, Loc.T("Он порочен, но добрых черт в нём больше, чем грешных. А добрый слушается."),
                               Loc.T("Она порочна, но добрых черт в ней больше, чем грешных. А добрая слушается."))
                        : Loc.T("Добрых черт в нём больше, чем грешных. А добрый слушается.");
                    return pious
                        ? P(w, Loc.T("Он благочестив, но грешных черт в нём больше, чем добрых. А грешник слушаться не привык."),
                               Loc.T("Она благочестива, но грешных черт в ней больше, чем добрых. А грешница слушаться не привыкла."))
                        : Loc.T("Грешных черт в нём больше, чем добрых. А грешник слушаться не привык.");
                case "Pride":
                    if (likes) return Loc.T("В нём смирение. Слушаться ему не обидно.");
                    if (c.IsEngaged && c.CommandLeavesFight) return Loc.T("В нём гордыня. Отступить на глазах у врага — позор.");
                    if (c.CommandVolume < 0.7f) return Loc.T("В нём гордыня. Приказ, крикнутый издали, — не приказ.");
                    return Loc.T("В нём гордыня. Подчиняться ему противно.");
                case "Envy":
                    return likes ? null : Loc.T("Он завидует владыке. А тому, кому завидуешь, не служат.");
                case "Greed":
                    return likes ? null : Loc.T("Ему не заплатили за прошлые дела. Жадность ведёт счёт.");
                case "Lust":
                    return likes ? null : Loc.T("Рядом то, чего он жаждет. Приказа он не слышит.");
                case "Sloth":
                    if (likes) return null;
                    return c.CommandIsPatrol ? Loc.T("В нём уныние. Ходить туда-сюда в дозоре — лень.")
                                             : Loc.T("В нём уныние. Приказ далёкий, идти далеко — лень.");
                case "Wrath":
                    return likes ? Loc.T("Приказ — бить. Гневу это по душе.") : null;
            }
            return null;
        }

        /// <summary>Голос решил соперник приказа: <paramref name="wants"/> — модуль тянет к нему.</summary>
        private static string Pull(string module, ActionType rival, bool wants, DecisionContext c, Warrior w)
        {
            // Верность тянет прочь от любого своеволия — какое бы оно ни было.
            if (module == "Loyalty")
                return wants ? Loc.T("Верности владыке в нём нет. Своя воля ему ближе.")
                             : Loc.T("В нём верность. Своевольничать при владыке он не станет.");

            switch (rival)
            {
                case ActionType.Attack: return Attack(module, wants, c, w);
                case ActionType.Idle:   return Idle(module, wants, c, w);
                case ActionType.Flee:   return Flee(module, wants, c, w);
                case ActionType.Loot:   return Loot(module, wants, c, w);
                case ActionType.SaveAlly: return Save(module, wants, c, w);
            }
            return null;
        }

        private static string Attack(string module, bool wants, DecisionContext c, Warrior w)
        {
            switch (module)
            {
                case "Memory":
                    if (!wants) return null;
                    return c.KnownMurdererInSight ? Loc.T("Это они его убили. Я помню их лица.")
                                                  : Loc.T("Враг убил его товарищей. Я это помню.");
                case "Wrath":
                    return wants ? Loc.T("В нём гнев. Гнев хочет крови, а враг рядом.")
                                 : Loc.T("В нём терпение. Терпеливому драка не к спеху.");
                case "Patience":
                    return wants ? null : Loc.T("В нём терпение. Их час ещё придёт — не сейчас.");
                case "Pride":
                    return wants ? Loc.T("В нём гордыня. Гордый не поворачивается к врагу спиной.")
                                 : Loc.T("В нём смирение. Ему незачем доказывать силу в драке.");
                case "Envy":
                    return wants ? Loc.T("Он завидует владыке и хочет показать, что он лучше.") : null;
                case "Greed":
                    if (wants) return Loc.T("В нём щедрость. Он бьётся не за себя — за правду.");
                    if (c.NearbyLoot > 0) return Loc.T("Рядом добыча. Драться — значит её упустить.");
                    if (c.PocketGold > 0) return Loc.T("У него полный кошель. Жалко рисковать им в драке.");
                    return Loc.T("В нём жадность. Драться даром жадный не станет.");
                case "Gluttony":
                    return wants ? P(w, Loc.T("В нём воздержанность. Он лёгок и собран — драться готов."),
                                        Loc.T("В ней воздержанность. Она легка и собрана — драться готова."))
                                 : Loc.T("В нём чревоугодие. Тяжёлому сытому брюху драка ни к чему.");
                case "Sloth":
                    if (wants) return Loc.T("В нём усердие. Начатое дело надо доделать.");
                    if (c.IsExhausted) return P(w, Loc.T("Он выбился из сил. Какая уж тут драка."), Loc.T("Она выбилась из сил. Какая уж тут драка."));
                    return Loc.T("В нём уныние. Драться — труд, а уйти проще.");
                case "Fear":
                    if (wants) return null;
                    return Hurt(c) ? P(w, Loc.T("Он тяжело ранен. Ещё удар — и конец."), Loc.T("Она тяжело ранена. Ещё удар — и конец."))
                                   : Loc.T("Драка — это боль и смерть. Страх говорит: не лезь.");
                case "Morality":
                    return wants ? Loc.T("Душа его порочна. Кровь ей мила.")
                                 : Loc.T("Душа его благочестива. Без нужды кровь не льют.");
                case "Lust":
                    return wants ? Loc.T("Его тянет туда, где жарко. Он не уйдёт, пока они здесь.") : null;
            }
            return null;
        }

        private static string Idle(string module, bool wants, DecisionContext c, Warrior w)
        {
            switch (module)
            {
                case "Sloth":
                    if (!wants) return Loc.T("В нём усердие. Стоять без дела ему не по нутру.");
                    return c.Fatigue > 0.3f ? P(w, Loc.T("Он устал. Уныние шепчет: постой, отдохни."), Loc.T("Она устала. Уныние шепчет: постой, отдохни."))
                                            : Loc.T("В нём уныние. Стоять проще, чем идти.");
                case "Gluttony":
                    return wants ? Loc.T("В нём чревоугодие. Сытое брюхо тянет к покою.")
                                 : Loc.T("В нём воздержанность. Сытая лень ему чужда.");
                case "Greed":
                    return wants ? Loc.T("У него полный кошель. Лучше переждать в стороне.") : null;
                case "Lust":
                    return wants ? P(w, Loc.T("Желанию некуда деться — и он застыл на месте."), Loc.T("Желанию некуда деться — и она застыла на месте.")) : null;
                case "Fear":
                    return wants ? null : Loc.T("Стоять здесь опасно. Страх гонит прочь.");
                case "Wrath":
                    return wants ? null : Loc.T("В нём гнев. Гневный на месте не стоит.");
                case "Pride":
                    return wants ? null : P(w, Loc.T("В нём гордыня. Гордый не признается, что устал."), Loc.T("В ней гордыня. Гордая не признается, что устала."));
            }
            return null;
        }

        private static string Flee(string module, bool wants, DecisionContext c, Warrior w)
        {
            switch (module)
            {
                case "Fear":
                    if (!wants) return null;
                    if (c.Surrounded) return Loc.T("Его окружили. Страх кричит: беги!");
                    return Hurt(c) ? P(w, Loc.T("Он тяжело ранен. Страх кричит: беги!"), Loc.T("Она тяжело ранена. Страх кричит: беги!"))
                                   : Loc.T("Здесь смерть. Страх кричит: беги!");
                case "Sloth":
                    return wants ? Loc.T("Опасно, а драться — труд. Уныние говорит: беги.") : null;
                case "Pride":
                    return wants ? Loc.T("В нём смирение. Бежать ему не стыдно.") : Loc.T("В нём гордыня. Бегство — позор.");
                case "Wrath":
                    return wants ? Loc.T("В нём терпение. Уйти от драки ему не стыдно.") : Loc.T("В нём гнев. Гневный не бежит.");
                case "Patience":
                    return wants ? null : Loc.T("В нём терпение. Терпеливый без причины не бежит.");
                case "Morality":
                    return wants ? null : Loc.T("Душа его благочестива. Бросить своих и бежать — грех.");
            }
            return null;
        }

        private static string Loot(string module, bool wants, DecisionContext c, Warrior w)
        {
            switch (module)
            {
                case "Greed":
                    return wants ? Loc.T("Рядом добыча, а в нём жадность.") : Loc.T("В нём щедрость. Чужое добро ему не нужно.");
                case "Gluttony":
                    return wants ? Loc.T("В нём чревоугодие. Рядом есть чем набить суму.") : null;
                case "Lust":
                    return wants ? Loc.T("Рядом то, чего он жаждет. Руки сами тянутся.") : null;
                case "Envy":
                    return wants ? Loc.T("Добыча достанется другим? Зависть этого не стерпит.") : null;
                case "Memory":
                    return wants ? Loc.T("Он помнит: здесь бывала добыча.") : null;
                case "Morality":
                    return wants ? Loc.T("Душа его порочна. Обирать мёртвых ей не стыдно.")
                                 : Loc.T("Душа его благочестива. Обирать мёртвых — грех.");
                case "Virtue":
                    return wants ? null : Loc.T("Добра в нём много. Обирать мёртвых он не станет.");
            }
            return null;
        }

        private static string Save(string module, bool wants, DecisionContext c, Warrior w)
        {
            switch (module)
            {
                case "Virtue":
                    return wants ? Loc.T("Добра в нём много. Своих не бросают.") : null;
                case "Morality":
                    return wants ? Loc.T("Душа его благочестива. Помоги ближнему.")
                                 : Loc.T("Душа его порочна. Чужая беда — не его забота.");
                case "Lust":
                    return wants ? Loc.T("Рядом тот, кто ему дорог. Привязанность сильнее приказа.") : null;
                case "Memory":
                    return wants ? Loc.T("Его однажды спасли. Он помнит долг.")
                                 : Loc.T("Его однажды предали. Он это помнит.");
                case "Greed":
                    return wants ? null : Loc.T("Спасать даром? Жадность за чужую жизнь не платит.");
                case "Envy":
                    return wants ? null : Loc.T("Зависть шепчет: пусть пропадёт — одним соперником меньше.");
                case "Pride":
                    return wants ? null : Loc.T("В нём гордыня. Спасать — значит признать, что кто-то важнее него.");
                case "Sloth":
                    return wants ? null : Loc.T("В нём уныние. Бежать на помощь — труд.");
            }
            return null;
        }

        /// <summary>Своей строки нет — хотя бы куда тянет: лучше честное «мне по душе драка», чем «послушай владыку».</summary>
        private static string Fallback(ActionType rival, bool against, Warrior w)
        {
            string what;
            switch (rival)
            {
                case ActionType.Attack:   what = Loc.T("драка"); break;
                case ActionType.Idle:     what = Loc.T("покой"); break;
                case ActionType.Flee:     what = Loc.T("бегство"); break;
                case ActionType.Loot:     what = Loc.T("добыча"); break;
                case ActionType.SaveAlly: what = Loc.T("спасать своих"); break;
                default:                  what = null; break;
            }
            if (what == null) return against ? Loc.T("Приказ мне не по душе.") : Loc.T("Приказ мне по душе.");
            return against ? Loc.F("Мне милее другое: {0}.", what) : Loc.F("Мне не по душе {0}.", what);
        }
    }
}
