// Assets/Scripts/Gameplay/Wardrobe.cs
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Что надето на воине — и почему именно это.
    ///
    /// Замысел записан в <c>docs/22-LOOK.md</c>: девять воинов различаются
    /// не девятью моделями, а двенадцатью предметами на общем теле.
    /// Предметы лежат в <c>Resources/Wear</c> (собирает
    /// <c>Tools/blender/wear.py</c>), а надевает их этот класс.
    ///
    /// <b>Ни одного жребия.</b> Что надето, вычисляется из того, кем воин
    /// является: ремесло, легенда, братство, опыт, имя. Тот же воин
    /// выглядит так же при каждом запуске и после каждой загрузки —
    /// иначе игрок перестаёт его узнавать, и вся затея рушится
    /// (<c>22-LOOK.md</c> §3).
    ///
    /// <b>Как предмет садится на кость.</b> Он собран в тех же
    /// координатах, что и тело, поэтому достаточно повесить его на кость
    /// с матрицей привязки этой кости (<c>bindposes</c>): она и переводит
    /// «где это лежит на теле» в «где это лежит на кости». Дальше предмет
    /// едет вместе с костью через все движения, ничего не зная о них.
    /// </summary>
    public static class Wardrobe
    {
        private const string Folder = "Wear/";

        private static bool _toldAboutMissing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => _toldAboutMissing = false;

        /// <summary>Какая кость носит этот предмет.</summary>
        private static readonly Dictionary<string, string> Worn = new()
        {
            { "Hood", "Head" },
            { "WideHat", "Head" },
            { "StrawHat", "Head" },
            { "InquisitorCap", "Head" },
            { "Circlet", "Head" },
            { "Crack", "Head" },

            { "RavenMantle", "Chest" },
            { "Quiver", "Chest" },
            { "Bow", "Chest" },
            { "Cloak", "Chest" },
            { "PauldronLeft", "LeftShoulder" },
            { "PauldronRight", "RightShoulder" },
            { "Net", "RightShoulder" },
            { "BrotherBand", "LeftUpperArm" },

            { "Flasks", "Spine" },
            { "Tabard", "Spine" },
        };

        /// <summary>Носит ли кто-нибудь такую часть. Спрашивает проверка.</summary>
        public static bool Knows(string item) => item != null && Worn.ContainsKey(item);

        /// <summary>
        /// Одеть тело. Зовётся из <see cref="WarriorLook.Build"/> сразу
        /// после того, как модель встала: раньше костей ещё нет, позже
        /// воин уже показан игроку голым.
        /// </summary>
        public static void Dress(GameObject model, Warrior warrior)
        {
            if (model == null || warrior == null) return;

            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin == null || skin.sharedMesh == null) return;

            var bones = skin.bones;
            var binds = skin.sharedMesh.bindposes;
            if (bones == null || binds == null || bones.Length != binds.Length) return;

            foreach (var item in For(warrior))
                Put(item, bones, binds);
        }

        private static void Put(string item, Transform[] bones, Matrix4x4[] binds)
        {
            if (!Worn.TryGetValue(item, out var boneName)) return;

            int index = -1;
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null && bones[i].name == boneName) { index = i; break; }

            if (index < 0) return;

            var prefab = Resources.Load<GameObject>(Folder + item);
            if (prefab == null)
            {
                if (_toldAboutMissing) return;
                _toldAboutMissing = true;

                Debug.Log($"[ГАРДЕРОБ] Части нет ({Folder}{item}) — воины идут "
                        + "без неё. Это не поломка: соберите Tools/blender/wear.py.");
                return;
            }

            var worn = Object.Instantiate(prefab, bones[index]);
            worn.name = item;

            // Матрица привязки переводит координаты тела в координаты кости.
            // Предмет собран в координатах тела — значит это ровно то
            // преобразование, которого ему не хватает.
            var m = binds[index];
            worn.transform.localPosition = m.GetColumn(3);
            worn.transform.localRotation = m.rotation;
            worn.transform.localScale = m.lossyScale;
        }

        /// <summary>
        /// Что надето на этом воине. Порядок перечисления постоянный:
        /// сперва ремесло, потом знаки положения, потом личное.
        /// </summary>
        public static IEnumerable<string> For(Warrior warrior)
        {
            if (warrior == null) yield break;

            // Живые: охотники узнаются по имени — оно у них и есть род
            // занятий. Четыре вида, и каждый отличим сверху.
            if (warrior.Shell == ShellType.Living)
            {
                string name = warrior.DisplayName ?? "";

                if (name.StartsWith("Инквизитор"))
                {
                    yield return "InquisitorCap";
                    yield return "Tabard";
                    yield return "PauldronLeft";
                    yield return "PauldronRight";
                }
                else if (name.StartsWith("Охотник-следопыт"))
                {
                    yield return "Hood";
                    yield return "Cloak";
                }
                else if (name.StartsWith("Ловчий"))
                {
                    yield return "WideHat";
                    yield return "Net";
                }
                else
                {
                    yield return "Hood";
                    yield return "Quiver";
                }

                yield break;
            }

            // Мёртвые: ремесло — врождённое и не меняется никогда
            // (22-LOOK.md §2), поэтому с него и начинаем.
            var trade = warrior.Soul != null ? warrior.Soul.Trade : Trade.None;

            switch (trade)
            {
                case Trade.Hunter:    yield return "Hood"; yield return "Quiver"; break;
                case Trade.Peasant:   yield return "StrawHat"; break;
                case Trade.Archer:    yield return "Bow"; break;
                case Trade.Alchemist: yield return "Flasks"; break;
                case Trade.Mage:      yield return "Hood"; yield return "Cloak"; break;
            }

            // Легенду видно первой: плащ из вороньих перьев — вещь
            // плечевая, и сверху опознаётся мгновенно (23-PROMPTS.md §3).
            if (warrior.Reputation != null && warrior.Reputation.LegendaryUnlocked)
            {
                yield return "RavenMantle";
                yield return "Circlet";
            }

            // Братья по оружию — одна лента на двоих. Была бирюзовым
            // кубом над головой; стала вещью, которую носят.
            if (Brother(warrior)) yield return "BrotherBand";

            // Опытный: наплечник. Рост отличает его сбоку, наплечник —
            // сверху, где игрок и смотрит.
            if (SquadRoster.TryGet(warrior.DisplayName, out var member)
                && Leadership.IsExperienced(member.Leadership))
                yield return "PauldronLeft";

            // Личное: скол черепа. Механически не значит ничего и не должен
            // (22-LOOK.md §5) — он нужен, чтобы Кир отличался от Ждана.
            // Берётся из имени: имя не меняется, и скол всегда на том же
            // месте у того же воина.
            if (Marked(warrior.DisplayName)) yield return "Crack";
        }

        private static bool Brother(Warrior warrior)
        {
            var perks = warrior.Soul?.Memory?.NarrativePerks;
            return perks != null && perks.Exists(p => p.PerkName == "Брат по оружию");
        }

        /// <summary>
        /// Расколот ли череп у этого имени.
        ///
        /// Своя свёртка, а не <c>string.GetHashCode</c>: у того нет
        /// обещания одинаковости между запусками, и воин менял бы облик
        /// от загрузки к загрузке — ровно то, против чего вся система.
        /// </summary>
        private static bool Marked(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            int sum = 0;
            foreach (char c in name) sum = (sum * 31 + c) & 0xFFFF;

            return (sum % 3) == 0;
        }
    }
}
