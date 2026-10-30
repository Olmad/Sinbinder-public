// Assets/Scripts/Gameplay/CampWell.cs
// Перевод: текст через Loc
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Колодец лагеря (КТ1 п. 15: «колодец смывает варево: ведро, звук, эффект спадает»). Варево Хальда
    /// (<see cref="Brew"/>) копится с каждым глотком, а избавиться от него было нечем — только ждать минуту.
    ///
    /// F у колодца — ведро поднимается из сруба, всплеск, Греховод умывается: зелень, тонкие голоса и быстрые
    /// ноги уходят (<see cref="Brew.Wash"/>). Глаза остаются — пятый глоток навсегда. Без варева — просто
    /// холодная вода.
    ///
    /// Модель — колодец деревни «Перетягивания» (Well). Место — на краю лагеря, в кольце 7–10 м от огня, первое
    /// свободное по кругу, без жребия. Ставит себя сам туда, где есть котёл и костёр.
    /// </summary>
    public class CampWell : MonoBehaviour
    {
        private const float Reach = 2.0f;
        private static readonly float[] Rings = { 7.5f, 9f, 10.5f, 12f, 13.5f };

        public static CampWell Instance { get; private set; }

        private Transform _bucket;
        private Vector3 _bucketRest;
        private bool _busy;
        private AudioClip _splash;
        private bool _toldCold;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void First()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
            Install();
        }

        private static void OnScene(Scene scene, LoadSceneMode mode) => Install();

        private static void Install()
        {
            if (Object.FindFirstObjectByType<CampWell>() != null) return;
            var fire = GameObject.Find("Костёр");
            if (fire == null || Object.FindFirstObjectByType<Cauldron>() == null) return;
            if (MapLoader.Prefab("Well") == null) return;
            // Место ищет сам, когда сетка дорог готова: в миг загрузки сцены её ещё нет (w3110h: «вне сетки» все 80).
            new GameObject("Колодец").AddComponent<CampWell>()._fire = fire.transform.position;
        }

        private Vector3 _fire;
        private bool _placed;

        void Start() => StartCoroutine(Seek());

        private IEnumerator Seek()
        {
            for (int i = 0; i < 40; i++)
            {
                if (Spot(_fire, i == 39, out var at, out var facing)) { Place(at, facing); yield break; }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Debug.LogWarning("[КОЛОДЕЦ] места на краю лагеря не нашлось — колодца не будет");
        }

        private void Place(Vector3 at, Quaternion facing)
        {
            var model = MapLoader.Prefab("Well");
            var go = gameObject;
            go.transform.SetPositionAndRotation(at, facing);
            var body = Instantiate(model, go.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = model.transform.rotation;
            body.transform.localScale = model.transform.localScale;

            var block = go.AddComponent<NavMeshObstacle>();
            block.shape = NavMeshObstacleShape.Box;
            block.size = new Vector3(1.4f, 1.4f, 1.4f);
            block.center = Vector3.up * 0.7f;
            block.carving = true;
            block.carveOnlyStationary = true;

            Bucket();
            _placed = true;
            Debug.Log($"[КОЛОДЕЦ] стоит в {Vector3.Distance(at, _fire):F1} м от огня, у ({at.x:F1}; {at.z:F1}); огонь ({_fire.x:F1}; {_fire.z:F1})");
        }

        /// <summary>Шестнадцать сторон по кругу от огня: первая, где стоять можно и ничто не мешает.</summary>
        private static bool Spot(Vector3 fire, bool tell, out Vector3 at, out Quaternion facing)
        {
            // Кольца от ближнего к дальнему: в 8,5 м лагерь тесен (палатки, брёвна, котёл — w3110h).
            int off = 0, busy = 0;
            string what = "";
            foreach (float ring in Rings)
                for (int i = 0; i < 16; i++)
                {
                    float a = (110f + i * 22.5f) * Mathf.Deg2Rad;
                    var p = fire + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ring;
                    if (!NavMesh.SamplePosition(p, out var hit, 1.0f, NavMesh.AllAreas)) { off++; continue; }
                    p = hit.position;
                    var blocker = Blocker(p);
                    if (blocker != null) { busy++; if (what.Length < 120) what += blocker.name + "; "; continue; }
                    // Не вплотную к тому, у чего уже есть своё F: у карточного стола колодец спорил с «подсесть
                    // к картам» (кадр w3110h).
                    if (Crowded(p)) { busy++; continue; }
                    var toFire = fire - p; toFire.y = 0f;
                    at = p;
                    facing = Quaternion.LookRotation(toFire.normalized);
                    return true;
                }
            if (tell) Debug.LogWarning($"[КОЛОДЕЦ] вне сетки {off}, занято {busy}: {what}");
            at = default;
            facing = Quaternion.identity;
            return false;
        }

        /// <summary>Ближе четырёх метров к столу, котлу, бревну или граммофону.</summary>
        private static bool Crowded(Vector3 p)
        {
            const float Room = 4f;
            if (CardTable.Instance != null && CampFocus.GroundDistance(p, CardTable.Instance.transform.position) < Room) return true;
            if (Cauldron.Instance != null && CampFocus.GroundDistance(p, Cauldron.Instance.transform.position) < Room) return true;
            foreach (var seat in Object.FindObjectsByType<LogSeat>(FindObjectsSortMode.None))
                if (CampFocus.GroundDistance(p, seat.Centre) < Room) return true;
            foreach (var g in Object.FindObjectsByType<Gramophone>(FindObjectsSortMode.None))
                if (CampFocus.GroundDistance(p, g.transform.position) < Room) return true;
            return false;
        }

        /// <summary>
        /// Занято вещью — не землёй, холмом или зоной: всё шире восьми метров не мешает. Простор 1,8 м, а не 0,95:
        /// колодец у палатки закрывал проход, и Хорь стоял перед ним (w3110h, w3110i).
        /// </summary>
        private static Collider Blocker(Vector3 p)
        {
            foreach (var c in Physics.OverlapSphere(p + Vector3.up * 1.0f, 1.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                var size = c.bounds.size;
                if (size.x > 8f || size.z > 8f) continue;
                return c;
            }
            return null;
        }

        void Awake() => Instance = this;

        private void Bucket()
        {
            // Ведро — тёмная кадка над срубом; в покое — внутри, его не видно.
            var bucket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bucket.name = "Ведро";
            Destroy(bucket.GetComponent<Collider>());
            bucket.transform.SetParent(transform, false);
            bucket.transform.localScale = new Vector3(0.28f, 0.14f, 0.28f);
            _bucketRest = new Vector3(0f, 0.1f, 0f);
            bucket.transform.localPosition = _bucketRest;
            var r = bucket.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null) r.sharedMaterial = new Material(shader) { color = new Color(0.30f, 0.21f, 0.13f), name = "Bucket" };
            _bucket = bucket.transform;
            _splash = Splash();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Колодец рядом с Греховодом. Нет — null.</summary>
        public static CampWell Nearest()
        {
            if (Instance == null || !Instance._placed || Instance._busy || !SinbinderPlayer.Exists) return null;
            if (RaidEvent.Running || CrystalBall.Raised) return null;
            var pause = GamePauseController.Instance;
            if (pause != null && pause.IsPaused) return null;
            return CampFocus.GroundDistance(SinbinderPlayer.Where, Instance.transform.position) <= Reach ? Instance : null;
        }

        public Vector3 Centre => transform.position + Vector3.up * 0.8f;
        public string Prompt => Loc.T("F — умыться у колодца");

        public void Wash()
        {
            if (_busy) return;
            StartCoroutine(Draw());
        }

        /// <summary>Ведро вверх за секунду, всплеск, умылся; ведро обратно.</summary>
        private IEnumerator Draw()
        {
            _busy = true;
            bool brewed = Brew.Active;
            var top = _bucketRest + Vector3.up * 1.0f;
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                _bucket.localPosition = Vector3.Lerp(_bucketRest, top, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            _bucket.localPosition = top;

            if (_splash != null) AudioSource.PlayClipAtPoint(_splash, transform.position + Vector3.up, 0.55f);
            Brew.Wash();
            if (brewed) UI.SpeechBubbles.Say(SinbinderPlayer.Instance, Loc.T("Ледяная. Отпустило."));
            else if (!_toldCold) UI.SpeechBubbles.Say(SinbinderPlayer.Instance, Loc.T("Ледяная."));
            _toldCold = true;
            Debug.Log("[КОЛОДЕЦ] умылся" + (brewed ? " — варево смыто" : ""));

            yield return new WaitForSeconds(0.6f);
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                _bucket.localPosition = Vector3.Lerp(top, _bucketRest, Mathf.SmoothStep(0f, 1f, t / 0.8f));
                yield return null;
            }
            _bucket.localPosition = _bucketRest;
            _busy = false;
        }

        /// <summary>
        /// Всплеск формулой: шум из хэша номера отсчёта (не жребий — тот же всплеск каждый раз), сглаженный
        /// до плеска, с резким началом и затуханием; второй, тише, — вода стекает с ладоней.
        /// </summary>
        private static AudioClip Splash()
        {
            const int rate = 22050;
            int n = (int)(rate * 0.9f);
            var data = new float[n];
            float low = 0f;
            for (int i = 0; i < n; i++)
            {
                uint h = (uint)i * 2654435761u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                float noise = (h & 0xFFFF) / 32768f - 1f;
                low += (noise - low) * 0.18f;                       // мягче: плеск, а не шипение
                float t = i / (float)rate;
                float env = Mathf.Exp(-t * 9f) * Mathf.Min(1f, t * 400f)
                          + 0.35f * Mathf.Exp(-(t - 0.35f) * (t - 0.35f) * 60f);
                data[i] = low * env * 0.9f;
            }
            var clip = AudioClip.Create("Всплеск", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
