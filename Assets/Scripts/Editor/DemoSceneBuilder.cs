#if UNITY_EDITOR
// Файл зависит от UnityEditor. Обёртка стоит выше using намеренно:
// без неё сборка плеера падает с CS0246. Тем же приёмом защищён
// LocationDatabaseSync — там на этом уже спотыкались.
// Assets/Scripts/Editor/DemoSceneBuilder.cs
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Sinbinder.Gameplay;

namespace Sinbinder.Utilets
{
    /// <summary>
    /// Собирает сцены демо по сценарию docs/00-GDD.md §8.
    ///
    /// Сцены строятся кодом, потому что в проекте нет ни префабов,
    /// ни моделей: всё, что можно поставить, — примитивы, свет и туман.
    /// Это ровно то, что предписывает пролог §4 «дешёвая эпика».
    ///
    /// Общий низ у всех сцен одинаков и собирается здесь один раз:
    /// менеджеры, Canvas с тремя ступенями прозрачности, атмосфера.
    /// Локация — это вариация каркаса, а не отдельная постройка. Иначе
    /// вышли бы четыре красивые пустые комнаты, в которых отказ —
    /// главный продукт демо — не показался бы ни в одной.
    ///
    /// Сборка идемпотентна: повторный запуск даёт те же сцены.
    /// </summary>
    public static class DemoSceneBuilder
    {
        private const string SceneDir = "Assets/Scenes";

        /// <summary>Туман из docs/00-GDD.md §9: плотность 0.04, цвет #1A1A1A.</summary>
        // Камера стоит в двадцати двух метрах и смотрит почти отвесно:
        // туман на таком расстоянии красит не даль, а весь кадр разом.
        // При прежних 0,04 серым было больше половины каждой точки —
        // снимки показали среднюю яркость 0,11 при чёрном фоне 0,10,
        // то есть лагерь был почти неотличим от пустого экрана.
        private const float FogDensity = 0.018f;
        private static readonly Color FogColor = new Color32(0x1A, 0x1A, 0x1A, 0xFF);

        // ---------- меню ----------

        [MenuItem("Sinbinder/Собрать сцены демо")]
        public static void BuildAll()
        {
            if (!ConfirmDiscard()) return;

            BuildCamp();
            BuildCryptEntrance();

            RetireRaid();
            StartFromCamp();

            AssetDatabase.SaveAssets();
            Debug.Log("[СЦЕНЫ] Готово: собраны все сцены демо.");
        }

        /// <summary>Точка входа для пакетного режима (-executeMethod).</summary>
        public static void BuildAllBatch() => BuildAll();

        /// <summary>
        /// Пересборка выбрасывает открытую сцену. Спрашиваем, пока есть
        /// у кого спрашивать: в пакетном режиме диалог показать некому.
        /// </summary>
        private static bool ConfirmDiscard()
        {
            if (Application.isBatchMode) return true;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return true;

            Debug.Log("[СЦЕНЫ] Сборка отменена: несохранённая сцена оставлена как есть.");
            return false;
        }

        // ---------- сцены ----------

        /// <summary>Доля 1: пробуждение в лагере. Восемь фигур у огня.</summary>
        private static void BuildCamp()
        {
            var scene = NewScene();
            Atmosphere(warm: true);
            // Земля вдвое шире прежней. С туманом войны её край стал
            // виден: под туманом разведанная земля светлее чёрного фона
            // за нею, и на снимке набега 24 сентября весь кадр обводила
            // ровная рамка — граница плоскости, а не тумана. Камера
            // стоит в двадцати двух метрах и видит дальше сорока.
            Ground("Земля", 9f, "Ground048");
            Look();
            Managers();

            // Доли 0 и 3 живут только здесь: строка открывает пролог,
            // совет собирается один раз и переносится дальше составом отряда.
            var canvas = Interface();
            BuildStartPanel(canvas);
            BuildCouncil(canvas);
            BuildTitle(canvas);

            // Камера стоит у палатки на возвышенности и смотрит вниз,
            // на лагерь. Прежняя постановка — «ниже роста фигур, силуэты
            // нависают» — считалась под бестелесного Греховода, у которого
            // не было кадра, где он есть. Теперь у него тело, и один кадр
            // должен показать и его самого, и отряд, которым он командует
            // (docs/09-PROLOGUE.md §3 и §4, сцена 1).
            var hill = new Vector3(0f, 0f, -12f);
            var eye = new Vector3(0f, 3.5f, -12.5f);
            var look = new Vector3(13f, 0f, 0f);

            // ── Раскладка лагеря (2 октября; автор: «Побудь немного дизайнером.
            // Расставь осмысленно объекты лагеря, потому что например сундук
            // находится в спуске с холма») ──
            //
            // Север — откуда придут охотники, юг — холм вожака. У подножия
            // холма — двор: спуск Греховода посередине, справа от спуска
            // стол совета под знаменем (там отряду объявляют решения), слева
            // склад — сундук Марги, бочки, ящики (добыча — под присмотром
            // вожака, на ровной земле). Впереди очаг, открытый к спуску:
            // вожак сходит прямо в круг. Дороги свободны: на восток — к кругу
            // выхода, на север — к дозору. План сверху до и после —
            // Docs/Образцы/план (Editor/CampPlan).
            //
            // До этого сундук стоял на самом спуске, бочка и два ящика — внутри
            // спуска и холма, ещё два ящика, бочка и два валуна — под палатками:
            // всё ставилось формулами, которые друг о друге не знали.
            //
            // Стол стоит в стороне от того, куда смотрит открывающий кадр.
            // Взгляд с холма ложится у костра, и стол, поставленный туда же,
            // открыл бы совет на первом же кадре — игрок бы не подошёл
            // к нему, а оказался. Сходится это или нет, считает CheckCamp,
            // а не глаз.
            var table = new Vector3(4.4f, 0f, -2.9f);

            Hill(hill, radius: 5f, height: 2.6f);

            // Подвижная: к столу игрок обязан подойти сам (§4, сцена 2).
            // Со статичной камерой стол просто стоял в кадре.
            CameraRig(eye, look, movable: true);

            var campfire = Campfire(Vector3.zero);
            campfire.AddComponent<PrologueCampSpawner>();

            // Первые полминуты: провожатый напрашивается в спутники,
            // Карган предупреждает о долге. Одно даёт игроку кому
            // приказывать до совета, другое — увидеть причину будущего
            // отказа заранее (00-GDD.md §8, третье требование).
            campfire.AddComponent<CampOpening>();

            // Повод отдать полдюжины приказов до того, как отказ начнёт
            // стоить крови: Карган просит собрать отряд к столу. Совет
            // за сбором не заперт — это повод, а не ворота.
            campfire.AddComponent<CampMuster>();

            Tents(hill, hillRadius: 5f);
            CouncilTable(table, yaw: TowardFire(table));
            CampClutter(table);

            // Стол с картами (автор, 2 октября): у огня, но не в кругу —
            // на свету костра, к северо-западу, между очагом и палатками.
            CardTableProp(new Vector3(-1.0f, 0f, 3.9f));

            // Сцена 3, первая половина: сундук с трофеями Марги. Стоит
            // по другую сторону спуска, чем стол, — чтобы к нему пришлось
            // идти отдельно, а не задеть взглядом заодно с советом.
            // Передом к огню. Сходится это или нет, считает CheckCamp.
            var chest = new Vector3(-4.0f, 0f, -3.2f);
            TrophyChestProp(chest, yaw: TowardFire(chest));

            CheckCamp(table, chest, eye, look, TentPlaces(hill, hillRadius: 5f));

            // Врагов в лагере нет: выступаем, когда назначен старший.
            // Здесь же пролог начинается — забываем прошлый отряд.
            // Дальше — разгром, событие этой же сцены (RaidEvent): своей
            // сцены у него нет, ведущий разворачивает его на месте.
            Director(RaidEvent.SceneName, waitForBattle: false, startsPrologue: true);

            Save(scene, "Prologue_Camp");
        }

        /// <summary>Бой у входа в склеп — чужого, найденного, а не родового.</summary>
        private static void BuildCryptEntrance()
        {
            var scene = NewScene();
            Atmosphere(warm: false);
            Ground("Камень", 9f, "PavingStones127");
            Look();
            Managers();

            var canvas = Interface();
            BuildSalary(canvas, askOnArrival: true);
            BuildDemoEnd(canvas);
            // Заставка говорит, зачем здесь склеп. Прежняя — «Кто-то уже занял
            // этот склеп» — осталась от боя у входа, вырезанного со сценой 7:
            // обещала хозяев, а в склепе пусто (автор, 10 октября: «фразы
            // нередко бессмысленные»).
            BuildTitle(canvas, Sinbinder.Core.Loc.N("Склеп давно заброшен. Здесь можно укрыться от Охотников."));

            // Подвижная, как и в трёх других сценах: управление, которое
            // работает везде кроме одного места, читается как поломка,
            // а не как замысел.
            CameraRig(new Vector3(0f, 4.5f, -10f), new Vector3(22f, 0f, 0f),
                      movable: true);

            CryptGate(new Vector3(0f, 0f, 8f));
            CryptHall();

            // Мастерская связывания (docs/37-DEMO.md §4, шаг 2): автор —
            // «в самом склепе создание воина». У левой стены, лицом к залу:
            // полка и стол тел — к стене, место поднятого — к середине.
            // Полка не засевает чужих душ: здесь связывают собранных в набеге.
            // Пока за выключателем «связывание» — спрятана (CryptWorkshop).
            var zone = BindingZone(new Vector3(-8f, 0f, 0f), seed: false);
            zone.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            var workshop = new GameObject("Мастерская");
            Wire(workshop.AddComponent<Sinbinder.Crypt.CryptWorkshop>(), ("_zone", zone));

            // Карта вылазок (docs/39-PLACES.md §2): у правой стены, напротив
            // мастерской, — тот же стол с шаром, что на полигоне. Автор:
            // «было бы хорошо, если бы была миссия с караваном» — обоз
            // на ней есть. Пока за выключателем «вылазки» — спрятана (CryptMap).
            var map = MapZone(new Vector3(8f, 0f, 0f), canvas);
            var mapKeeper = new GameObject("Карта");
            Wire(mapKeeper.AddComponent<Sinbinder.Crypt.CryptMap>(), ("_zone", map));

            var squad = new GameObject("Отряд");
            squad.transform.position = new Vector3(0f, 0f, -2f);
            squad.AddComponent<PrologueCampSpawner>();

            // Сперва потеря, потом дело (docs/37-DEMO.md §0): кто-то из отряда
            // говорит, что Каргана нет, и только потом — «нас мало», связывание
            // и плата. Плата и ведущий ждут, пока склеп встречает.
            new GameObject("Прибытие").AddComponent<CryptArrival>();

            // Боя здесь больше нет: сцена 7 сценария вырезана вместе
            // со сценой 6. Склеп остался ради того единственного, ради чего
            // он в демо и был, — эпилога: игрок входит, и следом входит
            // отряд, отправленный полчаса назад.
            //
            // Врагов нет, значит и ждать конца боя нечего: доля кончается
            // по времени, и следом показывается эпилог — кто вернулся.
            Director(null, waitForBattle: false, endsAfterSeconds: 14f,
                arrivalLine: Sinbinder.Core.Loc.N("В глубине зала — пустой трон и алтарь, а в нише замурован гроб."));

            Save(scene, "Crypt_Entrance");
        }

        /// <summary>
        /// Тренировочная площадка склепа — четвёртая зона хаба.
        ///
        /// Строится отдельным пунктом меню и в отдельную сцену: демо-пролог
        /// от неё не зависит и не ломается, пока площадка настраивается.
        ///
        /// Смысл зоны — <b>повторяемый опыт</b>. Игрок ставит условия
        /// рычагами, отдаёт приказ, читает объяснение, меняет ровно одно
        /// и повторяет. Это возможно только потому, что движок повторяем:
        /// правило «одинаковый вход даёт одинаковый выход» здесь
        /// перестаёт быть требованием к коду и становится механикой.
        ///
        /// Рычаги расставлены по одному на голос, а не по одному
        /// на предмет: площадка — это схема души, разложенная по комнате.
        /// </summary>
        [MenuItem("Sinbinder/Собрать полигон")]
        public static void BuildTestChamberScene()
        {
            var scene = NewScene();
            Atmosphere(warm: false);
            Ground("Плиты", 3f, "PavingStones127");
            Look();
            Managers();

            var canvas = Interface();
            BuildStartPanel(canvas);
            BuildTitle(canvas, Sinbinder.Core.Loc.N("Полигон. Поставьте условие — и повторите."));

            // Склеп начинается от первого лица: здесь ходят между зонами
            // и читают таблички, а тактический вид годится для поля,
            // а не для комнаты. Переключается на V — и вместе с видом
            // меняются руки: мышь вертит голову или водит курсором.
            CameraRig(new Vector3(0f, 3f, -8f), new Vector3(12f, 0f, 0f), movable: true,
                      view: RTS_Camera.CameraView.FirstPerson);

            // Площадка и её точки. Позиции жёсткие: повтор обязан ставить
            // всё туда же, иначе опыт не опыт.
            var chamber = new GameObject("Полигон");
            chamber.transform.position = Vector3.zero;

            var subject = Spot(chamber.transform, "Место подопытного", new Vector3(0f, 0f, 2f));
            var prop = Spot(chamber.transform, "Место предмета", new Vector3(3f, 0f, 3.5f));
            var foes = Spot(chamber.transform, "Место чужих", new Vector3(0f, 0f, 6f));

            var test = chamber.AddComponent<Sinbinder.Crypt.TestChamber>();
            Wire(test, ("_subjectSpot", subject), ("_propSpot", prop), ("_enemySpot", foes));

            // Рычаги вдоль стены, лицом к площадке. Первым — повтор:
            // он главный, и стоять он должен там, куда игрок смотрит,
            // вернувшись от подопытного.
            Lever(chamber.transform, test, Sinbinder.Crypt.TestLever.LeverKind.Repeat,
                  default, new Vector3(-5f, 0f, 0f));

            float x = -3f;
            foreach (Sinbinder.Crypt.Trial trial in
                     System.Enum.GetValues(typeof(Sinbinder.Crypt.Trial)))
            {
                Lever(chamber.transform, test,
                      Sinbinder.Crypt.TestLever.LeverKind.Condition, trial,
                      new Vector3(x, 0f, 0f));
                x += 1.6f;
            }

            Lever(chamber.transform, test,
                  Sinbinder.Crypt.TestLever.LeverKind.NextSubject,
                  default, new Vector3(x + 0.8f, 0f, 0f));

            BindingZone(new Vector3(-9f, 0f, 2f));
            MapZone(new Vector3(9f, 0f, 2f), canvas);
            UpgradeZone(new Vector3(0f, 0f, 10f));

            // Отряд: те самые девять душ лагеря. Полигон нарочно берёт
            // измеренных стендом, а не выдуманных — иначе опыт не с чем
            // сверять, и «он отказал» останется впечатлением.
            var squad = new GameObject("Отряд");
            squad.transform.position = new Vector3(0f, 0f, -6f);
            squad.AddComponent<PrologueCampSpawner>();

            // Тело Греховода: без него «подойти к рычагу» снова означало бы
            // «навести взгляд», а этот урок проекту уже дорого обошёлся.
            var spawn = new GameObject("Появление Греховода");
            spawn.transform.position = new Vector3(-4f, 0f, -3f);
            spawn.AddComponent<Sinbinder.Crypt.TestChamberEntry>();

            Save(scene, "Crypt_Test");
        }

        /// <summary>
        /// Зона первая: устройство связывания, полка с банками, стол тел.
        ///
        /// Три предмета и дорога между ними. Дорога здесь и есть механика:
        /// связывание, которое делается не сходя с места, — это меню,
        /// из которого его и вынимали. Игрок берёт банку с полки, несёт
        /// её к гнезду, возвращается за телом, кладёт, дёргает рычаг.
        ///
        /// И на этой дороге он может ошибиться: истлевшую душу голем
        /// не примет, и узнает игрок об этом у гнезда, а не из серой
        /// строки списка.
        /// </summary>
        private static GameObject BindingZone(Vector3 origin, bool seed = true)
        {
            var zone = new GameObject("Связывание");
            zone.transform.position = origin;

            // --- устройство ---
            var altar = new GameObject("Устройство");
            altar.transform.SetParent(zone.transform);
            altar.transform.position = origin;

            if (Prop("BindingDevice", altar.transform, origin) == null)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Устройство (примитив)";
                cube.transform.SetParent(altar.transform);
                cube.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                cube.transform.localScale = new Vector3(2.2f, 1f, 1.2f);
            }

            var rise = Spot(zone.transform, "Место поднятого", new Vector3(0f, 0f, -2.5f));

            var device = altar.AddComponent<Sinbinder.Crypt.BindingDevice>();
            Wire(device, ("_riseSpot", rise));

            Socket(zone.transform, device, Sinbinder.Crypt.BindingSocket.Slot.Soul,
                   new Vector3(-0.7f, 1.1f, 0f));
            Socket(zone.transform, device, Sinbinder.Crypt.BindingSocket.Slot.Shell,
                   new Vector3(0.7f, 1.1f, 0f));

            var handleGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handleGo.name = "Рычаг связывания";
            handleGo.transform.SetParent(zone.transform);
            handleGo.transform.localPosition = new Vector3(1.6f, 0.8f, 0f);
            handleGo.transform.localScale = new Vector3(0.14f, 0.8f, 0.14f);
            handleGo.AddComponent<Sinbinder.Crypt.BindingHandle>().Set(device);

            // --- полка с банками ---
            var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shelf.name = "Полка";
            shelf.transform.SetParent(zone.transform);
            shelf.transform.localPosition = new Vector3(-3.2f, 0.5f, 1.6f);
            shelf.transform.localScale = new Vector3(4.4f, 1f, 0.6f);

            // Банки расставляет сама полка: она вид на список жатвы,
            // а не собственный запас душ.
            var jars = new GameObject("Банки");
            jars.transform.SetParent(zone.transform);
            jars.transform.localPosition = new Vector3(-4.8f, 1f, 1.6f);
            var shelfOf = jars.AddComponent<Sinbinder.Crypt.SoulShelf>();

