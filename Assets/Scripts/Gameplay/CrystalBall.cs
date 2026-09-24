// Assets/Scripts/Gameplay/CrystalBall.cs
using System.Collections;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Хрустальный шар на столе военного совета.
    ///
    /// Это предмет, а не пункт меню, и он ведёт две сцены подряд
    /// (docs/09-PROLOGUE.md §4):
    ///
    /// — сцена 2: игрок подходит к столу, и шар открывает совет. Не таймер:
    ///   совет обязан быть тем, к чему игрок пришёл сам, иначе первое же
    ///   решение демо оказывается не его;
    /// — сцена 3: шар наливается красным, Карган гадает вслух, и лагерь
    ///   уходит в разгром. Тревога должна быть вещью в лагере, которую
    ///   видно от палатки, а не строкой в сводке.
    ///
    /// Пульсация берёт Time и потому недетерминирована. Это допустимо:
    /// правило повторяемости охраняет решения движка, а свет на решения
    /// не влияет никак. Ни один модуль сюда не смотрит.
    /// </summary>
    public class CrystalBall : MonoBehaviour
    {
        [SerializeField] private Light _glow;

        [Tooltip("Пока всё спокойно: холодный, ровный.")]
        [SerializeField] private Color _calm = new Color(0.45f, 0.62f, 0.95f);

        [Tooltip("Тревога сцены 3: отряды гибнут один за другим.")]
        [SerializeField] private Color _alarm = new Color(0.95f, 0.18f, 0.12f);

        [SerializeField] private float _calmIntensity = 1.4f;
        [SerializeField] private float _alarmIntensity = 4.5f;

        /// <summary>Насколько живо дышит свет. Ноль — не дышит вовсе.</summary>
        [SerializeField] private float _pulseDepth = 0.18f;
        [SerializeField] private float _pulseSpeed = 0.6f;

        [Tooltip("Насколько близко надо подвести взгляд к столу, чтобы шар "
               + "заметил игрока. Мерится по земле, в метрах.")]
        [SerializeField] private float _reach = CampFocus.TableReach;

        [Tooltip("Как часто напоминать о сундуке, пока его не разобрали. "
               + "Тревогу ведёт разобранный сундук, и только он: до 24 сентября "
               + "через две минуты она приходила сама.")]
        [SerializeField] private float _chestNudge = 60f;

        [Tooltip("Как часто Карган зовёт к горящему шару, пока Греховод "
               + "не подошёл. Отряды гаснут только у него на глазах.")]
        [SerializeField] private float _returnNudge = 40f;

        [Tooltip("Сколько игрок смотрит в горящий шар, прежде чем лагерь "
               + "уходит в разгром.")]
        [SerializeField] private float _watchSeconds = 6f;

        [Tooltip("Сколько отрядов гаснет на глазах. По сценарию их три: "
               + "два уже были в пути, третий игрок отправил сам.")]
        [SerializeField] private int _squadsOut = 3;

        [Tooltip("Сколько длится гибель одного отряда: вспышка и провал.")]
        [SerializeField] private float _outSeconds = 1.5f;

        public bool IsAlarmed { get; private set; }

        /// <summary>
        /// Была ли тревога. Статично — как и отбор на побеге: доля обязана
        /// пережить смену сцены, а следующая сцена должна отличать
        /// «тревога прогремела» от «шара в сцене не было».
        /// </summary>
        public static bool Raised { get; private set; }

        /// <summary>
        /// Ведёт ли шар сцену прямо сейчас. Директор смотрит сюда, чтобы
        /// не отсчитывать свой запас, пока сцену есть кому вести: иначе
        /// два срока пришлось бы держать согласованными руками, а они
        /// разъезжаются при первой же правке любого из них.
        /// </summary>
        public static bool Leading { get; private set; }

        private Transform _eye;
        private bool _sequenceStarted;
        private bool _leadsAlarm;
        private bool _showing;

        void Awake()
        {
            if (_glow == null) _glow = GetComponentInChildren<Light>();

            // Свой же прошлый прогон: без уборки второй запуск демо
            // из редактора начинался бы с уже отгремевшей тревогой.
            Raised = false;
            Leading = false;
            IsAlarmed = false;

            Apply();
        }

        void Start()
        {
            var cam = Camera.main;
            if (cam != null) _eye = cam.transform;
            else Debug.LogWarning("[ШАР] Камеры в сцене нет: подходить к столу "
                                + "нечем, совет придётся открывать самому.");

            // Сцену 3 ведёт только тот шар, у которого была сцена 2.
            // Стол стоит и в разгроме — там та же расстановка, чтобы место
            // узнавалось, — но старший к тому времени уже назначен, и без
            // этой проверки шар зажёгся бы там снова и увёл бы сцену
            // разгрома через тринадцать секунд, не дав добежать до края карты.
            _leadsAlarm = Object.FindFirstObjectByType<UI.CommanderCouncilUI>() != null;
        }

        /// <summary>
        /// Подвёл ли игрок взгляд к столу. Отвечает на вопрос, но решения
        /// не принимает: что делать с этим знанием — работа совета.
        /// </summary>
        public bool PlayerIsClose()
        {
            // Есть тело — спрашиваем тело. «Подойти» обязано значить
            // «дойти ногами»: пока Греховода в сцене не было, подойти
            // к столу можно было, не сходя с места, — достаточно навести
            // на него камеру. Отсюда и ощущение, что совет случается сам.
            if (SinbinderPlayer.Exists)
                return CampFocus.GroundDistance(SinbinderPlayer.Where,
                                                transform.position) <= _reach;

            if (_eye == null) return false;

            return CampFocus.Reached(_eye.position, _eye.forward,
                                     transform.position, _reach);
        }

        /// <summary>Тревога: шар наливается красным. Сцена 3.</summary>
        public void Alarm()
        {
            if (IsAlarmed) return;

            IsAlarmed = true;
            Raised = true;
            Apply();
            Debug.Log("[ШАР] Тревога: отряды гаснут.");
        }

        /// <summary>Вернуть спокойный свет.</summary>
        public void Calm()
        {
            IsAlarmed = false;
            Apply();
        }

        void OnDestroy()
        {
            // Сцену закрыли посреди тревоги. Оставить Leading поднятым —
            // значит запереть следующий лагерь навсегда.
            Leading = false;
        }

        private void Apply()
        {
            if (_glow == null) return;
            _glow.color = IsAlarmed ? _alarm : _calm;
            _glow.intensity = IsAlarmed ? _alarmIntensity : _calmIntensity;
        }

        void Update()
        {
            // Совет прошёл — начинается сцена 3. Один раз за сцену.
            if (_leadsAlarm && !_sequenceStarted
                && !string.IsNullOrEmpty(SquadRoster.CommanderName))
            {
                _sequenceStarted = true;
                Leading = true;
                StartCoroutine(AlarmRoutine());
            }

            Breathe();
        }

        /// <summary>
        /// Сцена 3 целиком: пауза, красный свет, догадка Каргана,
        /// и лагерь уходит в разгром.
        ///
        /// Время реальное: панель совета ставит игру на паузу, и на игровом
        /// времени тревога не наступила бы никогда, если бы пауза случайно
        /// не снялась.
        /// </summary>
        private IEnumerator AlarmRoutine()
        {
            // Порядок из прохождения автора: отправили отряд → идём
            // к сундуку → тревога → идём к шару. До 13 сентября тревога
            // ждала семь секунд после совета и лишь потом — сундук,
            // то есть приходила по часам даже тому, кто сундук уже открыл.
            //
            // Сундука в сцене может не быть совсем — тогда ждать некого,
            // и пустое ожидание было бы не осторожностью, а провалом.
            bool hasChest = Object.FindFirstObjectByType<TrophyChest>() != null;

            if (hasChest)
                yield return Beat.UntilPlayer(() => TrophyChest.Looted, _chestNudge,
                    "Карган ждёт у сундука Марги.");

            Alarm();

            var log = Object.FindFirstObjectByType<UI.BattleLogUI>();
            string name = SquadRoster.CommanderName;

            if (log != null)
            {
                // Про красный свет не пишем: он и так горит на столе,
                // и строка о том, что игрок видит глазами, — лишняя.

                // Догадка растёт из греха командира — того самого, который
                // в эпилоге решит, сколько их вернётся. Названия греха игрок
                // не увидит: он увидит, что Карган узнаёт человека.
                if (SquadRoster.TryGet(name, out var commander))
                    log.Write($"Карган: «Похоже, что-то случилось. "
                            + $"Вероятно, {name} {Homecoming.Guess(commander.Sin)}».");
            }

            // Зрелище — тому, кто смотрит. Игрок в этот миг у сундука,
            // и гасить отряды в шаре, от которого он отошёл, значит показать
            // главное демо спиной. Карган зовёт, и гаснут они, когда
            // Греховод подошёл.
            if (!PlayerIsClose())
            {
                log?.Write("Карган: «Владыка, взгляните в шар. Скорее».");

                yield return Beat.UntilPlayer(PlayerIsClose, _returnNudge,
                    "Карган: «Владыка, шар. Скорее».");
            }

            // «Игрок смотрит в шар — и видит, как его отряды гаснут один
            // за другим. Не текст, не сводка. Зрелище» (§4, сцена 3).
            // Зрелище здесь делается одним источником света: вспышка,
            // провал в темноту, тишина — и снова. Ровно столько раз,
            // сколько отрядов было в поле.
            yield return Extinguish();

            log?.Write("Карган: «Дело плохо, Владыка. Кто-то щёлкает наших "
                     + "ребят как косточки крысы».");

            yield return new WaitForSecondsRealtime(_watchSeconds);

            // Сцена доведена: дальше директор свободен и без нас.
            Leading = false;

            // Уводит лагерь шар, а не счётчик директора: сцена 3 кончается
            // тогда, когда договорил Карган.
            var director = Object.FindFirstObjectByType<PrologueDirector>();
            if (director != null) director.LeaveNow("");
            else Debug.LogWarning("[ШАР] Ведущего в сцене нет: тревога прогремела "
                                + "впустую, лагерь никуда не уйдёт.");
        }

        /// <summary>
        /// Отряды гаснут один за другим. Каждый — вспышка и провал:
        /// свет взлетает выше тревожного и падает почти в ноль, потом
        /// пауза, и следующий.
        ///
        /// Пауза между гибелями длиннее самой гибели: считать их игрок
        /// должен успевать, а торопливая череда вспышек читается как сбой
        /// освещения, а не как потеря.
        /// </summary>
        private IEnumerator Extinguish()
        {
            if (_glow == null || _squadsOut <= 0) yield break;

            // Дыхание молчит, пока идёт зрелище: иначе оно спорит
            // с вспышками за ту же яркость.
            _showing = true;

            for (int i = 0; i < _squadsOut; i++)
            {
                float t = 0f;
                while (t < _outSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / _outSeconds);

                    // Первая четверть — вспышка, остальное — провал.
                    float level = k < 0.25f
                        ? Mathf.Lerp(1f, 2.4f, k / 0.25f)
                        : Mathf.Lerp(2.4f, 0.08f, (k - 0.25f) / 0.75f);

                    _glow.intensity = _alarmIntensity * level;
                    yield return null;
                }

                yield return new WaitForSecondsRealtime(_outSeconds * 1.4f);
            }

            _showing = false;
        }

        private void Breathe()
        {
            if (_showing) return;
            if (_glow == null || _pulseDepth <= 0f) return;

            float baseline = IsAlarmed ? _alarmIntensity : _calmIntensity;
            float breath = Mathf.Sin(Time.time * _pulseSpeed * Mathf.PI * 2f);

            // Тревожный шар дышит вдвое чаще: это единственная разница,
            // которую видно от палатки, не подходя к столу.
            if (IsAlarmed) breath = Mathf.Sin(Time.time * _pulseSpeed * 4f * Mathf.PI);

            _glow.intensity = baseline * (1f + breath * _pulseDepth);
        }
    }
}
