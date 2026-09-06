// Assets/Scripts/Gameplay/CameraPullback.cs
using System.Collections;
using UnityEngine;
using Sinbinder.AOS;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Медленный отъезд камеры. Сцена 5 пролога.
    ///
    /// «Камера как единственная кинематография. Один медленный отъезд
    /// за весь пролог. Движение камеры по статичной геометрии читается
    /// как постановка и не стоит ни одной анимации» — второй из шести
    /// приёмов дешёвой эпики (docs/09-PROLOGUE.md §9).
    ///
    /// <b>Один</b> — здесь не фигура речи, а причина, по которой счётчик
    /// статический: отъезд, случающийся дважды, читается уже не как
    /// постановка, а как дёргающаяся камера.
    ///
    /// Отъезд полезен, а не только красив: он открывает край карты,
    /// до которого игроку сейчас бежать. Поэтому он идёт после тишины
    /// отказа и до того, как отряд тронется.
    ///
    /// Кривая — сглаженная, время реальное: отказ мог поставить игру
    /// на паузу, а отъезд обязан длиться ровно столько, сколько задуман.
    /// </summary>
    public class CameraPullback : MonoBehaviour
    {
        [Tooltip("Насколько отвести камеру назад вдоль её же взгляда.")]
        [SerializeField] private float _back = 9f;

        [Tooltip("И насколько поднять. Подъём открывает то, что за холмом.")]
        [SerializeField] private float _up = 4f;

        [Tooltip("Сколько длится отъезд.")]
        [SerializeField] private float _seconds = 3.5f;

        [Tooltip("Пауза после отказа: тишина должна отзвучать первой.")]
        [SerializeField] private float _afterRefusal = 1.6f;

        [Tooltip("Что пишет журнал. Пусто — не пишет.")]
        [SerializeField] private string _line = "Отсюда видно, как далеко до края.";

        /// <summary>
        /// Был ли отъезд. Статично и переживает смену сцен: он один
        /// на весь пролог, а не один на сцену.
        /// </summary>
        public static bool Spent { get; private set; }

        /// <summary>Забыть отъезд. Начало пролога, повторный запуск.</summary>
        public static void Forget() => Spent = false;

        private Camera _cam;
        private RTS_Camera _rts;
        private bool _running;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;
            _rts = _cam != null ? _cam.GetComponent<RTS_Camera>() : null;
        }

        void Start()
        {
            if (AOSEventHub.Instance != null)
                AOSEventHub.Instance.OnRefusal += OnRefusal;
        }

        void OnDestroy()
        {
            if (AOSEventHub.Instance != null)
                AOSEventHub.Instance.OnRefusal -= OnRefusal;

            // Сцену закрыли посреди отъезда — вернуть управление тому,
            // у кого оно было. Иначе следующая сцена унаследует камеру,
            // которая никого не слушается.
            if (_running && _rts != null) { _rts.Resync(); _rts.enabled = true; }
        }

        private void OnRefusal(Warrior warrior, Decision decision, DecisionContext context)
        {
            Play();
        }

        /// <summary>Отъехать. Публично: сцену может вести и не отказ.</summary>
        public void Play()
        {
            if (Spent || _running || _cam == null) return;

            Spent = true;
            StartCoroutine(Routine());
        }

        private IEnumerator Routine()
        {
            _running = true;

            yield return new WaitForSecondsRealtime(_afterRefusal);

            var t = _cam.transform;
            var from = t.position;
            var to = from - t.forward * _back + Vector3.up * _up;

            // Пока едем, ручная камера молчит: два хозяина у одной позиции
            // дерутся каждый кадр, и отъезд выходит рваным.
            bool hadRts = _rts != null && _rts.enabled;
            if (hadRts) _rts.enabled = false;

            if (!string.IsNullOrEmpty(_line))
                Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(_line);

            float elapsed = 0f;
            while (elapsed < _seconds)
            {
                elapsed += Time.unscaledDeltaTime;

                // Сглаживание на обоих концах: камера трогается и
                // останавливается плавно, а не рывком.
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _seconds));
                t.position = Vector3.Lerp(from, to, k);

                yield return null;
            }

            t.position = to;

            // Сказать ручной камере, где мы теперь: иначе она вернёт
            // всё назад одним кадром.
            if (_rts != null)
            {
                _rts.Resync();
                if (hadRts) _rts.enabled = true;
            }

            _running = false;
        }
    }
}
