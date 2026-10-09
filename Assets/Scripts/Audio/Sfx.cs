// Assets/Scripts/Audio/Sfx.cs
// Перевод: текст через Loc
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.Audio
{
    /// <summary>
    /// Звуки мира: удары, колокол тревоги, монеты, треск огня, шаги
    /// Греховода, ночь лагеря (курс п. 3,
    /// docs/41-SHOWCASE.md). Записи — <c>Assets/Resources/Sounds</c>, все
    /// CC0, откуда каждая — docs/43-SOUND.md. Автор, 2 октября: «можешь
    /// искать и скачивать подходящие звуки».
    ///
    /// Голоса — не здесь: они синтезом (<see cref="VoiceGenerator"/>), это
    /// звуковое лицо игры (00-GDD.md §9). Записи — только миру: огонь
    /// и удар синтезом звучат помехой.
    ///
    /// Ставит себя сам и живёт между сценами. Нет записи — молчит и говорит
    /// об этом один раз. Какой из вариантов удара звучит — по счёту,
    /// а не жребием: одинаковый вход — одинаковый выход.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        /// <summary>Сколько звуков могут звучать разом.</summary>
        private const int Voices = 10;

        /// <summary>Больше ударов за кадр не слышно — только каша.</summary>
        private const int PerFrame = 4;

        /// <summary>
        /// Шаг Греховода, метры. Бежит он 4,2 м/с (<c>SinbinderPlayer.Spawn</c>),
        /// бегущий делает около 2,7 шага в секунду — шаг полтора метра.
        /// </summary>
        private const float Stride = 1.5f;

        /// <summary>Сдвиг больше этого за кадр — его перенесли, а не он прошёл: не шаг.</summary>
        private const float Teleport = 3f;

        /// <summary>
        /// Громкость ночи лагеря. Сверчки — высокий повтор на все десять минут
        /// лагеря: громче — и они станут клавесином «Grind», который «вечно
        /// звучит в ушах» (docs/43-SOUND.md §6). Здесь — воздух, а не звук.
        /// </summary>
        private const float NightVolume = 0.08f;

        private const string CampScene = "Prologue_Camp";

        private static Sfx _instance;
        private static readonly Dictionary<string, AudioClip[]> Clips = new();
        private static readonly HashSet<string> Told = new();
        private static int _turn;

        private readonly List<AudioSource> _pool = new();
        private int _next;
        private int _frame = -1;
        private int _thisFrame;

        private AudioSource _night;
        private Coroutine _nightFade;
        private Vector3 _lastStep;
        private bool _stepArmed;
        private float _walked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm()
        {
            _instance = null;
            Clips.Clear();
            Told.Clear();
            _turn = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("Звуки мира");
            _instance = go.AddComponent<Sfx>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            for (int i = 0; i < Voices; i++)
            {
                var child = new GameObject("Звук");
                child.transform.SetParent(transform, false);
                var src = child.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 6f;
                src.maxDistance = 70f;
                src.dopplerLevel = 0f;
                _pool.Add(src);
            }

            SceneManager.sceneLoaded += OnScene;
            Fires();
            Night(SceneManager.GetActiveScene().name);
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnScene;
            if (_instance == this) _instance = null;
        }

        private void OnScene(Scene scene, LoadSceneMode mode)
        {
            Fires();
            Night(scene.name);
            _stepArmed = false;
        }

        void Update() => Steps();

        // ---------- что звучит ----------

        /// <summary>
        /// Удар по воину — по его телу: кость щёлкает, плоть глохнет, камень
        /// бьётся, бесплотный звенит, живой — глухой удар. Закрылся щитом
        /// в стойке (<see cref="Provocation"/>) — железо.
        /// </summary>
        public static void Hit(Warrior victim, Vector3 where)
        {
            if (_instance == null) return;

            string set;
            if (victim != null && Provocation.StanceBonus(victim) > 0f) set = "Hit/Shield";
            else
            {
                switch (victim != null ? victim.Shell : ShellType.Living)
                {
                    case ShellType.Skeleton: set = "Hit/Bone"; break;
                    case ShellType.Zombie:   set = "Hit/Flesh"; break;
                    case ShellType.Ghost:    set = "Hit/Spirit"; break;
                    case ShellType.Golem:    set = "Hit/Stone"; break;
                    default:                 set = "Hit/Man"; break;
                }
            }

            _instance.Play(Pick(set), where, 0.55f, 0.6f, Pitch());
        }

        /// <summary>Тревога: колокол бьёт трижды. Для игрока, а не для места — без пространства.</summary>
        public static void Bell()
        {
            if (_instance == null) return;
            _instance.StartCoroutine(_instance.Strikes());
        }

        /// <summary>Монеты из рук в руки: плата, отданный долг.</summary>
        public static void Coins(Vector3 where)
        {
            if (_instance == null) return;
            _instance.Play(Pick("Coins"), where, 0.6f, 0.3f, 1f);
        }

        /// <summary>
        /// Ночь замолкает: перед бедой сверчки затихают — мир затаил дыхание,
        /// и колокол тревоги бьёт уже в настоящей тишине. Зовёт
        /// <see cref="CrystalBall"/> на тревоге. До новой загрузки лагеря
        /// ночь не вернётся: после разгрома ей петь не о чем.
        /// </summary>
        public static void Hush(float fade = 1.5f)
        {
            if (_instance == null || _instance._night == null || !_instance._night.isPlaying) return;
            _instance.FadeNight(0f, fade);
        }

        private IEnumerator Strikes()
        {
            var bell = Load("Bell/Strike");
            for (int i = 0; i < bell.Length && i < 3; i++)
            {
                Play(bell[i], Vector3.zero, 0.55f, 0f, 1f);
                yield return new WaitForSecondsRealtime(1.1f);
            }
        }

        /// <summary>
        /// Треск у каждого живого огня (<see cref="FireFlicker"/>): громче
        /// у яркого костра, тише у фонаря. Петля одна — начало у каждого
        /// огня своё, от места, где он стоит: два костра не трещат в такт.
        /// </summary>
        private void Fires()
        {
            var fire = Load("Fire");
            if (fire.Length == 0) return;

            foreach (var f in Object.FindObjectsByType<FireFlicker>(FindObjectsSortMode.InstanceID))
            {
                if (f == null || f.transform.Find("Треск") != null) continue;

                var light = f.GetComponent<Light>();
                float bright = light != null ? light.intensity : 1f;

                var go = new GameObject("Треск");
                go.transform.SetParent(f.transform, false);
                var src = go.AddComponent<AudioSource>();
                src.clip = fire[0];
                src.loop = true;
                src.playOnAwake = false;
                src.spatialBlend = 0.8f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 4f;
                src.maxDistance = 45f;
                src.dopplerLevel = 0f;
                src.volume = Mathf.Clamp(bright / 6f, 0.12f, 0.6f);

                var p = f.transform.position;
                src.time = Mathf.Repeat(p.x * 3.7f + p.z * 5.3f, fire[0].length * 0.9f);
                src.Play();
            }
        }

        /// <summary>
        /// Шаги Греховода — только его: он тело, которым ходит игрок
        /// (00-GDD.md §4, «тело — микро»). Шаги всего отряда и охотников
        /// слились бы в кашу под голосами. Считаются по пройденному пути,
        /// а не по анимации: шаг — каждые <see cref="Stride"/> метров, идёт
        /// медленнее — шагает реже. Трава в лагере, камень в склепе.
        /// </summary>
        private void Steps()
        {
            var me = SinbinderPlayer.Instance;
            if (me == null || me.IsDead) { _stepArmed = false; return; }

            var p = me.transform.position;
            p.y = 0f;
            if (!_stepArmed) { _lastStep = p; _walked = 0f; _stepArmed = true; return; }

            float d = Vector3.Distance(p, _lastStep);
            _lastStep = p;
            if (d > Teleport) { _walked = 0f; return; }

            _walked += d;
            if (_walked < Stride) return;
            _walked -= Stride;

            string set = Surface(SceneManager.GetActiveScene().name);
            if (set == null) return;
            Play(Pick(set), me.transform.position, set == "Step/Grass" ? 0.4f : 0.3f, 0.6f, Pitch());
        }

        /// <summary>По чему идёт Греховод: в лагере — трава, в склепе — камень, в прочих сценах — тихо.</summary>
        public static string Surface(string scene)
        {
            switch (scene)
            {
                case CampScene:        return "Step/Grass";
                case "Crypt_Entrance":
                case "Crypt_Test":     return "Step/Stone";
                default:               return null;
            }
        }

        /// <summary>
        /// Ночь лагеря — сверчки, тихо, без пространства. Только в лагере:
        /// в склепе своя музыка звучит всё время, а гул подземелья спорил бы
        /// с ней (docs/43-SOUND.md §3).
        /// </summary>
        private void Night(string scene)
        {
            if (scene != CampScene)
            {
                if (_night != null) { if (_nightFade != null) StopCoroutine(_nightFade); _night.Stop(); }
                return;
            }

            var night = Load("Night");
            if (night.Length == 0) return;

            if (_night == null)
            {
                var go = new GameObject("Ночь");
                go.transform.SetParent(transform, false);
                _night = go.AddComponent<AudioSource>();
                _night.loop = true;
                _night.playOnAwake = false;
                _night.spatialBlend = 0f;
                _night.dopplerLevel = 0f;
            }

            _night.clip = night[0];
            _night.volume = 0f;
            _night.Play();
            FadeNight(NightVolume, 3f);
        }

        private void FadeNight(float to, float seconds)
        {
            if (_nightFade != null) StopCoroutine(_nightFade);
            _nightFade = StartCoroutine(NightFade(to, seconds));
        }

        private IEnumerator NightFade(float to, float seconds)
        {
            float from = _night.volume;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                _night.volume = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            _night.volume = to;
            if (to <= 0f) _night.Stop();
            _nightFade = null;
        }

        // ---------- как звучит ----------

        private void Play(AudioClip clip, Vector3 where, float volume, float spatial, float pitch)
        {
            if (clip == null) return;

            if (Time.frameCount != _frame) { _frame = Time.frameCount; _thisFrame = 0; }
            if (_thisFrame >= PerFrame) return;
            _thisFrame++;

            var src = _pool[_next];
            _next = (_next + 1) % _pool.Count;

            src.transform.position = where;
            src.spatialBlend = spatial;
            src.volume = volume;
            src.pitch = pitch;
            src.clip = clip;
            src.Play();
        }

        /// <summary>Вариант по кругу: три удара подряд звучат по-разному, и всегда в одном порядке.</summary>
        private static AudioClip Pick(string set)
        {
            var clips = Load(set);
            return clips.Length == 0 ? null : clips[(_turn++) % clips.Length];
        }

        /// <summary>Высота по кругу, без жребия: удар за ударом чуть иначе.</summary>
        private static float Pitch()
        {
            float[] steps = { 1f, 0.95f, 1.04f, 0.98f, 1.07f };
            return steps[_turn % steps.Length];
        }

        /// <summary>
        /// Записи набора: «Hit/Bone» — Bone0, Bone1, … пока есть; одиночная —
        /// по имени. Нет ни одной — сказать один раз, где положить.
        /// </summary>
        private static AudioClip[] Load(string set)
        {
            if (Clips.TryGetValue(set, out var ready)) return ready;

            var found = new List<AudioClip>();
            var single = Resources.Load<AudioClip>("Sounds/" + set);
            if (single != null) found.Add(single);
            else
                for (int i = 0; ; i++)
                {
                    var clip = Resources.Load<AudioClip>($"Sounds/{set}{i}");
                    if (clip == null) break;
                    found.Add(clip);
                }

            if (found.Count == 0 && Told.Add(set))
                Debug.LogWarning($"[ЗВУК] Нет записи «{set}» в Assets/Resources/Sounds — звучит тишина.");

            var clips = found.ToArray();
            Clips[set] = clips;
            return clips;
        }
    }
}
