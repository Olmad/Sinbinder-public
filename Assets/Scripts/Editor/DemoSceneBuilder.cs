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
        private const float FogDensity = 0.04f;
        private static readonly Color FogColor = new Color32(0x1A, 0x1A, 0x1A, 0xFF);

        // ---------- меню ----------

        [MenuItem("Sinbinder/Собрать сцены демо")]
        public static void BuildAll()
        {
            if (!ConfirmDiscard()) return;

            BuildCamp();
            BuildRaid();
            BuildCryptEntrance();

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
            Ground("Земля", 4f);
            Managers();

            // Доли 0 и 3 живут только здесь: строка открывает пролог,
            // совет собирается один раз и переносится дальше составом отряда.
            var canvas = Interface();
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

            // Стол стоит в стороне от того, куда смотрит открывающий кадр.
            // Взгляд с холма ложится у костра, и стол, поставленный туда же,
            // открыл бы совет на первом же кадре — игрок бы не подошёл
            // к нему, а оказался. Сходится это или нет, считает CheckCamp,
            // а не глаз.
            var table = new Vector3(3.0f, 0f, 2.2f);

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
            CouncilTable(table);

            // Сцена 3, первая половина: сундук с трофеями Марги. Стоит
            // по другую сторону от костра, чем стол, — чтобы к нему
            // пришлось идти отдельно, а не задеть взглядом заодно
            // с советом. Сходится это или нет, считает CheckCamp.
            var chest = new Vector3(-1.7f, 0f, -2.6f);
            TrophyChestProp(chest);

            CheckCamp(table, chest, eye, look, TentPlaces(hill, hillRadius: 5f));

            // Врагов в лагере нет: выступаем, когда назначен старший.
            // Здесь же пролог начинается — забываем прошлый отряд.
            Director("Prologue_Raid", waitForBattle: false, startsPrologue: true);

            Save(scene, "Prologue_Camp");
        }

        /// <summary>
        /// Тревога и разгром: те же восемь у того же огня, но с севера
        /// надвигаются охотники. Лагерь не переставляем — узнавание места
        /// и есть то, что делает разгром разгромом.
        /// </summary>
        private static void BuildRaid()
        {
            var scene = NewScene();
            Atmosphere(warm: true);
            Ground("Земля", 6f);
            Managers();

            var raidCanvas = Interface();
            BuildSalary(raidCanvas);
            BuildTitle(raidCanvas, "Лагерь знали не только свои.");

            // Сцена 5 живёт здесь: приказ отходить, отказ Каргана, побег.
            // Рог трубит на приказ, камера отъезжает после отказа — и то
            // и другое случается ровно по разу (docs/09-PROLOGUE.md §9).
            var camera = CameraRig(new Vector3(0f, 6.5f, -11f),
                                   new Vector3(28f, 0f, 0f), movable: true);
            camera.AddComponent<CameraPullback>();

            var horn = new GameObject("Рог");
            horn.AddComponent<AudioSource>();
            horn.AddComponent<RetreatHorn>();

            var campfire = Campfire(Vector3.zero);
            campfire.AddComponent<PrologueCampSpawner>();

            // Тот же лагерь, та же расстановка. Узнавание места и есть то,
            // что делает разгром разгромом, — значит палатки и холм обязаны
            // стоять там же, где стояли на доле 1.
            Hill(new Vector3(0f, 0f, -12f), radius: 5f, height: 2.6f);
            Tents(new Vector3(0f, 0f, -12f), hillRadius: 5f);
            CouncilTable(new Vector3(3.0f, 0f, 2.2f));

            // Две волны, как в сценарии (§4, сцена 4). Первая — трое слабых,
            // бой, который нельзя проиграть: игрок должен успеть поверить,
            // что он бог. Вторая выходит по опустевшему полю, заметно
            // сильнее, и она же открывает край карты — бежать полагается
            // от неё, а не вместо первой.
            Hunters(new Vector3(0f, 0f, 12f), Vector3.zero, count: 3, width: 5f,
                level: 1);

            // Уровень 2, а не выше: вторая волна обязана быть сильнее,
            // но не обязана всех положить. Побег — механика отбора, и
            // отбирать не из кого, если до края никто не добежал.
            // Жизнь 40, удар 7, защита 3 против своих 30 / 5 / 2.
            Hunters(new Vector3(0f, 0f, 15f), Vector3.zero, count: 6, width: 10f,
                level: 2, afterFieldClear: true, opensEscape: true,
                announce: "Карган: «Владыка, они узнали, где наш лагерь. "
                        + "Вероятно, от одного из наших. Тяжело это признавать, "
                        + "но нам нужно бежать».");

            // Уходим не по концу боя, а по краю карты: вторую волну
            // не полагается перебить, полагается унести от неё ноги.
            // Охотники идут с севера, значит бежать — на юг, за холм.
            Escape(new Vector3(0f, 0f, -25f), radius: 6f, openAtStart: false);

            // Прямо в склеп: сцены 6 и 7 сценария вырезаны из демо
            // (docs/09-PROLOGUE.md §10). Ни та ни другая не добавляли
            // механики — прогулка с разговорами и ещё один бой, — а тридцать
            // минут до расплаты доходило меньшинство.
            Director("Crypt_Entrance", waitForBattle: false, waitForEscape: true);

            Save(scene, "Prologue_Raid");
        }


        /// <summary>Бой у входа в склеп — чужого, найденного, а не родового.</summary>
        private static void BuildCryptEntrance()
        {
            var scene = NewScene();
            Atmosphere(warm: false);
            Ground("Камень", 5f);
            Managers();

            var canvas = Interface();
            BuildSalary(canvas);
            BuildDemoEnd(canvas);
            BuildTitle(canvas, "Кто-то уже занял этот склеп.");

            // Подвижная, как и в трёх других сценах: управление, которое
            // работает везде кроме одного места, читается как поломка,
            // а не как замысел.
            CameraRig(new Vector3(0f, 4.5f, -10f), new Vector3(22f, 0f, 0f),
                      movable: true);

            CryptGate(new Vector3(0f, 0f, 8f));

            var squad = new GameObject("Отряд");
            squad.transform.position = new Vector3(0f, 0f, -2f);
            squad.AddComponent<PrologueCampSpawner>();

            // Боя здесь больше нет: сцена 7 сценария вырезана вместе
            // со сценой 6. Склеп остался ради того единственного, ради чего
            // он в демо и был, — эпилога: игрок входит, и следом входит
            // отряд, отправленный полчаса назад.
            //
            // Врагов нет, значит и ждать конца боя нечего: доля кончается
            // по времени, и следом показывается эпилог — кто вернулся.
            Director(null, waitForBattle: false, endsAfterSeconds: 14f,
                arrivalLine: "Пустой трон. Алтарь. Замурованный гроб в нише.");

            Save(scene, "Crypt_Entrance");
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
            RenderSettings.ambientLight = new Color32(0x12, 0x12, 0x14, 0xFF);

            // Слабый холодный ключевой свет, чтобы геометрия читалась
            // и без костра. Он же — единственный источник в сценах без лагеря.
            var key = new GameObject("Холодный свет");
            key.transform.rotation = Quaternion.Euler(48f, 152f, 0f);
            var l = key.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(0.62f, 0.68f, 0.82f);
            l.intensity = warm ? 0.28f : 0.55f;
            l.shadows = LightShadows.Soft;
        }

        private static void Ground(string name, float scale)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = name;
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(scale, 1f, scale);

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

            // Кошелёк и вещи игрока. Его не было ни в одной сцене, поэтому
            // PlayerInventory.Instance всегда был пуст: плата после боя
            // уходила в никуда, а трофеи было некуда класть.
            managers.AddComponent<Sinbinder.Inventory.PlayerInventory>();

            // Угасающие души. Его не было ни в одной сцене, поэтому
            // жатва — механика сцены 4 — не работала вовсе.
            managers.AddComponent<SoulManager>();

            // Осматривает сцену на запуске и называет недостающие звенья.
            // Заведён потому, что пять разрывов подряд нашлись вручную
            // и по одному — а искать их надо не наугад.
            managers.AddComponent<SceneDoctor>();

            // Доля 6: полторы секунды тишины на первом отказе.
            managers.AddComponent<Sinbinder.UI.RefusalSilence>();

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
            bool movable = false)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(euler);

            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;

            go.AddComponent<AudioListener>();

            if (movable) go.AddComponent<RTS_Camera>();

            return go;
        }

        private static GameObject Campfire(Vector3 position)
        {
            var campfire = new GameObject("Костёр");
            campfire.transform.position = position;

            var light = new GameObject("Тёплый свет");
            light.transform.SetParent(campfire.transform);
            light.transform.localPosition = new Vector3(0f, 1.1f, 0f);

            var l = light.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.62f, 0.28f);
            l.intensity = 3.2f;
            l.range = 14f;
            l.shadows = LightShadows.Soft;

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

            // Палатка Греховода наверху, входом к лагерю: из неё он и выходит.
            Tent(position + new Vector3(0f, height, 0f), yaw: 0f, abandoned: false,
                name: "Палатка Греховода", size: 1.25f);

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
            const float clearance = 1.2f;

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
                    abandoned ? Fallen.NameFor(fallen - 1) : "");
                tent.transform.SetParent(camp.transform);
            }

            if (fallen != mourning)
                Debug.LogWarning($"[СБОРКА] Пустых палаток {fallen}, а должно быть {mourning}.");
        }

        /// <summary>
        /// Палатка — куб, повёрнутый на сорок пять градусов и наполовину
        /// ушедший в землю: над землёй остаётся треугольник. Один примитив
        /// на палатку, двадцать шесть примитивов на весь лагерь.
        ///
        /// Брошенная просела и завалилась набок, и рядом торчит колышек.
        /// Разница делается формой, а не цветом: материалов сборщик сцен
        /// не ставит нигде, и заводить их ради пяти палаток не стоит.
        /// </summary>
        private static GameObject Tent(Vector3 position, float yaw, bool abandoned,
            string name, float size, string fallenName = "")
        {
            var tent = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tent.name = name;
            tent.transform.position = position;

            float height = abandoned ? 0.55f : 1.0f;
            tent.transform.localScale = new Vector3(1.5f * size, 1.5f * size * height,
                                                    2.2f * size);
            tent.transform.rotation = Quaternion.Euler(abandoned ? 14f : 0f, yaw, 45f);

            if (!abandoned) return tent;

            var peg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            peg.name = "Колышек";
            peg.transform.SetParent(tent.transform.parent);
            peg.transform.position = position + new Vector3(0.9f, 0.35f, 0.9f);
            peg.transform.localScale = new Vector3(0.08f, 0.35f, 0.08f);
            peg.transform.rotation = Quaternion.Euler(9f, 0f, 5f);

            PegName(peg.transform.position, fallenName);

            return tent;
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
        private static void PegName(Vector3 pegTop, string fallenName)
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

        private static void CouncilTable(Vector3 position)
        {
            var table = new GameObject("Стол совета");
            table.transform.position = position;

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

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Хрустальный шар";
            ball.transform.SetParent(table.transform);
            ball.transform.localPosition = new Vector3(0f, 1.22f, 0f);
            ball.transform.localScale = Vector3.one * 0.42f;

            var glow = new GameObject("Свечение");
            glow.transform.SetParent(ball.transform);
            glow.transform.localPosition = Vector3.zero;

            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.45f, 0.62f, 0.95f);
            light.intensity = 1.4f;
            light.range = 7f;

            ball.AddComponent<CrystalBall>();
        }

        /// <summary>
        /// Сундук с трофеями: ящик и крышка на нём. Крышка — отдельный
        /// объект с собственной точкой поворота, иначе она открывалась бы
        /// вокруг собственной середины и въезжала бы в ящик.
        /// </summary>
        private static void TrophyChestProp(Vector3 position)
        {
            var chest = new GameObject("Сундук Марги");
            chest.transform.position = position;

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Ящик";
            box.transform.SetParent(chest.transform);
            box.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            box.transform.localScale = new Vector3(1.1f, 0.56f, 0.7f);

            // Петля у заднего края: крышка поворачивается вокруг неё.
            var hinge = new GameObject("Крышка");
            hinge.transform.SetParent(chest.transform);
            hinge.transform.localPosition = new Vector3(0f, 0.56f, -0.35f);

            var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lid.name = "Створка";
            lid.transform.SetParent(hinge.transform);
            lid.transform.localPosition = new Vector3(0f, 0.06f, 0.35f);
            lid.transform.localScale = new Vector3(1.15f, 0.12f, 0.75f);

            var trophy = chest.AddComponent<TrophyChest>();
            Wire(trophy, ("_lid", hinge.transform));
        }

        private static void Hunters(Vector3 position, Vector3 lookAt, int count,
            float width, int level = 1, bool afterFieldClear = false,
            bool opensEscape = false, string announce = "")
        {
            var go = new GameObject(afterFieldClear ? "Охотники: вторая волна"
                                                    : "Охотники");
            go.transform.position = position;
            go.transform.LookAt(new Vector3(lookAt.x, position.y, lookAt.z));

            var spawner = go.AddComponent<HunterSquadSpawner>();

            var so = new SerializedObject(spawner);
            so.FindProperty("_count").intValue = count;
            so.FindProperty("_lineWidth").floatValue = width;
            so.FindProperty("_level").intValue = level;
            so.FindProperty("_afterFieldClear").boolValue = afterFieldClear;
            so.FindProperty("_opensEscape").boolValue = opensEscape;
            so.FindProperty("_announce").stringValue = announce;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Вход в склеп: две опоры и перемычка. Больше и не нужно.</summary>
        private static void CryptGate(Vector3 position)
        {
            var gate = new GameObject("Вход в склеп");
            gate.transform.position = position;

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

            BuildLog(canvasGO.transform);
            BuildStrategy(canvasGO.transform);
            BuildHint(canvasGO.transform);
            BuildCommandHint(canvasGO.transform);
            BuildHarvestHint(canvasGO.transform);
            BuildSelectedUnit(canvasGO.transform);
            BuildTooltip(canvasGO.transform);
            BuildSoulAssembly(canvasGO.transform);
            // Выбор тела нужен везде, где можно собрать душу, а собрать
            // её можно в любой сцене с боем. Строим со всем остальным
            // интерфейсом, чтобы не гадать, где игрок нажмёт связывание.
            BuildShellPicker(canvasGO.transform);
            BuildDialogue(canvasGO.transform);

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
        /// Край карты: круг, до которого надо довести отряд. Ставится
        /// на противоположной от Охотников стороне — бежать полагается
        /// от них, а не сквозь них.
        /// </summary>
        private static void Escape(Vector3 position, float radius,
            bool openAtStart = true)
        {
            var go = new GameObject("Край карты");
            go.transform.position = position;

            var zone = go.AddComponent<EscapeZone>();
            var so = new SerializedObject(zone);
            so.FindProperty("_radius").floatValue = radius;
            so.FindProperty("_openAtStart").boolValue = openAtStart;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Край карты должен быть виден, иначе игрок не поймёт, куда
            // бежать, и решит, что механики нет. Два столба и холодный
            // свет между ними — дорога наружу.
            for (int side = -1; side <= 1; side += 2)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "Столб";
                post.transform.SetParent(go.transform);
                post.transform.localPosition = new Vector3(radius * 0.55f * side, 1.3f, 0f);
                post.transform.localScale = new Vector3(0.22f, 1.3f, 0.22f);
            }

            var beacon = new GameObject("Свет дороги");
            beacon.transform.SetParent(go.transform);
            beacon.transform.localPosition = new Vector3(0f, 2.4f, 0f);

            var light = beacon.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.58f, 0.72f, 0.95f);
            light.intensity = 2.2f;
            light.range = radius * 2.4f;
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

            var ui = panel.gameObject.AddComponent<Sinbinder.UI.PrologueTitleUI>();
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

            var backdrop = panel.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.03f, 0.03f, 0.04f, 0.98f);

            var title = Label("Заголовок", panel, 38, TextAnchor.UpperLeft,
                new Vector2(0f, -28f), 58f);
            var body = Label("Список", panel, 24, TextAnchor.UpperLeft,
                new Vector2(0f, -100f), 480f);

            var ui = panel.gameObject.AddComponent<Sinbinder.UI.DemoEndUI>();
            Wire(ui, ("_panel", panel.gameObject), ("_title", title), ("_body", body));
        }

        /// <summary>
        /// Плата после боя: второй соблазн пролога. Показывается сама,
        /// когда врагов на поле не осталось.
        /// </summary>
        private static void BuildSalary(Transform parent)
        {
            var panel = Panel("Плата", parent,
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f), size: new Vector2(760f, 300f),
                position: Vector2.zero);

            var backdrop = panel.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.06f, 0.05f, 0.04f, 0.96f);

            var title = Label("Заголовок", panel, 30, TextAnchor.UpperLeft,
                new Vector2(0f, -24f), 60f);

            var pay = Choice("Заплатить", panel, new Vector2(-170f, -60f), out var payLabel);
            var hold = Choice("Придержать", panel, new Vector2(170f, -60f), out var holdLabel);

            var ui = panel.gameObject.AddComponent<Sinbinder.UI.SalaryPanelUI>();
            Wire(ui, ("_panel", panel.gameObject), ("_title", title),
                     ("_payButton", pay), ("_payLabel", payLabel),
                     ("_withholdButton", hold), ("_withholdLabel", holdLabel));
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
            var panel = Panel("Военный совет", parent,
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f), size: new Vector2(980f, 560f),
                position: Vector2.zero);

            var backdrop = panel.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.05f, 0.05f, 0.06f, 0.96f);

            var title = Label("Заголовок", panel, 34, TextAnchor.UpperLeft,
                new Vector2(0f, -20f), 48f);

            // Контейнер строк: сами строки создаёт панель в рантайме,
            // потому что пророчество считается движком, а не редактором.
            var rows = Panel("Кандидаты", panel,
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0f, 1f), size: new Vector2(0f, 470f),
                position: new Vector2(0f, -80f));
            rows.offsetMin = new Vector2(24f, rows.offsetMin.y);
            rows.offsetMax = new Vector2(-24f, rows.offsetMax.y);

            // Компонент висит на Canvas, а не на самой панели: в Start он
            // панель выключает, а у выключенного объекта не крутится Update —
            // и слежение за тем, подошёл ли игрок к столу, не работало бы
            // ни разу. Ровно на этом уже обжёгся DialogueUI.
            var ui = parent.gameObject.AddComponent<Sinbinder.UI.CommanderCouncilUI>();
            Wire(ui, ("_panel", panel.gameObject), ("_title", title), ("_rows", rows),
                     ("_font", UIFont()));
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

            var backdrop = panel.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.05f, 0.05f, 0.06f, 0.96f);

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
        private static void BuildDialogue(Transform parent)
        {
            var panel = Panel("Разговор", parent,
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.5f, 0f),
                pivot: new Vector2(0.5f, 0f), size: new Vector2(1100f, 200f),
                position: new Vector2(0f, 200f));

            var backdrop = panel.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.05f, 0.05f, 0.06f, 0.90f);

            var speaker = Label("Говорящий", panel, 28, TextAnchor.UpperLeft, new Vector2(0f, -14f), 40f);
            var line = Label("Реплика", panel, 26, TextAnchor.UpperLeft, new Vector2(0f, -60f), 128f);

            // Компонент висит на Canvas, а не на самой панели: в Start он
            // панель выключает, а выключенный объект не крутит корутину
            // показа — разговор не начался бы ни разу.
            var ui = parent.gameObject.AddComponent<Sinbinder.UI.DialogueUI>();
            Wire(ui, ("_dialoguePanel", panel.gameObject),
                     ("_speakerNameText", speaker), ("_dialogueText", line));
        }

        /// <summary>
        /// Подсказка «как ходить». Компонент висит на Canvas, а не на самой
        /// панели: в Start он панель выключает, а выключенный объект
        /// не крутит Update — и неподвижность отслеживать было бы нечем.
        /// На этом уже обожглись дважды, диалог и военный совет.
        /// </summary>
        private static void BuildHint(Transform parent)
        {
            var panel = Panel("Как ходить", parent,
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.5f, 0f),
                pivot: new Vector2(0.5f, 0f), size: new Vector2(560f, 76f),
                position: new Vector2(0f, 190f));

            var backdrop = panel.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.05f, 0.05f, 0.06f, 0.82f);

            var line = Label("Строка", panel, 26, TextAnchor.MiddleCenter);
            line.color = new Color(0.88f, 0.86f, 0.82f);

            var ui = parent.gameObject.AddComponent<Sinbinder.UI.MovementHintUI>();
            Wire(ui, ("_panel", panel.gameObject), ("_text", line));
        }

        /// <summary>
        /// Вторая ступень обучения: приказывать. Между «как ходить»
        /// и «как жать души» — в том порядке, в каком они нужны игроку.
        ///
        /// Строка своя, а не общая с движением: обе могут оказаться
        /// на экране разом, если игрок пошёл сам, не дождавшись первой.
        /// </summary>
        private static void BuildCommandHint(Transform parent)
        {
            var panel = Panel("Как приказывать", parent,
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.5f, 0f),
                pivot: new Vector2(0.5f, 0f), size: new Vector2(760f, 76f),
                position: new Vector2(0f, 276f));

            var backdrop = panel.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.05f, 0.05f, 0.06f, 0.82f);

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
            var panel = Panel("Кто выделен", parent,
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.5f, 0f),
                pivot: new Vector2(0.5f, 0f), size: new Vector2(560f, 112f),
                position: new Vector2(0f, 16f));

            var backdrop = panel.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.05f, 0.05f, 0.06f, 0.88f);

            var name = Label("Имя", panel, 30, TextAnchor.MiddleLeft,
                new Vector2(0f, -10f), 38f);
            name.color = new Color(0.94f, 0.92f, 0.88f);

            var sin = Label("Шкала", panel, 22, TextAnchor.MiddleLeft,
                new Vector2(0f, -48f), 30f);
            sin.color = new Color(0.78f, 0.66f, 0.62f);

            var action = Label("Действие", panel, 22, TextAnchor.MiddleLeft,
                new Vector2(0f, -78f), 30f);
            action.color = new Color(0.72f, 0.76f, 0.80f);

            var ui = parent.gameObject.AddComponent<Sinbinder.UI.SelectedUnitPanelUI>();
            Wire(ui, ("_panel", panel.gameObject), ("_nameLine", name),
                     ("_sinLine", sin), ("_actionLine", action));
        }

        /// <summary>
        /// Подсказка о жатве. Отдельной строкой выше «как ходить»: обе
        /// живут внизу по центру, и наложиться друг на друга им нельзя.
        /// </summary>
        private static void BuildHarvestHint(Transform parent)
        {
            var panel = Panel("Как жать души", parent,
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.5f, 0f),
                pivot: new Vector2(0.5f, 0f), size: new Vector2(720f, 76f),
                position: new Vector2(0f, 366f));

            var backdrop = panel.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.05f, 0.05f, 0.06f, 0.82f);

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

            var backdrop = panel.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.05f, 0.05f, 0.06f, 0.80f);

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
        private static void BuildLog(Transform parent)
        {
            var panel = Panel("Журнал", parent,
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0f, 0f),
                pivot: new Vector2(0f, 0f), size: new Vector2(900f, 120f),
                position: new Vector2(40f, 40f));

            var group = panel.gameObject.AddComponent<CanvasGroup>();
            var line = Label("Строка", panel, 28, TextAnchor.LowerLeft);

            var ui = panel.gameObject.AddComponent<Sinbinder.UI.BattleLogUI>();
            Wire(ui, ("_line", line), ("_group", group));
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

            var ui = panel.gameObject.AddComponent<Sinbinder.UI.WarriorTooltipUI>();
            Wire(ui, ("_panel", panel), ("_text", text), ("_frame", frame));
        }

        /// <summary>
        /// Сборка души: имя, спектры словами, оболочка и пророчество.
        /// Пророчество — то самое, что на доле 3 делает отказ обещанием,
        /// а не подставой.
        /// </summary>
        private static void BuildSoulAssembly(Transform parent)
        {
            var panel = Panel("Сборка души", parent,
                anchorMin: new Vector2(1f, 1f), anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(1f, 1f), size: new Vector2(560f, 420f),
                position: new Vector2(-40f, -40f));

            var accent = panel.gameObject.AddComponent<Image>();
            accent.color = new Color(0.08f, 0.07f, 0.06f, 0.85f);

            var name = Label("Имя", panel, 32, TextAnchor.UpperLeft, new Vector2(0f, -16f), 44f);
            var spectra = Label("Спектры", panel, 22, TextAnchor.UpperLeft, new Vector2(0f, -70f), 150f);
            var shell = Label("Оболочка", panel, 22, TextAnchor.UpperLeft, new Vector2(0f, -228f), 44f);
            var prophecy = Label("Пророчество", panel, 22, TextAnchor.UpperLeft, new Vector2(0f, -280f), 120f);

            var ui = panel.gameObject.AddComponent<Sinbinder.UI.SoulAssemblyUI>();
            Wire(ui, ("_name", name), ("_spectra", spectra), ("_shell", shell),
                     ("_prophecy", prophecy), ("_accent", accent));
        }

        // ---------- мелкие помощники ----------

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
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.offsetMin = new Vector2(16f, 0f);
                rt.offsetMax = new Vector2(-16f, 0f);
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
                rt.anchoredPosition = offset;
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
