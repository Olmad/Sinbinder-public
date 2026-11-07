// Перевод: текст через Loc
using UnityEngine;
using System.Collections;

namespace Sinbinder.Dialogue
{
    public class DialogueCameraController : MonoBehaviour
    {
        public static DialogueCameraController Instance { get; private set; }

        [SerializeField] private float _transitionSpeed = 5f;

        [Header("Кадр говорящего")]
        [Tooltip("С какого расстояния начинается план. Кадр берёт верх "
               + "туловища, поэтому счёт на метры, а не на десятки.")]
        [SerializeField] private float _cameraDistance = 1.95f;

        [Tooltip("Вбок от оси взгляда: кадр в три четверти, а не в упор. "
               + "Ноль ставит камеру ровно перед лицом, и разговор читается "
               + "как допрос.")]
        [SerializeField] private float _sideOffset = 0.62f;

        [Tooltip("Высота камеры. На уровне плеча: снизу выходит героика, "
               + "сверху — жалость, а разговор равных снимают с глаз.")]
        [SerializeField] private float _cameraHeight = 1.48f;

        [Tooltip("Куда смотрит камера — высота точки на говорящем.")]
        [SerializeField] private float _lookHeight = 1.56f;

        [Tooltip("Поле зрения. Узкое сжимает перспективу — именно оно, "
               + "а не близость, делает план кинематографичным.")]
        [SerializeField] private float _dialogueFOV = 36f;

        [Tooltip("На сколько камера подъезжает за реплику. Медленный наезд "
               + "держит внимание там, где слова.")]
        [SerializeField] private float _pushIn = 0.45f;

        [Tooltip("За сколько секунд наезд доходит до конца.")]
        [SerializeField] private float _pushSeconds = 4.5f;

        [SerializeField] private float _swaySpeed = 0.5f;

        [Tooltip("Покачивание. Мелкое: наезд теперь несёт движение сам, "
               + "и качка поверх него читается как дрожь в руках.")]
        [SerializeField] private float _swayAmount = 0.07f;

        private Camera _cam;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private float _originalFOV;
        private bool _inDialogue = false;
        private Coroutine _swayCoroutine;
        private int _frameRevision;

        // Проверки кадра без выделения памяти на каждом шаге наезда.
        // Если буфер заполнен, место считаем занятым: пропуск стены хуже отказа от наезда.
        private readonly Collider[] _frameOverlaps = new Collider[32];
        private readonly RaycastHit[] _frameHits = new RaycastHit[32];
        private static readonly float[] FrameAngles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 180f };

        // Крупный план одного: говорящий смотрит на собеседника, а не в объектив (автор, 07.11:
        // «говорящий всегда смотрит в камеру, и это странно»). Камера — в стороне от линии взгляда:
        // база повёрнута на GazeOff, да ещё сдвиг вбок (_sideOffset при _cameraDistance — около 18°),
        // итого около 33° — лицо в три четверти, глаза мимо объектива. Запасные стороны — тоже
        // мимо взгляда (≈ −27°, 58°, −47°, 78°): на линии взгляда и за спиной планов нет.
        private const float GazeOff = 15f;
        private static readonly float[] GazeAngles = { 0f, -60f, 25f, -80f, 45f };
        private Transform _listener;
        private Vector3 _gaze = Vector3.forward;
        private float _frameYaw;
        private float _frameReach = 1f;
        private float _distanceMultiplier = 1f;

        public bool InDialogue => _inDialogue;

        /// <summary>На кого наехали последним. Спрашивает прогон: не заслонён ли он.</summary>
        public Transform Target { get; private set; }
        /// <summary>Второй участник общего плана; null у одиночной реплики.</summary>
        public Transform Partner { get; private set; }
        private Transform _pairFirst, _pairSecond;
        private Vector3 _pairSide;

        void Awake()
        {
            Instance = this;
            _cam = Camera.main;
        }

