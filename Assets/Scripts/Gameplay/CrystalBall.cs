// Assets/Scripts/Gameplay/CrystalBall.cs
// Перевод: текст через Loc
using System.Collections;
using UnityEngine;

using Sinbinder.Core;
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

        /// <summary>
        /// Камера смотрит в шар, и отряды гаснут на глазах. С какого мига
        /// (настоящее время) — <see cref="GazeSince"/>. Спрашивает прогон:
        /// снять кадр.
        /// </summary>
        public static bool Gazing { get; private set; }
        public static float GazeSince { get; private set; }

        private Transform _eye;
        private bool _sequenceStarted;
        private bool _leadsAlarm;
        private bool _showing;

        // ── Стекло и огни отрядов (41-SHOWCASE п. 7: «гаснущие огни в шаре») ──
        //
        // До 2 октября шар был серой сферой, а светился только свет вокруг
        // неё: гибель отрядов шла одной лампой — вспышка и провал, трижды.
        // Теперь шар — стекло цвета своего света, а внутри огни: по одному
        // на отряд в поле. До совета их два (два отряда уже в пути), после —
        // три (третий отправил игрок); на тревоге они гаснут по одному,
        // каждый — вспышкой и провалом вместе со светом.

        private Material _glass;
        private Mesh _glassMesh;
        private Texture2D _glassTexture;
        private Material _moteMaterial;
        private ParticleSystem _motes;
        private ParticleSystem.Particle[] _moteBuffer;

        /// <summary>Сколько огней горит.</summary>
        private int _lit;

        /// <summary>Который огонь гаснет сейчас (−1 — никакой) и как далеко зашло, 0…1.</summary>
        private int _dying = -1;
        private float _dyingK;

        void Awake()
        {
            if (_glow == null) _glow = GetComponentInChildren<Light>();

            // Свой же прошлый прогон: без уборки второй запуск демо
            // из редактора начинался бы с уже отгремевшей тревогой.
            Raised = false;
            Leading = false;
            IsAlarmed = false;
            Gazing = false;

            Apply();
            Glass();
            Motes();
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

            // Огни — только у шара совета: в склепе и на полигоне шар
            // открывает карту вылазок, и отрядов в поле у него нет.
            // Ядро света — у всех.
            if (_leadsAlarm) _lit = Mathf.Max(0, _squadsOut - 1);

            // Лагерь открыт записью посреди разгрома: тревога уже отгремела.
            // Шар горит красным, как горел, когда разгром начался, и сцену 3
            // заново не ведёт — иначе она увела бы разгром в склеп.
            // Огней нет: отряды погибли до разгрома.
            if (RaidEvent.Running)
            {
                _leadsAlarm = false;
                _lit = 0;
                Alarm();
            }
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

            // Свет краснеет — и бьёт колокол: тревогу слышно, даже стоя
            // спиной к шару у сундука. Музыка лагеря гаснет: колокол
            // бьёт в тишине, и тишина эта — уже тревога.
            Audio.Music.Stop(1.5f);
            Audio.Sfx.Bell();
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
            Gazing = false;

            if (_glass != null) Destroy(_glass);
            if (_glassMesh != null) Destroy(_glassMesh);
            if (_glassTexture != null) Destroy(_glassTexture);
            if (_moteMaterial != null) Destroy(_moteMaterial);
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

                // Старший назначен — его отряд ушёл: в шаре третий огонь.
                _lit = _squadsOut;

                StartCoroutine(AlarmRoutine());
            }

            Breathe();
        }

        void LateUpdate()
        {
            Tint();
            DrawMotes();
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
            {
                yield return Beat.UntilPlayer(() => TrophyChest.Looted, _chestNudge,
                    Loc.T("Карган ждёт у сундука Марги."));

                // Сценарий автора, 30 сентября: «Марга принёс добычу. Игрок идёт
                // собирать, тут у него появляются деньги. Кстати, наказание
                // затянулось. Игрок может подойти к Марге и отдать долг или
                // побежать по тревоге к шару». Строка — когда экран сундука
                // закрыт: деньги уже в кошеле, и выбор настоящий.
                yield return Beat.UntilPlayer(() => TrophyChest.Browsed, 0f, null);
                CampOpening.WarnAboutDebt();
            }

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
                    Herald.Line(Loc.F("Карган: «Похоже, что-то случилось. "
                            + "Вероятно, {0} {1}».", Loc.Name(name), Homecoming.Guess(commander.Sin, commander.Gender)));
            }

            // Зрелище — тому, кто смотрит. Игрок в этот миг у сундука,
            // и гасить отряды в шаре, от которого он отошёл, значит показать
            // главное демо спиной. Карган зовёт, и гаснут они, когда
            // Греховод подошёл.
            if (!PlayerIsClose())
            {
                Herald.Line(Loc.T("Карган: «Владыка, взгляните в шар. Скорее»."));

                yield return Beat.UntilPlayer(PlayerIsClose, _returnNudge,
                    Loc.T("Карган: «Владыка, шар. Скорее»."));
            }

            // «Игрок смотрит в шар — и видит, как его отряды гаснут один
            // за другим. Не текст, не сводка. Зрелище» (§4, сцена 3).
            // Вспышка, провал в темноту, тишина — и снова. Ровно столько
            // раз, сколько отрядов было в поле. С 2 октября — кадром шара:
            // сверху, с высоты камеры, шар — горсть точек на экране.
            yield return Gaze();

            Herald.Line(Loc.T("Карган: «Дело плохо, Владыка. Кто-то щёлкает наших "
                     + "ребят как косточки крысы»."));

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
                // Гаснет последний из горящих огней — вместе со светом.
                _dying = _lit > 0 ? _lit - 1 : -1;

                float t = 0f;
                while (t < _outSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / _outSeconds);
                    _dyingK = k;

                    // Первая четверть — вспышка, остальное — провал.
                    float level = k < 0.25f
                        ? Mathf.Lerp(1f, 2.4f, k / 0.25f)
                        : Mathf.Lerp(2.4f, 0.08f, (k - 0.25f) / 0.75f);

                    _glow.intensity = _alarmIntensity * level;
                    yield return null;
                }

                if (_dying >= 0) _lit = _dying;
                _dying = -1;

                yield return new WaitForSecondsRealtime(_outSeconds * 1.4f);
            }

            _showing = false;
        }

        /// <summary>
        /// Кадр шара (41-SHOWCASE п. 9): камера подъезжает к шару, мир стоит,
        /// огни гаснут, темнота держится миг — и камера возвращается. Потом
        /// договорит Карган («Дело плохо…») — уже своим наездом.
        ///
        /// Кадр занят Карганом — ждём, пока договорит, но не дольше восьми
        /// секунд. Игра стоит под панелью — кадр не отнимаем (так решает
        /// и <see cref="Herald"/>): огни гаснут без него, как до 2 октября.
        /// </summary>
        private IEnumerator Gaze()
        {
            var camera = Dialogue.DialogueCameraController.Instance;
            var pause = GamePauseController.Instance;
            var eye = Camera.main;
            bool framed = false;
            int mine = -1;

            if (camera != null && eye != null)
            {
                float waited = 0f;
                while ((camera.InDialogue || Herald.Busy) && waited < 8f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (!camera.InDialogue && (pause == null || !pause.IsPaused))
                {
                    framed = true;
                    camera.SaveCameraPosition();
                    if (pause != null) { pause.Pause(); mine = pause.Stamp; }

                    // Со стороны, откуда игрок и так смотрел: кадр не должен
                    // разворачивать мир. Чуть сверху — видно и стол.
                    Vector3 ball = transform.position;
                    Vector3 along = eye.transform.forward;
                    along.y = 0f;
                    along = along.sqrMagnitude < 0.01f ? Vector3.forward : along.normalized;
                    Vector3 from = ball - along * 1.35f + Vector3.up * 0.45f;

                    // Сперва время, потом флаг: прогон снимает кадр по ним
                    // двоим, и флаг при прошлом времени снимал кадр до наезда.
                    yield return camera.FocusOnPoint(ball, from, null, push: 9f);
                    GazeSince = Time.realtimeSinceStartup;
                    Gazing = true;
                }
            }

            yield return Extinguish();

            if (framed && camera != null)
            {
                // Последний огонь погас — темноту подержать: это и есть потеря.
                yield return new WaitForSecondsRealtime(0.8f);
                camera.StopSway();
                yield return camera.RestoreCamera();
            }

            if (framed && pause != null && pause.IsPaused && pause.Stamp == mine)
                pause.Resume();

            Gazing = false;
        }

        // ──────────────────────────────────
        // Стекло и огни
        // ──────────────────────────────────

        /// <summary>
        /// Шар — стекло цвета своего света. Шейдер <c>Sprites/Default</c>:
        /// он всегда в сборке (на нём кольца выбора и искры ударов),
        /// без освещения и полупрозрачный — сферу видно насквозь, и огни
        /// внутри не прячутся за ней. Верх светлее низа: так стекло
        /// читается шаром, а не плоским кругом.
        /// </summary>
        private void Glass()
        {
            var filter = GetComponent<MeshFilter>();
            var body = GetComponent<MeshRenderer>();
            var shader = Shader.Find("Sprites/Default");
            if (filter == null || body == null || filter.sharedMesh == null || shader == null) return;

            // Своя копия сетки — с белым цветом вершин: шейдер умножает
            // на него, а у готовой сферы цвета вершин нет. Сетку, которую
            // нельзя читать, не трогаем: стекло без неё останется серым шаром,
            // а не пропадёт.
            if (!filter.sharedMesh.isReadable) return;
            _glassMesh = Instantiate(filter.sharedMesh);
            var white = new Color32[_glassMesh.vertexCount];
            for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
            _glassMesh.colors32 = white;
            filter.sharedMesh = _glassMesh;

            const int h = 32;
            _glassTexture = new Texture2D(2, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[2 * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (h - 1f);
                byte rgb = (byte)(Mathf.Lerp(0.70f, 1f, v) * 255f);
                byte a = (byte)(Mathf.Lerp(0.60f, 1f, v) * 255f);
                px[y * 2] = px[y * 2 + 1] = new Color32(rgb, rgb, rgb, a);
            }
            _glassTexture.SetPixels32(px);
            _glassTexture.Apply();

            _glass = new Material(shader) { name = "Стекло шара (код)", mainTexture = _glassTexture };  // ключ
            body.sharedMaterial = _glass;
            body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            body.receiveShadows = false;

            Tint();
        }

        /// <summary>
        /// Цвет стекла — за светом шара, но тише его: стекло тонированное
        /// и прозрачное, светит не оно, а ядро внутри (<see cref="DrawMotes"/>).
        /// Первый заход — стекло ярче единицы и плотное — в кадре вышел
        /// матовым персиковым мячом: красное за единицей тонмаппинг и золото
        /// светов (взгляд) обесцветили, а плотность закрыла огни.
        /// </summary>
        private void Tint()
        {
            if (_glass == null || _glow == null) return;

            float level = Level();
            Color c = _glow.color * Mathf.Min(0.9f, 0.22f * level);
            c.a = Mathf.Clamp(0.16f + 0.05f * level, 0.16f, 0.42f);
            _glass.color = c;
        }

        /// <summary>Сколько сейчас света в шаре: 1 — спокойный, 3 — тревога, 7 — вспышка гибели.</summary>
        private float Level()
            => _glow == null ? 1f : _glow.intensity / Mathf.Max(0.01f, _calmIntensity);

        /// <summary>
        /// Огни отрядов: облако частиц без своего хода — частицы ставит
        /// <see cref="DrawMotes"/> каждый кадр. Рисуются после стекла
        /// (очередь на единицу дальше), иначе стекло легло бы поверх.
        /// </summary>
        private void Motes()
        {
            if (_motes != null) return;

            var go = new GameObject("Огни отрядов");
            go.transform.SetParent(transform.parent, false);

            _motes = go.AddComponent<ParticleSystem>();
            _motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _motes.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1000f;
            main.startSpeed = 0f;
            main.maxParticles = Mathf.Max(1, _squadsOut) + 1;

            var emission = _motes.emission;
            emission.enabled = false;
            var shape = _motes.shape;
            shape.enabled = false;

            _moteMaterial = new Material(HitBurst.Dot())
            {
                name = "Огни отрядов (код)",  // ключ
                renderQueue = 3001,
                // Ярче единицы: огонь в стекле должен светить, а не лежать точкой.
                color = new Color(2.2f, 2.0f, 1.7f, 1f),
            };

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _moteMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _motes.Play();
            _moteBuffer = new ParticleSystem.Particle[Mathf.Max(1, _squadsOut) + 1];
        }

        /// <summary>
        /// Первым — ядро: мягкое пятно цвета шара во всё стекло, яркость —
        /// за светом (оно и светит, а не стекло). Потом огни: плывут
        /// по кругу внутри, каждый своим ходом; гаснущий вспыхивает вдвое
        /// и тает в ноль — так же, как свет шара. Время — настоящее:
        /// на тревоге мир стоит.
        /// </summary>
        private void DrawMotes()
        {
            if (_motes == null || _moteBuffer == null) return;

            float t = Time.unscaledTime;
            Vector3 centre = transform.position;
            int slots = Mathf.Max(1, _squadsOut);
            int count = 0;

            // Цвет ядра — частица, то есть не ярче единицы; материал огней
            // ярче единицы вдвое, и ядро на тревоге светит, а не лежит.
            Color core = _glow != null ? _glow.color * Mathf.Min(1f, 0.3f * Level()) : Color.black;
            _moteBuffer[count++] = new ParticleSystem.Particle
            {
                position = centre,
                startSize = transform.lossyScale.x * 0.8f,
                startColor = new Color(core.r, core.g, core.b, 1f),
                startLifetime = 1000f,
                remainingLifetime = 1000f,
            };

            for (int j = 0; j < _lit && count < _moteBuffer.Length; j++)
            {
                float angle = t * 0.45f + j * Mathf.PI * 2f / slots;
                var offset = new Vector3(Mathf.Cos(angle) * 0.085f,
                                         Mathf.Sin(t * 0.8f + j * 1.7f) * 0.04f,
                                         Mathf.Sin(angle) * 0.085f);

                float size = 0.045f * (1f + 0.15f * Mathf.Sin(t * 2.3f + j));
                float alpha = 1f;
                if (j == _dying)
                {
                    float k = _dyingK;
                    size = k < 0.25f ? Mathf.Lerp(size, 0.12f, k / 0.25f)
                                     : Mathf.Lerp(0.12f, 0f, (k - 0.25f) / 0.75f);
                    alpha = k < 0.25f ? 1f : 1f - (k - 0.25f) / 0.75f;
                }

                _moteBuffer[count++] = new ParticleSystem.Particle
                {
                    position = centre + offset,
                    startSize = size,
                    startColor = new Color32(255, 236, 200, (byte)(alpha * 255f)),
                    startLifetime = 1000f,
                    remainingLifetime = 1000f,
                };
            }

            _motes.SetParticles(_moteBuffer, count);
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
