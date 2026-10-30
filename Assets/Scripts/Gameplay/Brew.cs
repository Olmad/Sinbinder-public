// Assets/Scripts/Gameplay/Brew.cs
// Перевод: текст через Loc
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Варево Брата Хальда в котле (Cauldron) — не похлёбка, и каждый глоток копится (автор, 28.10). По номеру
    /// глотка, без жребия:
    ///
    /// 1. мир на миг плывёт;
    /// 2. полминуты всё в зеленоватом свете;
    /// 3. минуту голоса в лагере выше (VoiceGenerator.Pitch);
    /// 4. минуту Греховод ходит быстрее;
    /// 5. глаза Греховода загораются зелёным — навсегда;
    /// 6 и дальше — у огня на миг зеленеет пламя, и что-то видно.
    ///
    /// Каждый следующий глоток держит и прежние: зелень дольше, голоса выше.
    /// </summary>
    public class Brew : MonoBehaviour
    {
        private static Brew _one;

        /// <summary>Множитель высоты голосов — пока действует третий глоток.</summary>
        public static float Pitch => _one != null && Time.time < _one._voicesUntil ? _one._voiceRise : 1f;

        private float _voicesUntil = -1f, _voiceRise = 1f;
        private float _washedAt = -1f;
        private float _tintUntil = -1f, _quickUntil = -1f;

        /// <summary>Варево ещё действует: зелень, тонкие голоса или быстрые ноги. Глаза — навсегда, их не считаем.</summary>
        public static bool Active => _one != null && (Time.time < _one._voicesUntil || Time.time < _one._tintUntil || Time.time < _one._quickUntil);

        /// <summary>
        /// Колодец смывает варево (КТ1 п. 15; <see cref="CampWell"/>): зелень уходит за секунду, голоса и шаг —
        /// как были. Глаза остаются зелёными: пятый глоток — навсегда, так и задумано (автор, 28.10).
        /// </summary>
        public static void Wash()
        {
            if (_one == null) return;
            _one._washedAt = Time.time;
            _one._voicesUntil = -1f;
            Debug.Log("[КОТЁЛ] варево смыто у колодца");
        }
        private Image _tint;
        private static readonly Color Green = new(0.35f, 0.95f, 0.45f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => _one = null;

        public static void Sip(int sip, Vector3 pot)
        {
            if (_one == null) _one = new GameObject("Варево Хальда").AddComponent<Brew>();
            _one.StartCoroutine(_one.Wobble(0.4f + 0.08f * sip));
            if (sip >= 2) _one.StartCoroutine(_one.Tint(25f + 5f * sip, Mathf.Min(0.22f, 0.08f + 0.02f * sip)));
            if (sip >= 3) { _one._voicesUntil = Time.time + 60f; _one._voiceRise = Mathf.Min(1.6f, 1.25f + 0.05f * (sip - 3)); }
            if (sip >= 4) _one.StartCoroutine(_one.Quick(60f, Mathf.Min(1.45f, 1.2f + 0.05f * (sip - 4))));
            if (sip == 5) GreenEyes();
            if (sip >= 6) _one.StartCoroutine(_one.Vision(pot));
            Debug.Log($"[КОТЁЛ] глоток {sip}");
        }

        /// <summary>Мир плывёт: поле зрения камеры дышит туда-сюда.</summary>
        private IEnumerator Wobble(float seconds)
        {
            var cam = Camera.main;
            if (cam == null) yield break;
            float fov = cam.fieldOfView, t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                if (cam == null) yield break;
                cam.fieldOfView = fov + 7f * Mathf.Sin(t / seconds * Mathf.PI) * Mathf.Sin(t * 14f) * 0.5f + 4f * Mathf.Sin(t / seconds * Mathf.PI);
                yield return null;
            }
            if (cam != null) cam.fieldOfView = fov;
        }

        /// <summary>Зеленоватый свет поверх мира: проступает и уходит.</summary>
        private IEnumerator Tint(float seconds, float alpha)
        {
            if (_tint == null)
            {
                var canvasGo = new GameObject("Зелень варева", typeof(Canvas));
                canvasGo.transform.SetParent(transform, false);
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5;
                var go = new GameObject("Зелень", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(canvasGo.transform, false);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                _tint = go.GetComponent<Image>();
                _tint.raycastTarget = false;
            }
            float t = 0f, start = Time.time;
            _tintUntil = Mathf.Max(_tintUntil, start + seconds);
            while (t < seconds)
            {
                t += Time.deltaTime;
                // Смыто — уйти за секунду с той яркости, что была.
                if (_washedAt >= start) { seconds = Mathf.Min(seconds, t + 1f); _tintUntil = -1f; }
                float k = Mathf.Min(1f, t / 2f) * Mathf.Min(1f, (seconds - t) / (_washedAt >= start ? 1f : 4f));
                _tint.color = new Color(Green.r * 0.4f, Green.g * 0.6f, Green.b * 0.4f, alpha * k);
                yield return null;
            }
            _tint.color = Color.clear;
        }

        /// <summary>Ноги несут: шаг Греховода быстрее, потом — как был.</summary>
        private IEnumerator Quick(float seconds, float k)
        {
            if (!SinbinderPlayer.Exists) yield break;
            var legs = SinbinderPlayer.Instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (legs == null) yield break;
            float was = legs.speed;
            legs.speed = was * k;
            float start = Time.time;
            _quickUntil = Mathf.Max(_quickUntil, start + seconds);
            while (Time.time - start < seconds && _washedAt < start) yield return null;
            if (_washedAt >= start) _quickUntil = -1f;
            if (legs != null) legs.speed = was;
        }

        /// <summary>Глаза Греховода — зелёные, навсегда.</summary>
        private static void GreenEyes()
        {
            if (!SinbinderPlayer.Exists) return;
            var model = SinbinderPlayer.Instance.GetComponentInChildren<Animator>();
            if (model != null) Wardrobe.Tint(model.gameObject, "Eye", Green, glow: 1.0f);
        }

        /// <summary>Видение: пламя у котла зеленеет на четыре секунды.</summary>
        private IEnumerator Vision(Vector3 pot)
        {
            var go = new GameObject("Видение");
            go.transform.position = pot + Vector3.up * 1.4f;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Green;
            light.range = 7f;
            float t = 0f;
            while (t < 4f)
            {
                t += Time.deltaTime;
                light.intensity = 2.5f * Mathf.Sin(t / 4f * Mathf.PI) * (0.7f + 0.3f * Mathf.Sin(t * 23f));
                yield return null;
            }
            Destroy(go);
        }
    }
}