        /// <summary>
        /// Двойник на Managers новой сцены гибнет (выживает Managers прежней),
        /// но Awake уже успел назвать его Instance. Передать имя живому.
        /// </summary>
        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            foreach (var other in FindObjectsByType<DialogueCameraController>(FindObjectsSortMode.None))
                if (other != this) { Instance = other; break; }
        }

        /// <summary>
        /// Камера, найденная заново, если прежней не стало.
        ///
        /// Контроллер стоит на Managers, а тот переживает смену сцен:
        /// на нём четыре синглтона, и каждый объявляет DontDestroyOnLoad.
        /// Камера смены сцен не переживает — то есть со второй сцены
        /// ссылка, взятая в Awake, указывает на уничтоженный объект,
        /// и первый же разговор упал бы вместе с наездом.
        ///
        /// Третий случай этой болезни подряд, после выделения и рамки.
        /// Разбор — 14-HANDOFF §12.4.
        /// </summary>
        private Camera Cam()
        {
            if (_cam == null) _cam = Camera.main;
            return _cam;
        }

        /// <summary>
        /// Запомнить, куда камера вернётся.
        ///
        /// <b>Дом принадлежит тому, кто вошёл первым.</b> Наездов
        /// в игре пять хозяев — разговор, поступок по своей воле,
        /// вручение титула, первая фраза поднятого, дымовой прогон, —
        /// и каждый зовёт это сам. Пока камера уже в кадре, «запомнить»
        /// означало бы запомнить **чужой кадр**: второй вошедший
        /// сохранял лицо воина как исходное положение, и
        /// <see cref="RestoreCamera"/> возвращал камеру туда же,
        /// откуда пытался уехать. Домой она после этого не приезжала
        /// никогда.
        ///
        /// Поэтому здесь отказ, а не перезапись. Это не чинит
        /// одновременность — строки на полосе всё ещё затрут друг
        /// друга, — но камера возвращается домой при любом порядке
        /// событий, а зритель прощает рывок и не прощает застрявший
        /// кадр.
        /// </summary>
        public void SaveCameraPosition()
        {
            if (_inDialogue)
            {
                // Не молча: это признак того, что два наезда сошлись,
                // и знать об этом полезнее, чем не знать.
                Debug.LogWarning("[КАМЕРА] Наезд поверх наезда: дом оставлен "
                               + "прежним. Кто-то вошёл в кадр, не дождавшись "
                               + "предыдущего.");
                return;
            }

            // Камера ещё едет домой (RestoreCamera): дом — там, куда она едет, а не там,
            // где её застал новый наезд. Прогон 06.11: разговор с Маргой начат, пока камера
            // уезжала от Ждана, — домом стало место у Ждана, у земли, в боку палатки,
            // и после долга камера вернулась туда, а не наверх.
            if (_homebound == _frameRevision) return;

            var cam = Cam();

            // Камеры может не быть вовсе: разговор начинается и от смерти
            // воина (Damageable.OnDestroy → CombatManager → DialogueTrigger),
            // а смерть случается и при выгрузке сцены, когда камера уже
            // уничтожена. Прогон 24 сентября упал здесь исключением
            // на семьдесят восьмой секунде.
            //
            // Молчать нельзя: без дома наезд не вернёт камеру назад,
            // и это стоит увидеть в журнале, а не искать потом глазами.
            if (cam == null)
            {
                Debug.LogWarning("[КАМЕРА] Дом не запомнен: камеры в сцене "
                               + "нет. Разговор начат в миг, когда сцена "
                               + "уже уходит.");
                return;
            }

            _originalPosition = cam.transform.position;
            _originalRotation = cam.transform.rotation;
            _originalFOV = cam.fieldOfView;
            _pairFirst = _pairSecond = null;
        }

        /// <summary>
        /// Дождаться, пока камера освободится.
        ///
        /// Для тех, кто может подождать: церемония стоит в очереди,
        /// поднятый ждёт своей секунды. Разговор ждать не может —
        /// он и есть главный хозяин кадра.
        ///
        /// Срок обязателен. Флаг «в кадре» снимает <see cref="RestoreCamera"/>,
        /// а его могут не позвать — воин погиб, сцену выгрузили, — и ждать
        /// тогда пришлось бы вечно. Вышел срок — входим поверх: рывок
        /// хуже тишины, но тишина хуже всего.
        /// </summary>
        public IEnumerator WaitUntilFree(float seconds = 12f)
        {
            float waited = 0f;
            while (_inDialogue && waited < seconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_inDialogue)
                Debug.LogWarning($"[КАМЕРА] Ждал освобождения {seconds:F0} сек "
                               + "и не дождался. Вхожу поверх.");
        }

        /// <summary>
        /// Навести камеру на говорящего и поднять полосы.
        ///
        /// Полосы поднимаются здесь, а не у каждого вызывающего, и это
        /// не экономия строк: наезд в игре один на всё — разговор,
        /// поступок по своей воле, вручение титула, — и рамка обязана
        /// быть при нём всегда. Заведи её у вызывающих, и четвёртый
        /// наезд однажды приедет без рамки, а заметят это на показе.
        ///
        /// <paramref name="caption"/> — что написать на нижней полосе.
        /// Пусто оставляет разговор: там строку печатает по букве
        /// <see cref="UI.DialogueUI"/>, и подставлять её целиком заранее
        /// значило бы показать реплику до того, как её произнесли.
        /// </summary>
        private readonly System.Collections.Generic.List<Renderer> _hiddenHero = new();

        /// <summary>Спрятать тело Греховода, если оно между камерой и говорящим (ближе 0,7 м к лучу взгляда).</summary>
        private void HideHeroIfInFrame(Vector3 from, Vector3 look, Transform speaker)
        {
            if (!Gameplay.SinbinderPlayer.Exists || speaker == Gameplay.SinbinderPlayer.Instance.transform)
            {
                ShowHeroAgain();
                return;
            }
            var hero = Gameplay.SinbinderPlayer.Instance;
            var chest = hero.transform.position + Vector3.up * 1.2f;
            var ray = look - from;
            float k = Mathf.Clamp01(Vector3.Dot(chest - from, ray) / Mathf.Max(1e-4f, ray.sqrMagnitude));
            if ((from + ray * k - chest).magnitude > 0.7f)
            {
                ShowHeroAgain();
                return;
            }
            if (_hiddenHero.Count > 0) return;

            foreach (var r in hero.GetComponentsInChildren<Renderer>())
                if (r.enabled) { r.enabled = false; _hiddenHero.Add(r); }
        }

        private void ShowHeroAgain()
        {
            foreach (var r in _hiddenHero) if (r != null) r.enabled = true;
            _hiddenHero.Clear();
        }

        /// <summary>
        /// Доля полуширины кадра, на которую взгляд уходит вправо от лица: справа ответы
        /// (<see cref="UI.TalkView"/>, от 60% ширины). Лицо по центру — и два ответа ложились
        /// Хорю на грудь (КТ1, кадр «разговор с выбором»). 0 — лицо по центру.
        /// </summary>
        private float _menuRoom;

        public IEnumerator FocusOn(Transform target, string caption = null, float distanceMultiplier = 1f,
                                   bool menu = false)
        {
            if (target == null) yield break;
            _menuRoom = menu ? 0.3f : 0f;

            // Камеры нет — сцена уходит, и SaveCameraPosition об этом уже
            // сказал. Наводить нечего: без этой строки наезд падал здесь же
            // на поле зрения (прогон 26 сентября — разговор, начатый смертью
            // тела при выгрузке лагеря).
            if (Cam() == null) yield break;

            int revision = ++_frameRevision;
            if (_swayCoroutine != null) StopCoroutine(_swayCoroutine);
            _swayCoroutine = null;
            _inDialogue = true;
            Target = target;
            Partner = null;
            ShowHeroAgain();
            _frameYaw = 0f;
            _frameReach = 1f;
            _distanceMultiplier = Mathf.Clamp(distanceMultiplier, 0.7f, 1.5f);
            Cam().fieldOfView = _dialogueFOV;

            // Полосы живут на Canvas и умирают со сценой, а контроллер
            // её переживает: спрашиваем каждый раз, а не держим ссылку.
            UI.Letterbox.Instance?.Show(caption);

            // Перед говорящим, а не за ним. Здесь стояло -target.forward,
            // то есть камера заходила со спины и наводилась на затылок:
            // весь разговор игрок смотрел людям в затылки.
            //
            // Мерка — рост говорящего, а не метры человека. Кадр подобран под
            // рост в метр восемьдесят, а скелеты и зомби ниже — голова у них
            // около метра, — и наезд смотрел поверх: в кадр попадала макушка
            // у нижней полосы (прогон 26 сентября, кадр «реплика с наездом»).
            // Все числа кадра умножаются на рост — композиция та же.
            _scale = Scale(target, out float ground);
            Vector3 anchor = new Vector3(target.position.x, ground, target.position.z);
            // Взгляд — на Греховода, если он рядом и снимают не его самого; иначе — куда смотрит.
            _listener = null;
            _gaze = Flat(target.forward);
            if (Gameplay.SinbinderPlayer.Exists && target != Gameplay.SinbinderPlayer.Instance.transform)
            {
                var to = Gameplay.SinbinderPlayer.Instance.transform.position - target.position; to.y = 0f;
                if (to.sqrMagnitude > 0.16f && to.sqrMagnitude < 225f)
                {
                    _listener = Gameplay.SinbinderPlayer.Instance.transform;
                    _gaze = to.normalized;
                }
            }
            Vector3 face = Quaternion.AngleAxis(GazeOff, Vector3.up) * _gaze;
            Vector3 side = Vector3.Cross(Vector3.up, face);

            Vector3 lookTarget = anchor + Vector3.up * (_lookHeight * _scale);
            if (!TrySpeakerFrame(anchor, face, side, lookTarget, _cameraDistance * _scale * _distanceMultiplier, 0f,
                                 target, out Vector3 baseCamPos, fresh: true))
            {
                // В тесноте оставляем прежний обзор и реплику на полосе.
                // Нельзя заменять отсутствие свободного ракурса камерой внутри стены.
                Overview();
                Debug.LogWarning("[КАМЕРА] Свободного ракурса для разговора нет: оставлен прежний обзор.");
                yield break;
            }

            // Греховод стоит ровно там, где встаёт камера, — он и подошёл поговорить, — и кадр
            // закрывала его спина (автор, 26.10: «при выборе показывается спина Греховода»).
            // Стоит между камерой и говорящим — тело на время наезда прячется (RestoreCamera вернёт).
            HideHeroIfInFrame(baseCamPos, lookTarget, target);

            yield return MoveCamera(baseCamPos, Aim(baseCamPos, lookTarget), target, lookTarget);

            if (!_inDialogue || Target != target || revision != _frameRevision) yield break;

            // Дальше живём на числах, а не на ссылке: говорящий может
            // погибнуть посреди собственной реплики, и наезд, тянущийся
            // к уничтоженному transform, упал бы вместе с разговором.
            // На этом уже обожглись в DialogueUI.
            _swayCoroutine = StartCoroutine(PushIn(anchor, face, side, lookTarget));
        }

        /// <summary>
        /// Двое в кадре. Сторона оси разговора сохраняется между ответами,
        /// а акцент смещается к говорящему. Под меню справа оставляем место.
        /// </summary>
        public IEnumerator FocusConversation(Transform speaker, Transform listener, bool menu = false)
        {
            if (speaker == null) yield break;
            if (listener == null || speaker == listener)
            {
                yield return FocusOn(speaker);
                yield break;
            }
            var cam = Cam();
            if (cam == null) yield break;
            bool samePair = (speaker == _pairFirst && listener == _pairSecond)
                         || (speaker == _pairSecond && listener == _pairFirst);
            if (!samePair)
            {
                _pairFirst = speaker;
                _pairSecond = listener;
                _pairSide = Vector3.Cross(Vector3.up, Flat(listener.position - speaker.position));
                if (Vector3.Dot(_pairSide, cam.transform.position - (speaker.position + listener.position) * 0.5f) < 0f)
                    _pairSide = -_pairSide;
            }
            int revision = ++_frameRevision;
            if (_swayCoroutine != null) StopCoroutine(_swayCoroutine);
            _swayCoroutine = null;
            ShowHeroAgain();
            _inDialogue = true;
            Target = speaker;
            Partner = listener;
            cam.fieldOfView = _dialogueFOV;
            UI.Letterbox.Instance?.Show();

            if (!TryPairFrame(speaker, listener, menu, out var from, out var look, fresh: true))
            {
                yield return FocusOn(speaker);
                yield break;
            }
            var rotation = Quaternion.LookRotation(look - from);
            if (samePair) cam.transform.SetPositionAndRotation(from, rotation);
            else yield return MoveCamera(from, rotation, speaker, look, listener: listener);
            if (!_inDialogue || revision != _frameRevision) yield break;
            _swayCoroutine = StartCoroutine(FollowPair(speaker, listener, menu));
        }

        private IEnumerator FollowPair(Transform speaker, Transform listener, bool menu)
        {
            while (_inDialogue)
            {
                var cam = Cam();
                if (cam == null) yield break;
                if (speaker == null || listener == null
                    || !TryPairFrame(speaker, listener, menu, out var from, out var look))
                {
                    Partner = null;
                    Overview();
                    yield break;
                }
                cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(look - from));
                // Смотрят друг на друга, а не вслед за движением объектива.
                var direction = Flat(listener.position - speaker.position);
                speaker.rotation = Quaternion.Slerp(speaker.rotation, Quaternion.LookRotation(direction),
                    1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
                yield return null;
            }
        }

        private static Bounds ConversationBody(Transform who)
        {
            float scale = Scale(who, out float ground);
            // От пояса до макушки, с запасом на плечи; оружие не раздувает кадр.
            return new Bounds(new Vector3(who.position.x, ground + 1.2f * scale, who.position.z),
                              new Vector3(0.8f, 1.2f, 0.8f) * scale);
        }

        /// <summary>Геометрия общего плана с учётом широкого/узкого экрана и меню.</summary>
        public static void ComposePair(Bounds first, Bounds second, Vector3 side, float fov, float aspect,
                                       bool menu, out Vector3 from, out Vector3 look)
        {
            side = Flat(side);
            var forward = -side;
            var right = Vector3.Cross(Vector3.up, forward);
            var centre = Vector3.Lerp(first.center, second.center, 0.42f);
            float vertical = Mathf.Tan(Mathf.Clamp(fov, 10f, 100f) * Mathf.Deg2Rad * 0.5f);
            float horizontal = vertical * Mathf.Max(0.3f, aspect);
            float width = menu ? 0.46f : 0.8f;
            float shift = menu ? 0.4f : 0f;
            float distance = Mathf.Max(PairDistance(first, centre, forward, right, horizontal, vertical * 0.6f, width, shift),
                                       PairDistance(second, centre, forward, right, horizontal, vertical * 0.6f, width, shift));
            distance = Mathf.Max(0.8f, distance) + 0.12f;
            look = centre + right * (distance * horizontal * shift);
            from = look + side * distance;
        }

        private static float PairDistance(Bounds body, Vector3 centre, Vector3 forward, Vector3 right,
                                          float horizontal, float vertical, float width, float shift)
        {
            var offset = body.center - centre;
            var ext = body.extents;
            float x = Vector3.Dot(offset, right);
            float radius = Mathf.Abs(right.x) * ext.x + Mathf.Abs(right.z) * ext.z;
            float y = Mathf.Abs(offset.y) + ext.y;
            float depth = Vector3.Dot(offset, forward) - Mathf.Abs(forward.x) * ext.x - Mathf.Abs(forward.z) * ext.z;
            // Сдвиг под меню зависит от расстояния камеры, а перспектива — ещё и
            // от глубины тела. Проверяем левую и правую границы отдельно.
            float rightEdge = ((x + radius) / horizontal - (width - shift) * depth) / width;
            float leftEdge = ((-x + radius) / horizontal - (width + shift) * depth) / width;
            return Mathf.Max(Mathf.Max(leftEdge, rightEdge), y / vertical - depth);
        }

        /// <summary>
        /// Общий план двоих. <paramref name="fresh"/> — план выбирают заново (реплика), и тогда
        /// кадр ещё и не должен быть закрыт вблизи (<see cref="Shut"/>); каждый кадр следования
        /// (<see cref="FollowPair"/>) держит выбранную сторону и проверяет только камеру и лица.
        /// </summary>
        private bool TryPairFrame(Transform speaker, Transform listener, bool menu,
                                  out Vector3 from, out Vector3 look, bool fresh = false)
        {
            var a = ConversationBody(speaker);
            var b = ConversationBody(listener);
            var cam = Cam();
            float bias = speaker == _pairFirst ? -12f : 12f;
            float least = float.MaxValue;
            // В тесноте (палатки, поленница, третий воин) камера поднимается над помехой
            // и смотрит на двоих сверху вниз. Следование держит выбранную высоту и сторону.
            _heldLift[0] = _pairLift;
            var lifts = fresh ? Lifts : _heldLift;
            var angles = fresh ? PairAngles : FrameAngles;
            foreach (float lift in lifts)
                foreach (float angle in angles)
                {
                    var side = Quaternion.AngleAxis(bias + angle, Vector3.up) * _pairSide;
                    ComposePair(a, b, side, _dialogueFOV, cam.aspect, menu, out from, out look);
                    from += Vector3.up * lift;
                    if (!ClearFrame(from, a.center + Vector3.up * (a.extents.y * 0.6f), speaker, listener)
                        || !ClearFrame(from, b.center + Vector3.up * (b.extents.y * 0.6f), speaker, listener)) continue;
                    if (fresh)
                    {
                        // Говорящий смотрит на слушающего (FollowPair): камера позади этой
                        // линии снимает его затылок — Марга говорила спиной (w0711h, w0711i).
                        var toCamera = Flat(from - speaker.position);
                        var facing = Flat(listener.position - speaker.position);
                        if (Vector3.Dot(toCamera.normalized, facing.normalized) < -0.26f) continue;
                        float shut = Shut(from, look, speaker, listener,
                            Mathf.Min(Vector3.Distance(from, a.center), Vector3.Distance(from, b.center)) - 0.5f);
                        if (shut > OpenEnough) { least = Mathf.Min(least, shut); continue; }
                        if (lift > 0f) Debug.Log($"[КАМЕРА] Общий план поднят на {lift:F1} м над помехой.");
                    }
                    _pairSide = Quaternion.AngleAxis(angle, Vector3.up) * _pairSide;
                    _pairLift = lift;
                    return true;
                }
            // Открытого общего плана нет: реплика уходит на крупный план говорящего
            // (FocusConversation → FocusOn) — лицо лучше, чем стена или спина. Закрытый
            // общий план брали до 07.11: полкадра ската у Ждана, Марга спиной у Марги (w0711g/h).
            if (fresh && least <= 1f)
                Debug.Log($"[КАМЕРА] Общий план: открытого нет (лучший закрыт на {least:P0}) — крупный план говорящего.");
            from = look = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Стороны общего плана при выборе: не дальше ±60° от поперечной оси. Под 90°
        /// камера встаёт за спиной одного из двоих — Марга говорила спиной (w0711h).
        /// </summary>
        private static readonly float[] PairAngles = { 0f, 30f, -30f, 60f, -60f };

        /// <summary>Подъёмы общего плана над помехой, метров: сначала на уровне глаз.</summary>
        private static readonly float[] Lifts = { 0f, 0.9f, 1.8f };

        /// <summary>На сколько поднят выбранный общий план (держит <see cref="FollowPair"/>).</summary>
        private float _pairLift;
        private readonly float[] _heldLift = new float[1];

        /// <summary>Какая доля кадра может быть закрыта вблизи, чтобы план годился.</summary>
        private const float OpenEnough = 0.2f;

        /// <summary>Доля кадра, закрытая вблизи, — сейчас, той же меркой (<see cref="Shut"/>). Сторожу прогона.</summary>
        public float ShutNow()
        {
            var cam = Cam();
            if (cam == null || !_inDialogue || Target == null) return 0f;
            var from = cam.transform.position;
            float reach = Vector3.Distance(from, ConversationBody(Target).center);
            if (Partner != null) reach = Mathf.Min(reach, Vector3.Distance(from, ConversationBody(Partner).center));
            return Shut(from, from + cam.transform.forward, Target, Partner, reach - 0.5f);
        }

        /// <summary>
        /// Какая доля кадра закрыта ближе тех, кого снимаем: стена, скат палатки, чужое тело.
        /// <see cref="ClearFrame"/> проверяет место камеры и лучи к лицам — а скат палатки
        /// сбоку закрывал полкадра разговора со Жданом, и перед ним стоял Карган
        /// (w0711e, «вещи — обмен вблизи»). Пятнадцать лучей по средней полосе кадра:
        /// верх и низ закрыты полосами кино. <paramref name="reach"/> — до ближнего из двоих.
        /// </summary>
        private float Shut(Vector3 from, Vector3 look, Transform speaker, Transform listener, float reach)
        {
            var cam = Cam();
            if (cam == null || reach < 0.3f) return 0f;
            var rotation = Quaternion.LookRotation(look - from);
            float tanV = Mathf.Tan(_dialogueFOV * Mathf.Deg2Rad * 0.5f);
            float tanH = tanV * cam.aspect;
            var own = speaker != null ? Gameplay.TentInterior.Around(speaker.position + Vector3.up * 0.1f) : null;
            var theirs = listener != null ? Gameplay.TentInterior.Around(listener.position + Vector3.up * 0.1f) : null;
            RefreshBodies();
            int shut = 0;
            for (int ix = 0; ix < 5; ix++)
                for (int iy = 0; iy < 3; iy++)
                {
                    var dir = (rotation * new Vector3((-0.8f + 0.4f * ix) * tanH, (-0.5f + 0.5f * iy) * tanV, 1f)).normalized;
                    if (Blocked(from, dir, reach, speaker, listener, own, theirs)) shut++;
                }
            return shut / 15f;
        }

        private bool Blocked(Vector3 from, Vector3 dir, float reach, Transform speaker, Transform listener,
                             Gameplay.TentInterior own, Gameplay.TentInterior theirs)
        {
            int hits = Physics.RaycastNonAlloc(from, dir, _frameHits, reach,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                // Земля и пол — не помеха: луч нижнего ряда может её задеть.
                if (_frameHits[i].normal.y > 0.7f) continue;
                if (!IgnoreInFrame(_frameHits[i].collider, speaker, listener)) return true;
            }
            for (int i = 1; i <= 12; i++)
            {
                var tent = Gameplay.TentInterior.Around(from + dir * (reach * i / 12f));
                if (tent != null && tent != own && tent != theirs) return true;
            }
            var ray = new Ray(from, dir);
            foreach (var (root, body) in Bodies)
            {
                if (root == null || Mine(root, speaker) || Mine(root, listener)) continue;
                if (body.Contains(from) || (body.IntersectRay(ray, out float d) && d < reach)) return true;
            }
            return false;
        }

        private static bool Mine(Transform root, Transform who)
            => who != null && (root == who || root.IsChildOf(who) || who.IsChildOf(root));

        /// <summary>
        /// Навести камеру на вещь, а не на говорящего: кадр шара
        /// (41-SHOWCASE п. 9 — «кадры шара»). Мерки говорящего — рост,
        /// лицо, плечо — к вещи не подходят: шар на столе ниже пояса
        /// и смотрит во все стороны сразу. Здесь кадр задаёт вызывающий:
        /// откуда смотреть и куда.
        ///
        /// Дальше — как у говорящего: полосы, узкое поле зрения, медленный
        /// подъезд (за <paramref name="push"/> секунд); дом и возврат — те же
        /// (<see cref="SaveCameraPosition"/>, <see cref="RestoreCamera"/>).
        /// </summary>
        public IEnumerator FocusOnPoint(Vector3 look, Vector3 from, string caption = null,
                                        float push = 0f, float fov = 0f, float reach = 0.2f)
        {
            if (Cam() == null) yield break;

            int revision = ++_frameRevision;
            if (_swayCoroutine != null) StopCoroutine(_swayCoroutine);
            _swayCoroutine = null;
            ShowHeroAgain();
            _inDialogue = true;
            Target = null;   // точка, а не говорящий
            Partner = null;
            // Свой угол — для общего плана, которому узкого мало (зал суда: кафедра,
            // весы и компас в одном кадре под полосами).
            Cam().fieldOfView = fov > 0f ? fov : _dialogueFOV;
            UI.Letterbox.Instance?.Show(caption);

            yield return MoveCamera(from, Quaternion.LookRotation((look - from).normalized));

            if (!_inDialogue || revision != _frameRevision) yield break;
            _swayCoroutine = StartCoroutine(PushTowards(look, from,
                push > 0f ? push : _pushSeconds, reach));
        }

        /// <summary>
        /// Кран: из одного кадра в другой плывут и место, и взгляд, и угол
        /// (зал суда: сначала снизу, из-за спины подсудимого, — потом вверх,
        /// на общий план). Камера ставится сразу, <paramref name="hold"/> секунд
        /// стоит, за <paramref name="seconds"/> переезжает, дальше — подъезд,
        /// как у <see cref="FocusOnPoint"/>.
        /// </summary>
        public void Crane(Vector3 lookFrom, Vector3 from, float fovFrom,
                          Vector3 lookTo, Vector3 to, float fovTo,
                          float hold, float seconds, float push, float reach)
        {
            var cam = Cam();
            if (cam == null) return;

            ++_frameRevision;
            if (_swayCoroutine != null) StopCoroutine(_swayCoroutine);
            ShowHeroAgain();
            _inDialogue = true;
            Target = null;
            Partner = null;
            UI.Letterbox.Instance?.Show(null);
            cam.fieldOfView = fovFrom;
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation((lookFrom - from).normalized));
            _swayCoroutine = StartCoroutine(CraneMove(lookFrom, from, fovFrom, lookTo, to, fovTo, hold, seconds, push, reach));
        }

        private IEnumerator CraneMove(Vector3 lookFrom, Vector3 from, float fovFrom,
                                      Vector3 lookTo, Vector3 to, float fovTo,
                                      float hold, float seconds, float push, float reach)
        {
            for (float t = 0f; t < hold && _inDialogue; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0f; t < seconds && _inDialogue; t += Time.unscaledDeltaTime)
            {
                var cam = Cam();
                if (cam == null) yield break;
                float k = Mathf.Clamp01(t / seconds);
                k = k * k * k * (k * (k * 6f - 15f) + 10f);   // мягче smoothstep: кран трогается и встаёт без толчка
                Vector3 pos = Vector3.Lerp(from, to, k);
                Vector3 aim = Vector3.Lerp(lookFrom, lookTo, k);
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation((aim - pos).normalized));
                cam.fieldOfView = Mathf.Lerp(fovFrom, fovTo, k);
                yield return null;
            }
            if (!_inDialogue) yield break;
            var end = Cam();
            if (end != null) end.fieldOfView = fovTo;
            yield return PushTowards(lookTo, to, push, reach);
        }

        /// <summary>
        /// Подъезд к вещи: на пятую часть пути (или на <paramref name="reach"/>), плавно,
        /// и стоять. Общему плану суда пятая часть много: к вердикту крайние судьи
        /// уезжали за край кадра (прогон 06.11).
        /// </summary>
        private IEnumerator PushTowards(Vector3 look, Vector3 from, float seconds, float reach = 0.2f)
        {
            Vector3 to = Vector3.Lerp(from, look, reach);
            float elapsed = 0f;

            while (_inDialogue)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(elapsed / seconds);
                k = k * k * (3f - 2f * k);

                var cam = Cam();
                if (cam == null) yield break;

                Vector3 pos = Vector3.Lerp(from, to, k);
                cam.transform.position = pos;
                cam.transform.rotation = Quaternion.LookRotation((look - pos).normalized);

                yield return null;
            }
        }

        /// <summary>Рост, под который подобран кадр: человек, метр восемьдесят.</summary>
        public const float Reference = 1.8f;

        /// <summary>Рост нынешнего говорящего в долях <see cref="Reference"/>.</summary>
        private float _scale = 1f;

        /// <summary>
        /// Во сколько раз говорящий ниже или выше человека и где у него земля.
        /// Тело — по сетке с костями, как у портрета (Snapshot.Portrait); нет
        /// её — по обычной. Круг выбора (LineRenderer) не тело и не считается.
        /// </summary>
        public static float Scale(Transform target, out float ground)
        {
            ground = target != null ? target.position.y : 0f;
            if (target == null) return 1f;

            Renderer body = target.GetComponentInChildren<SkinnedMeshRenderer>();
            if (body == null) body = target.GetComponentInChildren<MeshRenderer>();
            if (body == null) return 1f;

            var bounds = body.bounds;
            if (bounds.size.y < 0.3f) return 1f;

            ground = bounds.min.y;
            return Mathf.Clamp(bounds.size.y / Reference, 0.5f, 1.6f);
        }

        /// <summary>Направление по земле: наклон говорящего кадру не нужен.</summary>
        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
        }

        /// <summary>
        /// Взгляд на лицо — или правее него на <see cref="_menuRoom"/> полуширины кадра:
        /// тогда говорящий стоит левее середины, а справа — место под ответы.
        /// </summary>
        private Quaternion Aim(Vector3 from, Vector3 look)
        {
            var dir = look - from;
            var cam = Cam();
            if (_menuRoom > 0f && cam != null && dir.sqrMagnitude > 0.0001f)
            {
                var right = Vector3.Cross(Vector3.up, Flat(dir));
                float half = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f) * cam.aspect;
                dir += right * (dir.magnitude * half * _menuRoom);
            }
            return Quaternion.LookRotation(dir.normalized);
        }

        /// <summary>Где стоит камера при данном отдалении и сдвиге вбок.</summary>
        private Vector3 Frame(Vector3 anchor, Vector3 face, Vector3 side,
                              float distance, float sway)
        {
            return anchor
                 + face * distance
                 + side * ((_sideOffset + sway) * _scale)
                 + Vector3.up * (_cameraHeight * _scale);
        }

        /// <summary>Объём у камеры включает ближнюю плоскость, а не только её центр.</summary>
        private float FrameRadius()
        {
            var cam = Cam();
            float halfHeight = cam.nearClipPlane * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float halfWidth = halfHeight * cam.aspect;
            return Mathf.Max(0.12f, Mathf.Sqrt(cam.nearClipPlane * cam.nearClipPlane
                + halfHeight * halfHeight + halfWidth * halfWidth) + 0.03f);
        }

        /// <summary>Реплика продолжается, даже если рядом с говорящим нет места для камеры.</summary>
        private void Overview()
        {
            ShowHeroAgain();
            var cam = Cam();
            if (cam == null) return;
            cam.transform.SetPositionAndRotation(_originalPosition, _originalRotation);
            cam.fieldOfView = _originalFOV;
        }

        private static bool IgnoreInFrame(Collider obstacle, Transform speaker, Transform listener)
        {
            if (obstacle == null) return true;
            if (speaker != null && obstacle.transform.IsChildOf(speaker)) return true;
            if (listener != null && obstacle.transform.IsChildOf(listener)) return true;
            // Тело Греховода при необходимости скрывает HideHeroIfInFrame.
            return Gameplay.SinbinderPlayer.Exists
                && obstacle.transform.IsChildOf(Gameplay.SinbinderPlayer.Instance.transform);
        }

        /// <summary>Свободны и сама камера, и отрезок до лица либо следующей позиции.</summary>
        private bool ClearFrame(Vector3 from, Vector3 to, Transform speaker, Transform listener = null)
        {
            float radius = FrameRadius();
            int overlaps = Physics.OverlapSphereNonAlloc(from, radius, _frameOverlaps,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (overlaps == _frameOverlaps.Length) return false;
            for (int i = 0; i < overlaps; i++)
                if (!IgnoreInFrame(_frameOverlaps[i], speaker, listener)) return false;

            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.001f) return true;
            int hits = Physics.SphereCastNonAlloc(from, radius, delta / length, _frameHits,
                length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (hits == _frameHits.Length) return false;
            for (int i = 0; i < hits; i++)
                if (!IgnoreInFrame(_frameHits[i].collider, speaker, listener)) return false;
            return !ThroughTent(from, to, speaker, listener) && !BodyInTheWay(from, to, speaker, listener);
        }

        private static readonly System.Collections.Generic.List<(Transform root, Bounds body)> Bodies = new();
        private static int _bodiesFrame = -1;

        /// <summary>
        /// Чужое тело между камерой и лицом. Модели приходят без коллайдеров, а капсула
        /// WarriorRig лучом ловится не всегда: в w0511a наезд на Ждана снял вплотную
        /// затылок Брата Хальда. Тело — сердцевина рамки его сетки, раз в кадр на всех.
        /// Греховода не считаем: его на время плана прячет HideHeroIfInFrame.
        /// </summary>
        private static bool BodyInTheWay(Vector3 from, Vector3 to, Transform speaker, Transform listener)
        {
            RefreshBodies();

            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.5f) return false;
            var ray = new Ray(from, delta / length);
            foreach (var (root, body) in Bodies)
            {
                if (root == null || Mine(root, speaker) || Mine(root, listener)) continue;
                if (body.Contains(from)) return true;
                if (body.IntersectRay(ray, out float hit) && hit < length - 0.4f) return true;
            }
            return false;
        }

        /// <summary>Тела воинов — раз в кадр на всех (см. <see cref="BodyInTheWay"/>).</summary>
        private static void RefreshBodies()
        {
            if (_bodiesFrame != Time.frameCount)
            {
                _bodiesFrame = Time.frameCount;
                Bodies.Clear();
                var fight = Gameplay.CombatManager.Instance;
                if (fight != null)
                    foreach (var w in fight.GetAllWarriors())
                    {
                        if (w == null || w is Gameplay.SinbinderPlayer) continue;
                        var mesh = w.GetComponentInChildren<SkinnedMeshRenderer>();
                        if (mesh == null || !mesh.enabled) continue;
                        var b = mesh.bounds;
                        // Рамка скелетной сетки щедрая — руки, оружие, плащ. Берём сердцевину.
                        b.size = new Vector3(b.size.x * 0.6f, b.size.y * 0.95f, b.size.z * 0.6f);
                        Bodies.Add((w.transform, b));
                    }
            }
        }

        /// <summary>
        /// Палатка — ткань без коллайдера (<see cref="Gameplay.TentInterior"/>): луч её не видит,
        /// и камера ставила между собой и лицом скат «Палатки 3» (проход w0511a, разговор
        /// о долге с Маргой). Отрезок проверяем по объёму палаток, как DemoWalkthrough.
        /// Палатку, где стоит сам говорящий или слушающий, не считаем.
        /// </summary>
        private static bool ThroughTent(Vector3 from, Vector3 to, Transform speaker, Transform listener)
        {
            // Своя палатка — только у того, к чьему лицу отрезок. Отрезок к Греховоду, а палатку
            // Марги прощали: она стоит в ней — значит, и ткань между камерой и Греховодом
            // «своя». Сторож прогона прощает лишь палатку того, кто говорит (w0711l, 373 с).
            var whose = speaker;
            if (listener != null && (speaker == null
                || Flat(listener.position - to).sqrMagnitude < Flat(speaker.position - to).sqrMagnitude))
                whose = listener;
            var own = whose != null ? Gameplay.TentInterior.Around(whose.position + Vector3.up * 0.1f) : null;
            for (int i = 0; i <= 24; i++)
            {
                var p = Vector3.Lerp(from, to, i / 24f);
                if (Vector3.Distance(p, to) < 0.4f) break;
                var tent = Gameplay.TentInterior.Around(p);
                if (tent != null && tent != own) return true;
            }
            return false;
        }

        /// <summary>
        /// За спиной говорящего, ближе двух с половиной метров, — палатка или стена: фон крупного
        /// плана — плоская ткань во весь кадр (КТ1, кадр «Вызов»: бежевый скат палатки на две трети
        /// кадра за Карганом). Такой план берём, только если открытого без стены нет.
        /// Чужое тело за спиной стеной не считаем — оно не плоское и уходит.
        /// </summary>
        private static bool WallBehind(Vector3 from, Vector3 look, Transform speaker)
        {
            var dir = Flat(look - from);
            if (dir.sqrMagnitude < 0.0001f) return false;
            dir.Normalize();
            var own = speaker != null ? Gameplay.TentInterior.Around(speaker.position + Vector3.up * 0.1f) : null;
            for (float d = 0.7f; d <= 2.5f; d += 0.45f)
            {
                var tent = Gameplay.TentInterior.Around(look + dir * d);
                if (tent != null && tent != own) return true;
            }
            if (Physics.Raycast(look, dir, out var hit, 2.5f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<Gameplay.Warrior>() == null
                && (speaker == null || !hit.transform.IsChildOf(speaker)))
                return true;
            return false;
        }

        /// <summary>
        /// Держим выбранную сторону, пока она свободна. При помехе ищем другой ракурс,
        /// затем более близкий план. Порядок фиксирован; стены и палатки не прячем.
        /// </summary>
        private bool TrySpeakerFrame(Vector3 anchor, Vector3 face, Vector3 side, Vector3 look,
                                     float distance, float sway, Transform speaker, out Vector3 position,
                                     bool fresh = false)
        {
            var turn = Quaternion.AngleAxis(_frameYaw, Vector3.up);
            position = Frame(anchor, turn * face, turn * side, distance * _frameReach, sway);
            if (ClearFrame(position, look, speaker)
                && (!fresh || (Shut(position, look, speaker, null, Vector3.Distance(position, look) - 0.5f) <= OpenEnough
                               && !WallBehind(position, look, speaker))))
                return true;

            // При выборе плана (fresh) кадр ещё и не должен быть закрыт вблизи; открытого
            // нет — берём наименее закрытый: без плана реплика осталась бы без лица.
            float least = float.MaxValue, leastYaw = 0f, leastReach = 1f;
            Vector3 leastAt = Vector3.zero;
            // Открытый, но со стеной за спиной — запасной: берём, если чистого нет.
            bool walled = false;
            float walledYaw = 0f, walledReach = 1f;
            Vector3 walledAt = Vector3.zero;
            for (int near = 0; near < 3; near++)
            {
                float reach = 1f - near * 0.2f;
                foreach (float angle in GazeAngles)
                {
                    turn = Quaternion.AngleAxis(angle, Vector3.up);
                    var candidate = Frame(anchor, turn * face, turn * side, distance * reach, sway);
                    if (!ClearFrame(candidate, look, speaker)) continue;
                    if (fresh)
                    {
                        float shut = Shut(candidate, look, speaker, null, Vector3.Distance(candidate, look) - 0.5f);
                        if (shut > OpenEnough)
                        {
                            if (shut < least) { least = shut; leastYaw = angle; leastReach = reach; leastAt = candidate; }
                            continue;
                        }
                        if (WallBehind(candidate, look, speaker))
                        {
                            if (!walled) { walled = true; walledYaw = angle; walledReach = reach; walledAt = candidate; }
                            continue;
                        }
                    }
                    _frameYaw = angle;
                    _frameReach = reach;
                    position = candidate;
                    return true;
                }
            }
            if (walled)
            {
                Debug.Log("[КАМЕРА] Крупный план: за спиной у всех открытых планов стена или палатка — взят первый.");
                _frameYaw = walledYaw;
                _frameReach = walledReach;
                position = walledAt;
                return true;
            }
            if (least <= 1f)
            {
                Debug.Log($"[КАМЕРА] Крупный план: открытого нет, взят закрытый на {least:P0}.");
                _frameYaw = leastYaw;
                _frameReach = leastReach;
                position = leastAt;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Медленный наезд на верх туловища, с еле заметной качкой.
        ///
        /// Наезд, а не стояние: план, который не движется, читается как
        /// пауза в игре, а план, который подъезжает, — как то, что сейчас
        /// скажут важное. Это и есть весь приём.
        ///
        /// Сторона качки берётся из положения говорящего, а не жребием:
        /// правило проекта — одинаковый вход даёт одинаковый выход,
        /// и кадр под него подпадает так же, как решение.
        /// </summary>
        private IEnumerator PushIn(Vector3 anchor, Vector3 face, Vector3 side,
                                   Vector3 lookTarget)
        {
            float t = 0f;
            float elapsed = 0f;
            float direction = anchor.x + anchor.z >= 0f ? 1f : -1f;

            while (_inDialogue)
            {
                elapsed += Time.unscaledDeltaTime;
                t += _swaySpeed * Time.unscaledDeltaTime;

                float k = _pushSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / _pushSeconds);

                // Плавно на входе и на выходе: равномерный наезд заметен
                // как движение техники, сглаженный — как внимание.
                k = k * k * (3f - 2f * k);

                float distance = Mathf.Lerp(_cameraDistance, _cameraDistance - _pushIn, k) * _scale * _distanceMultiplier;
                float sway = Mathf.Sin(t) * _swayAmount * direction;

                // Камера может уйти посреди плана вместе со сценой.
                var cam = Cam();
                if (cam == null) yield break;

                // Говорящий жив — кадр за ним: сдвинулся — план следом (автор, 29.10: при «Вызове» Каргана
                // план стоял там, где говорящий был в начале). Лицом — к собеседнику, а не к камере
                // (автор, 07.11); держим и его: Хорь при разговоре с ответами отворачивался (29.10).
                if (Target != null)
                {
                    var now = new Vector3(Target.position.x, anchor.y, Target.position.z);
                    var moved = now - anchor;
                    if (moved.sqrMagnitude > 0.0004f) { anchor = now; lookTarget += moved; }
                    var want = _gaze;
                    if (_listener != null)
                    {
                        var to = _listener.position - Target.position; to.y = 0f;
                        if (to.sqrMagnitude > 0.04f) want = to.normalized;
                    }
                    Target.rotation = Quaternion.Slerp(Target.rotation, Quaternion.LookRotation(want),
                                                       1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
                }

                if (!TrySpeakerFrame(anchor, face, side, lookTarget, distance, sway, Target,
                                     out Vector3 pos))
                {
                    // Новая помеха перекрыла все планы — возвращаем обзор.
                    Overview();
                    yield break;
                }
                HideHeroIfInFrame(pos, lookTarget, Target);
                cam.transform.position = pos;
                cam.transform.rotation = Aim(pos, lookTarget);

                yield return null;
            }
        }

        public void StopSway()
        {
            _frameRevision++;
            _inDialogue = false;
            if (_swayCoroutine != null)
            {
                StopCoroutine(_swayCoroutine);
                _swayCoroutine = null;
            }
        }

        /// <summary>Номер кадра, в котором камера едет домой; −1 — не едет (см. SaveCameraPosition).</summary>
        private int _homebound = -1;

        public IEnumerator RestoreCamera()
        {
            _homebound = ++_frameRevision;
            _inDialogue = false;
            Target = null;
            Partner = null;
            _pairFirst = _pairSecond = null;
            ShowHeroAgain();
            if (_swayCoroutine != null)
            {
                StopCoroutine(_swayCoroutine);
                _swayCoroutine = null;
            }

            // Полосы уходят вместе с кадром. Опускаются они за свои
            // три десятых секунды, а камера едет своим ходом — ждать
            // друг друга им незачем, и ожидание было бы видно паузой.
            UI.Letterbox.Instance?.Hide();

            // Возвращать некого: камера ушла вместе со сценой.
            if (Cam() == null) { _homebound = -1; yield break; }

            Cam().fieldOfView = _originalFOV;
            yield return MoveCamera(_originalPosition, _originalRotation, avoidObstacles: true);
            if (_homebound == _frameRevision) _homebound = -1;
        }

        private IEnumerator MoveCamera(Vector3 targetPos, Quaternion targetRot,
                                       Transform speaker = null, Vector3? look = null,
                                       bool avoidObstacles = false, Transform listener = null)
        {
            int revision = _frameRevision;
            float duration = 1f / _transitionSpeed;
            float elapsed = 0f;

            var cam = Cam();
            if (cam == null) yield break;

            Vector3 startPos = cam.transform.position;
            Quaternion startRot = cam.transform.rotation;

            // Перебивка через стену — сразу на проверенный план. Плавный перелёт
            // разрешён лишь там, где помещается ближняя плоскость камеры.
            if ((speaker != null || avoidObstacles) && !ClearFrame(startPos, targetPos, speaker, listener))
            {
                cam.transform.SetPositionAndRotation(targetPos, targetRot);
                yield break;
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                t = t * t * (3f - 2f * t);

                // Сцена может уйти посреди переезда — и камера с ней.
                if (cam == null || revision != _frameRevision) yield break;

                Vector3 pos = Vector3.Lerp(startPos, targetPos, t);
                if (look.HasValue && !ClearFrame(pos, look.Value, speaker, listener))
                {
                    cam.transform.SetPositionAndRotation(targetPos, targetRot);
                    yield break;
                }
                cam.transform.position = pos;
                cam.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            if (cam == null || revision != _frameRevision) yield break;
            cam.transform.position = targetPos;
            cam.transform.rotation = targetRot;
        }
    }
}
