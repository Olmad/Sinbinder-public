// Assets/Scripts/Audio/Music.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sinbinder.Audio
{
    /// <summary>Что звучит: место пролога, а не имя файла.</summary>
    /// <remarks>Новое место — в конец: номера прежних не сдвигаются.</remarks>
    public enum Track { None, Camp, Raid, Crypt, Epilogue, Hunted, Menu }

    /// <summary>
    /// Короткий сигнал поверх темы: событие, а не место. Из архива автора:
    /// «Spark of Eternity» («Новая душа») — воин встал в мастерской склепа;
    /// «Whisper of the Fallen» («Смерть союзника») — Карган пал, прикрывая отход.
    /// </summary>
    public enum Cue { Spark, Fallen }

    /// <summary>
    /// Музыка пролога (курс п. 3, docs/41-SHOWCASE.md: «музыка лагеря
    /// и набега»). Автор, 2 октября: «думаю ты можешь начать интегрировать
    /// эти мелодии». Темы — его, версии «по задумке»; ноты, отрисовка
    /// и замеры — docs/43-SOUND.md §4, скрипты — <c>Tools/music</c>.
    ///
    /// | Где               | Тема                          | Файл в Resources/Music |
    /// |-------------------|-------------------------------|------------------------|
    /// | дверь в игру      | «Крысы»: колыбельная          | Lullaby                |
    /// | лагерь            | «Крысы»: песня, добыча — по очереди | RatSong, RatLoot |
    /// | набег             | Шуганули                      | Scatter                |
    /// | вторая волна      | The Hunted                    | Hunted                 |
    /// | склеп             | Bad is good, but Evil is better | BadIsGood            |
    /// | конец демо        | A Lord Returns                | LordReturns            |
    ///
    /// 23 октября — решения автора: «Крысы» — тема отряда Безымянного; колыбельная
    /// сюиты — в главное меню; «для лагеря хороша 6 или 7» — звучат по очереди,
    /// чтобы за десять минут лагеря ни одна не стала навязчивой; «под бой мне
    /// нравится черновой „Шуганули“». Прежние записи — `Docs/Музыка`.
    ///
    /// Темы одним файлом — петля без шва: отрисован круг дважды и взят второй
    /// проход. Темы несколькими файлами (лагерь) — пьесы с началом и концом,
    /// играют одна за другой по кругу.
    ///
    /// Поворотные места пролога говорят сами: тревога шара гасит лагерь —
    /// бьёт один колокол (<see cref="Gameplay.CrystalBall.Alarm"/>); набег
    /// включает «Шуганули» под строкой на чёрном (<see cref="Gameplay.RaidEvent"/>);
    /// вторая волна, выходя, меняет его на «The Hunted» — бой кончился,
    /// начинается бегство (<see cref="Gameplay.HunterSquadSpawner"/>);
    /// уход из доли гасит, пока длится задержка (<see cref="Gameplay.PrologueDirector"/>);
    /// экран конца — «Возвращение лорда», «Отряд не вернулся» — тишина.
    /// Тишину первого отказа делать не нужно: <see cref="UI.RefusalSilence"/>
    /// глушит весь звук, музыку тоже.
    ///
    /// Под репликами в кадре кино (<see cref="UI.Letterbox"/>) музыка
    /// приседает: голос Греховода и слова важнее. Облачка лагеря её
    /// не трогают — они идут всё время, и музыка дышала бы, как насос.
    ///
    /// Ставит себя сам и живёт между сценами, как <see cref="Sfx"/>.
    /// Нет файла — молчит и говорит об этом один раз.
    /// </summary>
    public class Music : MonoBehaviour
    {
        /// <summary>
        /// Громкость темы по месту: музыка — под миром, а не над ним.
        /// Лагерь тише всех — там читают облачка; набег громче — там удары
        /// и так громкие; конец демо — громче всех: больше ничего не звучит.
        /// Сверено с удар-звуками (0,55) и голосом: музыка ниже обоих.
        /// </summary>
        private static float LevelOf(Track track)
        {
            switch (track)
            {
                // Громкости новых тем 23 октября — по слуху, взвешенному A (43-SOUND.md):
                // лагерь — как прежний «Daydream» при 0,28 (−35,7 дБ(A)); песня
                // выровнена с добычей в самом файле. Набег — между приглушённым
                // «Grind» (−39,9) и маршем (−35,5): −36,3. Дверь — колыбельная, −34:
                // тише эпилога, громче лагеря — кроме неё ничего не звучит.
                case Track.Menu:     return 0.09f;
                case Track.Camp:     return 0.11f;
                case Track.Raid:     return 0.10f;
                case Track.Hunted:   return 0.38f;
                case Track.Crypt:    return 0.30f;
                case Track.Epilogue: return 0.50f;
                default:             return 0f;
            }
        }

        /// <summary>
        /// Записи темы в <c>Assets/Resources/Music</c>, по порядку. Одна — петля;
        /// несколько — играют одна за другой по кругу. Сменить тему месту —
        /// сменить строку здесь.
        /// </summary>
        public static string[] FilesOf(Track track)
        {
            switch (track)
            {
                case Track.Menu:     return new[] { "Lullaby" };
                case Track.Camp:     return new[] { "RatSong", "RatLoot" };
                case Track.Raid:     return new[] { "Scatter" };
                case Track.Hunted:   return new[] { "Hunted" };
                case Track.Crypt:    return new[] { "BadIsGood" };
                case Track.Epilogue: return new[] { "LordReturns" };
                default:             return null;
            }
        }

        /// <summary>Первая запись темы — та, с которой она начинается.</summary>
        public static string FileOf(Track track)
        {
            var files = FilesOf(track);
            return files != null && files.Length > 0 ? files[0] : null;
        }

        /// <summary>
        /// Тема, с которой сцена начинается. Набег своей сцены не имеет —
        /// его включает <see cref="Gameplay.RaidEvent"/> в лагере. Полигон
        /// склепа звучит как склеп. Прочие сцены (тестовые) — молча.
        /// </summary>
        public static Track ForScene(string scene)
        {
            switch (scene)
            {
                case "Prologue_Camp":  return Track.Camp;
                case "Crypt_Entrance": return Track.Crypt;
                case "Crypt_Test":     return Track.Crypt;
                default:               return Track.None;
            }
        }

        /// <summary>Файл сигнала в <c>Assets/Resources/Music</c>.</summary>
        public static string FileOf(Cue cue) => cue == Cue.Spark ? "Spark" : "Fallen";

        /// <summary>Громкость сигнала: громче темы — это миг, — но ниже ударов.</summary>
        private const float CueLevel = 0.5f;

        /// <summary>Во сколько раз тише тема, пока звучит сигнал.</summary>
        private const float UnderCue = 0.3f;

        /// <summary>Во сколько раз тише, пока кадр в рамке кино.</summary>
        private const float Duck = 0.45f;

        /// <summary>Музыка почти молчит, пока говорит Смерть (<see cref="DeathVoice"/>).</summary>
        public static bool Silenced { get; set; }

        /// <summary>Как быстро музыка приседает и встаёт, долей громкости в секунду.</summary>
        private const float DuckSpeed = 1.5f;

        private static Music _instance;
        private static readonly Dictionary<string, AudioClip> Clips = new();
        private static readonly HashSet<string> Told = new();

        /// <summary>Что звучит сейчас (или затихает к тишине — тогда None).</summary>
        public static Track Current { get; private set; }

        private readonly AudioSource[] _decks = new AudioSource[2];
        private readonly float[] _level = new float[2];     // 0…1 — сколько от громкости темы
        private readonly float[] _target = new float[2];
        private readonly float[] _speed = new float[2];
        private readonly Track[] _on = new Track[2];
        private readonly int[] _piece = new int[2];         // которая запись темы звучит на дорожке
        private float _duck = 1f;
        private AudioSource _cue;
        private float _cueUntil;
        private float _under = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm()
        {
            _instance = null;
            Clips.Clear();
            Told.Clear();
            Current = Track.None;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("Музыка");
            _instance = go.AddComponent<Music>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            for (int i = 0; i < _decks.Length; i++)
            {
                var child = new GameObject(i == 0 ? "Дорожка А" : "Дорожка Б");
                child.transform.SetParent(transform, false);
                var src = child.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                src.spatialBlend = 0f;           // музыка — не из места, а для игрока
                src.dopplerLevel = 0f;
                src.volume = 0f;
                _decks[i] = src;
            }

            var cue = new GameObject("Сигнал");
            cue.transform.SetParent(transform, false);
            _cue = cue.AddComponent<AudioSource>();
            _cue.playOnAwake = false;
            _cue.spatialBlend = 0f;
            _cue.dopplerLevel = 0f;

            SceneManager.sceneLoaded += OnScene;
            Play(ForScene(SceneManager.GetActiveScene().name), 2f);
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnScene;
            if (_instance == this) _instance = null;
        }

        private void OnScene(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Play(ForScene(scene.name), 3f);
        }

        // ---------- что звучит ----------

        /// <summary>
        /// Перейти к теме за <paramref name="fade"/> секунд: старая затихает,
        /// новая встаёт, обе разом. Та же тема — ничего не делать: она
        /// не начинается заново посреди фразы.
        /// </summary>
        public static void Play(Track track, float fade = 3f)
        {
            if (_instance == null || track == Current) return;
            _instance.Switch(track, Mathf.Max(0.05f, fade));
        }

        /// <summary>Затихнуть к тишине за <paramref name="fade"/> секунд.</summary>
        public static void Stop(float fade = 2f) => Play(Track.None, fade);

        /// <summary>
        /// Сыграть сигнал поверх темы. Тема на это время приседает
        /// (<see cref="UnderCue"/>) и встаёт, когда сигнал кончился. Новый
        /// сигнал обрывает прежний: два разом — каша.
        /// </summary>
        public static void Play(Cue cue)
        {
            if (_instance == null) return;
            var clip = Load(FileOf(cue));
            if (clip == null) return;

            var src = _instance._cue;
            src.Stop();
            src.clip = clip;
            src.volume = CueLevel * Core.Preferences.MusicScale;
            src.Play();
            _instance._cueUntil = Time.unscaledTime + clip.length;
            Debug.Log($"[МУЗЫКА] Сигнал {FileOf(cue)}.");
        }

        private void Switch(Track track, float fade)
        {
            Current = track;

            // Звучащее — затихает.
            for (int i = 0; i < _decks.Length; i++)
                Aim(i, 0f, fade);

            if (track == Track.None)
            {
                Debug.Log("[МУЗЫКА] Тишина.");
                return;
            }

            var clip = Load(FileOf(track));
            if (clip == null) return;

            // Новая — на ту дорожку, что тише: перехода не слышно, даже если
            // прошлый ещё не кончился.
            int back = _level[0] <= _level[1] ? 0 : 1;
            var deck = _decks[back];
            deck.Stop();
            deck.clip = clip;
            deck.time = 0f;
            deck.loop = FilesOf(track).Length == 1;   // несколько пьес — не по кругу каждая, а одна за другой
            deck.Play();
            _on[back] = track;
            _piece[back] = 0;
            _level[back] = 0f;
            Aim(back, 1f, fade);

            Debug.Log($"[МУЗЫКА] {FileOf(track)}.");
        }

        private void Aim(int deck, float to, float fade)
        {
            _target[deck] = to;
            _speed[deck] = 1f / fade;
        }

        void Update()
        {
            // Реальное время: пауза мира, урок стоп-кадром и разговор
            // останавливают игровое, а переход обязан доиграть.
            float dt = Time.unscaledDeltaTime;

            bool framed = UI.Letterbox.Instance != null && UI.Letterbox.Instance.Shown;
            // Говорит Смерть (пешка, DeathVoice) — музыка уходит совсем:
            // громкость ставится здесь каждый кадр, и чужая правка её не держала
            // (автор, 21.10: «пешка не заглушает музыку во время боя»).
            float want = Silenced ? 0.03f : framed ? Duck : 1f;
            _duck = Mathf.MoveTowards(_duck, want, DuckSpeed * dt);

            bool cueing = Time.unscaledTime < _cueUntil;
            _under = Mathf.MoveTowards(_under, cueing ? UnderCue : 1f, DuckSpeed * dt);

            float pref = Core.Preferences.MusicScale;
            _cue.volume = CueLevel * pref;
            for (int i = 0; i < _decks.Length; i++)
            {
                _level[i] = Mathf.MoveTowards(_level[i], _target[i], _speed[i] * dt);
                var deck = _decks[i];
                deck.volume = _level[i] * LevelOf(_on[i]) * _duck * _under * pref;

                if (_level[i] <= 0f && _target[i] <= 0f && deck.isPlaying)
                {
                    deck.Stop();
                    _on[i] = Track.None;
                }

                // Пьеса кончилась — следующая той же темы (лагерь: песня, добыча, песня…).
                if (!deck.loop && !deck.isPlaying && _target[i] > 0f && _on[i] != Track.None)
                    Next(i);
            }

            Door();
        }

        private void Next(int i)
        {
            var files = FilesOf(_on[i]);
            if (files == null || files.Length == 0) return;
            _piece[i] = (_piece[i] + 1) % files.Length;
            var clip = Load(files[_piece[i]]);
            if (clip == null) return;
            var deck = _decks[i];
            deck.clip = clip;
            deck.time = 0f;
            deck.Play();
            Debug.Log($"[МУЗЫКА] {files[_piece[i]]}.");
        }

        /// <summary>
        /// Дверь в игру (<see cref="UI.StartPanel"/>) — главное меню демо: пока
        /// на ней вопрос, звучит колыбельная «Крыс» (автор, 23.10: «я бы её
        /// в главное меню впихнул»); ответили — тема сцены.
        /// </summary>
        private void Door()
        {
            bool door = UI.StartPanel.Waiting;
            if (door && Current != Track.Menu) Play(Track.Menu, 1.5f);
            else if (!door && Current == Track.Menu) Play(ForScene(SceneManager.GetActiveScene().name), 3f);
        }

        /// <summary>Запись темы; нет — сказать один раз, где положить.</summary>
        private static AudioClip Load(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            if (Clips.TryGetValue(file, out var ready)) return ready;

            var clip = Resources.Load<AudioClip>("Music/" + file);
            if (clip == null && Told.Add(file))
                Debug.LogWarning($"[МУЗЫКА] Нет записи «{file}» в Assets/Resources/Music — звучит тишина.");

            Clips[file] = clip;
            return clip;
        }
    }
}
