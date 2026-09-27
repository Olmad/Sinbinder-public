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
    ///
    /// <b>Своя вещь оболочки.</b> Раз предмет собран в координатах тела,
    /// он садится только на то тело, по которому собран. С 27 сентября
    /// скелет стоит на своих суставах и носит череп по эталону
    /// (<c>Tools/blender/anatomy.py</c>) — наплечник, сшитый по общему телу,
    /// висел бы на нём в двух сантиметрах от плеча, обруч — вокруг пустоты.
    /// Поэтому вещь ищется сперва в <c>Wear/&lt;оболочка&gt;/</c>, и только
    /// потом в общей <c>Wear/</c>.
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
            { "Shade", "Head" },

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

            // Оружие — в правом кулаке, щит — на левом предплечье
            // (Tools/blender/wear.py, «оружие»).
            { "Sword", "RightHand" },
            { "Dagger", "RightHand" },
            { "Axe", "RightHand" },
            { "Club", "RightHand" },
            { "Shield", "LeftLowerArm" },
        };

        /// <summary>
        /// Что у воина в руке — словом из снаряжения: надетое оружие
        /// (<see cref="Warrior.Worn"/>) по имени вещи. Не узнали —
        /// null, и в руке остаётся оружие его ремесла.
        ///
        /// По имени, а не по полю вещи: у вещи нет «вида оружия»,
        /// а заводить его ради облика значило бы менять вещи, запись
        /// и обмен ради того, что видно глазом. Трофей Марги —
        /// «Топор с новым топорищем» — становится топором в руке.
        /// </summary>
        public static string WeaponOf(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;
            string n = itemName.ToLowerInvariant();

            if (n.Contains("топор") || n.Contains("секир")) return "Axe";
            if (n.Contains("меч") || n.Contains("клинок") || n.Contains("сабл")) return "Sword";
            if (n.Contains("нож") || n.Contains("кинжал") || n.Contains("стилет")) return "Dagger";
            if (n.Contains("дубин") || n.Contains("палиц") || n.Contains("булав") || n.Contains("молот"))
                return "Club";
            return null;
        }

        /// <summary>
        /// Оружие по ремеслу — то, с чем воин пришёл, если в руке нет
        /// ничего из снаряжения. Слово автора (`23-PROMPTS.md` §2):
        /// оружие — слот класса, а не тела.
        /// </summary>
        private static string TradeWeapon(Trade trade)
        {
            switch (trade)
            {
                case Trade.Hunter:    return "Axe";
                case Trade.Peasant:   return "Club";
                case Trade.Archer:    return "Dagger";
                case Trade.Alchemist: return "Dagger";
                case Trade.Mage:      return null;        // руки для книги и огня
                default:              return "Sword";
            }
        }

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

            string shell = ShellOf(skin);
            foreach (var item in For(warrior))
                Put(item, shell, bones, binds, null);
        }

        /// <summary>
        /// Какой оболочке принадлежит тело — по имени его меша:
        /// <c>bodies.py</c> называет меш «SkeletonMesh», «ZombieMesh».
        /// По этому имени ищется папка своих вещей. Не то имя — вещи
        /// берутся общие, как было до своих; проверяет соответствие
        /// папок и тел <c>ModelCheck</c>.
        /// </summary>
        public static string ShellOf(SkinnedMeshRenderer skin)
        {
            if (skin == null || skin.sharedMesh == null) return "";

            string name = skin.sharedMesh.name;
            return name.EndsWith("Mesh") ? name.Substring(0, name.Length - 4) : name;
        }

        /// <summary>
        /// Надеть одну вещь на уже собранную модель — снаружи, не через
        /// <see cref="For"/>. Нужен Греховоду: он не проходит по правилам
        /// ремесла и легенды (их считает <see cref="For"/> для обычных
        /// воинов), а вещь ему нужна одна и та же всегда, и своего цвета
        /// (<c>tint</c>) — единственное исключение из общей палитры
        /// (`23-PROMPTS.md` §2).
        /// </summary>
        public static GameObject Wear(GameObject model, string item, Color? tint = null)
        {
            if (model == null) return null;

            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin == null || skin.sharedMesh == null) return null;

            var bones = skin.bones;
            var binds = skin.sharedMesh.bindposes;
            if (bones == null || binds == null || bones.Length != binds.Length) return null;

            return Put(item, ShellOf(skin), bones, binds, tint);
        }

        /// <summary>Перекрасить всё, что рендерится под этим объектом. Через
        /// <c>.material</c> (не <c>.sharedMaterial</c>): иначе покраска одного
        /// воина перекрасила бы всех, кто носит ту же деталь гардероба.</summary>
        /// <summary>
        /// Покрасить один материал по имени — «Skin», «Cloth», «Eye».
        ///
        /// Нужно для лица Греховода: под капюшоном у него тьма, а не
        /// кожа, но руки и одежда при этом свои. Красить всё разом
        /// (<see cref="Tint(GameObject, Color)"/>) тут нельзя — пропадёт
        /// и разница между тканью и кожей, и свет в глазах.
        /// </summary>
        public static void Tint(GameObject go, string slot, Color color, float glow = 0f)
        {
            if (go == null || string.IsNullOrEmpty(slot)) return;

            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;
                bool touched = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].name.StartsWith(slot)) continue;

                    mats[i].color = color;
                    touched = true;

                    if (glow <= 0f || !mats[i].HasProperty("_EmissionColor")) continue;

                    mats[i].EnableKeyword("_EMISSION");
                    mats[i].SetColor("_EmissionColor", color * glow);
                }

                if (touched) r.materials = mats;
            }
        }

        /// <summary>
        /// Сделать материал матовым: без блика и без отражения неба.
        ///
        /// Нужно тени Греховода. Тьма, которая блестит, читается не тьмой,
        /// а чёрным лаком — шлемом: так и вышло 27 сентября, когда огонь
        /// греха вынесли перед лицом (<see cref="SinEyes"/>), и он лёг
        /// бликом на тень под капюшоном. Автор: «почини голову Греховода».
        /// </summary>
        public static void Matte(GameObject go, string slot)
        {
            if (go == null || string.IsNullOrEmpty(slot)) return;

            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;
                bool touched = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].name.StartsWith(slot)) continue;

                    if (mats[i].HasProperty("_Smoothness")) mats[i].SetFloat("_Smoothness", 0f);
                    if (mats[i].HasProperty("_Glossiness")) mats[i].SetFloat("_Glossiness", 0f);
                    if (mats[i].HasProperty("_Metallic")) mats[i].SetFloat("_Metallic", 0f);
                    touched = true;
                }

                if (touched) r.materials = mats;
            }
        }

        /// <summary>
        /// Материалы пустоты — их общая покраска не трогает: дно глазниц
        /// и ноздри («Hollow»), изнанка капюшона («Dark»), тень под ним
        /// («Void»). Багровый Греховод до 27 сентября красился целиком,
        /// с пустотой вместе, — и у нового черепа не осталось бы ни одной
        /// глазницы, а капюшон светился бы изнутри.
        /// </summary>
        private static readonly string[] Hollows = { "Hollow", "Dark", "Void" };

        public static void Tint(GameObject go, Color color)
        {
            if (go == null) return;

            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || System.Array.Exists(Hollows, h => mats[i].name.StartsWith(h)))
                        continue;
                    mats[i].color = color;
                }
                r.materials = mats;
            }
        }

        private static GameObject Put(string item, string shell, Transform[] bones, Matrix4x4[] binds,
                                      Color? tint)
        {
            if (!Worn.TryGetValue(item, out var boneName)) return null;

            int index = -1;
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null && bones[i].name == boneName) { index = i; break; }

            if (index < 0) return null;

            // Своя вещь оболочки — прежде общей (см. заголовок класса).
            var prefab = string.IsNullOrEmpty(shell)
                ? null
                : Resources.Load<GameObject>(Folder + shell + "/" + item);
            if (prefab == null) prefab = Resources.Load<GameObject>(Folder + item);
            if (prefab == null)
            {
                if (_toldAboutMissing) return null;
                _toldAboutMissing = true;

                Debug.Log($"[ГАРДЕРОБ] Части нет ({Folder}{item}) — воины идут "
                        + "без неё. Это не поломка: соберите Tools/blender/wear.py.");
                return null;
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

            // <b>Здесь ничего не домножается — и это не упущение.</b>
            //
            // Предмет, поставленный прямо в сцену, обязан сохранить
            // масштаб и поворот корня модели: ими Unity выравнивает
            // сантиметры файла и ось Z Blender. Вещь на кости — не
            // обязана: тело пришло из того же Blender, и его костяк уже
            // несёт тот же множитель. Замер 20 сентября: кость Head
            // у Греховода имеет общий масштаб 160 — сотня от единиц файла
            // и 1,6 роста воина.
            //
            // 19 сентября я домножил здесь ещё на сто, рассудив по
            // предметам, — и скол черепа уехал на сто пятьдесят два метра
            // от головы. Поймала это дымовая проверка, которая мерит
            // расстояние от надетого до его кости: глазами такое не
            // увидеть, потому что уехавшее просто исчезает из кадра.

            if (tint.HasValue) Tint(worn, tint.Value);

            return worn;
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

                // Оружие вида — последним: сверху его видно после головы
                // и плеч, и так же его и перечисляем.
                string held;
                if (name.StartsWith("Инквизитор"))
                {
                    yield return "InquisitorCap";
                    yield return "Tabard";
                    yield return "PauldronLeft";
                    yield return "PauldronRight";
                    yield return "Shield";
                    held = "Sword";
                }
                else if (name.StartsWith("Охотник-следопыт"))
                {
                    yield return "Hood";
                    yield return "Cloak";
                    held = "Dagger";
                }
                else if (name.StartsWith("Ловчий"))
                {
                    yield return "WideHat";
                    yield return "Net";
                    held = "Club";
                }
                else
                {
                    yield return "Hood";
                    yield return "Quiver";
                    held = "Axe";
                }

                yield return WeaponOf(warrior.Worn(Inventory.GearSlot.Weapon)?.Name) ?? held;
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

            // Оружие. Призрак не держит ничего: «бесплотный не может
            // взять — только смотреть, как берут другие» (bodies.py).
            // Остальные — надетое из снаряжения, а нет его — оружие
            // ремесла; легенда без ремесла — меч.
            if (warrior.Shell != ShellType.Ghost)
            {
                string weapon = WeaponOf(warrior.Worn(Inventory.GearSlot.Weapon)?.Name)
                             ?? TradeWeapon(trade);
                if (weapon != null) yield return weapon;

                var off = warrior.Worn(Inventory.GearSlot.Offhand);
                if (off != null && off.Name != null && off.Name.ToLowerInvariant().Contains("щит"))
                    yield return "Shield";
            }
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
        private static bool Marked(string name) => (Stamp(name) % 3) == 0;

        /// <summary>
        /// Свёртка имени: одно и то же имя даёт одно и то же число
        /// в любом запуске.
        ///
        /// Своя, а не <c>string.GetHashCode</c>: у того нет обещания
        /// одинаковости между запусками, и воин менял бы облик
        /// от загрузки к загрузке — ровно то, против чего вся система
        /// (CLAUDE.md, «ничего случайного»).
        ///
        /// Публичная потому, что по ней разводят не только скол черепа:
        /// лагерь берёт отсюда же рост и сложение своих девятерых.
        /// Две свёртки в проекте разошлись бы, и разошлись бы молча.
        /// </summary>
        public static int Stamp(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;

            int sum = 0;
            foreach (char c in name) sum = (sum * 31 + c) & 0xFFFF;

            return sum;
        }
    }
}
