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

        public IEnumerator FocusOn(Transform target, string caption = null, float distanceMultiplier = 1f)
        {
            if (target == null) yield break;

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
            Vector3 face = Flat(target.forward);
            Vector3 side = Flat(target.right);

            Vector3 lookTarget = anchor + Vector3.up * (_lookHeight * _scale);
            if (!TrySpeakerFrame(anchor, face, side, lookTarget, _cameraDistance * _scale * _distanceMultiplier, 0f,
                                 target, out Vector3 baseCamPos))
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

            yield return MoveCamera(baseCamPos,
                Quaternion.LookRotation((lookTarget - baseCamPos).normalized), target, lookTarget);

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

            if (!TryPairFrame(speaker, listener, menu, out var from, out var look))
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

        private bool TryPairFrame(Transform speaker, Transform listener, bool menu,
                                  out Vector3 from, out Vector3 look)
        {
            var a = ConversationBody(speaker);
            var b = ConversationBody(listener);
            var cam = Cam();
            float bias = speaker == _pairFirst ? -12f : 12f;
            foreach (float angle in FrameAngles)
            {
                var side = Quaternion.AngleAxis(bias + angle, Vector3.up) * _pairSide;
                ComposePair(a, b, side, _dialogueFOV, cam.aspect, menu, out from, out look);
                if (!ClearFrame(from, a.center + Vector3.up * (a.extents.y * 0.6f), speaker, listener)
                    || !ClearFrame(from, b.center + Vector3.up * (b.extents.y * 0.6f), speaker, listener)) continue;
                _pairSide = Quaternion.AngleAxis(angle, Vector3.up) * _pairSide;
                return true;
            }
            from = look = Vector3.zero;
            return false;
        }

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
                                        float push = 0f)
        {
            if (Cam() == null) yield break;

            int revision = ++_frameRevision;
            if (_swayCoroutine != null) StopCoroutine(_swayCoroutine);
            _swayCoroutine = null;
            ShowHeroAgain();
            _inDialogue = true;
            Target = null;   // точка, а не говорящий
            Partner = null;
            Cam().fieldOfView = _dialogueFOV;
            UI.Letterbox.Instance?.Show(caption);

            yield return MoveCamera(from, Quaternion.LookRotation((look - from).normalized));

            if (!_inDialogue || revision != _frameRevision) yield break;
            _swayCoroutine = StartCoroutine(PushTowards(look, from,
                push > 0f ? push : _pushSeconds));
        }

        /// <summary>Подъезд к вещи: на пятую часть пути, плавно, и стоять.</summary>
        private IEnumerator PushTowards(Vector3 look, Vector3 from, float seconds)
        {
            Vector3 to = Vector3.Lerp(from, look, 0.2f);
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

            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.5f) return false;
            var ray = new Ray(from, delta / length);
            foreach (var (root, body) in Bodies)
            {
                if (root == null) continue;
                if ((speaker != null && (root == speaker || root.IsChildOf(speaker) || speaker.IsChildOf(root)))
                    || (listener != null && (root == listener || root.IsChildOf(listener) || listener.IsChildOf(root))))
                    continue;
                if (body.Contains(from)) return true;
                if (body.IntersectRay(ray, out float hit) && hit < length - 0.4f) return true;
            }
            return false;
        }

        /// <summary>
        /// Палатка — ткань без коллайдера (<see cref="Gameplay.TentInterior"/>): луч её не видит,
        /// и камера ставила между собой и лицом скат «Палатки 3» (проход w0511a, разговор
        /// о долге с Маргой). Отрезок проверяем по объёму палаток, как DemoWalkthrough.
        /// Палатку, где стоит сам говорящий или слушающий, не считаем.
        /// </summary>
        private static bool ThroughTent(Vector3 from, Vector3 to, Transform speaker, Transform listener)
        {
            var own = speaker != null ? Gameplay.TentInterior.Around(speaker.position + Vector3.up * 0.1f) : null;
            var theirs = listener != null ? Gameplay.TentInterior.Around(listener.position + Vector3.up * 0.1f) : null;
            for (int i = 0; i <= 24; i++)
            {
                var p = Vector3.Lerp(from, to, i / 24f);
                if (Vector3.Distance(p, to) < 0.4f) break;
                var tent = Gameplay.TentInterior.Around(p);
                if (tent != null && tent != own && tent != theirs) return true;
            }
            return false;
        }

        /// <summary>
        /// Держим выбранную сторону, пока она свободна. При помехе ищем другой ракурс,
        /// затем более близкий план. Порядок фиксирован; стены и палатки не прячем.
        /// </summary>
        private bool TrySpeakerFrame(Vector3 anchor, Vector3 face, Vector3 side, Vector3 look,
                                     float distance, float sway, Transform speaker, out Vector3 position)
        {
            var turn = Quaternion.AngleAxis(_frameYaw, Vector3.up);
            position = Frame(anchor, turn * face, turn * side, distance * _frameReach, sway);
            if (ClearFrame(position, look, speaker)) return true;

            for (int near = 0; near < 3; near++)
            {
                float reach = 1f - near * 0.2f;
                foreach (float angle in FrameAngles)
                {
                    turn = Quaternion.AngleAxis(angle, Vector3.up);
                    var candidate = Frame(anchor, turn * face, turn * side, distance * reach, sway);
                    if (!ClearFrame(candidate, look, speaker)) continue;
                    _frameYaw = angle;
                    _frameReach = reach;
                    position = candidate;
                    return true;
                }
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

                // Говорящий жив — кадр за ним: сдвинулся — план следом; повернулся — лицом к камере (автор, 29.10:
                // Хорь при разговоре с ответами отворачивался, при «Вызове» Каргана камера снимала не то — план
                // стоял там, где говорящий был в начале).
                if (Target != null)
                {
                    var now = new Vector3(Target.position.x, anchor.y, Target.position.z);
                    var moved = now - anchor;
                    if (moved.sqrMagnitude > 0.0004f) { anchor = now; lookTarget += moved; }
                    var toCam = cam.transform.position - Target.position; toCam.y = 0f;
                    if (toCam.sqrMagnitude > 0.01f)
                        Target.rotation = Quaternion.Slerp(Target.rotation, Quaternion.LookRotation(toCam),
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
                cam.transform.rotation = Quaternion.LookRotation((lookTarget - pos).normalized);

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

        public IEnumerator RestoreCamera()
        {
            _frameRevision++;
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
            if (Cam() == null) yield break;

            Cam().fieldOfView = _originalFOV;
            yield return MoveCamera(_originalPosition, _originalRotation, avoidObstacles: true);
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
