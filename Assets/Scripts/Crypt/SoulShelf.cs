// Assets/Scripts/Crypt/SoulShelf.cs
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Полка с банками. Показывает то, что лежит у Греховода на руках
    /// собранным, — и ничего сверх того.
    ///
    /// Банки не хранят душ: души хранит <see cref="SoulManager"/>.
    /// Полка — это <b>вид</b> на его список, и потому она перестраивается
    /// всякий раз, когда список меняется. Заведи она свой запас — и через
    /// две вылазки на полке стояло бы одно, а в жатве числилось другое.
    /// Это ровно та вторая правда, из-за которой в проекте отношения
    /// вычисляются, а не хранятся.
    ///
    /// <b>Про засев.</b> На полигоне полка пуста: никто не умирал, жать
    /// было некого, и зона связывания оказалась бы витриной без товара.
    /// Поэтому при <c>_seedWhenEmpty</c> полка кладёт несколько душ сама
    /// — разного греха и разной свежести, чтобы правило «оболочка требует
    /// воли» было на чём показать. Это удобство полигона, и в настоящей
    /// игре оно выключено.
    /// </summary>
    public class SoulShelf : MonoBehaviour
    {
        [Tooltip("Сколько банок помещается на полке.")]
        [SerializeField] private int _slots = 6;

        [Tooltip("Расстояние между банками.")]
        [SerializeField] private float _step = 0.7f;

        [Tooltip("Положить пробные души, если жатва пуста. Только для полигона.")]
        [SerializeField] private bool _seedWhenEmpty = true;

        private readonly List<SoulJar> _jars = new();
        private ShelfPlace _place;
        private int _firstIndex;
        private int _knownCount = -1;

        void Start()
        {
            if (_seedWhenEmpty) Seed();
            Rebuild();
        }

        void Update()
        {
            // Список душ меняют жатва и связывание, а не полка. Дешевле
            // и честнее заметить это по длине, чем заводить событие,
            // о котором однажды забудут.
            var souls = SoulManager.Instance;
            int count = souls != null ? souls.Harvested.Count : 0;

            if (count != _knownCount) Rebuild();
        }

        /// <summary>Какой это номер в списке жатвы. Банка спрашивает перед тем, как взять.</summary>
        public int IndexOf(int slot) => _firstIndex + slot;

        /// <summary>Расставить банки заново по нынешнему списку душ.</summary>
        public void Rebuild()
        {
            foreach (var jar in _jars)
                if (jar != null)
                {
                    if (Application.isPlaying) Destroy(jar.gameObject);
                    else DestroyImmediate(jar.gameObject);
                }

            _jars.Clear();

            var souls = SoulManager.Instance;
            var list = souls != null ? souls.Harvested : null;

            _knownCount = list != null ? list.Count : 0;
            _firstIndex = 0;

            if (list == null || list.Count == 0) return;

            int shown = Mathf.Min(_slots, list.Count);

            for (int i = 0; i < shown; i++)
            {
                var kept = list[_firstIndex + i];
                if (kept.Soul == null) continue;

                _jars.Add(MakeJar(i, kept));
            }

            MakePlace(shown);
        }

        /// <summary>
        /// Свободное место после последней банки. Сюда игрок ставит душу
        /// из рук — до этого полка была витриной в одну сторону: с неё
        /// брали, на неё не клали.
        ///
        /// Одно место, а не все свободные: ставить в третью ячейку,
        /// когда вторая пуста, незачем, а меток на полке было бы вдвое
        /// больше банок.
        /// </summary>
        private void MakePlace(int after)
        {
            if (_place != null) Destroy(_place.gameObject);
            if (after >= _slots) return;           // полка полна

            var go = new GameObject("Свободное место");
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(after * _step, 0.25f, 0f);

            // Коллайдер нужен, чтобы место можно было разглядеть подписью,
            // но видимого тела у него нет: пустое место и должно выглядеть
            // пустым.
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.4f, 0.5f, 0.4f);
            box.isTrigger = true;

            _place = go.AddComponent<ShelfPlace>();
            _place.Bind(this);
        }

        private SoulJar MakeJar(int slot, SoulManager.Kept kept)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = $"Банка — {kept.Soul.Name}";
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(slot * _step, 0.25f, 0f);
            go.transform.localScale = new Vector3(0.22f, 0.25f, 0.22f);

            var jar = go.AddComponent<SoulJar>();
            jar.Fill(this, slot, kept.Soul, kept.Quality);

            var plate = new GameObject("Табличка");
            plate.transform.SetParent(go.transform);
            plate.transform.localPosition = new Vector3(0f, 2.2f, 0f);

            var text = plate.AddComponent<TextMesh>();
            text.font = PlateFont();
            if (text.font != null)
                text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.text = jar.Label;
            text.characterSize = 0.28f;
            text.fontSize = 64;
            text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.82f, 0.86f, 0.90f);

            plate.AddComponent<UI.Billboard>();

            // Гаснет, пока на банку не посмотрят: полка на девять душ,
            // подписанных разом, читается как список, а не как полка.
            plate.AddComponent<UI.WorldPlate>();

            return jar;
        }

        /// <summary>
        /// Шрифт для таблички на банке.
        ///
        /// <b>Без него таблички не было видно вовсе.</b> <c>TextMesh</c>,
        /// созданный кодом, приходит с пустым полем <c>font</c>, а пустой
        /// шрифт означает не «шрифт по умолчанию», а «рисовать нечем»:
        /// материал у <c>MeshRenderer</c> тоже берётся у шрифта. Девять
        /// банок на полке стояли безымянными, и заметить это было некому —
        /// полку, по <c>26-VERSION.md</c>, не видел никто.
        ///
        /// Берём у уже существующего текста в сцене: он собран сборщиком
        /// сцен, шрифт ему назначен там же и уезжает в сборку вместе
        /// со сценой. Своего поля не заводим нарочно — оно потребовало бы
        /// пересборки сцен, а этого добра перед показом лучше не трогать.
        ///
        /// Не нашлось — говорим вслух один раз. Отсутствие шрифта
        /// событие, а не ноль.
        /// </summary>
        private static Font PlateFont()
        {
            var any = Object.FindFirstObjectByType<UnityEngine.UI.Text>(FindObjectsInactive.Include);
            if (any != null && any.font != null) return any.font;

            var builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtin != null) return builtin;

            if (!_toldAboutFont)
            {
                _toldAboutFont = true;
                Debug.LogWarning("[ПОЛКА] Шрифта для табличек нет: ни одного "
                               + "Text в сцене, ни встроенного. Банки будут "
                               + "стоять безымянными — это видно глазом "
                               + "и чинится назначением шрифта.");
            }
            return null;
        }

        private static bool _toldAboutFont;

        /// <summary>
        /// Пробные души для полигона: четыре греха и четыре ступени
        /// распада. Ровно столько, чтобы правило допуска тел было видно
        /// целиком — от свежей, которую примет любое тело, до истлевшей,
        /// которую примет один призрак.
        /// </summary>
        private void Seed()
        {
            var souls = SoulManager.Instance;
            if (souls == null || souls.Harvested.Count > 0) return;

            Put(souls, "Марга Копатель", SinType.Greed, MoralType.Vicious, 65f,
                SoulQuality.Shock);
            Put(souls, "Брат Хальд", SinType.Wrath, MoralType.Pious, 55f,
                SoulQuality.Acceptance);
            Put(souls, "Вейн Тихий", SinType.Sloth, MoralType.Pious, 40f,
                SoulQuality.Fading);
            Put(souls, "Одноглазый Хорь", SinType.Envy, MoralType.Vicious, 45f,
                SoulQuality.Dissolved);
        }

        private static void Put(SoulManager souls, string name, SinType sin,
            MoralType moral, float intensity, SoulQuality quality)
        {
            var soul = new SoulData(name, sin, moral, 1, intensity);

            // Потери качества вшивает тот же SoulDecay, что и настоящая
            // жатва: пробная душа обязана быть такой же, какой пришла бы
            // с поля, иначе полигон учил бы неверному.
            var kept = SoulDecay.Harvest(soul, quality);
            if (kept != null) souls.PutBack(new SoulManager.Kept(kept, quality));
        }
    }
}
