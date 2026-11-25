// Assets/Scripts/Core/DreamGuards.cs
// Перевод: текст через Loc (имена — из WarriorGenerator)
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Core
{
    /// <summary>
    /// Павшие стражи у пяти печатей сна (№69 §17, §17.1). Ограничения генератора утвердил автор
    /// 24.11: «сделаем чёткое условие генератора: что вот эти ребяты должны быть братья. Вот этот
    /// обязательно гневный, ну а Охотников более чем хватает».
    ///
    /// Задаётся душа, не решение: встанет ли страж на печать — голосуют его модули.
    ///   0 — гневный: Гнев главный и «жжёт»; не из Ордена и не «Охотник» по ремеслу,
    ///       чтобы одна душа не читалась сразу двумя печатями;
    ///   1, 2 — братья: общий знак родни (SoulData.Kin), одного дома и ремесла; ни гнева, ни Ордена;
    ///       грех не жадность и не уныние — им приказ «встань» проигрывает добыче и лени, а пара обязательна;
    ///   3 — охотник Ордена, тихий: приказ «встань на печать» в нём ничего не задевает (как прежний страж);
    ///   4 — охотник Ордена, какой выпал: может и отказать — тогда есть 3.
    /// Пятеро на четыре воинские печати при любом seed и любой ветке сна: поднятый у дороги —
    /// лишний кандидат, не обязательный. Имена не повторяются: Make получает занятые и дописывает новые.
    ///
    /// Без Unity-сцены — проверяется стендом (Tools/bench, «СТРАЖИ ПЕЧАТЕЙ»).
    /// </summary>
    public static class DreamGuards
    {
        public const int Count = 5;

        public static List<SoulData> Make(int seed, List<string> names)
        {
            names ??= new List<string>();
            var souls = new List<SoulData>();
            // Набросок, у которого грех уже тот, что нужен роли: тогда прозвище и примета сходятся с душой
            // («Молельник» с гневом читался ложью). Перебор номеров ограничен и без жребия; не нашлось —
            // первый, а грех правится ниже.
            Sketch Next(int n, SoulOrigin origin, System.Func<Sketch, bool> fits)
            {
                Sketch first = null, s = null;
                for (int k = 0; k < 64; k++)
                {
                    s = WarriorGenerator.Make(seed + n + k * 977, origin, names);
                    first ??= s;
                    if (fits(s)) break;
                    s = null;
                }
                s ??= first;
                names.Add(s.Name);
                return s;
            }
            // «Ратко Охотник» по ремеслу читался бы крестом Ордена — таким даём крестьянина.
            Trade Ours(Trade t) => t == Trade.Hunter || Trades.OfOrder(t) ? Trade.Peasant : t;
            SinType Calm(SinType s, SinType instead)
                => s == SinType.Wrath || s == SinType.Greed || s == SinType.Sloth ? instead : s;

            bool Quiet(SinType s) => s != SinType.Wrath && s != SinType.Greed && s != SinType.Sloth;
            var w = Next(101, SoulOrigin.Sortie, s => s.Sin == SinType.Wrath && !Trades.OfOrder(s.Trade));
            var wrath = new SoulData(w.Name, SinType.Wrath, w.Moral == MoralType.Pious ? MoralType.Neutral : w.Moral, 1, Mathf.Max(70f, w.Strength));
            wrath.SetTrade(Ours(w.Trade));
            souls.Add(wrath);

            string kin = "сон-братья-" + seed;
            var a = Next(102, SoulOrigin.Pact, s => Quiet(s.Sin));
            var b = Next(103, SoulOrigin.Pact, s => Quiet(s.Sin));
            var trade = Ours(a.Trade);
            foreach (var (s, instead) in new[] { (a, SinType.Pride), (b, SinType.Envy) })
            {
                var soul = new SoulData(s.Name, Calm(s.Sin, instead), s.Moral, 1, 35f);
                soul.SetTrade(trade);
                soul.SetKin(kin);
                souls.Add(soul);
            }

            var q = Next(104, SoulOrigin.Harvest, s => s.Sin == SinType.Envy);
            var quiet = new SoulData(q.Name, SinType.Envy, MoralType.Pious, 1, 15f);
            quiet.SetTrade(q.Trade);
            souls.Add(quiet);

            var r = Next(105, SoulOrigin.Harvest, s => true);
            var any = new SoulData(r.Name, r.Sin, r.Moral, 1, Mathf.Max(30f, Mathf.Abs(r.Strength)));
            any.SetTrade(r.Trade);
            souls.Add(any);
            return souls;
        }
    }
}
