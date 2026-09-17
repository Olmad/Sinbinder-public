// Assets/Scripts/Gameplay/SinEyes.cs
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
            float force = Mathf.Clamp01(Mathf.Abs(warrior.Soul.Get(sin)));

            Paint(colour, force);
            Lamp(colour, force);
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
        /// </summary>
        private void Lamp(Color colour, float force)
        {
            var go = new GameObject("Огонь греха");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.92f, 0.06f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = colour;
            light.range = 1.9f;
            light.intensity = 0.55f + force * 0.9f;
            light.shadows = LightShadows.None;
        }
    }
}
