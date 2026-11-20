// Assets/Scripts/Dialogue/VillageLines.cs
// Перевод: текст через Loc
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.Dialogue
{
    /// <summary>
    /// О чём говорят жители «Перетягивания» у своего очага (автор, 28.10: «в перетягивании вообще нет
    /// разговоров»). Не лагерные строки (CampLines — про отряд демо), а деревенские: про Греховодов, про
    /// Орден на тропах, про то, что творится рядом, и грех на грех. Ответ — на ту же тему, по греху отвечающего.
    /// Без жребия: тема — от имён двоих и минуты партии.
    /// </summary>
    public static class VillageLines
    {
        private enum Topic { Lord, Order, Event, Sin }

        public static (string First, string Answer) Exchange(Warrior a, Warrior b)
        {
            if (a?.Soul == null || b?.Soul == null) return ("", "");
            if (CampLines.Mute(a))
                return (CampLines.Gesture(a), CampLines.Mute(b) ? CampLines.Gesture(b) : CampLines.Acknowledge(b));
            int k = unchecked(Wardrobe.Stamp(a.Soul.Name) + Wardrobe.Stamp(b.Soul.Name)
                + (int)(UnityEngine.Time.time / 60f));
            var ev = Nearest(a);
            var topic = (Topic)VillageDialogueRules.TopicIndex(k);
            if (topic == Topic.Event && ev == null) topic = Topic.Sin;
            switch (topic)
            {
                case Topic.Lord: return LordTalk(a, b);
                case Topic.Order: return (Loc.T("Поговаривают, Орден снова на тропах. Не к добру."), CampLines.Mute(b) ? CampLines.Gesture(b) : OrderAnswer(b));
                case Topic.Event: return (Loc.F("У знака неподалёку — «{0}». Кто пойдёт посмотреть?", Loc.T(ev.Entry.Title)), CampLines.Mute(b) ? CampLines.Gesture(b) : EventAnswer(b));
                default: return (SinOpening(a, b), CampLines.Mute(b)
                    ? CampLines.Gesture(b) : VillageDialogueRules.SinAnswer(a.Soul.Sin, b.Soul.Sin));
            }
        }

        private static MapEvent Nearest(Warrior a)
        {
            MapEvent best = null;
            float d = 60f;
            foreach (var e in MapEvent.All)
            {
                if (e == null || e.Done || e.Entry == null) continue;
                float k = CampFocus.GroundDistance(e.transform.position, a.transform.position);
                if (k < d) { d = k; best = e; }
            }
            return best;
        }

        /// <summary>О Греховоде игрока — как к нему этот житель: словом, без числа.</summary>
        private static (string, string) LordTalk(Warrior a, Warrior b)
        {
            if (Lords.Player == null) return (SinOpening(a, b), CampLines.Mute(b)
                ? CampLines.Gesture(b) : VillageDialogueRules.SinAnswer(a.Soul.Sin, b.Soul.Sin));
            float v = Allegiance.Of(a.Id, a.Loyalty, Lords.Player);
            string first = v >= Hostility.Follows ? Loc.T("Наш-то Греховод — не худший из них.")
                         : v <= Hostility.Despised ? Loc.T("С этим Греховодом лучше не связываться.")
                         : Loc.T("Что скажешь про Греховода? Стоит ему верить?");
            VillageLordTone tone = v >= Hostility.Follows ? VillageLordTone.Respected
                : v <= Hostility.Despised ? VillageLordTone.Distrusted
                : VillageLordTone.Undecided;
            string answer = CampLines.Mute(b)
                ? CampLines.Gesture(b) : VillageDialogueRules.LordAnswer(b.Soul.Sin, tone);
            return (first, answer);
        }

        private static string OrderAnswer(Warrior b) => b.Soul.Sin switch
        {
            SinType.Wrath => Loc.T("Пусть приходят. Топор наточен."),
            SinType.Pride => Loc.T("Мы им не дичь."),
            SinType.Sloth => Loc.T("Придут — тогда и встанем."),
            SinType.Greed => Loc.T("С охотника снять есть что."),
            SinType.Envy => Loc.T("К соседям бы сперва зашли."),
            SinType.Lust => Loc.T("Лишь бы не к нашим очагам."),
            _ => Loc.T("Значит, спать вполглаза."),
        };

        private static string EventAnswer(Warrior b) => b.Soul.Sin switch
        {
            SinType.Greed => Loc.T("Пойдёт тот, кому заплатят."),
            SinType.Wrath => Loc.T("Я бы пошёл. Позвал бы кто."),
            SinType.Sloth => Loc.T("Без меня сходят."),
            SinType.Pride => Loc.T("Позовут лучших — пойду."),
            SinType.Envy => Loc.T("Сходят другие — и всё им достанется."),
            _ => Loc.T("Не наше это дело. Пока."),
        };

        private static string SinOpening(Warrior a, Warrior b) => a.Soul.Sin switch
        {
            SinType.Greed => Loc.T("Сколько у тебя припрятано? Только честно."),
            SinType.Pride => Loc.T("В этой деревне только я чего-то стою."),
            SinType.Wrath => Loc.T("Ещё день без дела — и я кого-нибудь пришибу."),
            SinType.Sloth => Loc.T("Хорошо сидим. Не вставай."),
            SinType.Envy => Grammar.Pick(b.Gender, Loc.T("Видал, что у соседа новое?"), Loc.T("Видала, что у соседа новое?")),
            SinType.Lust => Loc.T("У очага теплее, когда не один."),
            SinType.Gluttony => Loc.T("Что-то есть хочется. Опять."),
            _ => Loc.T("Тихо сегодня."),
        };

    }
}
