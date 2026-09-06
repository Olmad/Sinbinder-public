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

        [Tooltip("Сколько держать лагерь после совета, прежде чем зажечься. "
               + "Отряд должен успеть уйти на глазах, иначе тревога придёт "
               + "раньше, чем игрок заметит, что лагерь поредел.")]
        [SerializeField] private float _alarmAfterCouncil = 7f;

        [Tooltip("Сколько ждать, пока игрок разберёт трофеи Марги. Тревога — "
               + "вторая половина сцены 3, и приходить раньше первой ей "
               + "незачем. Срок нужен на случай, когда игрок к сундуку "
               + "так и не подошёл: сцена не должна ждать вечно.")]
        [SerializeField] private float _waitForChest = 40f;

        [Tooltip("Сколько игрок смотрит в горящий шар, прежде чем лагерь "
               + "уходит в разгром.")]
        [SerializeField] private float _watchSeconds = 6f;

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
            yield return new WaitForSecondsRealtime(_alarmAfterCouncil);

            // Первая половина сцены 3 — трофеи. Тревога ждёт её, но не
            // бесконечно: игрок мог не пойти к сундуку вовсе, и запирать
            // на этом демо нельзя.
            float waited = 0f;
            while (!TrophyChest.Looted && waited < _waitForChest)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!TrophyChest.Looted)
                Debug.Log("[ШАР] Трофеи так и не разобрали: тревога не ждёт дольше.");

            Alarm();

            var log = Object.FindFirstObjectByType<UI.BattleLogUI>();
            string name = SquadRoster.CommanderName;

            if (log != null)
            {
                log.Write("Хрустальный шар наливается красным.");

                // Догадка растёт из греха командира — того самого, который
                // в эпилоге решит, сколько их вернётся. Названия греха игрок
                // не увидит: он увидит, что Карган узнаёт человека.
                if (SquadRoster.TryGet(name, out var commander))
                    log.Write($"Карган: «Похоже, что-то случилось. "
                            + $"Вероятно, {name} {Homecoming.Guess(commander.Sin)}».");
            }

            yield return new WaitForSecondsRealtime(_watchSeconds);

            log?.Write("Карган: «Дело плохо, Владыка. Кто-то щёлкает наших "
                     + "ребят как косточки крысы».");

            // Сцена доведена: дальше директор свободен и без нас.
            Leading = false;

            // Уводит лагерь шар, а не счётчик директора: сцена 3 кончается
            // тогда, когда договорил Карган.
            var director = Object.FindFirstObjectByType<PrologueDirector>();
            if (director != null) director.LeaveNow("");
            else Debug.LogWarning("[ШАР] Ведущего в сцене нет: тревога прогремела "
                                + "впустую, лагерь никуда не уйдёт.");
        }

        private void Breathe()
        {
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