            // Засев — удобство полигона: там никто не умирал. В склепе демо
            // полка показывает только собранное игроком.
            if (!seed)
            {
                var so = new SerializedObject(shelfOf);
                so.FindProperty("_seedWhenEmpty").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // --- стол с телами ---
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Стол тел";
            table.transform.SetParent(zone.transform);
            table.transform.localPosition = new Vector3(3.2f, 0.5f, 1.6f);
            table.transform.localScale = new Vector3(3.6f, 1f, 0.9f);

            float x = 2f;
            foreach (Sinbinder.Core.ShellType type in
                     System.Enum.GetValues(typeof(Sinbinder.Core.ShellType)))
            {
                // Живое тело на стол не кладут: связывают мёртвых.
                if (!Sinbinder.Core.ShellKinds.Bindable(type)) continue;

                var stand = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                stand.name = $"Тело — {type}";
                stand.transform.SetParent(zone.transform);
                stand.transform.localPosition = new Vector3(x, 1.25f, 1.6f);
                stand.transform.localScale = new Vector3(0.3f, 0.25f, 0.3f);

                stand.AddComponent<Sinbinder.Crypt.ShellStand>().Set(type);

                Plate(stand.transform, CryptHandsName(type), 1.8f);
                x += 0.9f;
            }

            return zone;
        }

        /// <summary>
        /// Зона третья: шар с картой вылазок.
        ///
        /// Стол и шар те же, что в лагере, — и это не экономия, а правило:
        /// предмет, который в одном месте открывает совет, а в другом
        /// выглядит иначе, игрок считает двумя разными предметами.
        ///
        /// Карта открывается подходом к шару, как и совет: подойти
        /// значит дойти ногами.
        /// </summary>
        private static GameObject MapZone(Vector3 origin, Transform canvas)
        {
            var zone = new GameObject("Карта вылазок");
            zone.transform.position = origin;

            var table = CouncilTable(origin, Sinbinder.Core.Loc.N("Карта вылазок"));
            table.transform.SetParent(zone.transform, true);

            // Доска вылазок — счёт, а не предмет: ей незачем стоять
            // на видном месте, но она обязана быть в сцене.
            var board = new GameObject("Вылазки");
            board.transform.position = origin;
            board.transform.SetParent(zone.transform, true);
            board.AddComponent<Sinbinder.Crypt.MissionBoard>();

            BuildMissionMap(canvas);
            return zone;
        }

        /// <summary>
        /// Зона вторая: два гнезда под улучшения склепа.
        ///
        /// Пустых, и это главное. Гнездо — честное обещание: место есть,
        /// а принести туда что-то можно только с вылазки. Комната без
        /// гнёзд не обещала бы ничего; комната с десятью обещала бы
        /// строительство, которого не будет.
        ///
        /// Ровно два, и оба стоят на виду: игрок обязан с первого взгляда
        /// понимать, что это предел, а не начало ветки.
        /// </summary>
        private static void UpgradeZone(Vector3 origin)
        {
            var zone = new GameObject("Гнёзда");
            zone.transform.position = origin;

            for (int i = 0; i < Sinbinder.Crypt.CryptUpgrades.Limit; i++)
            {
                var plinth = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plinth.name = $"Гнездо {i + 1}";
                plinth.transform.SetParent(zone.transform);
                plinth.transform.localPosition = new Vector3(i * 3f - 1.5f, 0.5f, 0f);
                plinth.transform.localScale = new Vector3(1.1f, 1f, 1.1f);

                plinth.AddComponent<Sinbinder.Crypt.UpgradeSocket>().SetEmpty();

                Plate(plinth.transform, Sinbinder.Core.Loc.N("Пустое гнездо"), 1.6f);
            }
        }

        /// <summary>Панель карты. Строится как совет и по тем же правилам.</summary>
        private static void BuildMissionMap(Transform parent)
        {
            var panel = Panel("Карта вылазок", parent,
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f), size: new Vector2(1000f, 640f),
                position: Vector2.zero);

            var backdrop = Backdrop(panel, Weight.Screen);

            var title = Label("Заголовок", panel, 30, TextAnchor.UpperLeft,
                new Vector2(0f, -20f), 56f);

            var rows = Panel("Точки", panel,
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0f, 1f), size: new Vector2(0f, 550f),
                position: new Vector2(0f, -86f));
            rows.offsetMin = new Vector2(24f, rows.offsetMin.y);
            rows.offsetMax = new Vector2(-24f, rows.offsetMax.y);

