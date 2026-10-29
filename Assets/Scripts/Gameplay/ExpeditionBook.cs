// Assets/Scripts/Gameplay/ExpeditionBook.cs
// Перевод: текст через Loc
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Книга экспедиций «Перетягивания» (54-TUG.md; автор, 28.10: «нужно ещё экспедиций, чтобы их было много.
    /// Например, тридцать»; «на карте рандомные, с условно рандомными — в определённых рамках — наградами»).
    /// Экспедиция — запись, а не класс: что на месте и две развилки с последствиями. Боевые решают пошедшие —
    /// голосом своего греха; мирные — Греховод (клавиши 1 и 2 у места). Награды — в рамках записи, выбор внутри
    /// рамок — от номера партии (<see cref="MapEvent"/>).
    /// </summary>
    public static class ExpeditionBook
    {
        public sealed class Option
        {
            public string Label = "";
            public SinType[] Favour = System.Array.Empty<SinType>();
            public int Cost;                 // монеты Греховода (купить, перекупить)
            public int CoinsMin, CoinsMax;   // монеты Греховоду — в рамках
            public Trophies.Kind[] Trophy = System.Array.Empty<Trophies.Kind>();   // один из — в рамках
            public int Stone, Bones, Flesh, Souls, Jars;
            public int Recruits;             // новые люди у места: поднимутся к Греховоду
            public float RecruitTrust = 50f;
            public float Followers;          // пошедшим — всем
            public SinType[] PleasedSins = System.Array.Empty<SinType>();
            public float Pleased;            // пошедшим этих грехов — ещё
            public SinType[] VexedSins = System.Array.Empty<SinType>();
            public float Vexed;              // пошедшим этих грехов — минус
            public float Residents;          // жителям ближнего очага — всем
            public float Pious;              // жителям и пошедшим набожным — ещё
            public bool SplitResidents;      // половине жителей +Residents, другой — минус
            public bool Deserters;           // беглецы переходят к Греховоду
            public bool Ransom;              // выкуп у прежнего хозяина
            public bool Gamble;              // кости: от номера партии — выигрыш или проигрыш
            public float DelayHunters;       // следующая волна охотников — позже, с
            public string Line = "";         // что скажет пошедший
        }

        public sealed class Entry
        {
            public string Id = "", Title = "", Story = "";
            public int Guards;
            public ShellType GuardShell = ShellType.Living;
            public string[] Props = System.Array.Empty<string>();
            public int Camp;                 // ничьи у места (дезертиры)
            public Option A = new(), B = new();
            public bool Parley => Guards == 0;
        }

        private static readonly SinType[] Selfish = { SinType.Greed, SinType.Envy, SinType.Lust, SinType.Gluttony };
        private static readonly SinType[] Proud = { SinType.Pride, SinType.Wrath };
        private static readonly Trophies.Kind[] Cargo = { Trophies.Kind.GolemRecipe, Trophies.Kind.Jar, Trophies.Kind.Treasury, Trophies.Kind.Forge, Trophies.Kind.Pillory, Trophies.Kind.Coins };
        private static readonly Trophies.Kind[] Schemes = { Trophies.Kind.Treasury, Trophies.Kind.Forge, Trophies.Kind.Pillory };

        public static readonly Entry[] All =
        {
            // ---------------------------------------------------------- боевые: решают пошедшие
            new() { Id = "caravan", Title = Loc.N("Обоз Ордена"), Story = Loc.N("Телега под охраной. Что везут — узнаете, когда охраны не станет."),
                Guards = 3, Props = new[] { "Crate", "Crate", "Barrel" },
                A = new() { Label = Loc.N("Разделить добычу между пошедшими"), Favour = Selfish, CoinsMin = 2, CoinsMax = 2, Trophy = Cargo, Followers = 6f, PleasedSins = new[] { SinType.Greed, SinType.Envy }, Pleased = 6f, Line = Loc.N("Вот это по-честному, владыка.") },
                B = new() { Label = Loc.N("Всё — владыке"), Favour = Proud, CoinsMin = 10, CoinsMax = 10, Trophy = Cargo, VexedSins = new[] { SinType.Greed }, Vexed = 6f, Line = Loc.N("Всё — ему. Как всегда.") } },
            new() { Id = "village", Title = Loc.N("Деревня в беде"), Story = Loc.N("Охотники у очага. Жители смотрят, кто придёт на помощь."),
                Guards = 4,
                A = new() { Label = Loc.N("Остаться и помочь"), Favour = Proud, Residents = 10f, Followers = -2f },
                B = new() { Label = Loc.N("Забрать припасы и уйти"), Favour = Selfish, CoinsMin = 5, CoinsMax = 7, Residents = -10f, PleasedSins = new[] { SinType.Greed }, Pleased = 4f, Line = Loc.N("Припасы — это дело.") } },
            new() { Id = "prisoner", Title = Loc.N("Пленник Ордена"), Story = Loc.N("Стража ведёт связанного. Он смотрит на вас."),
                Guards = 3,
                A = new() { Label = Loc.N("Освободить и позвать с собой"), Favour = new[] { SinType.Pride, SinType.Lust, SinType.Gluttony }, Recruits = 1, RecruitTrust = 62f, VexedSins = new[] { SinType.Wrath, SinType.Envy }, Vexed = 4f, Line = Loc.N("Ещё один рот, владыка?") },
                B = new() { Label = Loc.N("Отпустить на все четыре стороны"), Favour = new[] { SinType.Wrath, SinType.Envy }, Residents = 4f, Followers = 2f } },
            new() { Id = "chapel", Title = Loc.N("Заброшенная часовня"), Story = Loc.N("Призраки стерегут полку с банками. В банках — души."),
                Guards = 2, GuardShell = ShellType.Ghost, Props = new[] { "TorchStand", "Rock" },
                A = new() { Label = Loc.N("Взять души"), Favour = Proud, Souls = 2, Pious = -8f, Line = Loc.N("Это святое место, владыка. Было.") },
                B = new() { Label = Loc.N("Оставить мёртвым их покой"), Favour = new[] { SinType.Sloth, SinType.Envy }, Pious = 6f, Followers = 2f } },
            new() { Id = "barrow", Title = Loc.N("Курган"), Story = Loc.N("Насыпь древних. Камень в ней крепче любого — и мёртвые его стерегут."),
                Guards = 3, GuardShell = ShellType.Ghost, Props = new[] { "Rock", "Rock", "Stump" },
                A = new() { Label = Loc.N("Тревожить мёртвых"), Favour = new[] { SinType.Pride, SinType.Wrath, SinType.Greed }, Stone = 2, Trophy = new[] { Trophies.Kind.GolemRecipe, Trophies.Kind.Coins }, Pious = -6f },
                B = new() { Label = Loc.N("Оставить курган"), Favour = new[] { SinType.Sloth, SinType.Envy }, Pious = 6f } },
            new() { Id = "bandits", Title = Loc.N("Разбойничий стан"), Story = Loc.N("Награбленное свалено у костра. Хозяева у костра тоже."),
                Guards = 4, Props = new[] { "Crate", "Barrel", "Campfire" },
                A = new() { Label = Loc.N("Забрать награбленное"), Favour = new[] { SinType.Greed, SinType.Wrath, SinType.Gluttony }, CoinsMin = 8, CoinsMax = 14 },
                B = new() { Label = Loc.N("Вернуть деревне"), Favour = new[] { SinType.Pride, SinType.Envy }, CoinsMin = 2, CoinsMax = 2, Residents = 12f } },
            new() { Id = "ambush", Title = Loc.N("Засада на тропе"), Story = Loc.N("Орден ждал кого-то другого. Дождался вас."),
                Guards = 3,
                A = new() { Label = Loc.N("Обыскать павших"), Favour = Selfish, CoinsMin = 4, CoinsMax = 7, Jars = 1 },
                B = new() { Label = Loc.N("Сжечь тела — пусть Орден знает"), Favour = Proud, Followers = 5f, PleasedSins = new[] { SinType.Wrath }, Pleased = 4f, Line = Loc.N("Пусть горят. Пусть видят.") } },
            new() { Id = "jarcart", Title = Loc.N("Повозка с банками"), Story = Loc.N("Орден свозит пустые банки — для чужих душ."),
                Guards = 2, Props = new[] { "Crate", "Barrel" },
                A = new() { Label = Loc.N("Забрать банки"), Favour = new[] { SinType.Greed, SinType.Sloth, SinType.Envy }, Jars = 2 },
                B = new() { Label = Loc.N("Разбить — Ордену не служить"), Favour = Proud, Followers = 6f } },
            new() { Id = "forge", Title = Loc.N("Брошенная кузня"), Story = Loc.N("Горн ещё тёплый. Кузнецов охраняли — теперь охраняют кузню."),
                Guards = 2, Props = new[] { "Barrel", "Crate" },
                A = new() { Label = Loc.N("Забрать чертежи"), Favour = new[] { SinType.Greed, SinType.Pride }, Trophy = new[] { Trophies.Kind.Forge } },
                B = new() { Label = Loc.N("Перековать оружие пошедшим"), Favour = new[] { SinType.Wrath, SinType.Pride }, Followers = 6f, PleasedSins = new[] { SinType.Wrath, SinType.Pride }, Pleased = 6f, Line = Loc.N("Вот это клинок, владыка.") } },
            new() { Id = "banner", Title = Loc.N("Знамя Ордена"), Story = Loc.N("Знамя на шесте — знак, что здесь их земля."),
                Guards = 3, Props = new[] { "TorchStand" },
                A = new() { Label = Loc.N("Сорвать и сжечь"), Favour = Proud, Followers = 8f, Residents = 4f },
                B = new() { Label = Loc.N("Забрать — на обмен"), Favour = new[] { SinType.Greed, SinType.Envy }, CoinsMin = 7, CoinsMax = 9 } },
            new() { Id = "quarry", Title = Loc.N("Каменоломня"), Story = Loc.N("Големы Ордена гонят рабочих. Камень сложен грудой."),
                Guards = 2, GuardShell = ShellType.Golem, Props = new[] { "Rock", "Rock", "Rock" },
                A = new() { Label = Loc.N("Забрать камень"), Favour = new[] { SinType.Greed, SinType.Sloth }, Stone = 3 },
                B = new() { Label = Loc.N("Освободить рабочих"), Favour = new[] { SinType.Pride, SinType.Lust }, Recruits = 2, RecruitTrust = 52f } },
            new() { Id = "tithe", Title = Loc.N("Сборщики десятины"), Story = Loc.N("Сундук с податью. Подать собрали с деревни."),
                Guards = 3, Props = new[] { "Crate" },
                A = new() { Label = Loc.N("Вернуть людям"), Favour = new[] { SinType.Pride, SinType.Envy }, CoinsMin = 2, CoinsMax = 2, Residents = 12f },
                B = new() { Label = Loc.N("Оставить себе"), Favour = new[] { SinType.Greed, SinType.Gluttony, SinType.Lust }, CoinsMin = 10, CoinsMax = 14, Residents = -6f } },
            new() { Id = "graverobbers", Title = Loc.N("Расхитители могил"), Story = Loc.N("Копают чужих мёртвых. Ваших — пока нет."),
                Guards = 3, Props = new[] { "Stump", "Crate" },
                A = new() { Label = Loc.N("Отнять добро"), Favour = Selfish, CoinsMin = 6, CoinsMax = 9, Trophy = new[] { Trophies.Kind.Treasury, Trophies.Kind.Pillory } },
                B = new() { Label = Loc.N("Похоронить заново"), Favour = new[] { SinType.Sloth, SinType.Pride }, Pious = 8f, Bones = 2 } },
            new() { Id = "mill", Title = Loc.N("Горящая мельница"), Story = Loc.N("Орден поджёг мельницу и ждёт, кто придёт тушить."),
                Guards = 2, Props = new[] { "Barrel", "Barrel" },
                A = new() { Label = Loc.N("Тушить"), Favour = new[] { SinType.Pride, SinType.Sloth }, Residents = 8f, Followers = -2f },
                B = new() { Label = Loc.N("Растащить мешки"), Favour = new[] { SinType.Gluttony, SinType.Greed }, CoinsMin = 3, CoinsMax = 5, PleasedSins = new[] { SinType.Gluttony }, Pleased = 8f, Line = Loc.N("Мука! Целый мешок!") } },
            new() { Id = "healer", Title = Loc.N("Пленённая знахарка"), Story = Loc.N("Её ведут на суд. Она знает травы — и души."),
                Guards = 3,
                A = new() { Label = Loc.N("Освободить"), Favour = new[] { SinType.Lust, SinType.Pride }, Recruits = 1, RecruitTrust = 58f, Souls = 1 },
                B = new() { Label = Loc.N("Отдать Ордену за награду"), Favour = new[] { SinType.Greed, SinType.Envy }, CoinsMin = 10, CoinsMax = 10, Pious = -4f } },
            new() { Id = "relic", Title = Loc.N("Реликвия Ордена"), Story = Loc.N("Её везут в склеп Ордена. Значит, она чего-то стоит."),
                Guards = 4, Props = new[] { "Crate", "TorchStand" },
                A = new() { Label = Loc.N("Унести"), Favour = new[] { SinType.Pride, SinType.Greed }, Trophy = new[] { Trophies.Kind.GolemRecipe, Trophies.Kind.Pillory } },
                B = new() { Label = Loc.N("Разбить на глазах у всех"), Favour = new[] { SinType.Wrath, SinType.Envy }, Followers = 6f, Residents = 4f } },
            new() { Id = "patrol", Title = Loc.N("Ночной дозор"), Story = Loc.N("Дозор Ордена обходит дороги. Сегодня — вашу."),
                Guards = 3,
                A = new() { Label = Loc.N("Добить и забрать броню"), Favour = new[] { SinType.Greed, SinType.Wrath }, CoinsMin = 4, CoinsMax = 6, PleasedSins = new[] { SinType.Wrath }, Pleased = 6f },
                B = new() { Label = Loc.N("Отпустить последнего — пусть расскажет"), Favour = new[] { SinType.Pride }, Followers = 4f, PleasedSins = new[] { SinType.Pride }, Pleased = 4f, Line = Loc.N("Пусть знают, кто здесь хозяин.") } },
            new() { Id = "bridge", Title = Loc.N("Застава у моста"), Story = Loc.N("Орден берёт пошлину с каждого, кто идёт мимо."),
                Guards = 4, Props = new[] { "Palisade", "Crate" },
                A = new() { Label = Loc.N("Брать пошлину самим"), Favour = new[] { SinType.Greed, SinType.Gluttony }, CoinsMin = 9, CoinsMax = 12 },
                B = new() { Label = Loc.N("Открыть мост для всех"), Favour = new[] { SinType.Pride, SinType.Lust }, Residents = 8f } },

            // ---------------------------------------------------------- мирные: решает Греховод (1 и 2)
            new() { Id = "merchant", Title = Loc.N("Торговец"), Story = Loc.N("Бродячий торговец разложил товар. Ему всё равно, кто платит."),
                Props = new[] { "Crate", "Barrel" },
                A = new() { Label = Loc.N("Купить схему"), Cost = 8, Trophy = Schemes, VexedSins = new[] { SinType.Greed }, Vexed = 2f, Line = Loc.N("Дорого, владыка.") },
                B = new() { Label = Loc.N("Купить банку"), Cost = 5, Jars = 1, VexedSins = new[] { SinType.Greed }, Vexed = 2f } },
            new() { Id = "deserters", Title = Loc.N("Дезертиры"), Story = Loc.N("Сбежали от своего Греховода и греются у чужого огня."),
                Camp = 3, Props = new[] { "Campfire" },
                A = new() { Label = Loc.N("Принять к себе"), Deserters = true, RecruitTrust = 40f, Followers = -2f, VexedSins = new[] { SinType.Pride }, Vexed = 4f, Line = Loc.N("Беглецов берём, владыка? Они и от нас сбегут.") },
                B = new() { Label = Loc.N("Вернуть хозяину за выкуп"), Ransom = true, CoinsMin = 12, CoinsMax = 16, Followers = 2f, PleasedSins = new[] { SinType.Envy }, Pleased = 3f } },
            new() { Id = "quarrel", Title = Loc.N("Ссора у колодца"), Story = Loc.N("Две стороны деревни делят воду. Ещё слово — и за топоры."),
                Props = new[] { "Barrel" },
                A = new() { Label = Loc.N("Встать за одних"), SplitResidents = true, Residents = 12f },
                B = new() { Label = Loc.N("Разнять подарками"), Cost = 4, Residents = 5f } },
            new() { Id = "preacher", Title = Loc.N("Проповедник Ордена"), Story = Loc.N("Говорит деревне, что все Греховоды — одно зло."),
                Props = new[] { "TorchStand" },
                A = new() { Label = Loc.N("Прогнать силой"), Residents = 6f, Pious = -16f },
                B = new() { Label = Loc.N("Перекупить"), Cost = 10, Residents = 4f, PleasedSins = new[] { SinType.Greed }, Pleased = 4f } },
            new() { Id = "refugees", Title = Loc.N("Беженцы"), Story = Loc.N("Бегут от Ордена. Просят крова."),
                A = new() { Label = Loc.N("Принять в склеп"), Recruits = 2, RecruitTrust = 36f, Followers = -2f, VexedSins = new[] { SinType.Gluttony }, Vexed = 3f, Line = Loc.N("Ещё рты, владыка.") },
                B = new() { Label = Loc.N("Указать дорогу в деревню"), Residents = 10f, Pious = 4f } },
            new() { Id = "wounded", Title = Loc.N("Раненый воин Ордена"), Story = Loc.N("Брошен своими. Ещё дышит."),
                A = new() { Label = Loc.N("Взять в плен"), Recruits = 1, RecruitTrust = 30f, Pious = -4f },
                B = new() { Label = Loc.N("Перевязать и отпустить"), Residents = 4f, PleasedSins = new[] { SinType.Lust, SinType.Pride }, Pleased = 3f } },
            new() { Id = "dice", Title = Loc.N("Игрок в кости"), Story = Loc.N("«Сыграем, владыка? Ставка небольшая»."),
                A = new() { Label = Loc.N("Сыграть"), Cost = 5, Gamble = true, CoinsMin = 0, CoinsMax = 12, PleasedSins = new[] { SinType.Lust, SinType.Greed }, Pleased = 2f },
                B = new() { Label = Loc.N("Прогнать"), VexedSins = new[] { SinType.Greed }, Vexed = 3f, Followers = 1f } },
            new() { Id = "seer", Title = Loc.N("Гадалка"), Story = Loc.N("Говорит, что знает, когда придут охотники."),
                A = new() { Label = Loc.N("Заплатить и выслушать"), Cost = 3, DelayHunters = 60f, PleasedSins = new[] { SinType.Sloth }, Pleased = 4f, Line = Loc.N("Значит, время ещё есть.") },
                B = new() { Label = Loc.N("Прогнать"), Pious = 4f } },
            new() { Id = "grave", Title = Loc.N("Свежая могила"), Story = Loc.N("Земля ещё мягкая."),
                A = new() { Label = Loc.N("Раскопать"), Bones = 2, Flesh = 1, Pious = -6f },
                B = new() { Label = Loc.N("Помолиться и уйти"), Pious = 5f } },
            new() { Id = "oath", Title = Loc.N("Камень клятвы"), Story = Loc.N("Здесь клялись князья. Камень помнит."),
                Props = new[] { "Rock" },
                A = new() { Label = Loc.N("Принести клятву перед верными"), Followers = 6f, PleasedSins = new[] { SinType.Pride }, Pleased = 4f },
                B = new() { Label = Loc.N("Разбить камень"), Stone = 1, PleasedSins = new[] { SinType.Wrath }, Pleased = 6f, Pious = -6f } },
            new() { Id = "child", Title = Loc.N("Потерявшийся ребёнок"), Story = Loc.N("Сидит у дороги и не плачет — уже не может."),
                A = new() { Label = Loc.N("Отвести в деревню"), Residents = 10f },
                B = new() { Label = Loc.N("Пройти мимо"), Pious = -8f, PleasedSins = new[] { SinType.Sloth }, Pleased = 1f } },
            new() { Id = "cache", Title = Loc.N("Тайник охотников"), Story = Loc.N("Орден прячет здесь припасы для следующей облавы."),
                Props = new[] { "Crate", "Crate" },
                A = new() { Label = Loc.N("Забрать"), CoinsMin = 5, CoinsMax = 7, Jars = 1 },
                B = new() { Label = Loc.N("Испортить — облава задержится"), DelayHunters = 90f, PleasedSins = new[] { SinType.Wrath }, Pleased = 3f } },
        };
    }
}
