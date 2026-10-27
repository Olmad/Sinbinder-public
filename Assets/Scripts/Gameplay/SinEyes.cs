// Assets/Scripts/Gameplay/SinEyes.cs
// Перевод: текст через Loc
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Грех горит в воине — и горит его цветом.
    ///
    /// Слово автора от 15 сентября: «у всех воинов горят глаза под цвет
    /// греха, это даст очень много понимания». Замысел верный: под камерой
    /// цвет узнаётся быстрее любой надписи, а чисел игрок в этой игре
    /// не видит нигде (00-GDD.md §7).
    ///
    /// <b>Носителей два, и это не украшение, а замер.</b> Огонь в глазницах
    /// рендерится верно и вблизи читается отлично — но тактическая камера
    /// стоит под питчем 84° (<see cref="RTS_Camera"/>), то есть смотрит почти
    /// отвесно вниз, и лица с неё не видно вовсе. Проверено рендером с того
    /// же угла: у скелета, зомби и призрака глаз не видно ни одного пикселя,
    /// у голема — тонкая полоска.
    ///
    /// Поэтому цвет несут двое:
    ///
    /// * <b>материал «Глаз»</b> — для всего, где лицо видно: взгляд
    ///   из-за плеча, наезды на своеволие, разговоры, церемония титула;
    /// * <b>точечный свет у головы</b> — для тактического взгляда. Он ложится
    ///   на плечи и на землю вокруг, и сверху виден именно он.
    ///
    /// Цвет у обоих один и берётся из <see cref="SinPalette"/> — той же
    /// таблицы, что красит рамку подсказки. Двух правд о грехе быть не может.
    ///
    /// Жребия здесь нет: цвет и сила — чистая функция от души.
    /// </summary>
    public class SinEyes : MonoBehaviour
    {
        /// <summary>Имя материала, который красим. Задаётся в bodies.py.</summary>
        private const string Slot = "Eye";

        /// <summary>Сказано один раз на запуск: жалуются иначе все девять.</summary>
        private static bool _toldAboutSlot;

        /// <summary>
        /// Сбрасывает память о жалобе к началу игры. Зовёт сама Unity —
        /// руками этого не делает никто, и заводить публичный <c>Forget</c>
        /// «на всякий случай» нельзя: рядом уже лежит такой же в
        /// <see cref="WarriorLook"/>, и его не зовут ниоткуда с самого
        /// появления. Мёртвый код хуже отсутствующего (CLAUDE.md).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => _toldAboutSlot = false;

        void Start()
        {
            var warrior = GetComponentInParent<Warrior>();

            if (warrior == null || warrior.Soul == null)
            {
                // Тело без души — событие, а не ноль: гореть нечему,
                // и молчать об этом нельзя.
                Debug.LogWarning("[ГЛАЗА] " + name + ": тела без души не бывает, "
                               + "а грех гореть не может.");
                return;
            }

            SinType sin = warrior.Soul.Sin;
            Color colour = SinPalette.Of(sin);

            // Сила греха — не число на экране, а яркость. Слабый грех
            // тлеет, сильный жжёт; между ними видна разница, а цифры нет.
            // Спектр — −100…+100 (SoulData.Get): до 23.10 здесь не делили на сто,
            // и у любого греха сильнее единицы сила была полной — горели все
            // одинаково, и легенда не выделялась ничем (отзыв 21.10, п. 12).
            float force = Mathf.Clamp01(Mathf.Abs(warrior.Soul.Get(sin)) / 100f);

            Paint(colour, force);
            Lamp(colour, force);

            // Одноглазый — с одним глазом (автор, 26.10: «у Одноглазого Хоря — 2 глаза»).
            if (warrior.DisplayName != null && warrior.DisplayName.Contains("Одноглаз")) Patch();
        }

        /// <summary>
        /// Повязка на правую глазницу: тёмная кожаная накладка на кости Head, поверх
        /// огонька. Где глаз — по вершинам материала «Глаз» (bodies.py кладёт их
        /// в глазницы своей оболочки), так что повязка садится на любое тело.
        /// Огонёк в глазнице остаётся — его закрывает повязка, а свет у головы
        /// идёт от второго глаза.
        /// </summary>
        private void Patch()
        {
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;

                int slot = -1;
                var shared = smr.sharedMaterials;
                for (int i = 0; i < shared.Length && i < mesh.subMeshCount; i++)
                    if (shared[i] != null && shared[i].name.StartsWith(Slot)) { slot = i; break; }
                if (slot < 0) continue;

                int head = -1;
                for (int i = 0; i < smr.bones.Length; i++)
                    if (smr.bones[i] != null && smr.bones[i].name == "Head") { head = i; break; }
                if (head < 0) continue;

                // Меш в осях Blender: вверх — +Z, взгляд — −Y (как знаки на AxeTree), правый — −X.
                var verts = mesh.vertices;
                var idx = mesh.GetTriangles(slot);
                Vector3 sum = Vector3.zero, min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
                int n = 0;
                foreach (int v in idx)
                {
                    var p = verts[v];
                    if (p.x > 0f) continue;
                    sum += p; n++;
                    min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                }
                if (n == 0) continue;

                Vector3 centre = sum / n;
                float size = Mathf.Max(max.x - min.x, max.z - min.z) * 2.4f;
                // Глаз сидит в черепе на семи десятых радиуса — повязку выносим на лицо.
                Vector3 onFace = centre + Vector3.down * size * 0.6f;

                var bone = smr.bones[head];
                var bind = mesh.bindposes[head];
                var patch = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                patch.name = "EyePatch";
                Object.Destroy(patch.GetComponent<Collider>());
                patch.transform.SetParent(bone, false);
                patch.transform.localPosition = bind.MultiplyPoint3x4(onFace);
                patch.transform.localRotation = Quaternion.LookRotation(bind.MultiplyVector(Vector3.down),
                                                                        bind.MultiplyVector(Vector3.forward));
                // Размер — в единицах меша, через ту же привязку (у кости свой масштаб).
                float local = bind.MultiplyVector(Vector3.right * size).magnitude;
                patch.transform.localScale = new Vector3(local * 1.2f, local, local * 0.18f);

                var r = patch.GetComponent<Renderer>();
                var mat = new Material(smr.sharedMaterial.shader) { name = "EyePatch" };
                mat.color = new Color(0.07f, 0.055f, 0.045f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return;
            }
        }

        /// <summary>
        /// Красит материал «Глаз». Берём <c>materials</c>, а не
        /// <c>sharedMaterials</c>: общий материал покрасил бы разом весь
        /// отряд в грех того, кто встал последним.
        /// </summary>
        private void Paint(Color colour, float force)
        {
            bool found = false;

            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                var mats = renderer.materials;

                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].name.StartsWith(Slot)) continue;

                    found = true;
                    mats[i].color = colour;

                    if (mats[i].HasProperty("_EmissionColor"))
                    {
                        mats[i].EnableKeyword("_EMISSION");
                        mats[i].SetColor("_EmissionColor", colour * (1.4f + force * 3.0f));
                    }
                }

                renderer.materials = mats;
            }

            if (found || _toldAboutSlot) return;

            _toldAboutSlot = true;
            Debug.Log("[ГЛАЗА] Материала «" + Slot + "» на модели нет — гореть "
                    + "нечему. Так бывает у кубов-заглушек: модели собираются "
                    + "скриптом Tools/blender/bodies.py, и материал заводится там.");
        }

        /// <summary>
        /// Огонёк у головы. Маленький, без теней и не дальше двух метров:
        /// светильник на каждом воине — это девять источников в кадре,
        /// и тени от них стоили бы дороже всего остального вместе.
        ///
        /// <b>Перед лицом, а не в черепе.</b> До 27 сентября он стоял
        /// в <c>(0; 0,92; 0,06)</c> — внутри черепа, у самой лицевой кости:
        /// светил в глазницы изнутри, а наружу череп был повёрнут к нему
        /// тыльной стороной и оставался в тени. На портретах прогона —
        /// тёмный шар с огоньком посередине (нашла сессия игры, §111.4).
        /// Теперь огонь на ладонь впереди глаз: отсвет глаз ложится
        /// на скулы, надбровье и грудь, как свет от горящих глаз и должен.
        /// </summary>
        private void Lamp(Color colour, float force)
        {
            var go = new GameObject("Огонь греха");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.95f, 0.16f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = colour;
            light.range = 1.9f;
            light.intensity = 0.55f + force * 0.9f;
            light.shadows = LightShadows.None;
        }
    }
}