            // На Canvas, а не на панель, которую сам выключает: у
            // выключенного объекта не крутится Update, и подход к шару
            // остался бы незамеченным. Совет на этом уже спотыкался.
            var ui = parent.gameObject.AddComponent<Sinbinder.UI.MissionMapUI>();
            Wire(ui, ("_panel", panel.gameObject), ("_title", title), ("_rows", rows),
                     ("_font", UIFont()));
        }

        /// <summary>Гнездо устройства.</summary>
        private static void Socket(Transform parent, Sinbinder.Crypt.BindingDevice device,
            Sinbinder.Crypt.BindingSocket.Slot slot, Vector3 local)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = slot == Sinbinder.Crypt.BindingSocket.Slot.Soul
                ? "Гнездо души" : "Ложе тела";
            go.transform.SetParent(parent);
            go.transform.localPosition = local;
            go.transform.localScale = new Vector3(0.34f, 0.08f, 0.34f);

            go.AddComponent<Sinbinder.Crypt.BindingSocket>().Set(slot, device);

            Plate(go.transform, go.name, 2.6f);
        }

        /// <summary>Надпись в мире. Игрок читает её, подходя, и не уходит в интерфейс.</summary>
        private static void Plate(Transform parent, string text, float height)
        {
            var plate = new GameObject("Табличка");
            plate.transform.SetParent(parent);
            plate.transform.localPosition = new Vector3(0f, height, 0f);

            var mesh = plate.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.characterSize = 0.1f;
            mesh.fontSize = 64;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(0.86f, 0.84f, 0.78f);

            plate.AddComponent<Sinbinder.UI.Billboard>();

            // Молчит, пока на предмет не посмотрят. Комната из десятка
            // подписанных разом предметов — это стена текста, сквозь
            // которую не видно самой комнаты.
            plate.AddComponent<Sinbinder.UI.WorldPlate>();
        }

        /// <summary>Имя оболочки для таблички. Берётся оттуда же, откуда его берёт игра.</summary>
        private static string CryptHandsName(Sinbinder.Core.ShellType type)
            => Sinbinder.Crypt.CryptHands.ShellName(type);

        /// <summary>Пустая точка-ориентир. Позиция жёсткая и видна в сцене.</summary>
        private static Transform Spot(Transform parent, string name, Vector3 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = local;
            return go.transform;
        }

        /// <summary>Рычаг с табличкой. Надпись берётся из каталога, а не пишется тут.</summary>
        private static void Lever(Transform parent, Sinbinder.Crypt.TestChamber chamber,
            Sinbinder.Crypt.TestLever.LeverKind kind, Sinbinder.Crypt.Trial trial,
            Vector3 local)
        {
            var stand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stand.name = "Рычаг";
            stand.transform.SetParent(parent);
            stand.transform.localPosition = local + new Vector3(0f, 0.6f, 0f);
            stand.transform.localScale = new Vector3(0.18f, 0.6f, 0.18f);

            var lever = stand.AddComponent<Sinbinder.Crypt.TestLever>();
            lever.Set(kind, trial, chamber);

            // Табличка стоит в мире, а не в интерфейсе: игрок читает её,
            // подходя, и не отрывается от комнаты.
            var plate = new GameObject("Табличка");
            plate.transform.SetParent(stand.transform);
            plate.transform.localPosition = new Vector3(0f, 1.5f, 0f);

            var text = plate.AddComponent<TextMesh>();
            text.text = lever.Label;
            text.characterSize = 0.12f;
            text.fontSize = 64;
            text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.86f, 0.84f, 0.78f);

            plate.AddComponent<Sinbinder.UI.Billboard>();

            // Молчит, пока на предмет не посмотрят. Комната из десятка
            // подписанных разом предметов — это стена текста, сквозь
            // которую не видно самой комнаты.
            plate.AddComponent<Sinbinder.UI.WorldPlate>();
        }

        // ---------- общий каркас ----------

        private static UnityEngine.SceneManagement.Scene NewScene()
            => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        /// <summary>
        /// Холод и туман. Лагерь обязан остаться единственным тёплым светом
        /// во всём демо (пролог §4.6), и достигается это не яркостью костра,
        /// а темнотой вокруг него.
        /// </summary>
        private static void Atmosphere(bool warm)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = FogDensity;
            RenderSettings.fogColor = FogColor;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color32(0x1C, 0x1C, 0x21, 0xFF);

            // Слабый холодный ключевой свет, чтобы геометрия читалась
            // и без костра. Он же — единственный источник в сценах без лагеря.
            // Низко над горизонтом: «длинные тени, мало источников света»
            // (00-GDD.md §9). Под прежними сорока восемью градусами тень
            // от палатки была короче самой палатки, и лагерь читался
            // плоским — при камере, которая и так смотрит почти отвесно.
            var key = new GameObject("Холодный свет");
            key.transform.rotation = Quaternion.Euler(20f, 152f, 0f);
            var l = key.AddComponent<Light>();
            l.type = LightType.Directional;

            // Холоднее прежнего: стиль держится на встрече тёплого огня
            // с холодным окружением, и обе стороны обязаны быть видны.
            l.color = new Color(0.55f, 0.63f, 0.86f);
            // Ночь остаётся ночью, но воин обязан читаться на земле:
            // до правки его силуэт отличался от грунта на три сотых
            // яркости, и на показе зритель увидел бы чёрный прямоугольник.
            // Яркость поднята вместе с наклоном, а не вместо него. Свет,
            // падающий под двадцатью градусами, кладёт на землю вдвое
            // меньше, чем под сорока восемью (синус против синуса), и
            // прежние 0,42 давали чёрный экран: замер по снимку — 0,096
            // средней яркости при чёрном фоне 0,10. Тени остались
            // длинными, лагерь снова виден.
            l.intensity = warm ? 0.92f : 1.15f;
            l.shadows = LightShadows.Soft;
        }

        private static void Ground(string name, float scale, string soil = null)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = name;
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(scale, 1f, scale);

            // Поверхность земли занимает большую часть кадра: камера стоит
            // почти отвесно. До 18 сентября тридцать девять скачанных
            // текстур лежали в проекте мёртвым грузом, а земля была
            // одноцветной плоскостью.
            if (!string.IsNullOrEmpty(soil))
            {
                var material = MaterialBuilder.Get(soil);
                var renderer = ground.GetComponent<Renderer>();

                if (material != null && renderer != null) renderer.sharedMaterial = material;
                else Debug.LogWarning($"[СЦЕНЫ] Материала {soil} нет — земля останется "
                                    + "одноцветной. Соберите: Sinbinder → Собрать материалы.");
            }

            // Поверхность навигации. Без неё агент — мёртвый груз:
            // SetDestination не находит, куда идти, и воин стоит. Навмеша
            // в собранных сценах не было вовсе, поэтому в демо не двигался
            // никто — охотники не доходили до лагеря, а на побеге до края
            // карты было некого доводить, и сцена вставала намертво.
            //
            // Печём при запуске, а не храним ассетом: хранимые данные надо
            // не забыть пересобрать после каждой правки геометрии, а забыть
            // это ровно тот вид ошибки, который здесь ловят всем проектом —
            // молчаливый. Земля плоская, выпечка стоит доли секунды.
            var surface = ground.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
            ground.AddComponent<GroundNavMesh>();
        }

        /// <summary>
        /// Объект обязан называться ровно «Managers»: AOSSceneSetup ищет его
        /// по имени и без него не вешает AOSEventHub — а без хаба отказ
        /// не поднимет ни журнал, ни микропаузу (11-MISSING §2.3).
        /// </summary>
        private static void Managers()
        {
            var managers = new GameObject("Managers");
            managers.AddComponent<CombatManager>();
            managers.AddComponent<SelectionManager>();
            managers.AddComponent<AOS.AOSSceneSetup>();

            // Пауза нужна и совету доли 3, и строке доли 0: без неё панель
            // висит, а бой под ней продолжается.
            managers.AddComponent<Core.GamePauseController>();

            // Ступень прозрачности. Без него лестница из 00-GDD.md §7
            // остаётся документом: все четыре ступени были бы включены
            // всегда, и трассировка с очками и весами сыпалась бы игроку.
            managers.AddComponent<Core.TransparencySettings>();

            // Режим партии. Без него Commitment существовал бы только
            // в замерах: включить его было бы нечем.
            managers.AddComponent<Core.GameModeSettings>();

            // Кошелёк и вещи игрока. Его не было ни в одной сцене, поэтому
            // PlayerInventory.Instance всегда был пуст: плата после боя
            // уходила в никуда, а трофеи было некуда класть.
            managers.AddComponent<Sinbinder.Inventory.PlayerInventory>();

            // Кто решает, чья надпись сейчас горит. Один на сцену:
            // луч пускается раз за кадр, а не по разу на предмет.
            managers.AddComponent<Sinbinder.UI.PlateSight>();

            // Кинематография разговора. Её не было ни в одной сцене:
            // DialogueUI искал контроллер и не находил, поэтому FocusOn
            // не вызывался никогда — наезда на говорящего не случалось
            // ни разу за всё время. В тактическом виде это было почти
            // незаметно (камера и так стоит), а от первого лица бросается
            // в глаза: взгляд остаётся приклеенным к глазам героя.
            managers.AddComponent<Sinbinder.Dialogue.DialogueCameraController>();

            // Церемония титула. Найдена смотром сцен: TitleCeremony —
            // статический класс, он искал этот компонент и не находил,
            // поэтому титулы присуждались молча, без единой сцены
            // за всё время. Тот же разрыв, что был у камеры разговора.
            managers.AddComponent<AOS.TitleCeremonyBehaviour>();

            // Угасающие души. Его не было ни в одной сцене, поэтому
            // жатва — механика сцены 4 — не работала вовсе.
            managers.AddComponent<SoulManager>();

            // Осматривает сцену на запуске и называет недостающие звенья.
            // Заведён потому, что пять разрывов подряд нашлись вручную
            // и по одному — а искать их надо не наугад.
            managers.AddComponent<SceneDoctor>();

            // Доля 6: полторы секунды тишины на первом отказе.
            managers.AddComponent<Sinbinder.UI.RefusalSilence>();

            // Наезд на того, кто решил сам. Без него уникальные моменты
            // движка проходят мимо игрока молча — автор заметил убегающего
            // воина краем глаза, и это был единственный способ их заметить.
            managers.AddComponent<MomentCamera>();

            // Быстрое сохранение и загрузка. Без него закрыть игру
            // и продолжить завтра нечем ни в одном режиме.
            managers.AddComponent<Sinbinder.UI.QuickSave>();

            // Разговор при встрече. Ссылку на базу ставим здесь, а не
            // оставляем загрузчику: так видно в инспекторе, откуда берутся
            // реплики. Базы ещё нет — Wire промолчит, и DialogueLoader
            // подхватит её из Resources сам.
            var trigger = managers.AddComponent<Sinbinder.Dialogue.DialogueTrigger>();
            Wire(trigger, ("_dialogueDatabase",
                AssetDatabase.LoadAssetAtPath<Sinbinder.Dialogue.DialogueDatabase>(
                    "Assets/Resources/DialogueDatabase.asset")));
        }

        /// <summary>
        /// Камера. В боевых долях — подвижная: RTS_Camera лежит в проекте
        /// и не была подключена ни к одной сцене, то есть игрок не мог
        /// отвести взгляд от точки, куда его поставили. Для доли 5 это
        /// не мелочь: край карты, до которого надо довести отряд, стоит
        /// за спиной у неподвижной камеры.
        /// </summary>
        private static GameObject CameraRig(Vector3 position, Vector3 euler,
            bool movable = false,
            RTS_Camera.CameraView view = RTS_Camera.CameraView.Tactical)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(euler);

            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;

            // Без этой галочки профиль «Взгляд» не действует вовсе.
            // Цвет, виньетка, зерно и свечение огня лежали в проекте
            // с 18 сентября и не работали ни в одной сцене: URP
            // выключает постобработку у камеры по умолчанию, а включить
            // её забыли — и это не видно ниоткуда, кроме как глазами.
            var urp = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;
            urp.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing;
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;

            go.AddComponent<AudioListener>();

            if (movable) go.AddComponent<RTS_Camera>().SetView(view);

            return go;
        }

        /// <summary>
        /// Обстановка лагеря: брёвна у огня, склад у сундука, знамя
        /// над столом совета, валуны у подножия холма и на краю.
        ///
        /// Лагерь без этого — девять фигур и костёр посреди пустоты.
        /// Места названы числами, без жребия: одинаковый вход даёт
        /// одинаковый выход, и лагерь узнаётся со второго запуска.
        ///
        /// Каждая вещь стоит там, где ей есть дело (2 октября): до этого
        /// припасы ставились формулой вокруг костра и уходили под палатки
        /// и внутрь холма. Ни одна не стоит на спуске, в палатке или
        /// на дороге — к кругу выхода (восток) и к дозору (север).
        ///
        /// Никаких компонентов: это мир, а не предметы, с которыми
        /// говорят. Предмет, который обещает взаимодействие и молчит,
        /// хуже отсутствующего.
        /// </summary>
        private static void CampClutter(Vector3 table)
        {
            var clutter = new GameObject("Обстановка");

            // Брёвна вокруг костра: на них сидят, и они задают круг,
            // в котором стоит отряд. Круг открыт на юг — к спуску: вожак
            // сходит с холма прямо к огню, а не в спину сидящим. Прежние
            // два южных бревна лежали на самом спуске.
            foreach (float a in new[] { 40f, 130f, 190f, 350f })
                Prop("LogBench", clutter.transform, Ring(a, 2.6f), a + 90f);

            // Склад — слева от спуска, за сундуком Марги: бочки и ящики,
            // один ящик на другом. Добыча и припасы — в одном углу, у холма
            // вожака, а не по кругу лагеря.
            Prop("Barrel", clutter.transform, new Vector3(-5.8f, 0f, -3.9f), 20f);
            Prop("Barrel", clutter.transform, new Vector3(-5.1f, 0f, -5.0f), 75f);
            Prop("Crate", clutter.transform, new Vector3(-6.0f, 0f, -4.9f), 12f);
            Prop("Crate", clutter.transform, new Vector3(-6.0f, 0.6f, -4.9f), 35f);
            Prop("Crate", clutter.transform, new Vector3(-3.4f, 0f, -5.4f), -8f);

            // Знамя — за столом совета, со стороны холма: место, где отряду
            // объявляют решения, видно от огня.
            var banner = table + new Vector3(1.0f, 0f, -1.3f);
            Prop("Banner", clutter.transform, banner, TowardFire(banner));

            // Частокола больше нет. Автор, 26 сентября: «Забор в качестве
            // декораций это здорово, но если подумать логически — забор нужен
            // в полевом лагере?» Походный лагерь за ночь не обносят.

            // Валуны: два у подножия холма — холм из них и вырос, —
            // один на северо-западном краю, за палатками.
            Prop("Rock", clutter.transform, new Vector3(-5.7f, 0f, -10.4f), 20f, 1.2f);
            Prop("Rock", clutter.transform, new Vector3(6.0f, 0f, -10.9f), 140f, 0.9f);
            Prop("Rock", clutter.transform, new Vector3(-12.2f, 0f, 5.6f), 70f, 1.0f);
        }

        /// <summary>Поворот «передом к костру» для вещи в этой точке, в градусах.</summary>
        private static float TowardFire(Vector3 at) => Mathf.Atan2(-at.x, -at.z) * Mathf.Rad2Deg;

        /// <summary>
        /// Стол с картами. Автор, 2 октября: «не хватает стола с картами, чтобы
        /// воины хоть как-то развлекались, и возможность к ним подсесть».
        ///
        /// Ящик вместо стола — в походе скатертей нет; вокруг две бочки
        /// и ящик поменьше — сиденья; на ящике колода, раскрытые карты
        /// и горсть монет — кон; свеча в фонаре: в темноте не сыграешь.
        /// Сторона к огню открыта — оттуда подсаживается Греховод.
        /// Кто играет — решают души (<see cref="Sinbinder.Gameplay.CardTable"/>).
        /// </summary>
        private static void CardTableProp(Vector3 position)
        {
            float yaw = TowardFire(position);
            var root = new GameObject("Стол с картами");
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var crate = Prop("Crate", root.transform, position, yaw + 8f);
            Prop("Barrel", root.transform, root.transform.TransformPoint(new Vector3(-0.95f, 0f, 0.1f)), yaw + 30f);
            Prop("Barrel", root.transform, root.transform.TransformPoint(new Vector3(0.95f, 0f, 0.1f)), yaw - 20f);
            Prop("Crate", root.transform, root.transform.TransformPoint(new Vector3(0f, 0f, -0.95f)), yaw + 45f, 0.8f);

            // Верх ящика — по самой модели: карты должны лечь, а не висеть.
            float top = 0.6f;
            if (crate != null)
            {
                float max = float.MinValue;
                foreach (var r in crate.GetComponentsInChildren<Renderer>()) max = Mathf.Max(max, r.bounds.max.y);
                if (max > float.MinValue) top = max - position.y;
            }

            var card = Plain("Карты", new Color(0.86f, 0.81f, 0.70f), smoothness: 0.2f);
            var coin = Plain("Монеты", new Color(0.83f, 0.66f, 0.28f), smoothness: 0.75f);

            Piece(root.transform, "Колода", card, new Vector3(0.04f, top + 0.012f, 0.02f),
                  new Vector3(0.065f, 0.024f, 0.095f), 8f);
            for (int i = 0; i < 4; i++)
                Piece(root.transform, "Карта", card, new Vector3(-0.20f + i * 0.045f, top + 0.002f, 0.13f),
                      new Vector3(0.065f, 0.003f, 0.095f), -20f + i * 12f);
            for (int i = 0; i < 3; i++)
                Piece(root.transform, "Карта", card, new Vector3(0.16f + i * 0.04f, top + 0.002f, -0.12f),
                      new Vector3(0.065f, 0.003f, 0.095f), 160f + i * 14f);

            for (int i = 0; i < 5; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                c.name = "Монета";
                c.transform.SetParent(root.transform, false);
                c.transform.localPosition = new Vector3(-0.05f + (i % 3) * 0.03f, top + 0.003f + (i / 3) * 0.006f,
                                                        -0.02f + (i % 2) * 0.02f);
                c.transform.localScale = new Vector3(0.03f, 0.003f, 0.03f);
                Object.DestroyImmediate(c.GetComponent<Collider>());
                var r = c.GetComponent<Renderer>();
                r.sharedMaterial = coin;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Lantern(root.transform, new Vector3(0.24f, top + 0.09f, 0.22f), range: 2.8f, intensity: 0.9f, shadows: false);

            root.AddComponent<Sinbinder.Gameplay.CardTable>();
            Plate(root.transform, Sinbinder.Core.Loc.N("Карты — F"), 1.5f);
        }

        /// <summary>Плоская мелочь на столе: карта, колода. Без коллайдера и тени.</summary>
        private static void Piece(Transform parent, string name, Material material, Vector3 at, Vector3 size, float yaw)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Точка на круге вокруг костра. Угол в градусах.</summary>
        private static Vector3 Ring(float degrees, float radius)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
        }

        /// <summary>
        /// Общий взгляд сцены: цвет, виньетка, зерно, свечение огня.
        ///
        /// Один профиль на все сцены (<c>Assets/Settings/Взгляд.asset</c>) —
        /// иначе лагерь, набег и склеп разъедутся по тону, и это будет
        /// видно как разные игры, склеенные вместе.
        /// </summary>
        private static void Look()
        {
            var profile = EffectsBuilder.Look();
            if (profile == null)
            {
                Debug.LogWarning("[СЦЕНЫ] Профиля взгляда нет — кадр останется "
                               + "плоским. Соберите: Sinbinder → Собрать эффекты.");
                return;
            }

            var go = new GameObject("Взгляд");
            var volume = go.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;
        }

        /// <summary>
        /// Искры над огнём: угли поднимаются и гаснут.
        ///
        /// Костёр из поленьев и света — предмет; костёр, от которого летят
        /// искры, — огонь. Разница стоит одной системы частиц и делает
        /// кадр живым, а не собранным.
        /// </summary>
        private static void Embers(Transform parent, Vector3 position, float scale, float rate)
        {
            var material = EffectsBuilder.SparkOf();
            if (material == null) return;

            var go = new GameObject("Искры");
            go.transform.SetParent(parent);
            go.transform.position = position;

            var particles = go.AddComponent<ParticleSystem>();

            var main = particles.main;
            main.duration = 4f;
            main.loop = true;
            main.startLifetime = 1.6f * scale;
            main.startSpeed = 0.9f * scale;
            main.startSize = 0.07f * scale;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.62f, 0.22f), new Color(1f, 0.36f, 0.10f));
            main.gravityModifier = -0.06f;      // вверх: горячее поднимается
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = rate;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.22f * scale;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            // Гаснут, а не исчезают: искра, пропадающая целой, читается
            // как ошибка, а не как уголь.
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f),
                        new GradientColorKey(new Color(1f, 0.45f, 0.15f), 0.6f),
                        new GradientColorKey(new Color(0.35f, 0.10f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f),
                        new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);

            var shrink = particles.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.25f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static GameObject Campfire(Vector3 position)
        {
            var campfire = new GameObject("Костёр");
            campfire.transform.position = position;

            Prop("Campfire", campfire.transform, position);
            Embers(campfire.transform, position + new Vector3(0f, 0.35f, 0f), 1f, 26f);

            var light = new GameObject("Тёплый свет");
            light.transform.SetParent(campfire.transform);
            light.transform.localPosition = new Vector3(0f, 1.1f, 0f);

            var l = light.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.62f, 0.28f);
            // Ярче и шире прежних 3,2 и 14 м (41-SHOWCASE п. 2): на кадрах
            // 1 октября костёр был оранжевой точкой, свет не ложился ни на землю,
            // ни на палатки — встречи тёплого с холодным не было видно.
            l.intensity = 4.6f;
            l.range = 17f;
            l.shadows = LightShadows.Soft;

            // Живой огонь: дышит и пляшет, тени палаток шевелятся.
            light.AddComponent<Sinbinder.Gameplay.FireFlicker>().Tune(0.22f, 2.6f, 0.06f);

            return campfire;
        }

        /// <summary>
        /// Возвышенность, на которой стоит палатка Греховода. Сплющенный
        /// цилиндр: холм из одного примитива, зато лагерь виден сверху.
        /// </summary>
        private static void Hill(Vector3 position, float radius, float height)
        {
            var hill = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hill.name = "Возвышенность";
            hill.transform.position = position + new Vector3(0f, height * 0.5f, 0f);
            hill.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);

            // Земля, а не белый примитив: без материала холм светится
            // посреди ночного лагеря ярче костра — на снимке сцены он
            // читался куском чужой игры.
            // Той же землёй, что и всё вокруг: гравий светлее грунта,
            // и сверху холм читался бледным блином посреди лагеря,
            // а не возвышенностью. Форму ему теперь задаёт длинная тень.
            Cover(hill, "Ground048");

            // Палатка Греховода наверху, входом к лагерю: в ней он просыпается,
            // из неё и выходит (слово автора, 26 сентября). Крупнее прежней
            // в полтора раза: внутри — стол с картой, полка с банками, стойка
            // и постель, а между ними ему самому нужно пройти в рост. При
            // прежних 1,25 над головой хватало крыши на полосу в ладонь.
            Tent(position + new Vector3(0f, height, 0f), yaw: 0f, abandoned: false,
                name: "Палатка Греховода", size: 1.9f, master: true);

            Slope(position, radius, height);
        }

        /// <summary>
        /// Склон с холма к лагерю.
        ///
        /// Пока Греховод был камерой, холм мог быть барабаном с отвесными
        /// стенками: смотреть с него ничто не мешало. Теперь он с него
        /// сходит — а навмеш вертикальных стен не печёт, и без склона
        /// вершина оказалась бы отдельным островом. Игрок вышел бы
        /// из палатки и застрял в первой же сцене насмерть, ровно там,
        /// где демо начинается.
        ///
        /// Наклон держим заметно положе сорока пяти градусов — порога,
        /// выше которого навмеш поверхность отбрасывает. Запас нужен
        /// потому, что печётся всё в рантайме и проверить глазом
        /// перед запуском нечего.
        /// </summary>
        private static void Slope(Vector3 hillCentre, float radius, float height)
        {
            // Вниз — в сторону костра. Он стоит в начале координат
            // в обеих прологовых сценах.
            Vector3 down = Vector3.zero - hillCentre;
            down.y = 0f;

            if (down.sqrMagnitude < 0.01f) down = Vector3.forward;
            down.Normalize();

            // Разбег вдвое длиннее подъёма: около двадцати семи градусов.
            float run = height * 2f;

            Vector3 top = hillCentre + down * radius + new Vector3(0f, height, 0f);
            Vector3 foot = hillCentre + down * (radius + run);

            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Спуск";
            ramp.transform.position = (top + foot) * 0.5f;

            // Локальная ось Z вдоль склона, локальная Y — нормаль
            // поверхности. LookRotation сам разложит и то и другое,
            // поэтому ни знака угла, ни порядка Эйлера здесь знать
            // не нужно — а именно на них тут проще всего ошибиться.
            ramp.transform.rotation = Quaternion.LookRotation(foot - top, Vector3.up);
            ramp.transform.localScale = new Vector3(
                radius * 0.8f, 0.3f, Vector3.Distance(top, foot));

            Cover(ramp, "Ground048");
        }

        /// <summary>
        /// Лагерь: двадцать пять палаток вокруг костра, из них пять
        /// брошенных. Пустая палатка с колышком — вся предыстория, которая
        /// нужна: отряд нёс потери до того, как игрок проснулся
        /// (docs/09-PROLOGUE.md §4, сцена 1). Ни строчки объяснения.
        ///
        /// Раскладка по индексу, без Random: лагерь обязан выглядеть
        /// одинаково при каждом запуске, иначе игрок не узнает место
        /// на доле 4, а именно узнавание делает разгром разгромом.
        ///
        /// Место под холмом освобождается, и брошенные размечаются уже
        /// по выжившим местам. Иначе палатка уезжает внутрь возвышенности,
        /// а вместе с ней теряется и одна из пяти пустых — то есть
        /// пропадает ровно та деталь, ради которой всё это ставится.
        /// </summary>
        /// <summary>
        /// Где стоят палатки. Отдельно от их постройки, потому что знать
        /// это нужно не только им: стол совета обязан не влезть внутрь
        /// палатки, а проверить это можно только по тому же списку.
        /// Две расстановки разошлись бы молча.
        /// </summary>
        private static List<Vector3> TentPlaces(Vector3 hillCentre, float hillRadius)
        {
            const int total = 25;
            const int inner = 11;

            // Два метра, а не метр двадцать: при прежнем запасе у подножия
            // холма, на самом спуске, стояли две палатки внутреннего кольца —
            // почти невидимые с высоты и поперёк дороги Греховода (автор,
            // 26 сентября: «их почти не видно, но при этом они мешаются»).
            const float clearance = 2.0f;

            var places = new List<Vector3>();

            for (int i = 0; i < total; i++)
            {
                bool ring = i < inner;
                int index = ring ? i : i - inner;
                int count = ring ? inner : total - inner;

                float radius = ring ? 6.2f : 9.4f;
                float angle = (index / (float)count) * Mathf.PI * 2f
                            + (ring ? 0f : Mathf.PI / count);

                var position = new Vector3(Mathf.Sin(angle) * radius, 0f,
                                           Mathf.Cos(angle) * radius);

                // Под холмом палаток нет: там стоит одна, наверху.
                if (Vector3.Distance(position, hillCentre) < hillRadius + clearance)
                    continue;

                // И во дворе у подножия — тоже: там спуск вожака, стол
                // совета и склад (BuildCamp). Двор — между холмом и очагом,
                // по шесть с половиной метров в обе стороны от спуска.
                if (Mathf.Abs(position.x) < 6.5f && position.z < -2f && position.z > -6f)
                    continue;

                places.Add(position);
            }

            return places;
        }

        private static void Tents(Vector3 hillCentre, float hillRadius)
        {
            const int mourning = 5;

            if (Fallen.Count < mourning)
                Debug.LogWarning($"[СБОРКА] Павших названо {Fallen.Count}, "
                               + $"а пустых палаток {mourning}: имена на колышках "
                               + "пойдут по кругу.");

            var places = TentPlaces(hillCentre, hillRadius);

            var camp = new GameObject("Палатки");
            int fallen = 0;

            for (int i = 0; i < places.Count; i++)
            {
                // Пятеро павших, разведённых по кругу ровно: пустые не
                // должны сбиться в одну сторону, игрок обязан заметить их
                // не приглядываясь.
                bool abandoned = fallen < mourning
                              && i >= fallen * places.Count / mourning;
                if (abandoned) fallen++;

                var position = places[i];
                float yaw = Mathf.Atan2(position.x, position.z) * Mathf.Rad2Deg + 180f;

                var tent = Tent(position, yaw, abandoned,
                    abandoned ? $"Палатка павшего {fallen}" : $"Палатка {i + 1}", 1f,
                    abandoned ? Fallen.NameFor(fallen - 1) : "", index: i);
                tent.transform.SetParent(camp.transform);
            }

            if (fallen != mourning)
                Debug.LogWarning($"[СБОРКА] Пустых палаток {fallen}, а должно быть {mourning}.");
        }

        /// <summary>
        /// Палатка — модель из <c>Props/Tent</c>, а если модели нет — куб,
        /// повёрнутый на сорок пять градусов и наполовину ушедший в землю.
        ///
        /// <b>Корень — пустой объект</b> на земле посреди палатки, повёрнутый
        /// входом вперёд и без масштаба; модель, нутро и стены — его дети.
        /// Корнем палатку и ищут (спавнер Греховода, жизнь лагеря), и берут
        /// у него одно — место. До 27 сентября корнем была сама модель,
        /// а у неё в масштабе множитель единиц файла и поворот осей Blender:
        /// мерить в ней метры пола было нельзя.
        ///
        /// Брошенная просела и завалилась набок, и рядом торчит колышек.
        /// Разница делается формой, а не цветом. В брошенную не входят:
        /// нутро и пол — только у жилых (<see cref="Furnish"/>).
        /// </summary>
        private static GameObject Tent(Vector3 position, float yaw, bool abandoned,
            string name, float size, string fallenName = "", bool master = false, int index = 0)
        {
            var tent = new GameObject(name);
            tent.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            // Модель, если она есть; куб — если нет. Тот же уговор, что
            // и у тел (WarriorLook): сцена собирается в любом случае.
            var model = Prop("Tent", tent.transform, position, yaw, size);

            if (model != null)
            {
                // Брошенная просела и накренилась: пять таких по кругу —
                // вся предыстория, которая нужна (09-PROLOGUE.md §4).
                if (abandoned)
                {
                    // Приседает вдвое по высоте — но от того масштаба,
                    // который уже стоит, а не от единицы: в корне модели
                    // множитель единиц файла (см. ModelCheck).
                    // Приседает по локальной Z: оси у модели блендеровские,
                    // и высота у неё — Z, а не Y. Сжатая по Y палатка
                    // просто стала бы уже, а не ниже.
                    var was = model.transform.localScale;
                    model.transform.localScale = new Vector3(was.x, was.y, was.z * 0.62f);
                    model.transform.rotation = Quaternion.Euler(9f, yaw, 6f)
                                             * Axis(Resources.Load<GameObject>("Props/Tent"));
                }
                else
                {
                    Furnish(tent, model, master, index);
                }
            }
            else
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Заглушка";
                cube.transform.SetParent(tent.transform);
                cube.transform.position = position;

                float height = abandoned ? 0.55f : 1.0f;
                cube.transform.localScale = new Vector3(1.5f * size, 1.5f * size * height,
                                                        2.2f * size);
                cube.transform.rotation = Quaternion.Euler(abandoned ? 14f : 0f, yaw, 45f);
            }

            if (!abandoned) return tent;

            var pegAt = position + new Vector3(0.9f, 0f, 0.9f);
            var peg = Prop("TentPeg", tent.transform.parent, pegAt, yaw);

            if (peg == null)
            {
                peg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                peg.name = "Колышек";
                peg.transform.SetParent(tent.transform.parent);
                peg.transform.position = pegAt + new Vector3(0f, 0.35f, 0f);
                peg.transform.localScale = new Vector3(0.08f, 0.35f, 0.08f);
                peg.transform.rotation = Quaternion.Euler(9f, 0f, 5f);
            }

            PegName(tent.transform, pegAt + new Vector3(0f, 0.62f, 0f), fallenName);

            return tent;
        }

        /// <summary>
        /// Сколько крыши нужно над ногами Греховода, чтобы он прошёл в рост:
        /// сам он 1,6, и ещё ладонь, чтобы скат не чиркал по капюшону.
        /// </summary>
        private const float Headroom = 1.75f;

        /// <summary>
        /// Нутро жилой палатки: пол, стены и обстановка. Слово автора,
        /// 26 сентября: «чтобы в палатки можно было войти, осмотреться,
        /// увидеть внутренний интерьер».
        ///
        /// <b>Пол.</b> Навмеш печётся по видимой геометрии, а агент выпечки
        /// ростом два метра: под скатами ниже этого пола не оставалось вовсе.
        /// Поэтому сама модель в выпечку не идёт, а стены заданы невидимыми
        /// объёмами «не пройти» вдоль скатов и полога. Между ними — полоса,
        /// где над головой Греховода хватает крыши: в его палатке она
        /// в два метра шириной, в прочих — тропка по коньку, куда входят
        /// пригнувшись (первое лицо опускает взгляд под скат само,
        /// <see cref="RTS_Camera"/>).
        ///
        /// <b>Обстановка</b> — из того, что есть в <c>Resources/Props</c>,
        /// и из простых тел там, где предмета пока нет: постель, банки,
        /// фонарь. Их модели — задача сессии моделей (28-ORDERS, её пункт 6);
        /// места под них названы так, чтобы замена нашлась по имени.
        /// </summary>
        private static void Furnish(GameObject tent, GameObject model, bool master, int index)
        {
            var shape = Measure(tent.transform, model);
            if (!shape.HasValue)
            {
                Debug.LogWarning($"[СБОРКА] {tent.name}: форму палатки не снять с модели — "
                               + "нутра и пола у неё не будет.");
                return;
            }

            var (half, top, front, back) = shape.Value;

            var ignore = model.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
            ignore.ignoreFromBuild = true;

            // Ширина пола — там, где крыши хватает над головой. В рядовой
            // палатке её не хватает нигде, кроме линии конька: там тропка
            // в три клетки навмеша, уже не бывает.
            float path = Mathf.Max(0.25f, half * (1f - Headroom / top));
            float radius = UnityEngine.AI.NavMesh.GetSettingsByID(0).agentRadius;
            float inner = path + radius;

            // Снаружи стена — ровно по основанию ската, ни шагом дальше:
            // навмеш сам отступает от неё на радиус агента. Первый заход
            // (e04fbbb) вынес её на 0,35 за скат, и между палатками внутреннего
            // кольца закрылся проход к воротам — прогон, дословно: «до ворот
            // не дойти: Греховод, Косой Ждан, Брат Хальд…». Демо не проходилось.
            float outer = half + 0.05f;
            int blocked = UnityEngine.AI.NavMesh.GetAreaFromName("Not Walkable");

            foreach (float side in new[] { 1f, -1f })
                Wall(tent.transform, "Скат", blocked,
                     new Vector3(side * (inner + outer) * 0.5f, top * 0.5f, (front + back - 0.3f) * 0.5f),
                     new Vector3(outer - inner, top + 1f, front - back + 0.3f));

            Wall(tent.transform, "Полог", blocked,
                 new Vector3(0f, top * 0.5f, back - 0.15f),
                 new Vector3(outer * 2f, top + 1f, 0.3f));

            var inside = tent.AddComponent<TentInterior>();
            inside.Shape(half, top, front, back, model.GetComponentsInChildren<Renderer>());

            if (master)
            {
                Headquarters(tent.transform, half, top, front, back, path);
                Debug.Log($"[СБОРКА] {tent.name}: {half * 2f:0.00} × {front - back:0.00} м, "
                        + $"конёк {top:0.00} м, пол в рост — {path * 2f:0.00} м шириной.");
            }
            else
            {
                Quarters(tent.transform, half, top, front, back, path, index);
            }
        }

        /// <summary>
        /// Форма палатки, снятая с модели: полуширина у земли, конёк, вход
        /// и полог — в метрах, в осях корня. Меряется ткань — первый
        /// материал модели (props.py: CLOTH), а не всё подряд: колышки
        /// торчат за скаты, шесты — над коньком.
        /// </summary>
        private static (float Half, float Top, float Front, float Back)? Measure(
            Transform root, GameObject model)
        {
            var filter = model.GetComponentInChildren<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || mesh.subMeshCount == 0) return null;

            var verts = mesh.vertices;
            var cloth = mesh.GetTriangles(0);
            if (verts.Length == 0 || cloth.Length == 0) return null;

            var toWorld = filter.transform.localToWorldMatrix;
            float half = 0f, top = 0f, front = float.MinValue, back = float.MaxValue;

            foreach (int i in cloth)
            {
                var p = root.InverseTransformPoint(toWorld.MultiplyPoint3x4(verts[i]));

                top = Mathf.Max(top, p.y);
                front = Mathf.Max(front, p.z);
                back = Mathf.Min(back, p.z);

                if (p.y < 0.05f) half = Mathf.Max(half, Mathf.Abs(p.x));
            }

            // Палатка шире полуметра, выше метра и длиннее метра. Иначе
            // первым материалом оказалось не то, и лучше сказать об этом,
            // чем поставить стены по шесту.
            if (half < 0.5f || top < 1f || front - back < 1f) return null;

            return (half, top, front, back);
        }

        /// <summary>Невидимая стена для навмеша: объём «не пройти».</summary>
        private static void Wall(Transform tent, string name, int area, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(tent, false);

            var volume = go.AddComponent<Unity.AI.Navigation.NavMeshModifierVolume>();
            volume.center = centre;
            volume.size = size;
            volume.area = area;
        }

        /// <summary>
        /// Рядовая палатка: постель вдоль ската, сундучок у полога, фонарь
        /// под коньком. Сторона постели — по номеру палатки, без жребия:
        /// лагерь обязан выглядеть одинаково при каждом запуске.
        /// </summary>
        private static void Quarters(Transform tent, float half, float top, float front, float back,
                                     float path, int index)
        {
            float side = index % 2 == 0 ? 1f : -1f;
            float length = Mathf.Min(1.7f, front - back - 0.5f);

            Bedroll(tent, new Vector3(side * Mathf.Min(half - 0.25f, path + 0.35f), 0f,
                                      back + 0.2f + length * 0.5f), 0.55f, length);

            SmallChest(tent, new Vector3(-side * (path + 0.30f), 0f, back + 0.35f), side * 90f, 0.42f);

            Lantern(tent, new Vector3(0f, top - 0.40f, back + 0.45f),
                    range: 2.4f, intensity: 0.7f, shadows: false);
        }

        /// <summary>
        /// Палатка Греховода — «ещё более наполненная» (слово автора): постель,
        /// в которой он просыпается, стол с картой у полога, полка с банками
        /// для душ, стойка с оружием, сундук. Всё вдоль скатов и у полога;
        /// середина свободна — по ней он выходит к лагерю.
        /// </summary>
        private static void Headquarters(Transform tent, float half, float top, float front,
                                         float back, float path)
        {
            // Коврик посередине: без него пол палатки — та же земля, что
            // снаружи, и нутро не читается жильём.
            // Тёмно-багровый — цвет Греховода: ткань палатки на кадре сверху
            // читалась белой простынёй посреди ночи.
            var rug = Block(tent, "Коврик", "Fabric061", new Vector3(0f, 0.006f, (front + back) * 0.5f + 0.2f),
                            new Vector3(Mathf.Min(1.8f, path * 1.7f), 0.012f, (front - back) * 0.62f));
            rug.GetComponent<Renderer>().sharedMaterial = Plain("Коврик", new Color(0.16f, 0.04f, 0.05f), smoothness: 0.12f);

            // Постель у левого ската, изголовьем к пологу. Рядом — место,
            // где он встаёт: спавнер находит его по имени.
            float bedX = -(path + 0.35f);
            Bedroll(tent, new Vector3(bedX, 0f, back + 1.3f), 0.8f, 2.0f);

            var wake = new GameObject("Где проснулся");
            wake.transform.SetParent(tent, false);
            wake.transform.localPosition = new Vector3(bedX + 0.9f, 0f, back + 1.5f);

            // Стол с картой у полога, правее середины: походный, ниже
            // и уже стола совета. Карта — на коже, как и положено в поле.
            var table = Prop("CouncilTable", tent, tent.TransformPoint(new Vector3(0.45f, 0f, back + 0.55f)),
                             tent.eulerAngles.y);
            if (table != null)
            {
                var s = table.transform.localScale;
                table.transform.localScale = new Vector3(s.x * 0.55f, s.y * 0.50f, s.z * 0.85f);
                table.name = "Стол с картой";
            }

            // Столешница стола совета — на 0,97 (props.py), здесь ×0,85.
            Block(tent, "Карта", "Leather033A", new Vector3(0.45f, 0.831f, back + 0.55f),
                  new Vector3(0.78f, 0.012f, 0.42f), yaw: 4f);

            // Полка с банками для душ вдоль правого ската, лицом к середине.
            var shelf = new GameObject("Полка с банками");
            shelf.transform.SetParent(tent, false);
            shelf.transform.localPosition = new Vector3(path + 0.27f, 0f, back + 1.9f);
            shelf.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

            var board = Prop("SoulShelf", shelf.transform, shelf.transform.position,
                             shelf.transform.eulerAngles.y);
            if (board != null)
            {
                var s = board.transform.localScale;
                board.transform.localScale = new Vector3(s.x * 0.35f, s.y * 0.80f, s.z * 0.75f);
            }

            // Банки души (сессия моделей, 27 сентября: Props/SoulJar,
            // SoulJarFull). Пустые все, кроме одной: в ней тлеет последняя,
            // кого он не донёс. Грехи её — Уныние с Жадностью, тлеет
            // слабо: не донёс — значит, гасла. Нет моделей — прежние
            // цилиндры: палатка без полки хуже палатки с заглушками.
            // Доска полки — на 0,97 (props.py), здесь ×0,75 = 0,73; банка
            // стоит дном на доске (опора модели — у дна).
            for (int i = 0; i < 4; i++)
            {
                var at = new Vector3(-0.55f + i * 0.36f, 0.73f, 0.02f);
                bool full = i == 2;
                var jar = Prop(full ? "SoulJarFull" : "SoulJar", shelf.transform,
                               shelf.transform.TransformPoint(at), shelf.transform.eulerAngles.y);

                if (jar == null)
                {
                    jar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    jar.transform.SetParent(shelf.transform, false);
                    jar.transform.localPosition = at + new Vector3(0f, 0.11f, 0f);
                    jar.transform.localScale = new Vector3(0.16f, 0.11f, 0.16f);
                    Object.DestroyImmediate(jar.GetComponent<Collider>());
                    jar.GetComponent<Renderer>().sharedMaterial = full
                        ? Plain("Банка с душой", new Color(0.24f, 0.34f, 0.31f), new Color(0.22f, 0.52f, 0.44f))
                        : Plain("Банка", new Color(0.20f, 0.25f, 0.24f));
                }
                else if (full)
                {
                    jar.AddComponent<SoulJarGlow>().Set(Sinbinder.Core.SinType.Sloth,
                                                        Sinbinder.Core.SinType.Greed, 0.35f);
                }

                jar.name = full ? "Банка с душой" : "Банка для души";
            }

            // Стойка с оружием у правого ската, у входа. Оружие — дело
            // сессии моделей (её пункт 2); пока на стойке — катана с витрины.
            var rack = new GameObject("Стойка с оружием");
            rack.transform.SetParent(tent, false);
            rack.transform.localPosition = new Vector3(path + 0.22f, 0f, front - 1.05f);

            foreach (float z in new[] { -0.40f, 0.40f })
                Block(rack.transform, "Стояк", "Planks037A", new Vector3(0f, 0.62f, z),
                      new Vector3(0.07f, 1.24f, 0.07f));

            Block(rack.transform, "Перекладина", "Planks037A", new Vector3(0f, 1.08f, 0f),
                  new Vector3(0.06f, 0.06f, 0.92f));
            Block(rack.transform, "Упор", "Planks037A", new Vector3(0f, 0.22f, 0f),
                  new Vector3(0.06f, 0.06f, 0.92f));

            var blade = Prop("Katana", rack.transform, rack.transform.TransformPoint(new Vector3(-0.08f, 0.30f, 0.12f)),
                             tent.eulerAngles.y + 90f);
            if (blade != null)
                blade.transform.rotation = Quaternion.AngleAxis(-10f, tent.right) * blade.transform.rotation;

            // Сундук в ногах постели, крышкой к середине.
            SmallChest(tent, new Vector3(bedX + 0.15f, 0f, back + 2.85f), 90f, 0.70f);

            var crate = Prop("Crate", tent, tent.TransformPoint(new Vector3(bedX - 0.05f, 0f, front - 0.75f)),
                             tent.eulerAngles.y + 20f, 0.6f);
            if (crate != null) crate.name = "Ящик";

            // Фонарь под коньком. С тенью: ткань держит свет внутри,
            // и палатка светится входом, а не стенами.
            Lantern(tent, new Vector3(0f, top - 0.80f, back + 1.9f),
                    range: 5.5f, intensity: 1.5f, shadows: true);
        }

        /// <summary>Постель: кожаная подстилка и свёрнутое в изголовье одеяло.</summary>
        private static void Bedroll(Transform tent, Vector3 at, float width, float length)
        {
            var bed = new GameObject("Постель");
            bed.transform.SetParent(tent, false);
            bed.transform.localPosition = at;

            Block(bed.transform, "Подстилка", "Leather033A",
                  new Vector3(0f, 0.05f, 0f), new Vector3(width, 0.10f, length));
            Block(bed.transform, "Одеяло", "Fabric061",
                  new Vector3(0f, 0.15f, -length * 0.5f + 0.22f), new Vector3(width * 0.9f, 0.10f, 0.34f));
        }

        /// <summary>
        /// Сундучок: сундук Марги в меньшем размере, с крышкой на той же
        /// петле у заднего края (<see cref="TrophyChestProp"/>).
        /// </summary>
        private static void SmallChest(Transform tent, Vector3 at, float yaw, float scale)
        {
            var box = new GameObject("Сундучок");
            box.transform.SetParent(tent, false);
            box.transform.localPosition = at;
            box.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            box.transform.localScale = Vector3.one * scale;

            float y = box.transform.eulerAngles.y;
            if (Prop("Chest", box.transform, box.transform.position, y) == null)
            {
                Object.DestroyImmediate(box);
                return;
            }

            Prop("ChestLid", box.transform, box.transform.TransformPoint(new Vector3(0f, 0.56f, -0.35f)), y);
        }

        /// <summary>
        /// Фонарь под коньком: огонёк и тёплый свет. У рядовых — без тени
        /// и на пару метров: двадцать огней с тенями стоили бы дороже всего
        /// лагеря. У Греховода — с мягкой тенью.
        /// </summary>
        private static void Lantern(Transform tent, Vector3 at, float range, float intensity, bool shadows)
        {
            var lamp = new GameObject("Фонарь");
            lamp.transform.SetParent(tent, false);
            lamp.transform.localPosition = at;

            // В выпечку навмеша не идёт: в рядовой палатке огонёк висит
            // ниже роста агента выпечки и отрезал бы конец тропки под собой.
            lamp.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild = true;

            var flame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flame.name = "Огонёк";
            flame.transform.SetParent(lamp.transform, false);
            flame.transform.localScale = new Vector3(0.09f, 0.13f, 0.09f);
            Object.DestroyImmediate(flame.GetComponent<Collider>());

            var renderer = flame.GetComponent<Renderer>();
            renderer.sharedMaterial = Plain("Фонарь", new Color(0.95f, 0.62f, 0.30f),
                                            new Color(1.25f, 0.72f, 0.32f));
            // Огонёк не заслоняет собственный свет: свет сидит внутри него.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.68f, 0.38f);
            light.range = range;
            light.intensity = intensity;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;

            // Фонарь — огонь под стеклом: дышит едва, на месте.
            lamp.AddComponent<Sinbinder.Gameplay.FireFlicker>().Tune(0.08f, 1.8f, 0f);
        }

        /// <summary>
        /// Простое тело из куба под материалом-текстурой. Без коллайдера:
        /// это обстановка, а не преграда щелчку по воину, который стоит рядом.
        /// </summary>
        private static GameObject Block(Transform parent, string name, string surface,
                                        Vector3 at, Vector3 size, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Cover(go, surface);
            return go;
        }

        private const string PlainDir = "Assets/Materials/Сцены";

        /// <summary>
        /// Материал одного цвета — для того, у чего нет текстуры: стекло
        /// банок, огонёк фонаря. Лежит ассетом и пересобирается поверх себя,
        /// как материалы из текстур (<see cref="MaterialBuilder"/>): иначе
        /// ссылки из сцен рвались бы при каждой сборке.
        /// </summary>
        private static Material Plain(string name, Color colour, Color glow = default,
                                      float smoothness = 0.55f)
        {
            if (!AssetDatabase.IsValidFolder(PlainDir))
                AssetDatabase.CreateFolder("Assets/Materials", "Сцены");

            string path = $"{PlainDir}/{name}.mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = colour;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);

            if (glow.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", glow);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Имя павшего над колышком.
        ///
        /// Единственная надпись во всём мире демо, а не в интерфейсе, —
        /// и потому единственная, ради которой заведён TextMesh. Смотрит
        /// туда же, куда камера: обе камеры лагеря стоят к югу и глядят
        /// на север, значит текст, не повёрнутый никак, читается сразу.
        ///
        /// Пустое имя — не пустая надпись, а ни одной: колышек без имени
        /// выглядит как недоделка, а колышек с пустым текстом — как баг.
        /// </summary>
        private static void PegName(Transform tent, Vector3 pegTop, string fallenName)
        {
            if (string.IsNullOrEmpty(fallenName)) return;

            var font = UIFont();
            if (font == null)
            {
                Debug.LogWarning($"[СБОРКА] Шрифта нет — колышек «{fallenName}» "
                               + "останется без имени.");
                return;
            }

            var go = new GameObject($"Имя: {fallenName}");
            go.transform.position = pegTop + new Vector3(0f, 0.42f, 0f);

            // В детях у палатки, а не у колышка: гаснущую надпись зажигает
            // взгляд на предмет, а колышек — палочка в восемь сантиметров,
            // и целиться в неё через лагерь было бы мучением. Смотришь
            // на просевшую палатку — читаешь, кто в ней жил.
            if (tent != null)
            {
                go.transform.SetParent(tent, worldPositionStays: true);

                // Палатка — куб, растянутый по трём осям врозь, и надпись
                // унаследовала бы это растяжение. Гасим его обратным
                // масштабом: имя павшего обязано читаться, а не плыть.
                var k = tent.lossyScale;
                go.transform.localScale = new Vector3(
                    k.x == 0f ? 1f : 1f / k.x,
                    k.y == 0f ? 1f : 1f / k.y,
                    k.z == 0f ? 1f : 1f / k.z);
            }

            var text = go.AddComponent<TextMesh>();
            text.text = fallenName;
            text.font = font;
            text.fontSize = 42;
            text.characterSize = 0.10f;
            text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.78f, 0.75f, 0.70f);

            // Без материала шрифта TextMesh рисует розовым «шейдер потерян».
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = font.material;

            go.AddComponent<Sinbinder.UI.WorldPlate>();
        }

        /// <summary>
        /// Стол военного совета с хрустальным шаром. Шар — предмет в лагере,
        /// который видно от палатки: на доле 3 он наливается красным,
        /// и это первое, что игрок замечает, не подходя к столу.
        /// </summary>
        /// <summary>
        /// Сходится ли постановка лагеря сама с собой.
        ///
        /// Две ошибки, которые глазами в коде не видны, а в сцене видны
        /// сразу и поздно: стол, влезший в палатку, и стол, стоящий ровно
        /// там, куда смотрит открывающий кадр. Второе тише и хуже —
        /// совет открылся бы на первом же кадре сам, и «игрок подошёл
        /// к столу» превратилось бы в «игрок там оказался».
        ///
        /// Взгляд считается тем же правилом, которым его считает шар
        /// в рантайме (<see cref="CampFocus"/>), иначе проверка проверяла
        /// бы не то, что происходит.
        /// </summary>
        private static void CheckCamp(Vector3 table, Vector3 chest, Vector3 eye,
            Vector3 euler, List<Vector3> tents)
        {
            const float tentClearance = 2.4f;
            const float apartness = 5f;

            var ball = table + new Vector3(0f, 1.22f, 0f);
            var forward = Quaternion.Euler(euler) * Vector3.forward;

            Clearance("Стол совета", table, tents, tentClearance);
            Clearance("Сундук Марги", chest, tents, tentClearance);

            // Две цели сцены не должны сливаться в одну: подойдя к столу,
            // игрок не должен заодно открыть и сундук.
            float between = CampFocus.GroundDistance(table, chest);
            if (between < apartness)
                Debug.LogWarning($"[СБОРКА] Стол и сундук в {between:0.0} м друг от друга — "
                               + "к ним придётся подходить одним шагом.");

            if (!CampFocus.TryGroundPoint(eye, forward, ball.y, out var focus))
            {
                Debug.LogWarning("[СБОРКА] Открывающий кадр не смотрит в землю: "
                               + "подводить взгляд к столу будет нечем.");
                return;
            }

            Untouched("шара", focus, ball);

            if (CampFocus.TryGroundPoint(eye, forward, chest.y, out var lowFocus))
                Untouched("сундука", lowFocus, chest);
        }

        /// <summary>Не влез ли предмет в палатку.</summary>
        private static void Clearance(string what, Vector3 where,
            List<Vector3> tents, float clearance)
        {
            float nearest = float.MaxValue;
            foreach (var t in tents)
                nearest = Mathf.Min(nearest, Vector3.Distance(t, where));

            if (nearest < clearance)
                Debug.LogWarning($"[СБОРКА] {what} в {nearest:0.0} м от палатки — "
                               + "они пересекутся.");
        }

        /// <summary>
        /// Не стоит ли предмет там, куда смотрит открывающий кадр.
        /// Если стоит — он сработает сам на первом кадре, и «игрок подошёл»
        /// превратится в «игрок оказался».
        /// </summary>
        private static void Untouched(string what, Vector3 focus, Vector3 thing)
        {
            float d = CampFocus.GroundDistance(focus, thing);

            if (d <= CampFocus.TableReach)
                Debug.LogWarning($"[СБОРКА] Взгляд открывающего кадра уже в {d:0.0} м "
                               + $"от {what} при радиусе {CampFocus.TableReach:0.0} — "
                               + "сработает само, и игрок к нему не подойдёт.");
            else
                Debug.Log($"[СБОРКА] До {what} от открывающего кадра {d:0.0} м "
                        + $"при радиусе {CampFocus.TableReach:0.0}.");
        }

        /// <summary>
        /// Стол с шаром. <paramref name="plate"/> — табличка над шаром: в лагере
        /// шар открывает совет (F), в склепе и на полигоне — карту вылазок,
        /// подходом. Одна табличка на оба стола врала в одном из них.
        /// </summary>
        private static GameObject CouncilTable(Vector3 position, string plate = null, float yaw = 0f)
        {
            var table = new GameObject("Стол совета");
            table.transform.position = position;

            if (Prop("CouncilTable", table.transform, position) == null)
            {
                var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
                top.name = "Столешница";
                top.transform.SetParent(table.transform);
                top.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                top.transform.localScale = new Vector3(1.6f, 0.12f, 1.1f);

                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "Опора";
                leg.transform.SetParent(table.transform);
                leg.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                leg.transform.localScale = new Vector3(0.35f, 0.9f, 0.35f);
            }

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Хрустальный шар";
            ball.transform.SetParent(table.transform);
            ball.transform.localPosition = new Vector3(0f, 1.22f, 0f);
            ball.transform.localScale = Vector3.one * 0.42f;

            // Подпись с клавишей. Совет открывается нажатием, и об этом
            // надо сказать там, где нажимают, — а не строкой в журнале,
            // которую к тому времени уже пролистали.
            Plate(ball.transform, plate ?? Sinbinder.Core.Loc.N("Военный совет — F"), 1.4f);

            var glow = new GameObject("Свечение");
            glow.transform.SetParent(ball.transform);
            glow.transform.localPosition = Vector3.zero;

            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.45f, 0.62f, 0.95f);
            light.intensity = 1.4f;
            light.range = 7f;

            ball.AddComponent<CrystalBall>();

            // Поворот — в конце, корнем: дети ставились по миру, и повернуть
            // их вместе можно только так. Шар — на оси, ему всё равно.
            table.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return table;
        }

        /// <summary>
        /// Сундук с трофеями: ящик и крышка на нём. Крышка — отдельный
        /// объект с собственной точкой поворота, иначе она открывалась бы
        /// вокруг собственной середины и въезжала бы в ящик.
        /// </summary>
        private static void TrophyChestProp(Vector3 position, float yaw = 0f)
        {
            var chest = new GameObject("Сундук Марги");
            chest.transform.position = position;

            bool modelled = Prop("Chest", chest.transform, position) != null;

            if (!modelled)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "Ящик";
                box.transform.SetParent(chest.transform);
                box.transform.localPosition = new Vector3(0f, 0.28f, 0f);
                box.transform.localScale = new Vector3(1.1f, 0.56f, 0.7f);
            }

            // Петля у заднего края: крышка поворачивается вокруг неё.
            var hinge = new GameObject("Крышка");
            hinge.transform.SetParent(chest.transform);
            hinge.transform.localPosition = new Vector3(0f, 0.56f, -0.35f);

            if (Prop("ChestLid", hinge.transform, position + new Vector3(0f, 0.56f, -0.35f)) == null)
            {
                var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lid.name = "Створка";
                lid.transform.SetParent(hinge.transform);
                lid.transform.localPosition = new Vector3(0f, 0.06f, 0.35f);
                lid.transform.localScale = new Vector3(1.15f, 0.12f, 0.75f);
            }

            var trophy = chest.AddComponent<TrophyChest>();
            Wire(trophy, ("_lid", hinge.transform));

            // Передом к тем, кто подходит: поворот корнем, вместе с крышкой
            // и её петлёй — так крышка и открывается от себя.
            chest.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>Вход в склеп: две опоры и перемычка. Больше и не нужно.</summary>
        /// <summary>
        /// Зал склепа: трон, алтарь, гроб в нише.
        ///
        /// Реплика прибытия говорит о них дословно — «Пустой трон. Алтарь.
        /// Замурованный гроб в нише» — а в сцене не стояло ни одного:
        /// модели собраны 16 сентября и с тех пор лежали без места. Игра,
        /// которая называет то, чего не показывает, читается как обман,
        /// и первым это заметит зритель на показе.
        ///
        /// Трон пустой нарочно. Склеп занят — но тем, кого не видно:
        /// это и есть вся мысль эпилога, и высказать её лучше пустым
        /// креслом, чем ещё одной строкой текста.
        /// </summary>
        private static void CryptHall()
        {
            var hall = new GameObject("Зал");

            // Алтарь по середине: к нему подходит отряд и на нём же
            // спрашивают плату.
            Prop("Altar", hall.transform, new Vector3(0f, 0f, 3.4f));

            // Трон сдвинут и развёрнут: стоящий строго по оси читается
            // мебелью, а поставленный боком — местом, которое занимали.
            Prop("Throne", hall.transform, new Vector3(-3.4f, 0f, 6.1f), 34f);

            // Гроб у стены, торцом к камере: «в нише» — значит не посреди
            // прохода.
            Prop("Coffin", hall.transform, new Vector3(4.8f, 0f, 5.2f), -74f);

            // Ниша: короткая стенка за гробом. Без неё гроб стоит
            // в чистом поле, и слово «ниша» опять ничем не подтверждено.
            var niche = GameObject.CreatePrimitive(PrimitiveType.Cube);
            niche.name = "Ниша";
            niche.transform.SetParent(hall.transform);
            niche.transform.position = new Vector3(6.1f, 1.1f, 5.6f);
            niche.transform.rotation = Quaternion.Euler(0f, -74f, 0f);
            niche.transform.localScale = new Vector3(0.4f, 2.2f, 3.2f);

            var stone = MaterialBuilder.Get("Bricks076A");
            var renderer = niche.GetComponent<Renderer>();
            if (stone != null && renderer != null) renderer.sharedMaterial = stone;
        }

        private static void CryptGate(Vector3 position)
        {
            var gate = new GameObject("Вход в склеп");
            gate.transform.position = position;

            if (Prop("CryptGate", gate.transform, position) == null)
            {
                for (int i = -1; i <= 1; i += 2)
                {
                    var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pillar.name = i < 0 ? "Опора левая" : "Опора правая";
                    pillar.transform.SetParent(gate.transform);
                    pillar.transform.localPosition = new Vector3(i * 1.6f, 1.5f, 0f);
                    pillar.transform.localScale = new Vector3(0.8f, 3f, 0.8f);
                }

                var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lintel.name = "Перемычка";
                lintel.transform.SetParent(gate.transform);
                lintel.transform.localPosition = new Vector3(0f, 3.2f, 0f);
                lintel.transform.localScale = new Vector3(4f, 0.6f, 0.9f);
            }

            // Факелы по сторонам входа: единственный свет у склепа.
            for (int i = -1; i <= 1; i += 2)
            {
                var at = position + new Vector3(i * 1.9f, 1.6f, -0.5f);
                if (Prop("Torch", gate.transform, at, i < 0 ? -90f : 90f) == null) continue;

                var glow = new GameObject("Огонь");
                glow.transform.SetParent(gate.transform);
                glow.transform.position = at + new Vector3(0f, 0.7f, -0.2f);

                var fire = glow.AddComponent<Light>();
                fire.type = LightType.Point;
                fire.color = new Color(1f, 0.58f, 0.26f);
                fire.intensity = 2.4f;
                fire.range = 9f;
                glow.AddComponent<Sinbinder.Gameplay.FireFlicker>().Tune(0.25f, 3f, 0.05f);

                Embers(gate.transform, at + new Vector3(0f, 0.72f, -0.2f), 0.5f, 14f);
            }
        }

        // ---------- интерфейс: три ступени прозрачности ----------

        /// <summary>
        /// Ступени 1–3 из docs/07-VERDICT.md и 11-MISSING §2.3: значок,
        /// подсказка при наведении, журнал. Четвёртую ступень —
        /// трассировку для разработчика — игрок не видит, её тут нет.
        /// </summary>
        private static Transform Interface()
        {
            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGO.AddComponent<GraphicRaycaster>();

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            BuildSelectionBox(canvasGO.transform);
            BuildLog(canvasGO.transform);
            BuildMomentCaption(canvasGO.transform);
            BuildClarityPanel(canvasGO.transform);
            BuildSaveSlots(canvasGO.transform);
            BuildPause(canvasGO.transform);
            BuildStrategy(canvasGO.transform);

            // Сверху вниз — в том порядке, в каком они лягут в стопке:
            // подпись предмета у самой панели выделенного, выше — уроки.
            var stack = BuildStack(canvasGO.transform);
            BuildHarvestHint(canvasGO.transform, stack);
            BuildCommandHint(canvasGO.transform, stack);
            BuildHint(canvasGO.transform, stack);
            BuildPlateLine(stack);

            BuildSelectedUnit(canvasGO.transform);
            BuildSatchel(canvasGO.transform);
            BuildTooltip(canvasGO.transform);

            // «Сборку души» в сцены не ставим. Панель собрана, но
            // заполнить её некому: SoulAssemblyUI.Show не зовёт никто
            // (14-HANDOFF §, 11-MISSING §). Стояла она при этом в углу
            // каждого кадра пустым тёмным ящиком — снимки прохождения
            // 18 сентября это и показали. Пустое окно в углу читается
            // поломкой игры, а не заготовкой на будущее; вернём вместе
            // с тем, кто его наполнит.
            // Выбор тела нужен везде, где можно собрать душу, а собрать
            // её можно в любой сцене с боем. Строим со всем остальным
            // интерфейсом, чтобы не гадать, где игрок нажмёт связывание.
            BuildShellPicker(canvasGO.transform);
            // Полосы — последними из всего интерфейса: порядок отрисовки
            // у Canvas задаётся порядком детей, и рамка кадра обязана
            // лежать поверх журнала, панелей и подписей. Построй её
            // раньше — и сквозь чёрное полезет полоса журнала.
            BuildLetterbox(canvasGO.transform);

            // Панель искусителей отложена до полной версии вместе
            // с механикой (docs/09-PROLOGUE.md §7). Метод, который её
            // собирал, лежит в хвосте Assets/Scripts/UI/TemptationPanelUI.cs.later
            // и возвращается вместе с ней.

            return canvasGO.transform;
        }

        /// <summary>
        /// Ведущий пролога: чем кончается доля и куда идти дальше.
        /// Без него четыре сцены остаются четырьмя тестами.
        /// </summary>
        private static void Director(string nextScene, bool waitForBattle,
            bool startsPrologue = false, bool waitForEscape = false,
            float endsAfterSeconds = 0f, string arrivalLine = "")
        {
            var go = new GameObject("Ведущий пролога");
            var director = go.AddComponent<PrologueDirector>();

            var so = new SerializedObject(director);
            so.FindProperty("_nextScene").stringValue = nextScene ?? "";
            so.FindProperty("_waitForBattle").boolValue = waitForBattle;
            so.FindProperty("_waitForEscape").boolValue = waitForEscape;
            so.FindProperty("_startsPrologue").boolValue = startsPrologue;
            so.FindProperty("_endsAfterSeconds").floatValue = endsAfterSeconds;
            so.FindProperty("_arrivalLine").stringValue = arrivalLine;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Доля 0: чёрный экран и одна строка. Полотно строится последним
        /// и лежит поверх всего остального — до первого кадра игры игрок
        /// не должен видеть ни журнала, ни подсказок.
        /// </summary>
        /// <summary>
        /// Полноэкранная строка на три секунды. Приём §9.4: «текст крупно
        /// и редко, четыре полноэкранные строки на весь пролог».
        ///
        /// Четыре — это по одной на сцену. Стояла одна, в лагере, и приём
        /// из-за этого не читался как приём: единственная строка выглядит
        /// заставкой, а четыре — ритмом.
        ///
        /// Слова — работа автора. Две из них канон («Греху всё равно, чьё
        /// это тело», «Не командуй. Искушай.»), две другие поставлены
        /// облаком и меняются одной строкой в инспекторе.
        /// </summary>
        private static void BuildTitle(Transform parent, string text = null)
        {
            var panel = Panel("Заставка", parent,
                anchorMin: Vector2.zero, anchorMax: Vector2.one,
                pivot: new Vector2(0.5f, 0.5f), size: Vector2.zero, position: Vector2.zero);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;

            var black = panel.gameObject.AddComponent<Image>();
            black.color = Color.black;

            var group = panel.gameObject.AddComponent<CanvasGroup>();

            var line = Label("Строка", panel, 46, TextAnchor.MiddleCenter);
            line.color = new Color(0.88f, 0.86f, 0.82f);

            // На холст, как и все панели, что прячут себя: заставка гаснет
            // уже отработав, но правило одно на всех — DemoSmoke ищет
            // интерфейс, выключивший сам себя, и исключений не держит.
            var ui = parent.gameObject.AddComponent<Sinbinder.UI.PrologueTitleUI>();
            Wire(ui, ("_panel", panel.gameObject), ("_group", group), ("_line", line));

            if (!string.IsNullOrEmpty(text))
            {
                var so = new SerializedObject(ui);
                so.FindProperty("_text").stringValue = text;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }


        /// <summary>Экран конца демо: кто вернулся.</summary>
        private static void BuildDemoEnd(Transform parent)
        {
            var panel = Panel("Конец демо", parent,
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f), size: new Vector2(900f, 620f),
                position: Vector2.zero);

            var backdrop = Backdrop(panel, Weight.Screen);

            var title = Label("Заголовок", panel, 38, TextAnchor.UpperLeft,
                new Vector2(0f, -28f), 58f);
            var body = Label("Список", panel, 24, TextAnchor.UpperLeft,
                new Vector2(0f, -100f), 480f);

            // Как у экрана «Греховод пал»: без кнопок из эпилога некуда
            // было уйти — меню паузы, пока игра стоит, не открывается.
            var again = EndButton(Sinbinder.Core.Loc.N("Начать сначала"), panel, -190f);
            var quit = EndButton(Sinbinder.Core.Loc.N("Выйти из игры"), panel, 190f);

            // На холст, а не на панель: панель выключается в Start, а
            // выключенный объект не находит FindFirstObjectByType — директор
            // склепа не видел конца демо и писал «Демо окончено» в консоль,
            // оставляя игрока в пустом склепе. Нашёл прогон DemoWalkthrough.
            var ui = parent.gameObject.AddComponent<Sinbinder.UI.DemoEndUI>();
            Wire(ui, ("_panel", panel.gameObject), ("_title", title), ("_body", body),
                     ("_again", again), ("_quit", quit));
        }

        /// <summary>
        /// Кнопка у нижнего края панели конца — того же вида, что у экрана
        /// «Греховод пал» (<c>GameOverUI</c>): 320 на 84, середина на 70
        /// от края. Два экрана конца обязаны выглядеть одним.
        /// </summary>
        private static Button EndButton(string text, RectTransform panel, float x)
        {
            var go = new GameObject(text, typeof(RectTransform));
            go.transform.SetParent(panel, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320f, 84f);
            rt.anchoredPosition = new Vector2(x, 70f);

            var plate = go.AddComponent<Image>();
            plate.color = new Color(0.13f, 0.12f, 0.11f, 1f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = plate;

            var label = Label("Подпись", rt, 26, TextAnchor.MiddleCenter);
            label.color = new Color(0.94f, 0.90f, 0.80f);
            label.raycastTarget = false;    // клик обязан доходить до кнопки
            label.text = text;

            return button;
        }

        /// <summary>
        /// Плата после боя: второй соблазн пролога. Показывается сама,
        /// когда врагов на поле не осталось.
        /// </summary>
        private static void BuildSalary(Transform parent, bool askOnArrival = false)
        {
            var panel = Panel("Плата", parent,
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f), size: new Vector2(760f, 300f),
                position: Vector2.zero);

            var backdrop = Backdrop(panel, Weight.Screen);

            var title = Label("Заголовок", panel, 30, TextAnchor.UpperLeft,
                new Vector2(0f, -24f), 60f);

            var pay = Choice("Заплатить", panel, new Vector2(-170f, -60f), out var payLabel);
            var hold = Choice("Придержать", panel, new Vector2(170f, -60f), out var holdLabel);

            // На холст, а не на панель: компонент панель выключает, а на
            // выключенном объекте не идёт корутина ожидания заставки —
            // вопрос о плате в склепе не прозвучал бы ни разу. Тот же
            // случай, от которого сборщик уже защищает совет и разговор.
            var ui = parent.gameObject.AddComponent<Sinbinder.UI.SalaryPanelUI>();
            Wire(ui, ("_panel", panel.gameObject), ("_title", title),
                     ("_payButton", pay), ("_payLabel", payLabel),
                     ("_withholdButton", hold), ("_withholdLabel", holdLabel));

            var so = new SerializedObject(ui);
            so.FindProperty("_askOnArrival").boolValue = askOnArrival;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Кнопка выбора с подписью в две строки.</summary>
        private static Button Choice(string name, RectTransform parent, Vector2 position, out Text label)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320f, 120f);
            rt.anchoredPosition = position;

            var plate = go.AddComponent<Image>();
            plate.color = new Color(0.13f, 0.12f, 0.11f, 1f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = plate;

            label = Label("Подпись", rt, 24, TextAnchor.MiddleCenter);
            label.raycastTarget = false;    // клик обязан доходить до кнопки

            return button;
        }

        /// <summary>
        /// Доля 3: военный совет. Панель, у которой в демо самая важная
        /// работа — показать строку «когда велено отойти — не отходит»
        /// до того, как она сбудется.
        /// </summary>
        private static void BuildCouncil(Transform parent)
        {
            // Три столбца: кто, каков он, куда идти. Слева выбирают,
            // посередине читают о выбранном, справа — о деле. Раньше всё
            // это лежало одной колонкой, и четыре строки пророчества
            // на каждого превращали список в простыню, по которой игрок
            // щёлкал не читая — а щелчок был сразу и назначением.
            var panel = Panel("Военный совет", parent,
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f), size: new Vector2(1280f, 760f),
                position: Vector2.zero);

            var backdrop = Backdrop(panel, Weight.Screen);

            var title = Label("Заголовок", panel, 34, TextAnchor.UpperLeft,
                new Vector2(0f, -20f), 48f);

            var rows = Column("Кандидаты", panel, 0f, 0.33f);
            var middle = Column("Кто выбран", panel, 0.345f, 0.655f);
            var right = Column("Дело", panel, 0.67f, 1f);

            Tint(middle, new Color(0.09f, 0.08f, 0.08f, 0.55f));
            Tint(right, new Color(0.09f, 0.08f, 0.08f, 0.55f));

            var detail = Label("Описание", middle, 21, TextAnchor.UpperLeft,
                new Vector2(0f, -16f), 380f);
            detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            detail.verticalOverflow = VerticalWrapMode.Truncate;

            var quest = Label("Задача", right, 21, TextAnchor.UpperLeft,
                new Vector2(0f, -16f), 460f);
            quest.horizontalOverflow = HorizontalWrapMode.Wrap;
            quest.verticalOverflow = VerticalWrapMode.Truncate;
            quest.color = new Color(0.80f, 0.76f, 0.70f);

            // Назначение отдельной кнопкой, внизу среднего столбца: решение
            // принимается один раз и нарочно, а не первым касанием списка.
            var confirm = Choice("Назначить", middle, new Vector2(0f, -232f),
                                 out var confirmLabel);

            var ui = parent.gameObject.AddComponent<Sinbinder.UI.CommanderCouncilUI>();
            Wire(ui, ("_panel", panel.gameObject), ("_title", title), ("_rows", rows),
                     ("_font", UIFont()), ("_detail", detail), ("_quest", quest),
                     ("_confirm", confirm), ("_confirmLabel", confirmLabel));
        }

        /// <summary>Столбец панели: доля ширины от левого края до правого.</summary>
        private static RectTransform Column(string name, RectTransform parent,
                                            float from, float to)
        {
            var rt = Panel(name, parent,
                anchorMin: new Vector2(from, 0f), anchorMax: new Vector2(to, 1f),
                pivot: new Vector2(0.5f, 0.5f), size: Vector2.zero, position: Vector2.zero);

            rt.offsetMin = new Vector2(20f, 24f);
            rt.offsetMax = new Vector2(-20f, -84f);
            return rt;
        }

        /// <summary>Подложка столбца: отделяет его от соседнего без рамок.</summary>
        private static void Tint(RectTransform rt, Color color)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        /// <summary>
        /// Выбор тела для собранной души.
        ///
        /// Четыре оболочки собраны как ассеты, смещения настоящие, дрейф
        /// работает — и всем этим связывание пользовалось на четверть,
        /// держа зашитого зомби. Панель открывает кран на уже проложенной
        /// трубе, и заодно делает урок сцены 4 механикой: истлевшую душу
        /// тяжёлое тело не примет, и промедление отнимает у игрока
        /// не качество, а выбор.
        /// </summary>
        private static void BuildShellPicker(Transform parent)
        {
            var panel = Panel("Выбор тела", parent,
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f), size: new Vector2(900f, 700f),
                position: Vector2.zero);

            var backdrop = Backdrop(panel, Weight.Screen);

            var title = Label("Заголовок", panel, 30, TextAnchor.UpperLeft,
                new Vector2(0f, -20f), 44f);

            var rows = Panel("Тела", panel,
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0f, 1f), size: new Vector2(0f, 620f),
                position: new Vector2(0f, -74f));
            rows.offsetMin = new Vector2(24f, rows.offsetMin.y);
            rows.offsetMax = new Vector2(-24f, rows.offsetMax.y);

            // На Canvas, а не на панель, которую сам выключает: та же
            // ошибка, что уже стоила совету неработающего Update.
            var ui = parent.gameObject.AddComponent<Sinbinder.UI.ShellPickerUI>();
            Wire(ui, ("_panel", panel.gameObject), ("_title", title), ("_rows", rows),
                     ("_font", UIFont()));
        }

        /// <summary>
        /// Разговор при встрече. Без него база реплик остаётся файлом,
        /// который никто не открывает: DialogueTrigger сочиняет реплики,
        /// а слушать их некому — реплики уходят в событие и пропадают.
        /// </summary>
        /// <summary>
        /// Разговор. Своей панели у него больше нет: реплика печатается
        /// на нижней полосе кадра (<see cref="Sinbinder.UI.Letterbox"/>),
        /// потому что наезд камеры и реплика — одно событие, а не два.
        ///
        /// Панель при этом не заводится вовсе, и поле <c>_dialoguePanel</c>
        /// остаётся пустым: <c>DialogueUI</c> его проверяет на null
        /// в каждом обращении, а полосу показывает и прячет камера.
        /// Вторая панель поверх полосы означала бы две копии одной
        /// реплики на экране.
        /// </summary>
        private static void BuildDialogue(Transform parent, Text speaker, Text line)
        {
            // Компонент висит на Canvas, а не на панели: панели нет,
            // а корутину показа кто-то крутить обязан.
            var ui = parent.gameObject.AddComponent<Sinbinder.UI.DialogueUI>();
            Wire(ui, ("_speakerNameText", speaker), ("_dialogueText", line));
        }

        /// <summary>
        /// Чёрные полосы кадра и строка на нижней.
        ///
        /// Строится в самом конце интерфейса, чтобы лечь поверх всего
        /// остального, и сразу отдаёт свои две строки разговору: реплика,
        /// слово поступка и фраза церемонии показываются одним и тем же
        /// текстом в одном и том же месте экрана.
        /// </summary>
        private static void BuildLetterbox(Transform parent)
        {
            var holder = new GameObject("Полосы кадра", typeof(RectTransform));
            holder.transform.SetParent(parent, false);

            var box = (RectTransform)holder.transform;
            box.anchorMin = Vector2.zero;
            box.anchorMax = Vector2.one;
            box.offsetMin = Vector2.zero;
            box.offsetMax = Vector2.zero;

            var top = Bar("Полоса сверху", box, atTop: true);
            var bottom = Bar("Полоса снизу", box, atTop: false);

            // Имя говорящего — над репликой и мельче её. У поступка
            // говорящего нет, и тогда строка прячется целиком.
            //
            // Доли высоты полосы, а не точки от её краёв: полоса выезжает
            // и на большом экране выше расчётной, и отступы в точках ставили
            // имя ПОД строку (кадр «реплика с наездом»; автор, 26 сентября:
            // «имя должно писаться над репликой»). Верхняя треть — имя,
            // остальное — реплика, при любой высоте.
            var speaker = Label("Говорящий", bottom, 22, TextAnchor.LowerCenter);
            speaker.color = new Color(0.72f, 0.68f, 0.60f);
            Band(speaker.rectTransform, 0.64f, 0.95f);

            var line = Label("Строка", bottom, 30, TextAnchor.UpperCenter);
            line.color = new Color(0.94f, 0.92f, 0.86f);
            Band(line.rectTransform, 0.04f, 0.62f);

            var letterbox = holder.AddComponent<Sinbinder.UI.Letterbox>();
            Wire(letterbox, ("_top", top), ("_bottom", bottom),
                            ("_speaker", speaker), ("_line", line));

            BuildDialogue(parent, speaker, line);
        }

        /// <summary>
        /// Одна полоса. Растянута по ширине и прижата к своему краю:
        /// меняется только высота, и опора стоит у края — иначе полоса
        /// росла бы в обе стороны и лезла бы в середину кадра.
        /// </summary>
        private static RectTransform Bar(string name, RectTransform parent, bool atTop)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            float edge = atTop ? 1f : 0f;
            rt.anchorMin = new Vector2(0f, edge);
            rt.anchorMax = new Vector2(1f, edge);
            rt.pivot = new Vector2(0.5f, edge);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 0f);

            var image = go.AddComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;

            // Строка выезжает вместе с полосой, а не висит в воздухе
            // над ней: без обрезки текст был бы виден ещё до того,
            // как полоса доедет.
            go.AddComponent<RectMask2D>();

            return rt;
        }

        /// <summary>
        /// Полоска родителя по высоте — от доли <paramref name="from"/> до доли
        /// <paramref name="to"/> снизу, во всю ширину с полями по бокам.
        /// </summary>
        private static void Band(RectTransform rt, float from, float to)
        {
            rt.anchorMin = new Vector2(0f, from);
            rt.anchorMax = new Vector2(1f, to);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(120f, 0f);
            rt.offsetMax = new Vector2(-120f, 0f);
        }

        /// <summary>Растянуть по ширине родителя с отступами сверху и снизу.</summary>
        private static void Stretch(RectTransform rt, float top, float bottom)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(120f, bottom);
            rt.offsetMax = new Vector2(-120f, top);
        }

        /// <summary>
        /// Подсказка «как ходить». Компонент висит на Canvas, а не на самой
        /// панели: в Start он панель выключает, а выключенный объект
        /// не крутит Update — и неподвижность отслеживать было бы нечем.
        /// На этом уже обожглись дважды, диалог и военный совет.
        /// </summary>
        /// <summary>
        /// Стопка над панелью выделенного: подпись предмета и три урока.
        ///
        /// Раньше у каждой строки было своё место на экране, отсчитанное
        /// от старого места панели выделенного. Панель подняли над сумой —
        /// а подсказку ходьбы и подпись предмета нет, и обе легли на неё:
        /// «Щёлкните по Греховоду…» поверх «Греховод · Никто не выделен»
        /// на всех кадрах лагеря 25 сентября.
        ///
        /// Стопка не даёт этому повториться: места считает она сама,
        /// а погашенная строка места не занимает. Видимые ложатся впритык
        /// к панели и друг к другу, сколько бы их ни было сразу.
        /// </summary>
        private static RectTransform BuildStack(Transform parent)
        {
            // Нижний край — над полосой выделенного с зазором.
            // Имя читает GearPanel: строка «F — …» встаёт над стопкой.
            var stack = Panel("Над выделенным", parent,
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.5f, 0f),
                pivot: new Vector2(0.5f, 0f), size: new Vector2(760f, 0f),
                position: new Vector2(0f, ConsoleHeight + 20f));

            var layout = stack.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.spacing = 8f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Высота — по видимым строкам, и растёт стопка вверх: опора
            // у нижнего края. Без этого высота нулевая, а раскладка при
            // нехватке места ведёт строки от верхнего края вниз, мимо
            // выравнивания, — подсказка снова легла на панель выделенного
            // (первый прогон стопки, 26 сентября).
            var fit = stack.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return stack;
        }

        /// <summary>Строка стопки: место ей назначит стопка, здесь только размер.</summary>
        private static RectTransform StackRow(string name, RectTransform stack, Vector2 size)
            => Panel(name, stack,
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.5f, 0f),
                pivot: new Vector2(0.5f, 0f), size: size, position: Vector2.zero);

        private static void BuildHint(Transform parent, RectTransform stack)
        {
            var panel = StackRow("Как ходить", stack, new Vector2(560f, 76f));

            var backdrop = Backdrop(panel, Weight.Strip);

            var line = Label("Строка", panel, 26, TextAnchor.MiddleCenter);
            line.color = new Color(0.88f, 0.86f, 0.82f);

            var ui = parent.gameObject.AddComponent<Sinbinder.UI.MovementHintUI>();
            Wire(ui, ("_panel", panel.gameObject), ("_text", line));
        }

        /// <summary>
        /// Прямоугольник выделения — тот самый, что тянут мышью.
        ///
        /// <see cref="Gameplay.SelectionManager"/> умеет тянуть рамку
        /// с самого начала, но поле <c>_selectionBox</c> не заполнял никто:
        /// выделение работало, а на экране не было видно ничего. Игрок
        /// тянул мышь по пустому месту и не мог понять, выделяет он или
        /// промахивается, — а это первый жест, которым в тактике вообще
        /// пользуются.
        ///
        /// Опора и якорь в левом нижнем углу: менеджер ставит рамке
        /// <c>position</c> в меньший угол и растит <c>sizeDelta</c>
        /// от него. При любой другой опоре рамка росла бы из середины
        /// и уезжала бы от курсора.
        ///
        /// Заливка плюс кромка, а не одна заливка: на светлой земле
        /// полупрозрачный прямоугольник без края теряется.
        /// </summary>
        private static void BuildSelectionBox(Transform parent)
        {
            var rt = Panel("Рамка выделения", parent,
                anchorMin: Vector2.zero, anchorMax: Vector2.zero,
                pivot: Vector2.zero, size: Vector2.zero, position: Vector2.zero);

            var fill = rt.gameObject.AddComponent<Image>();
            fill.color = new Color(0.45f, 0.85f, 0.5f, 0.14f);

            // Рамка не должна перехватывать щелчки: она рисуется поверх
            // земли ровно в тот момент, когда игрок по земле и щёлкает.
            fill.raycastTarget = false;

            Edge("Верх", rt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 2f));
            Edge("Низ", rt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2f));
            Edge("Левая", rt, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(2f, 0f));
            Edge("Правая", rt, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(2f, 0f));

            // Выключена: менеджер включает её на нажатие и гасит на отпуск.
            rt.gameObject.SetActive(false);
        }

        /// <summary>
        /// Сума: шесть ячеек справа внизу.
        ///
        /// Справа, потому что слева журнал, а по центру — выделенный воин.
        /// Три блока по краям нижней полосы, и ни один не наезжает
        /// на другой.
        ///
        /// Массивы связываются вручную: Wire умеет одну ссылку, а тут их
        /// по шесть на поле. Заводить ради этого перегрузку не стали —
        /// место в проекте одно.
        /// </summary>
        private static void BuildSatchel(Transform parent)
        {
            const int cells = Sinbinder.Core.Satchel.Size;
            const float cell = 132f;
            const float gap = 6f;

            var panel = Panel("Сума", parent,
                anchorMin: new Vector2(1f, 0f), anchorMax: new Vector2(1f, 0f),
                pivot: new Vector2(1f, 0f),
                size: new Vector2(cells * cell + (cells - 1) * gap, 66f),
                position: new Vector2(-40f, 40f));

            var texts = new Text[cells];
            var frames = new Image[cells];

            for (int i = 0; i < cells; i++)
            {
                var slot = Panel($"Ячейка {i + 1}", panel,
                    anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0f, 1f),
                    pivot: new Vector2(0f, 0.5f), size: new Vector2(cell, 0f),
                    position: new Vector2(i * (cell + gap), 0f));

                frames[i] = slot.gameObject.AddComponent<Image>();
                frames[i].color = new Color(0.10f, 0.09f, 0.09f, 0.80f);

                texts[i] = Label("Что", slot, 18, TextAnchor.MiddleCenter);
                texts[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                texts[i].verticalOverflow = VerticalWrapMode.Truncate;
            }

            var ui = parent.gameObject.AddComponent<Sinbinder.UI.SatchelUI>();
            Wire(ui, ("_panel", panel.gameObject));
            WireArray(ui, "_cells", texts);
            WireArray(ui, "_frames", frames);
        }

        /// <summary>Связать поле-массив. Wire умеет только одиночные ссылки.</summary>
        private static void WireArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);

            if (p == null)
            {
                Debug.LogWarning($"[СЦЕНЫ] У {target.GetType().Name} нет поля {field}");
                return;
            }

            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }
        /// <summary>
        /// Строка, в которой читается подпись предмета.
        ///
        /// Прямо над панелью выделенного воина, впритык: обе живут внизу
        /// по центру и вместе читаются как один блок, а не как две надписи,
        /// не поделившие экран. Нижняя строка стопки — место ей даёт она.
        ///
        /// Компонент, который её заполняет, стоит на Managers — там же,
        /// где и наблюдатель; здесь только связываем.
        /// </summary>
        private static void BuildPlateLine(RectTransform stack)
        {
            var panel = StackRow("Подпись предмета", stack, new Vector2(760f, 48f));

            var backdrop = Backdrop(panel, Weight.Strip);

            var line = Label("Строка", panel, 24, TextAnchor.MiddleCenter);
            line.color = new Color(0.90f, 0.88f, 0.82f);

            var sight = Object.FindFirstObjectByType<Sinbinder.UI.PlateSight>();
            if (sight != null) Wire(sight, ("_panel", panel.gameObject), ("_line", line));
            else Debug.LogWarning("[СЦЕНЫ] PlateSight в сцене не найден: "
                               + "подписи предметов показывать будет некому.");

            panel.gameObject.SetActive(false);
        }

        /// <summary>Одна сторона рамки. Растягивается вдоль, толщина — из size.</summary>
        private static void Edge(string name, RectTransform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = new Color(0.58f, 0.95f, 0.6f, 0.85f);
            image.raycastTarget = false;
        }

        /// <summary>
        /// Вторая ступень обучения: приказывать. Между «как ходить»
        /// и «как жать души» — в том порядке, в каком они нужны игроку.
        ///
        /// Строка своя, а не общая с движением: обе могут оказаться
        /// на экране разом, если игрок пошёл сам, не дождавшись первой.
        /// </summary>
        private static void BuildCommandHint(Transform parent, RectTransform stack)
        {
            var panel = StackRow("Как приказывать", stack, new Vector2(760f, 76f));

            var backdrop = Backdrop(panel, Weight.Strip);

            var line = Label("Строка", panel, 24, TextAnchor.MiddleCenter);
            line.color = new Color(0.88f, 0.86f, 0.82f);

            var ui = parent.gameObject.AddComponent<Sinbinder.UI.CommandHintUI>();
            Wire(ui, ("_panel", panel.gameObject), ("_text", line));
        }

        /// <summary>
        /// Нижняя панель: кто выделен, чем он живёт, что делает.
        ///
        /// Внизу по центру, под обеими подсказками: подсказки уходят,
        /// когда игрок научился, а панель остаётся навсегда — значит
        /// её место ниже, у самого края.
        ///
        /// Компонент на Canvas, а не на панели: панель он выключает сам,
        /// когда показывать некого, а у выключенного объекта не крутится
        /// Update. Тот же урок, что с военным советом и диалогом.
        /// </summary>
        private static void BuildSelectedUnit(Transform parent)
        {
            // Полоса во всю ширину у нижнего края — макет лагеря
            // (docs/42-INTERFACE.md §3). Тёмная кожа и железная кромка
            // сверху; видна только при выборе — гасит её сам компонент.
            // Слева две зоны: душа и голоса. Правая часть — отряд и приказы —
            // следующим шагом; пока там прежние сетка приказов и сума.
            var panel = Panel("Кто выделен", parent,
                anchorMin: Vector2.zero, anchorMax: new Vector2(1f, 0f),
                pivot: new Vector2(0.5f, 0f), size: new Vector2(0f, ConsoleHeight),
                position: Vector2.zero);

            var leather = panel.gameObject.AddComponent<Image>();
            leather.color = new Color(0.114f, 0.086f, 0.071f, 0.97f);

            var edge = Panel("Кромка", panel,
                anchorMin: new Vector2(0f, 1f), anchorMax: Vector2.one,
                pivot: new Vector2(0.5f, 1f), size: new Vector2(0f, 5f), position: Vector2.zero);
            var iron = edge.gameObject.AddComponent<Image>();
            iron.color = new Color(0.29f, 0.28f, 0.26f, 1f);
            iron.raycastTarget = false;

            // ---- душа ----
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            var well = At("Огонь души", panel, new Rect(28f, 30f, 112f, 124f));
            var wellImage = well.gameObject.AddComponent<Image>();
            wellImage.color = new Color(0.05f, 0.04f, 0.03f, 1f);
            wellImage.raycastTarget = false;

            // Капля пламени (UiStyle.Drop) — ставит панель при запуске; здесь места.
            var outer = At("Снаружи", well, new Rect(14f, 8f, 84f, 105f)).gameObject.AddComponent<Image>();
            outer.raycastTarget = false;
            var inner = At("Внутри", well, new Rect(30f, 46f, 52f, 64f)).gameObject.AddComponent<Image>();
            inner.raycastTarget = false;

            var name = Line("Имя", panel, new Rect(158f, 26f, 400f, 44f), 36);
            name.font = Sinbinder.UI.UiStyle.Title;     // имена — заглавными с засечками
            var craft = Line("Ремесло", panel, new Rect(158f, 74f, 400f, 28f), 20);
            var sins = Line("Грехи", panel, new Rect(158f, 102f, 400f, 28f), 20);
            var body = Line("Тело", panel, new Rect(158f, 130f, 400f, 28f), 20);

            // Руки Греховода: F «Говорить», E «Забрать душу» (42-INTERFACE §3).
            // Плашки, а не кнопки: делают их клавиши; тусклая — недоступно.
            // Слова ставит панель при показе (Loc.T): вписанное здесь осталось бы
            // русским в английской игре.
            var handsTitle = Line("Руки Греховода", panel, new Rect(28f, 166f, 400f, 20f), 15);
            var (talkPlate, talkText) = Hand(panel, new Rect(28f, 190f, 220f, 46f), "F");
            var (harvestPlate, harvestText) = Hand(panel, new Rect(260f, 190f, 272f, 46f), "E");
            var handsHint = Line("Подсказка рук", panel, new Rect(28f, 242f, 520f, 24f), 16);

            // ---- голоса ----
            var voices = At("Голоса", panel, new Rect(576f, 22f, 600f, 236f));
            var voicesBack = voices.gameObject.AddComponent<Image>();
            voicesBack.color = new Color(0.149f, 0.114f, 0.090f, 1f);
            voicesBack.raycastTarget = false;
            // Длинная причина обрезается краем зоны, а не лезет в соседнюю:
            // в макете так вылезали голоса в колонку отряда (разбор, круг 2).
            voices.gameObject.AddComponent<RectMask2D>();

            var title = Line("Заголовок", voices, new Rect(20f, 10f, 560f, 34f), 27);
            title.font = Sinbinder.UI.UiStyle.Title;

            var dot = At("Знак голоса", voices, new Rect(20f, 60f, 18f, 18f)).gameObject.AddComponent<Image>();
            dot.sprite = knob;
            dot.raycastTarget = false;
            var voice = Line("Голос", voices, new Rect(48f, 48f, 532f, 42f), 34, FontStyle.Bold);
            var reason = Line("Причина", voices, new Rect(48f, 92f, 532f, 28f), 20);

            var plateRect = At("Ваш приказ", voices, new Rect(14f, 132f, 572f, 76f));
            var plate = plateRect.gameObject.AddComponent<Image>();
            plate.color = new Color(0.227f, 0.173f, 0.133f, 1f);
            plate.raycastTarget = false;
            var order = Line("Строка", plateRect, new Rect(16f, 8f, 540f, 32f), 22, FontStyle.Bold);
            var orderWhy = Line("Почему", plateRect, new Rect(16f, 40f, 540f, 28f), 19);

            // Группа: «Лиска медлит · ещё трое — в строю» (42-INTERFACE §3).
            var group = Line("Остальные", voices, new Rect(20f, 210f, 560f, 24f), 17);

            // Греховод: на месте голосов — сума и кошель. Прячет и показывает панель.
            var satchel = Line("Сума", voices, new Rect(20f, 50f, 360f, 180f), 19);
            satchel.alignment = TextAnchor.UpperLeft;
            satchel.lineSpacing = 1.15f;
            var purse = Line("Кошель", voices, new Rect(392f, 50f, 196f, 180f), 18);
            purse.alignment = TextAnchor.UpperLeft;

            var ui = parent.gameObject.AddComponent<Sinbinder.UI.SelectedUnitPanelUI>();
            Wire(ui, ("_panel", panel.gameObject),
                     ("_flameOuter", outer), ("_flameInner", inner),
                     ("_nameLine", name), ("_craftLine", craft), ("_sinLine", sins), ("_bodyLine", body),
                     ("_handsTitle", handsTitle), ("_talkPlate", talkPlate), ("_talkText", talkText),
                     ("_harvestPlate", harvestPlate), ("_harvestText", harvestText), ("_handsHint", handsHint),
                     ("_voicesTitle", title), ("_voiceDot", dot), ("_voiceLine", voice), ("_reasonLine", reason),
                     ("_orderPlate", plate), ("_orderLine", order), ("_orderWhy", orderWhy),
                     ("_groupLine", group), ("_satchelText", satchel), ("_purseText", purse));
        }

        /// <summary>
        /// Плашка руки Греховода: овал кожи, светлая плашка клавиши слева, слово.
        /// Цвет плашки и слова ставит панель — горит или приглушена.
        /// </summary>
        private static (Image, Text) Hand(RectTransform parent, Rect r, string key)
        {
            var rt = At("Рука " + key, parent, r);
            var plate = rt.gameObject.AddComponent<Image>();
            plate.raycastTarget = false;     // овал ставит панель при запуске: рисованный кодом спрайт в сцену не сохранится

            var keyRect = At("Клавиша", rt, new Rect(14f, 11f, 26f, 24f));
            keyRect.gameObject.AddComponent<Image>().color = new Color(0.54f, 0.51f, 0.47f, 1f);
            var letter = Line("Буква", keyRect, new Rect(0f, 0f, 26f, 24f), 16, FontStyle.Bold);
            letter.alignment = TextAnchor.MiddleCenter;
            letter.color = new Color(0.08f, 0.06f, 0.05f);
            letter.text = key;

            var word = Line("Слово", rt, new Rect(50f, 0f, r.width - 58f, r.height), 19, FontStyle.Bold);
            return (plate, word);
        }

        /// <summary>Высота нижней полосы выбранного, точки холста 1920×1080.</summary>
        private const float ConsoleHeight = 280f;

        /// <summary>Прямоугольник от левого верхнего угла родителя: x вправо, y вниз.</summary>
        private static RectTransform At(string name, RectTransform parent, Rect r)
            => Panel(name, parent,
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(0f, 1f),
                pivot: new Vector2(0f, 1f), size: r.size, position: new Vector2(r.x, -r.y));

        /// <summary>Строка текста в прямоугольнике: одна строка, без переноса (макет: переносов нет).</summary>
        private static Text Line(string name, RectTransform parent, Rect r, int size,
                                 FontStyle style = FontStyle.Normal)
        {
            var text = At(name, parent, r).gameObject.AddComponent<Text>();
            // Жирный — своим файлом (PT Serif Bold), а не дорисованный Unity.
            text.font = style == FontStyle.Bold ? Sinbinder.UI.UiStyle.BodyBold : UIFont();
            text.fontSize = size;
            text.fontStyle = style == FontStyle.Bold ? FontStyle.Normal : style;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(0.90f, 0.86f, 0.78f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = "";
            return text;
        }

        /// <summary>
        /// Подсказка о жатве. Верхней строкой стопки: обе с «как ходить»
        /// живут внизу по центру, и наложиться друг на друга им нельзя.
        /// </summary>
        private static void BuildHarvestHint(Transform parent, RectTransform stack)
        {
            // Шире прочих: строка о жатве длинная, и в семьсот двадцать она
            // ложилась в три строки впритык к краям плашки.
            var panel = StackRow("Как жать души", stack, new Vector2(960f, 76f));

            var backdrop = Backdrop(panel, Weight.Strip);

            var line = Label("Строка", panel, 24, TextAnchor.MiddleCenter);
            line.color = new Color(0.88f, 0.86f, 0.82f);

            var ui = parent.gameObject.AddComponent<Sinbinder.UI.HarvestHintUI>();
            Wire(ui, ("_panel", panel.gameObject), ("_text", line));
        }

        /// <summary>
        /// Седьмой рычаг игрока: установка отряда.
        ///
        /// Движок читал её всё это время — BehaviorResolver складывает
        /// склонность отряда с характером воина, — а тронуть её игроку
        /// было нечем: панели не было ни в одной сцене. Рычаг существовал
        /// в виде труб без воды, как до этого искушения.
        ///
        /// Стоит слева вверху и гаснет через несколько секунд после
        /// переключения: это состояние, а не сообщение, и висеть постоянно
        /// ему незачем.
        /// </summary>
        private static void BuildStrategy(Transform parent)
        {
            var panel = Panel("Установка отряда", parent,
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(0f, 1f),
                pivot: new Vector2(0f, 1f), size: new Vector2(460f, 300f),
                position: new Vector2(40f, -40f));

            var backdrop = Backdrop(panel, Weight.Board);

            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;   // состояние, а не кнопки

            var label = Label("Строка", panel, 21, TextAnchor.UpperLeft,
                new Vector2(0f, -14f), 272f);
            label.color = new Color(0.88f, 0.86f, 0.82f);

            // Компонент на Canvas: панель он не выключает, но гасит
            // CanvasGroup, и держать его на ней всё равно не за чем.
            var ui = parent.gameObject.AddComponent<Sinbinder.UI.SquadStrategyUI>();
            Wire(ui, ("_label", label), ("_group", group));
        }

        /// <summary>Ступень 3: журнал. Пишет словами, что и почему произошло.</summary>
        /// <summary>
        /// Журнал-консоль: записи копятся, ничего не гаснет, есть прокрутка.
        ///
        /// Раньше здесь была одна строка с очередью, и ушедшую строку было
        /// не вернуть. В игре, которая продаётся объяснением отказа, это
        /// дороже любой другой потери: объяснение обязано оставаться
        /// на экране столько, сколько игрок захочет.
        /// </summary>
        private static void BuildLog(Transform parent)
        {
            var panel = Panel("Журнал", parent,
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0f, 0f),
                // Уже прежнего: девятьсот точек журнала доходили
                // до середины экрана и лезли под подпись выделенного,
                // а записей в нём редко больше пяти.
                // Над нижней полосой выбранного (макет лагеря): та выезжает
                // во всю ширину и закрыла бы журнал. Уже стопки подсказок
                // по центру — им не пересечься.
                pivot: new Vector2(0f, 0f), size: new Vector2(520f, 220f),
                position: new Vector2(40f, ConsoleHeight + 20f));

            var backdrop = Backdrop(panel, Weight.Board);

            // Окно просмотра с маской: без неё текст вылезал бы за края
            // панели, и прокрутка выглядела бы как поехавшая вёрстка.
            var view = Panel("Окно", panel,
                anchorMin: Vector2.zero, anchorMax: Vector2.one,
                pivot: new Vector2(0.5f, 0.5f), size: Vector2.zero, position: Vector2.zero);
            view.offsetMin = new Vector2(14f, 12f);
            view.offsetMax = new Vector2(-14f, -12f);

            var viewImage = view.gameObject.AddComponent<Image>();
            viewImage.color = new Color(1f, 1f, 1f, 0.004f);
            view.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            // Содержимое растёт вниз от верхнего края: опора сверху,
            // высота по тексту. При опоре в центре список ездил бы
            // сам по себе с каждой новой строкой.
            var content = Panel("Записи", view,
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0.5f, 1f), size: new Vector2(0f, 0f),
                position: Vector2.zero);

            var text = content.gameObject.AddComponent<Text>();
            text.font = UIFont();
            text.fontSize = 22;
            text.alignment = TextAnchor.UpperLeft;
            text.color = new Color(0.88f, 0.86f, 0.82f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            var ui = panel.gameObject.AddComponent<Sinbinder.UI.BattleLogUI>();
            Wire(ui, ("_line", text), ("_scroll", scroll));
        }

        /// <summary>
        /// Ступень 1: одно слово над тем, кто решил сам.
        ///
        /// Не в панели, а поверх всего и без фона: подпись ходит за
        /// воином по экрану, пока камера к нему наезжает. Панель здесь
        /// была бы рамкой, летающей по полю боя.
        /// </summary>
        private static void BuildMomentCaption(Transform parent)
        {
            var holder = new GameObject("Подпись момента", typeof(RectTransform));
            holder.transform.SetParent(parent, false);

            // Приведение обязательно: объект создан с RectTransform,
            // но статический тип у transform всё равно Transform,
            // а Label просит именно RectTransform.
            var box = (RectTransform)holder.transform;

            var line = Label("Слово", box, 40, TextAnchor.MiddleCenter);
            line.color = new Color(0.96f, 0.92f, 0.80f);

            // Опора по центру: подпись ставится в точку над головой,
            // и при любой другой опоре она уезжала бы вбок тем сильнее,
            // чем длиннее слово.
            var rt = line.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(420f, 60f);

            // Вторая строка: причина, вполсилы и мельче. Отдельным
            // Text, а не переносом в том же: у них разные прозрачность,
            // размер и своя галочка в настройках.
            var cause = Label("Причина", box, 24, TextAnchor.MiddleCenter);
            cause.color = new Color(0.90f, 0.88f, 0.84f);

            var crt = cause.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0f, 0f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(620f, 44f);

            var ui = holder.AddComponent<Sinbinder.UI.MomentCaption>();
            Wire(ui, ("_line", line), ("_cause", cause));
        }

        /// <summary>
        /// Настройка прозрачности: готовые наборы и галочки.
        ///
        /// Компонент вешается на Canvas, а не на панель, которую сам
        /// выключает: у выключенного объекта не крутится Update,
        /// и нажатие O осталось бы незамеченным. Совет и карта на этом
        /// уже спотыкались, и это третий раз.
        /// </summary>
        private static void BuildClarityPanel(Transform parent)
        {
            var panel = Panel("Прозрачность", parent,
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f), size: new Vector2(760f, 620f),
                position: Vector2.zero);

            var backdrop = Backdrop(panel, Weight.Screen);

            var title = Label("Заголовок", panel, 28, TextAnchor.UpperLeft,
                new Vector2(0f, -20f), 52f);

            var rows = Panel("Строки", panel,
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0f, 1f), size: new Vector2(0f, 540f),
                position: new Vector2(0f, -78f));
            rows.offsetMin = new Vector2(24f, rows.offsetMin.y);
            rows.offsetMax = new Vector2(-24f, rows.offsetMax.y);

            var ui = parent.gameObject.AddComponent<Sinbinder.UI.ClarityPanel>();
            Wire(ui, ("_panel", panel.gameObject), ("_title", title), ("_rows", rows),
                     ("_font", UIFont()));
        }

        /// <summary>
        /// Дверь в игру: режим партии и «продолжить».
        ///
        /// Ставится <b>только в первую сцену</b>, а не в общий набор:
        /// демо идёт через три сцены, и вопрос, заданный в каждой,
        /// превратил бы уговор в формальность. Сам компонент к тому же
        /// спрашивает один раз за запуск — два сторожа здесь не лишние,
        /// потому что сцены собираются по одной и забыть легко.
        /// </summary>
        private static void BuildStartPanel(Transform parent)
        {
            var panel = Panel("Начало", parent,
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f), size: new Vector2(1000f, 720f),
                position: Vector2.zero);

            var backdrop = Backdrop(panel, Weight.Screen);

            var title = Label("Заголовок", panel, 34, TextAnchor.UpperLeft,
                new Vector2(0f, -28f), 60f);

            var rows = Panel("Строки", panel,
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0f, 1f), size: new Vector2(0f, 600f),
                position: new Vector2(0f, -104f));
            rows.offsetMin = new Vector2(28f, rows.offsetMin.y);
            rows.offsetMax = new Vector2(-28f, rows.offsetMax.y);

            var ui = parent.gameObject.AddComponent<Sinbinder.UI.StartPanel>();
            Wire(ui, ("_panel", panel.gameObject), ("_title", title), ("_rows", rows),
                     ("_font", UIFont()));
        }

        /// <summary>
        /// Пауза. В каждой сцене, в отличие от начала: остановиться
        /// игрок вправе где угодно.
        /// </summary>
        private static void BuildPause(Transform parent)
        {
            var panel = Panel("Пауза", parent,
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f), size: new Vector2(760f, 600f),
                position: Vector2.zero);

            var backdrop = Backdrop(panel, Weight.Screen);

            var title = Label("Заголовок", panel, 30, TextAnchor.UpperLeft,
                new Vector2(0f, -22f), 54f);

            var rows = Panel("Строки", panel,
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0f, 1f), size: new Vector2(0f, 510f),
                position: new Vector2(0f, -82f));
            rows.offsetMin = new Vector2(24f, rows.offsetMin.y);
            rows.offsetMax = new Vector2(-24f, rows.offsetMax.y);

            var ui = parent.gameObject.AddComponent<Sinbinder.UI.PauseMenu>();
            Wire(ui, ("_panel", panel.gameObject), ("_title", title), ("_rows", rows),
                     ("_font", UIFont()));
        }

        /// <summary>Гнёзда сохранений. На Canvas по той же причине, что и прочие.</summary>
        private static void BuildSaveSlots(Transform parent)
        {
            var panel = Panel("Сохранения", parent,
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f), size: new Vector2(860f, 560f),
                position: Vector2.zero);

            var backdrop = Backdrop(panel, Weight.Screen);

            var title = Label("Заголовок", panel, 28, TextAnchor.UpperLeft,
                new Vector2(0f, -20f), 52f);

            var rows = Panel("Строки", panel,
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0f, 1f), size: new Vector2(0f, 480f),
                position: new Vector2(0f, -78f));
            rows.offsetMin = new Vector2(24f, rows.offsetMin.y);
            rows.offsetMax = new Vector2(-24f, rows.offsetMax.y);

            var ui = parent.gameObject.AddComponent<Sinbinder.UI.SaveSlotsPanel>();
            Wire(ui, ("_panel", panel.gameObject), ("_title", title), ("_rows", rows),
                     ("_font", UIFont()));
        }

        /// <summary>Ступень 2: подсказка при наведении.</summary>
        private static void BuildTooltip(Transform parent)
        {
            var panel = Panel("Подсказка", parent,
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(0f, 1f),
                pivot: new Vector2(0f, 1f), size: new Vector2(520f, 220f),
                position: new Vector2(0f, 0f));

            var frame = panel.gameObject.AddComponent<Image>();
            frame.color = new Color(0.06f, 0.06f, 0.07f, 0.88f);

            var text = Label("Текст", panel, 24, TextAnchor.UpperLeft);

            // На холст, а не на панель. Подсказка прячет панель в Awake,
            // и, живя на ней же, выключала саму себя: Update не шёл ни разу,
            // и вторая ступень прозрачности — «почему он так решил» при
            // наведении — не показалась в демо ни одному игроку.
            var ui = parent.gameObject.AddComponent<Sinbinder.UI.WarriorTooltipUI>();
            Wire(ui, ("_panel", panel), ("_text", text), ("_frame", frame));
        }

        private static GameObject Prop(string name, Transform parent,
            Vector3 position, float yaw = 0f, float scale = 1f)
        {
            var prefab = Resources.Load<GameObject>("Props/" + name);
            if (prefab == null) return null;

            var go = (GameObject)Object.Instantiate(prefab, parent);
            go.name = name;
            go.transform.position = position;

            // Поворот складывается с поворотом осей модели, а не заменяет
            // его. Blender пишет FBX с осью Z вверх, и Unity доворачивает
            // корень на 270° по X. Заданный напрямую поворот стирал это,
            // и предмет ложился на бок — сакура оказалась ростом 2,6 м
            // вместо 4,3 при высоте дерева три метра.
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Axis(prefab);
            // Умножаем, а не задаём: корень модели несёт множитель
            // единиц файла (у предметов из одного объекта это ×100).
            // Заданный напрямую масштаб стирал его, и предмет становился
            // сантиметровым — невидимым, но исправно стоящим в сцене.
            go.transform.localScale = go.transform.localScale * scale;

            Surface(go, name);
            return go;
        }

        /// <summary>Накрыть примитив той же землёй, что и всё вокруг.</summary>
        private static void Cover(GameObject go, string id)
        {
            var material = MaterialBuilder.Get(id);
            var renderer = go.GetComponent<Renderer>();
            if (material != null && renderer != null) renderer.sharedMaterial = material;
        }

        /// <summary>
        /// Поворот осей модели: тем, чем импортёр переводит Z-вверх
        /// Blender в Y-вверх Unity. Складывать с ним, а не затирать —
        /// иначе предмет ложится набок (см. ModelCheck, «поворот 270»).
        /// </summary>
        private static Quaternion Axis(GameObject prefab)
            => prefab == null ? Quaternion.identity : prefab.transform.localRotation;

        /// <summary>
        /// Чем покрыт предмет. Подменяется <b>только первый материал</b> —
        /// тот, из чего предмет сделан; второй и третий остаются свои:
        /// железные обручи бочки, тёмные прорези, перья. Подменить все
        /// значило бы потерять разницу между частями, ради которой
        /// у каждой модели их три.
        /// </summary>
        private static void Surface(GameObject go, string prop)
        {
            string id = SurfaceOf(prop);
            if (id == null) return;

            var material = MaterialBuilder.Get(id);
            if (material == null) return;

            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                if (slots.Length == 0) continue;

                slots[0] = material;
                renderer.sharedMaterials = slots;
            }
        }

        private static string SurfaceOf(string prop)
        {
            switch (prop)
            {
                case "Tent":
                    return "Fabric061";

                case "CouncilTable":
                case "Chest":
                case "ChestLid":
                case "Barrel":
                case "Crate":
                case "LogBench":
                case "TentPeg":
                case "Torch":
                    return "Planks037A";

                case "Rock":
                    return "Rock050";

                case "CryptGate":
                case "Throne":
                case "Altar":
                case "Coffin":
                    return "Bricks076A";

                default:
                    return null;      // знамя, сакура, катана — свой цвет
            }
        }

        // ──────────────────────────────────
        // Три веса панели
        // ──────────────────────────────────

        /// <summary>
        /// Фон и рамка панели. <b>Три веса, и это весь список.</b>
        ///
        /// Заведено 19 сентября по словам автора: «интерфейс должен быть
        /// хоть чуть-чуть для человека, сейчас он даже мне глаза режет».
        /// Померил сперва контраст — он оказался ни при чём, весь текст
        /// читается с запасом (9–15 к 1 при норме 4.5, разбор в 30-UI).
        ///
        /// Резало другое: **двенадцать панелей были одного веса**.
        /// Четыре разных почти-чёрных фона, три прозрачности, ни одной
        /// рамки — и на тёмной сцене все они читались одинаковыми
        /// пятнами с острыми краями. Глазу негде остановиться: приказ
        /// отряда, журнал и летучая подсказка выглядели одинаково важными.
        ///
        /// Разделение не выдумано — оно уже было в числах, просто
        /// не названо: модальные экраны стояли на 0.96–0.98, постоянные
        /// панели на 0.80–0.88, летучие подсказки на 0.82.
        /// Здесь это сделано осознанным.
        /// </summary>
        private enum Weight
        {
            /// <summary>Экран, который игрок читает: совет, карта, конец демо.</summary>
            Screen,

            /// <summary>Мебель: всегда в кадре — приказы, выделенный, журнал.</summary>
            Board,

            /// <summary>Летучая подсказка поверх мира. Рамка ей была бы тяжела.</summary>
            Strip,
        }

        private static readonly Color PanelInk = new Color(0.05f, 0.05f, 0.06f);
        private static readonly Color PanelEdge = new Color(0.62f, 0.58f, 0.50f);

        private static Image Backdrop(RectTransform panel, Weight weight)
        {
            float fill = weight switch
            {
                Weight.Screen => 0.96f,
                Weight.Board => 0.86f,
                _ => 0.82f,
            };

            var backdrop = panel.gameObject.AddComponent<Image>();
            backdrop.color = new Color(PanelInk.r, PanelInk.g, PanelInk.b, fill);

            float edge = weight switch
            {
                Weight.Screen => 0.30f,
                Weight.Board => 0.16f,
                _ => 0f,
            };

            if (edge > 0f) Frame(panel, edge);
            return backdrop;
        }

        /// <summary>
        /// Волосяная рамка по краю панели.
        ///
        /// Четырьмя полосками, а не картинкой: спрайта рамки в проекте
        /// нет, заводить его ради одной линии — лишний ассет, который
        /// потом ищут. Полоски не ловят мышь и лежат под содержимым,
        /// а содержимое стоит с отступом — пересечься им негде.
        ///
        /// Рамка здесь делает больше, чем кажется: она превращает пятно
        /// в предмет. Без неё панель на тёмной сцене — дыра, с ней —
        /// табличка.
        /// </summary>
        private static void Frame(RectTransform panel, float alpha)
        {
            const float Hair = 1.5f;

            var color = new Color(PanelEdge.r, PanelEdge.g, PanelEdge.b, alpha);

            (Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)[] sides =
            {
                (new Vector2(0f, 1f), Vector2.one,        new Vector2(0f, -Hair), Vector2.zero),
                (Vector2.zero,        new Vector2(1f, 0f), Vector2.zero,          new Vector2(0f, Hair)),
                (Vector2.zero,        new Vector2(0f, 1f), Vector2.zero,          new Vector2(Hair, 0f)),
                (new Vector2(1f, 0f), Vector2.one,        new Vector2(-Hair, 0f), Vector2.zero),
            };

            foreach (var side in sides)
            {
                var go = new GameObject("Край", typeof(RectTransform));
                go.transform.SetParent(panel, false);

                var rt = (RectTransform)go.transform;
                rt.anchorMin = side.min;
                rt.anchorMax = side.max;
                rt.offsetMin = side.offMin;
                rt.offsetMax = side.offMax;

                var image = go.AddComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
            }
        }

        private static RectTransform Panel(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }

        private static Text Label(string name, RectTransform parent, int size, TextAnchor anchor)
            => Label(name, parent, size, anchor, Vector2.zero, 0f);

        private static Text Label(string name, RectTransform parent, int size, TextAnchor anchor,
            Vector2 offset, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            if (height <= 0f)
            {
                // Растягиваем на всю панель с полями.
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(16f, 12f);
                rt.offsetMax = new Vector2(-16f, -12f);
            }
            else
            {
                // Поле слева — в самой позиции. Стояло оно в offsetMin,
                // а следующая строка ставила позицию заново и стирала его:
                // текст всех панелей с заголовком лёг на левую рамку,
                // а справа поле вышло двойным. Видно на снимках
                // прохождения 25 сентября — эпилог, «Кто выделен», отряд.
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(-32f, height);
                rt.anchoredPosition = new Vector2(16f + offset.x, offset.y);
            }

            var text = go.AddComponent<Text>();
            text.font = UIFont();
            text.fontSize = size;
            text.alignment = anchor;
            text.color = new Color(0.90f, 0.88f, 0.84f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = "";
            return text;
        }

        /// <summary>
        /// Встроенный шрифт. В новых версиях Unity Arial.ttf убран,
        /// его заменяет LegacyRuntime.ttf — пробуем оба, иначе весь текст
        /// интерфейса окажется невидимым, а причина неочевидной.
        /// </summary>
        private static Font UIFont()
        {
            // Книжная антиква макета (42-INTERFACE §1, п. 9) — на всех панелях
            // сразу: «один шрифт» (41-SHOWCASE п. 8). Нет файла — встроенный.
            var style = Sinbinder.UI.UiStyle.Body;
            if (style != null) return style;

            foreach (var name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try
                {
                    var f = Resources.GetBuiltinResource<Font>(name);
                    if (f != null) return f;
                }
                catch { /* этой версии Unity такой шрифт неизвестен */ }
            }

            Debug.LogWarning("[СЦЕНЫ] Встроенный шрифт не найден — текст интерфейса будет пуст.");
            return null;
        }

        /// <summary>
        /// Поля компонентов приватные, поэтому связываем через SerializedObject.
        /// Молчать при опечатке в имени поля нельзя: ссылка просто осталась бы
        /// пустой, и интерфейс молчал бы без единой ошибки.
        /// </summary>
        private static void Wire(Object target, params (string Field, Object Value)[] links)
        {
            var so = new SerializedObject(target);
            foreach (var link in links)
            {
                if (link.Value == null) continue;

                var p = so.FindProperty(link.Field);
                if (p == null)
                {
                    Debug.LogWarning($"[СЦЕНЫ] У {target.GetType().Name} нет поля {link.Field}");
                    continue;
                }
                p.objectReferenceValue = link.Value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Save(UnityEngine.SceneManagement.Scene scene, string name)
        {
            string path = $"{SceneDir}/{name}.unity";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            RegisterInBuildSettings(path);
            Debug.Log($"[СЦЕНЫ] Собрана: {path}");
        }

        /// <summary>
        /// Сцены набега больше нет: с 24 сентября разгром — событие лагеря
        /// (<see cref="RaidEvent"/>), и запись посреди него открывает лагерь.
        /// Проект, где старая сцена ещё лежит или стоит в списке сборки,
        /// при пересборке от неё избавляется: иначе в игру попала бы копия
        /// лагеря, в которую никто не ведёт, а список сборки ссылался бы
        /// на удалённый файл, и сборка игры падала бы на нём. Сцена целиком
        /// собиралась этим же сборщиком — своего в ней ничего нет.
        /// </summary>
        private static void RetireRaid()
        {
            const string raid = SceneDir + "/Prologue_Raid.unity";

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(raid) != null)
            {
                AssetDatabase.DeleteAsset(raid);
                Debug.Log($"[СЦЕНЫ] Удалена: {raid} — разгром теперь событие лагеря.");
            }

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.RemoveAll(s => s.path == raid) > 0)
                EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// Демо начинается с лагеря. Unity запускает сцену с нулевым
        /// номером в списке сборки, а туда попадала SampleScene — то есть
        /// собранный плеер стартовал бы в пустой заготовке Unity.
        /// </summary>
        private static void StartFromCamp()
        {
            const string first = SceneDir + "/Prologue_Camp.unity";

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int index = scenes.FindIndex(s => s.path == first);
            if (index <= 0) return;

            var camp = scenes[index];
            scenes.RemoveAt(index);
            scenes.Insert(0, camp);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void RegisterInBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;

            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
