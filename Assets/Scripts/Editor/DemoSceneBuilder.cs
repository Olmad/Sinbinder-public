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
            BuildEscape();
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
            Hill(new Vector3(0f, 0f, -12f), radius: 5f, height: 2.6f);
            CameraRig(new Vector3(0f, 3.5f, -12.5f), new Vector3(13f, 0f, 0f));

            var campfire = Campfire(Vector3.zero);
            campfire.AddComponent<PrologueCampSpawner>();

            Tents(new Vector3(0f, 0f, -12f), hillRadius: 5f);
            CouncilTable(new Vector3(3.2f, 0f, -3.4f));

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
            BuildSalary(Interface());
            CameraRig(new Vector3(0f, 6.5f, -11f), new Vector3(28f, 0f, 0f), movable: true);

            var campfire = Campfire(Vector3.zero);
            campfire.AddComponent<PrologueCampSpawner>();

            // Тот же лагерь, та же расстановка. Узнавание места и есть то,
            // что делает разгром разгромом, — значит палатки и холм обязаны
            // стоять там же, где стояли на доле 1.
            Hill(new Vector3(0f, 0f, -12f), radius: 5f, height: 2.6f);
            Tents(new Vector3(0f, 0f, -12f), hillRadius: 5f);
            CouncilTable(new Vector3(3.2f, 0f, -3.4f));

            Hunters(new Vector3(0f, 0f, 12f), Vector3.zero, count: 4, width: 7f);

            // Уходим не по концу боя, а по краю карты: вторую волну
            // не полагается перебить, полагается унести от неё ноги.
            // Охотники идут с севера, значит бежать — на юг, за холм.
            Escape(new Vector3(0f, 0f, -25f), radius: 6f);
            Director("Prologue_Escape", waitForBattle: false, waitForEscape: true);

            Save(scene, "Prologue_Raid");
        }

        /// <summary>
        /// Побег. Тёплого света больше нет — дальше только холод
        /// (пролог §4.6), и это единственное, чем сцена отличается
        /// от предыдущей по свету.
        /// </summary>
        private static void BuildEscape()
        {
            var scene = NewScene();
            Atmosphere(warm: false);
            Ground("Дорога", 8f);
            Managers();
            BuildSalary(Interface());
            CameraRig(new Vector3(0f, 5.5f, -12f), new Vector3(24f, 0f, 0f), movable: true);

            var squad = new GameObject("Отряд");
            squad.transform.position = Vector3.zero;
            squad.AddComponent<PrologueCampSpawner>();

            // Погоня отстаёт, но идёт: охотников больше, чем было в лагере.
            Hunters(new Vector3(0f, 0f, 16f), Vector3.zero, count: 6, width: 10f);
            Director("Crypt_Entrance", waitForBattle: true);

            Save(scene, "Prologue_Escape");
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

            CameraRig(new Vector3(0f, 4.5f, -10f), new Vector3(22f, 0f, 0f));

            CryptGate(new Vector3(0f, 0f, 8f));

            var squad = new GameObject("Отряд");
            squad.transform.position = new Vector3(0f, 0f, -2f);
            squad.AddComponent<PrologueCampSpawner>();

            Hunters(new Vector3(0f, 0f, 5f), Vector3.zero, count: 4, width: 6f);

            // Последняя доля: дальше не сцена, а список тех, кто дошёл.
            Director(null, waitForBattle: true);

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
        private static void CameraRig(Vector3 position, Vector3 euler, bool movable = false)
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
        private static void Tents(Vector3 hillCentre, float hillRadius)
        {
            const int total = 25;
            const int inner = 11;
            const int mourning = 5;
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
                    abandoned ? $"Палатка павшего {fallen}" : $"Палатка {i + 1}", 1f);
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
            string name, float size)
        {
            var tent = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tent.name = name;
            tent.transform.position = position;

            float height = abandoned ? 0.55f : 1.0f;
            tent.transform.localScale = new Vector3(1.5f * size, 1.5f * size * height,
                                                    2.2f * size);
            tent.transform.rotation = Quaternion.Euler(abandoned ? 14f : 0f, yaw, 45f);

            if (!abandoned) return tent;

            // Колышек: имя павшего повесить пока не на что — TextMesh требует
            // шрифта, которого в сборщике нет. Колышек стоит, имя ждёт.
            var peg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            peg.name = "Колышек";
            peg.transform.SetParent(tent.transform.parent);
            peg.transform.position = position + new Vector3(0.9f, 0.35f, 0.9f);
            peg.transform.localScale = new Vector3(0.08f, 0.35f, 0.08f);
            peg.transform.rotation = Quaternion.Euler(9f, 0f, 5f);

            return tent;
        }

        /// <summary>
        /// Стол военного совета с хрустальным шаром. Шар — предмет в лагере,
        /// который видно от палатки: на доле 3 он наливается красным,
        /// и это первое, что игрок замечает, не подходя к столу.
        /// </summary>
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

        private static void Hunters(Vector3 position, Vector3 lookAt, int count, float width)
        {
            var go = new GameObject("Охотники");
            go.transform.position = position;
            go.transform.LookAt(new Vector3(lookAt.x, position.y, lookAt.z));

            var spawner = go.AddComponent<HunterSquadSpawner>();

            var so = new SerializedObject(spawner);
            so.FindProperty("_count").intValue = count;
            so.FindProperty("_lineWidth").floatValue = width;
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
            BuildTooltip(canvasGO.transform);
            BuildSoulAssembly(canvasGO.transform);
            BuildDialogue(canvasGO.transform);
            BuildTemptations(canvasGO.transform);

            return canvasGO.transform;
        }

        /// <summary>
        /// Ведущий пролога: чем кончается доля и куда идти дальше.
        /// Без него четыре сцены остаются четырьмя тестами.
        /// </summary>
        private static void Director(string nextScene, bool waitForBattle,
            bool startsPrologue = false, bool waitForEscape = false)
        {
            var go = new GameObject("Ведущий пролога");
            var director = go.AddComponent<PrologueDirector>();

            var so = new SerializedObject(director);
            so.FindProperty("_nextScene").stringValue = nextScene ?? "";
            so.FindProperty("_waitForBattle").boolValue = waitForBattle;
            so.FindProperty("_waitForEscape").boolValue = waitForEscape;
            so.FindProperty("_startsPrologue").boolValue = startsPrologue;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Край карты: круг, до которого надо довести отряд. Ставится
        /// на противоположной от Охотников стороне — бежать полагается
        /// от них, а не сквозь них.
        /// </summary>
        private static void Escape(Vector3 position, float radius)
        {
            var go = new GameObject("Край карты");
            go.transform.position = position;

            var zone = go.AddComponent<EscapeZone>();
            var so = new SerializedObject(zone);
            so.FindProperty("_radius").floatValue = radius;
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
        private static void BuildTitle(Transform parent)
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
        }

        /// <summary>
        /// Восьмой рычаг: вещи, которые можно вложить в руки воину.
        /// Панель висит всё время — искушение медленное и обратимое,
        /// у него нет «своего момента».
        /// </summary>
        private static void BuildTemptations(Transform parent)
        {
            var panel = Panel("Искушения", parent,
                anchorMin: new Vector2(1f, 0f), anchorMax: new Vector2(1f, 0f),
                pivot: new Vector2(1f, 0f), size: new Vector2(520f, 268f),
                position: new Vector2(-40f, 40f));

            var backdrop = panel.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.07f, 0.06f, 0.06f, 0.85f);

            var hint = Label("Подсказка", panel, 19, TextAnchor.LowerLeft,
                new Vector2(0f, -222f), 40f);

            var rows = Panel("Вещи", panel,
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0f, 1f), size: new Vector2(0f, 210f),
                position: new Vector2(0f, -14f));
            rows.offsetMin = new Vector2(14f, rows.offsetMin.y);
            rows.offsetMax = new Vector2(-14f, rows.offsetMax.y);

            var ui = panel.gameObject.AddComponent<Sinbinder.UI.TemptationPanelUI>();
            Wire(ui, ("_rows", rows), ("_hint", hint), ("_font", UIFont()));
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

            var ui = panel.gameObject.AddComponent<Sinbinder.UI.CommanderCouncilUI>();
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
