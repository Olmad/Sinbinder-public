// Runtime-built, PHYSICAL expedition zones. No Unity scene address or
// pre-baked demo-only layout is required. All geometry is real mesh/collider,
// with a separate NavMeshSurface scoped to its own map.
//
// 27.11 (автор: экспедиции «пустые и бесцветные»): ходят и сталкиваются
// по-прежнему с примитивами — раскладка и навмеш те же, что были, — а
// видно настоящие вещи лагеря: ели, мёртвые деревья, камни, шатёр,
// факелы с огнём (Resources/MapProps) и землю с текстурой
// (Resources/MapTerrainMaterials). Трава, кусты и лес за краем карты —
// только вид: коллайдеров у них нет, навмеш их не видит.
using System.Collections.Generic;
using Sinbinder.Core;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace Sinbinder.Gameplay
{
    public static class ExpeditionMapBuilder
    {
        public static Vector3 Origin(ExpeditionMapKind kind)
            => kind == ExpeditionMapKind.SaltRoad
                ? new Vector3(2000f, 0f, 0f)
                : new Vector3(2500f, 0f, 0f);

        /// <summary>Имя повозки на карте обоза: её находит отъезд (CartRoll).</summary>
        public const string Cart = "Повозка";

        /// <summary>Начало имени света над меткой; дальше — подпись метки.</summary>
        public const string Beacon = "Световая метка · ";

        /// <summary>
        /// Карта вылазки. <paramref name="caravanGone"/> — обоз отпустили в прошлый раз:
        /// он уехал, и повозок на дороге больше нет (место пустует, 69 §7 п.1б).
        /// </summary>
        public static GameObject Build(ExpeditionMapPlan plan, bool caravanGone = false)
        {
            bool road = plan.Kind == ExpeditionMapKind.SaltRoad;
            var baseAt = Origin(plan.Kind);
            var root = new GameObject("Экспедиция · " + plan.Title);
            root.transform.position = baseAt;

            var ground = Part(root.transform, "Земля", PrimitiveType.Plane,
                new Vector3(0f, -0.08f, 0f),
                new Vector3(plan.Width / 10f, 1f, plan.Depth / 10f),
                road
                    ? new Color(0.48f, 0.38f, 0.24f)
                    : new Color(0.22f, 0.28f, 0.24f));
            if (ground == null) { Object.Destroy(root); return null; }
            // Тот же шаг текстуры, что у земли лагеря: хвоя бора у часовни,
            // старая дорога у обоза.
            Wear(ground, road ? "Ground048" : "Ground077", plan.Width / 8f, plan.Depth / 8f,
                road ? new Color(0.92f, 0.84f, 0.72f) : Color.white);

            // No invisible drop-off at the edge; four actual enclosing
            // rock banks, with a walkable interior and a clear centre path.
            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? -1f : 1f;
                var side = Part(root.transform, "Край карты (боковой)", PrimitiveType.Cube,
                    new Vector3(sign * plan.Width * 0.5f, 1.4f, 0f),
                    new Vector3(2.2f, 3f, plan.Depth + 2f),
                    new Color(0.19f, 0.17f, 0.16f));
                Wear(side, "Rock022", plan.Depth / 4f, 1f, Grey(0.62f));
                var end = Part(root.transform, "Край карты (торцевой)", PrimitiveType.Cube,
                    new Vector3(0f, 1.4f, sign * plan.Depth * 0.5f),
                    new Vector3(plan.Width + 2f, 3f, 2.2f),
                    new Color(0.19f, 0.17f, 0.16f));
                Wear(end, "Rock022", plan.Width / 4f, 1f, Grey(0.62f));
            }

            if (road)
            {
                // Wide, bent roadside path gives the player a distinct
                // readable line from the starting camp to the wagons.
                for (int i = 0; i < 17; i++)
                {
                    float z = -43f + i * 5f;
                    float x = Mathf.Sin(i * 0.37f) * 2.8f;
                    // Плита — по ходу дороги и чуть длиннее шага: без ступенек
                    // на изгибе. Высота та же — ходят по ней, как раньше.
                    float yaw = Mathf.Atan(2.8f * 0.37f * Mathf.Cos(i * 0.37f) / 5f) * Mathf.Rad2Deg;
                    var strip = Part(root.transform, "Утоптанная дорога", PrimitiveType.Cube,
                        new Vector3(x, -0.055f, z), new Vector3(12.5f, 0.05f, 6.1f),
                        new Color(0.59f, 0.48f, 0.31f), yaw);
                    Wear(strip, "Gravel043", 12.5f / 4f, 6.1f / 4f, new Color(1f, 0.93f, 0.8f));
                }
            }
            else
            {
                for (int i = 0; i < 16; i++)
                {
                    float yaw = Mathf.Atan(8f * 0.16f * Mathf.Cos(i * 0.16f) / 5f) * Mathf.Rad2Deg;
                    var strip = Part(root.transform, "Мокрая тропа", PrimitiveType.Cube,
                        new Vector3(8f * Mathf.Sin(i * 0.16f), -0.055f, -42f + i * 5f),
                        new Vector3(8.5f, 0.05f, 6.3f), new Color(0.27f, 0.26f, 0.23f), yaw);
                    Wear(strip, "Ground048", 8.5f / 5f, 6.3f / 5f, new Color(0.9f, 0.84f, 0.74f));
                }
            }

            int n = 0;
            foreach (var p in plan.Props)
            {
                if (caravanGone && p.Kind == ExpeditionPropKind.Wagon) { n++; continue; }
                AddProp(root.transform, p, plan.Kind, n++);
            }

            // Built after terrain and props but BEFORE warriors. Otherwise
            // moving bodies are baked into the navigation surface.
            // По коллайдерам, а не по видимым сеткам: одетые модели и трава
            // не меняют проходимость — она та же, что у примитивов.
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            // Failing closed is safer than moving the entire AOS squad onto
            // a map with no navigable entrance, leaving it permanently stuck.
            var entrance = Point(plan, 0f, plan.EntranceZ + 4f);
            if (!NavMesh.SamplePosition(entrance, out _, 3f, NavMesh.AllAreas))
            {
                Debug.LogError("[EXPEDITION] NavMesh has no valid entrance: " + plan.Id);
                surface.RemoveData();
                Object.Destroy(root);
                return null;
            }

            Scatter(root.transform, plan);
            return root;
        }

        public static Vector3 Point(ExpeditionMapPlan map, float x, float z)
            => Origin(map.Kind) + new Vector3(x, 0.17f, z);

        public static GameObject Mark(Transform parent, string label, Vector3 local,
                                      Color color, float radius = 1.1f)
        {
            var go = Part(parent, label, PrimitiveType.Cylinder,
                new Vector3(local.x, 0.13f, local.z),
                new Vector3(radius, 0.12f, radius), color);
            // A vertical beacon, visible in both tactical and first person.
            // Имя с подписью метки: убрать цель — значит убрать и её свет (отпущенный обоз).
            var beacon = new GameObject(Beacon + label);
            beacon.transform.SetParent(parent, false);
            beacon.transform.localPosition = new Vector3(local.x, 3.5f, local.z);
            var light = beacon.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 10f;
            light.intensity = 1.9f;
            light.color = color;
            return go;
        }

        private static GameObject Part(Transform root, string name, PrimitiveType type,
                                       Vector3 local, Vector3 scale, Color color,
                                       float yaw = 0f)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(root, false);
            part.transform.localPosition = local;
            part.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            part.transform.localScale = scale;
            var renderer = part.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = color;
            return part;
        }

        private static Color Grey(float v) => new Color(v, v, v);

        // Одна копия материала на каждую пару «текстура × шаг»: дорога из
        // семнадцати плит — один материал, а не семнадцать.
        private static readonly Dictionary<string, Material> _worn = new Dictionary<string, Material>();

        /// <summary>
        /// Текстура земли или камня на примитив. Нет текстуры — остаётся
        /// прежний ровный цвет: карта не ломается, только беднеет.
        /// </summary>
        private static void Wear(GameObject part, string id, float tilesX, float tilesY, Color tint)
        {
            var renderer = part != null ? part.GetComponent<Renderer>() : null;
            if (renderer == null) return;
            string key = id + "|" + tilesX.ToString("0.##") + "|" + tilesY.ToString("0.##") + "|" + ColorUtility.ToHtmlStringRGB(tint);
            if (!_worn.TryGetValue(key, out var material) || material == null)
            {
                var source = MapAssets.TerrainMaterial(id);
                if (source == null) return;
                material = new Material(source) { name = source.name + " (экспедиция)" };
                material.mainTextureScale = new Vector2(tilesX, tilesY);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
                _worn[key] = material;
            }
            var instance = renderer.material;
            renderer.sharedMaterial = material;
            if (instance != null && instance != material) Object.Destroy(instance);
        }

        /// <summary>Материал готовой вещи лагеря — на примитив. Нет вещи — прежний цвет.</summary>
        private static void Borrow(GameObject part, string prop)
        {
            var renderer = part != null ? part.GetComponent<Renderer>() : null;
            var prefab = MapLoader.Prefab(prop);
            var source = prefab != null ? prefab.GetComponentInChildren<Renderer>() : null;
            if (renderer == null || source == null || source.sharedMaterial == null) return;
            var instance = renderer.material;
            renderer.sharedMaterial = source.sharedMaterial;
            if (instance != null) Object.Destroy(instance);
        }

        /// <summary>
        /// Настоящая модель на месте примитива. Примитив остаётся телом —
        /// коллайдер и навмеш его, — но становится невидимым. Модели нет —
        /// примитив виден, как раньше.
        /// </summary>
        private static GameObject Dress(Transform root, GameObject stand, string prop,
                                        Vector3 local, float yaw, float height, float footprint = 0f,
                                        float sink = 0.06f)
        {
            var go = Model(root, prop, local, yaw, height, footprint, sink);
            if (go != null && stand != null)
            {
                var r = stand.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
            }
            return go;
        }

        /// <summary>
        /// Модель из Resources/MapProps по высоте (или по пятну на земле),
        /// стоит на земле, без коллайдеров. Поворот импорта сохраняется,
        /// как у сборщика сцен.
        /// </summary>
        private static GameObject Model(Transform root, string prop, Vector3 local, float yaw,
                                        float height, float footprint = 0f, float sink = 0.06f)
        {
            var prefab = MapLoader.Prefab(prop);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, root);
            go.name = prop;
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.localRotation;
            go.transform.localScale = prefab.transform.localScale;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);

            if (!Measure(go, out var b) || b.size.y < 0.01f) { Object.Destroy(go); return null; }
            float k = footprint > 0f
                ? footprint / Mathf.Max(0.01f, (b.size.x + b.size.z) * 0.5f)
                : (height > 0f ? height / b.size.y : 1f);
            go.transform.localScale = prefab.transform.localScale * k;

            Measure(go, out b);
            float groundY = root.position.y + local.y;
            go.transform.position += Vector3.up * (groundY - sink - b.min.y);
            return go;
        }

        /// <summary>
        /// Плоской модели — высоту тела: тянем только ту ось модели, что
        /// смотрит вверх (у моделей из Blender это часто Z), и снова ставим на землю.
        /// </summary>
        private static void Stretch(GameObject go, float groundY, float height, float sink)
        {
            if (go == null || !Measure(go, out var b) || b.size.y < 0.01f) return;
            float k = height / b.size.y;
            if (k <= 1f) return;
            var up = go.transform.InverseTransformDirection(Vector3.up);
            var ls = go.transform.localScale;
            go.transform.localScale = new Vector3(
                ls.x * Mathf.Lerp(1f, k, Mathf.Abs(up.x)),
                ls.y * Mathf.Lerp(1f, k, Mathf.Abs(up.y)),
                ls.z * Mathf.Lerp(1f, k, Mathf.Abs(up.z)));
            Measure(go, out b);
            go.transform.position += Vector3.up * (groundY - sink - b.min.y);
        }

        private static bool Measure(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        private static readonly string[] Pines = { "Pine", "TallPineA", "TallPineB", "TallPineC" };
        private static readonly string[] DeadTrees = { "DeadTreeA", "DeadTreeB", "DeadTreeC" };

        private static void AddProp(Transform root, ExpeditionProp p, ExpeditionMapKind kind, int n)
        {
            var local = new Vector3(p.X, 0f, p.Z);
            float s = p.Size;
            var stone = kind == ExpeditionMapKind.SaltRoad
                ? new Color(0.37f, 0.31f, 0.26f) : new Color(0.32f, 0.34f, 0.32f);
            switch (p.Kind)
            {
                case ExpeditionPropKind.Rock:
                case ExpeditionPropKind.Rubble:
                {
                    var block = Part(root, p.Kind == ExpeditionPropKind.Rock ? "Валун" : "Обвал",
                        PrimitiveType.Cube, local + Vector3.up * s * 0.6f,
                        new Vector3(1.6f * s, 1.2f * s, 1.7f * s), stone, p.Yaw);
                    // Камень чуть шире и выше тела, чтобы невидимый край не торчал.
                    // Низ модели уже верха — камень сидит в земле, а не на ножке.
                    var rock = Dress(root, block, "Rock", local, p.Yaw, 0f, 1.95f * s, 0.3f * s);
                    Stretch(rock, local.y + root.position.y, 1.5f * s, 0.32f * s);
                    break;
                }
                case ExpeditionPropKind.DeadTree:
                case ExpeditionPropKind.Pine:
                {
                    bool pine = p.Kind == ExpeditionPropKind.Pine;
                    var trunk = Part(root, "Ствол", PrimitiveType.Cylinder, local + Vector3.up * 2.2f * s,
                        new Vector3(0.28f * s, 2.2f * s, 0.28f * s),
                        new Color(0.23f, 0.17f, 0.13f), p.Yaw);
                    GameObject crown = null;
                    if (pine)
                        crown = Part(root, "Еловые ветви", PrimitiveType.Cylinder,
                            local + Vector3.up * 3.5f * s,
                            new Vector3(1.5f * s, 1.45f * s, 1.5f * s),
                            new Color(0.13f, 0.23f, 0.18f));
                    var tree = pine
                        ? Dress(root, trunk, Pines[n % Pines.Length], local, p.Yaw + n * 41f, 7.5f * s)
                        : Dress(root, trunk, DeadTrees[n % DeadTrees.Length], local, p.Yaw, 5f * s);
                    if (tree != null && crown != null) crown.GetComponent<Renderer>().enabled = false;
                    break;
                }
                case ExpeditionPropKind.Grave:
                {
                    var slab = Part(root, "Могильная плита", PrimitiveType.Cube,
                        local + Vector3.up * 0.85f * s,
                        new Vector3(0.55f*s,1.7f*s,0.3f*s),
                        new Color(0.42f,0.44f,0.43f),p.Yaw);
                    Wear(slab, "Rock022", 0.6f, 1.2f, Grey(0.78f));
                    var mound = Part(root, "Могильный холм", PrimitiveType.Cube,
                        local + new Vector3(0f,0.10f,-0.7f*s),
                        new Vector3(1.3f*s,0.2f,1.6f*s),stone,p.Yaw);
                    Wear(mound, "Ground077", 0.7f, 0.8f, Grey(0.7f));
                    break;
                }
                case ExpeditionPropKind.Wagon:
                {
                    // Повозка целиком — один предмет: отпущенный обоз уезжает весь (CartRoll).
                    var cart = new GameObject(Cart).transform;
                    cart.SetParent(root, false);
                    var turn = Quaternion.Euler(0f, p.Yaw, 0f);
                    var body = Part(cart, "Обоз · кузов", PrimitiveType.Cube,
                        local+Vector3.up*1.2f*s,new Vector3(4.4f*s,1.2f*s,2.7f*s),
                        new Color(0.35f,0.21f,0.13f),p.Yaw);
                    // Доски кузова — того же дерева, что ящики лагеря.
                    Borrow(body, "Crate");
                    for(int i=0;i<4;i++)
                    {
                        // Место колеса — в осях кузова: без поворота (до 27.11) на повёрнутой
                        // повозке колесо торчало из-под передка.
                        var wheel=Part(cart,"Колесо повозки",PrimitiveType.Cylinder,
                            local+turn*new Vector3((i<2?-1.5f:1.5f)*s,0.62f*s,
                                                   (i%2==0?-1.45f:1.45f)*s),
                            new Vector3(0.66f*s,0.12f*s,0.66f*s),
                            new Color(0.15f,0.12f,0.11f));
                        wheel.transform.rotation=root.rotation*Quaternion.Euler(90f,p.Yaw,0f);
                    }
                    // Груз — то, за чем обоз и идёт: ящики и бочка на кузове.
                    float top = 1.8f * s;
                    Model(cart, "Crate", local + turn * new Vector3(-1.2f * s, top, 0.35f * s), p.Yaw + 6f, 0f, 1.05f * s, 0f);
                    Model(cart, "Crate", local + turn * new Vector3(0.1f * s, top, -0.4f * s), p.Yaw - 9f, 0f, 0.95f * s, 0f);
                    Model(cart, "Barrel", local + turn * new Vector3(1.35f * s, top, 0.3f * s), p.Yaw, 1.05f * s, 0f, 0f);
                    // Фонарь на передке: обоз идёт ночью («Почему едете ночью?»), и ночью
                    // от первого лица его видно издалека, а не только в пятнадцати шагах.
                    var lantern = new GameObject("Фонарь обоза");
                    lantern.transform.SetParent(cart, false);
                    lantern.transform.localPosition = local + turn * new Vector3(2.3f * s, 2.4f * s, 0f);
                    var glow = lantern.AddComponent<Light>();
                    glow.type = LightType.Point;
                    glow.range = 11f;
                    glow.intensity = 2.2f;
                    glow.color = new Color(1f, 0.68f, 0.36f);
                    var flame = Part(cart, "Огонь фонаря", PrimitiveType.Sphere,
                        lantern.transform.localPosition, Vector3.one * 0.22f, new Color(1f, 0.62f, 0.25f));
                    Object.Destroy(flame.GetComponent<Collider>());
                    var flameRenderer = flame.GetComponent<Renderer>();
                    if (flameRenderer != null && flameRenderer.material.HasProperty("_EmissionColor"))
                    {
                        flameRenderer.material.EnableKeyword("_EMISSION");
                        flameRenderer.material.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.2f) * 3f);
                    }
                    break;
                }
                case ExpeditionPropKind.Tent:
                {
                    var tent = Part(root,"Тент дозорных",PrimitiveType.Cube,local+Vector3.up*1.3f*s,
                        new Vector3(4f*s,2.6f*s,3.2f*s),
                        new Color(0.26f,0.20f,0.14f));
                    Dress(root, tent, "Tent", local, p.Yaw, 0f, 4.2f * s, 0f);
                    break;
                }
                case ExpeditionPropKind.Column:
                {
                    var column = Part(root,"Разбитая колонна",PrimitiveType.Cylinder,
                        local+Vector3.up*1.9f*s,
                        new Vector3(0.62f*s,1.9f*s,0.62f*s),stone);
                    Wear(column, "Rock022", 1f, 1.6f * s, Grey(0.75f));
                    break;
                }
                case ExpeditionPropKind.Wall:
                {
                    var wall = Part(root,"Стена часовни",PrimitiveType.Cube,
                        local+new Vector3(0f,2.4f*s,0f),
                        new Vector3(37f,4.8f*s,1.6f),stone);
                    Wear(wall, "Rock022", 37f / 4f, 4.8f * s / 4f, Grey(0.72f));
                    break;
                }
                case ExpeditionPropKind.Altar:
                {
                    var altar = Part(root,"Алтарный камень",PrimitiveType.Cube,
                        local+Vector3.up*1f*s,
                        new Vector3(3.5f*s,2f*s,2f*s),
                        new Color(0.47f,0.47f,0.44f));
                    Wear(altar, "Rock022", 1.2f, 0.7f, Grey(0.86f));
                    break;
                }
                case ExpeditionPropKind.Torch:
                {
                    var pole = Part(root,"Столб факела",PrimitiveType.Cylinder,
                        local+Vector3.up*1.3f,
                        new Vector3(0.15f,1.3f,0.15f),new Color(0.19f,0.13f,0.1f));
                    // Факел лагеря: свой огонь, искры и свет. Нет его — прежний шар с огнём.
                    if (Model(root, "TorchStand", local, p.Yaw, 0f, 0f, 0f) != null)
                    {
                        pole.GetComponent<Renderer>().enabled = false;
                        break;
                    }
                    var flame=Part(root,"Огонь",PrimitiveType.Sphere,
                        local+Vector3.up*2.65f,new Vector3(0.4f,0.55f,0.4f),
                        new Color(1f,0.46f,0.08f));
                    Object.Destroy(flame.GetComponent<Collider>());
                    var light=flame.AddComponent<Light>();
                    light.type=LightType.Point;light.color=new Color(1f,0.62f,0.23f);
                    light.range=9f;light.intensity=2f;
                    break;
                }
            }
        }

        /// <summary>
        /// Только вид, после запекания навмеша: трава и кусты внутри карты,
        /// лес (или камни степи) за её краем, костёр у шатра дозорных.
        /// Раскладка постоянная — одна и та же при каждом входе, как и сама карта.
        /// </summary>
        private static void Scatter(Transform root, ExpeditionMapPlan plan)
        {
            bool road = plan.Kind == ExpeditionMapKind.SaltRoad;
            var rng = new System.Random(road ? 1709 : 2311);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            float halfW = plan.Width * 0.5f, halfD = plan.Depth * 0.5f;

            // Внутри: не на тропе, не у входа и цели, не в вещах.
            bool Free(float x, float z, float clear)
            {
                float pathX = road ? 2.8f * Mathf.Sin((z + 43f) / 5f * 0.37f)
                                   : 8f * Mathf.Sin((z + 42f) / 5f * 0.16f);
                float pathHalf = road ? 6.25f : 4.25f;
                if (Mathf.Abs(x - pathX) < pathHalf + 0.8f) return false;
                if (new Vector2(x, z - plan.EntranceZ).sqrMagnitude < 49f) return false;
                if (new Vector2(x, z - plan.GoalZ).sqrMagnitude < 64f) return false;
                if (!road && z > 27f && Mathf.Abs(x) < 22f) return false;   // двор часовни
                foreach (var p in plan.Props)
                {
                    float r = (p.Kind == ExpeditionPropKind.Wagon || p.Kind == ExpeditionPropKind.Tent ? 3.6f : 1.6f) * p.Size + clear;
                    if (p.Kind == ExpeditionPropKind.Wall) continue;
                    if ((new Vector2(x - p.X, z - p.Z)).sqrMagnitude < r * r) return false;
                }
                return true;
            }

            void Inside(string prop, int count, float minH, float maxH, float clear)
            {
                for (int i = 0, tries = 0; i < count && tries < count * 8; tries++)
                {
                    float x = R(-halfW + 2.5f, halfW - 2.5f), z = R(-halfD + 2.5f, halfD - 2.5f);
                    if (!Free(x, z, clear)) continue;
                    Model(root, prop, new Vector3(x, 0f, z), R(0f, 360f), R(minH, maxH), 0f, 0.04f);
                    i++;
                }
            }

            Inside("GrassTuft", road ? 70 : 150, 0.3f, 0.6f, 0.2f);
            Inside("Bush", road ? 12 : 34, 0.7f, 1.4f, 0.6f);
            Inside("Stump", road ? 5 : 12, 0.35f, 0.6f, 0.5f);

            // За краем: стена леса у часовни, у обоза — камни, сушняк и редкие ели.
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float length = alongX ? plan.Width + 16f : plan.Depth + 16f;
                float sign = side % 2 == 0 ? -1f : 1f;
                for (float t = -length * 0.5f; t <= length * 0.5f; t += road ? 6.5f : 4.2f)
                {
                    for (int row = 0; row < (road ? 1 : 2); row++)
                    {
                        float off = R(3.5f, 7f) + row * 6.5f;
                        float u = t + R(-1.6f, 1.6f) + row * 2.1f;
                        var at = alongX
                            ? new Vector3(u, 0f, sign * (halfD + off))
                            : new Vector3(sign * (halfW + off), 0f, u);
                        double roll = rng.NextDouble();
                        if (!road)
                            Model(root, Pines[rng.Next(Pines.Length)], at, R(0f, 360f), R(9f, 15f));
                        else if (roll < 0.45)
                            Model(root, "Rock", at, R(0f, 360f), 0f, R(2.2f, 4.5f), 0.3f);
                        else if (roll < 0.8)
                            Model(root, DeadTrees[rng.Next(DeadTrees.Length)], at, R(0f, 360f), R(4f, 7f));
                        else
                            Model(root, Pines[rng.Next(Pines.Length)], at, R(0f, 360f), R(7f, 11f));
                    }
                }
            }

            // Дозорные у обоза греются у огня рядом с шатром.
            if (road)
                foreach (var p in plan.Props)
                    if (p.Kind == ExpeditionPropKind.Tent)
                    {
                        Model(root, "Campfire", new Vector3(p.X - 5.5f, 0f, p.Z - 4f), 0f, 0f, 0f, 0f);
                        break;
                    }
        }
    }
}
