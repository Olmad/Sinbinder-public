// Runtime-built, PHYSICAL expedition zones. No Unity scene address or
// pre-baked demo-only layout is required. All geometry is real mesh/collider,
// with a separate NavMeshSurface scoped to its own map.
using UnityEngine;
using Unity.AI.Navigation;

namespace Sinbinder.Gameplay
{
    public static class ExpeditionMapBuilder
    {
        public static Vector3 Origin(ExpeditionMapKind kind)
            => kind == ExpeditionMapKind.SaltRoad
                ? new Vector3(2000f, 0f, 0f)
                : new Vector3(2500f, 0f, 0f);

        public static GameObject Build(ExpeditionMapPlan plan)
        {
            var baseAt = Origin(plan.Kind);
            var root = new GameObject("Экспедиция · " + plan.Title);
            root.transform.position = baseAt;

            var ground = Part(root.transform, "Земля", PrimitiveType.Plane,
                new Vector3(0f, -0.08f, 0f),
                new Vector3(plan.Width / 10f, 1f, plan.Depth / 10f),
                plan.Kind == ExpeditionMapKind.SaltRoad
                    ? new Color(0.48f, 0.38f, 0.24f)
                    : new Color(0.22f, 0.28f, 0.24f));
            if (ground == null) { Object.Destroy(root); return null; }

            // No invisible drop-off at the edge; four actual enclosing
            // rock banks, with a walkable interior and a clear centre path.
            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? -1f : 1f;
                Part(root.transform, "Край карты (боковой)", PrimitiveType.Cube,
                    new Vector3(sign * plan.Width * 0.5f, 1.4f, 0f),
                    new Vector3(2.2f, 3f, plan.Depth + 2f),
                    new Color(0.19f, 0.17f, 0.16f));
                Part(root.transform, "Край карты (торцевой)", PrimitiveType.Cube,
                    new Vector3(0f, 1.4f, sign * plan.Depth * 0.5f),
                    new Vector3(plan.Width + 2f, 3f, 2.2f),
                    new Color(0.19f, 0.17f, 0.16f));
            }

            if (plan.Kind == ExpeditionMapKind.SaltRoad)
            {
                // Wide, bent roadside path gives the player a distinct
                // readable line from the starting camp to the wagons.
                for (int i = 0; i < 17; i++)
                {
                    float z = -43f + i * 5f;
                    float x = Mathf.Sin(i * 0.37f) * 2.8f;
                    Part(root.transform, "Утоптанная дорога", PrimitiveType.Cube,
                        new Vector3(x, -0.055f, z), new Vector3(12.5f, 0.05f, 5.25f),
                        new Color(0.59f, 0.48f, 0.31f));
                }
            }
            else
            {
                for (int i = 0; i < 16; i++)
                {
                    Part(root.transform, "Мокрая тропа", PrimitiveType.Cube,
                        new Vector3(8f * Mathf.Sin(i * 0.16f), -0.055f, -42f + i * 5f),
                        new Vector3(8.5f, 0.05f, 5.4f), new Color(0.27f, 0.26f, 0.23f));
                }
            }

            foreach (var p in plan.Props) AddProp(root.transform, p, plan.Kind);

            // Built after terrain and props but BEFORE warriors. Otherwise
            // moving bodies are baked into the navigation surface.
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.BuildNavMesh();
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
            var beacon = new GameObject("Световая метка");
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

        private static void AddProp(Transform root, ExpeditionProp p, ExpeditionMapKind kind)
        {
            var local = new Vector3(p.X, 0f, p.Z);
            float s = p.Size;
            var stone = kind == ExpeditionMapKind.SaltRoad
                ? new Color(0.37f, 0.31f, 0.26f) : new Color(0.32f, 0.34f, 0.32f);
            switch (p.Kind)
            {
                case ExpeditionPropKind.Rock:
                case ExpeditionPropKind.Rubble:
                    Part(root, p.Kind == ExpeditionPropKind.Rock ? "Валун" : "Обвал",
                        PrimitiveType.Cube, local + Vector3.up * s * 0.6f,
                        new Vector3(1.6f * s, 1.2f * s, 1.7f * s), stone, p.Yaw);
                    break;
                case ExpeditionPropKind.DeadTree:
                case ExpeditionPropKind.Pine:
                    Part(root, "Ствол", PrimitiveType.Cylinder, local + Vector3.up * 2.2f * s,
                        new Vector3(0.28f * s, 2.2f * s, 0.28f * s),
                        new Color(0.23f, 0.17f, 0.13f), p.Yaw);
                    if (p.Kind == ExpeditionPropKind.Pine)
                        Part(root, "Еловые ветви", PrimitiveType.Cylinder,
                            local + Vector3.up * 3.5f * s,
                            new Vector3(1.5f * s, 1.45f * s, 1.5f * s),
                            new Color(0.13f, 0.23f, 0.18f));
                    break;
                case ExpeditionPropKind.Grave:
                    Part(root, "Могильная плита", PrimitiveType.Cube,
                        local + Vector3.up * 0.85f * s,
                        new Vector3(0.55f*s,1.7f*s,0.3f*s),
                        new Color(0.42f,0.44f,0.43f),p.Yaw);
                    Part(root, "Могильный холм", PrimitiveType.Cube,
                        local + new Vector3(0f,0.10f,-0.7f*s),
                        new Vector3(1.3f*s,0.2f,1.6f*s),stone,p.Yaw);
                    break;
                case ExpeditionPropKind.Wagon:
                    Part(root, "Обоз · кузов", PrimitiveType.Cube,
                        local+Vector3.up*1.2f*s,new Vector3(4.4f*s,1.2f*s,2.7f*s),
                        new Color(0.35f,0.21f,0.13f),p.Yaw);
                    for(int i=0;i<4;i++)
                    {
                        var wheel=Part(root,"Колесо повозки",PrimitiveType.Cylinder,
                            local+new Vector3((i<2?-1.5f:1.5f)*s,0.62f*s,
                                              (i%2==0?-1.5f:1.5f)*s),
                            new Vector3(0.66f*s,0.12f*s,0.66f*s),
                            new Color(0.15f,0.12f,0.11f));
                        wheel.transform.rotation=Quaternion.Euler(90f,p.Yaw,0f);
                    }
                    break;
                case ExpeditionPropKind.Tent:
                    Part(root,"Тент дозорных",PrimitiveType.Cube,local+Vector3.up*1.3f*s,
                        new Vector3(4f*s,2.6f*s,3.2f*s),
                        new Color(0.26f,0.20f,0.14f));
                    break;
                case ExpeditionPropKind.Column:
                    Part(root,"Разбитая колонна",PrimitiveType.Cylinder,
                        local+Vector3.up*1.9f*s,
                        new Vector3(0.62f*s,1.9f*s,0.62f*s),stone);
                    break;
                case ExpeditionPropKind.Wall:
                    Part(root,"Стена часовни",PrimitiveType.Cube,
                        local+new Vector3(0f,2.4f*s,0f),
                        new Vector3(37f,4.8f*s,1.6f),stone);
                    break;
                case ExpeditionPropKind.Altar:
                    Part(root,"Алтарный камень",PrimitiveType.Cube,
                        local+Vector3.up*1f*s,
                        new Vector3(3.5f*s,2f*s,2f*s),
                        new Color(0.47f,0.47f,0.44f));
                    break;
                case ExpeditionPropKind.Torch:
                    Part(root,"Столб факела",PrimitiveType.Cylinder,
                        local+Vector3.up*1.3f,
                        new Vector3(0.15f,1.3f,0.15f),new Color(0.19f,0.13f,0.1f));
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
}
