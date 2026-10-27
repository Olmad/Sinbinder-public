// Assets/Scripts/Gameplay/RearguardMoment.cs
// Перевод: текст через Loc
using System.Collections;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Миг арьергарда: телохранитель отказался уходить — камера на нём на три
    /// с половиной секунды, полосы кино, мир замедлен, а он — нет: три удара
    /// мечом в щит, руки в стороны, «ну, кто первый?».
    ///
    /// Автор, 26 октября: «да — на всё» — миг, а не ролик (кодекс §4; 28-ORDERS,
    /// «Арьергард Каргана — мигом»), и «нужна какая-нибудь анимация, которая
    /// „бросает вызов“: стучит по щиту, разводит руки, ожидая удара». Клип —
    /// D (<see cref="WarriorAnimation.Challenge"/>, 64 кадра; удары — на 10, 19
    /// и 28-м кадре). Развилка движка не трогается: миг только показывает
    /// отказ, который уже случился (<see cref="EscapeZone"/>), — и только его.
    ///
    /// <b>Мир замедлен, не стоит.</b> Бегство остаётся спешкой: охотники идут,
    /// отряд бежит — медленно, пока камера на нём. Сам он играет вызов в своём
    /// времени (аниматор — по реальному): герой мига двигается, мир вокруг
    /// застыл в полудвижении. Голос и железо — тоже в реальном.
    ///
    /// Кадр занят разговором, мир стоит (пауза, урок) — мига нет: строка уходит
    /// <see cref="Herald"/>, как было до 27.10. Отнимать кадр у разговора нельзя
    /// (то же правило у <see cref="MomentCamera"/>).
    /// </summary>
    public class RearguardMoment : MonoBehaviour
    {
        /// <summary>Сколько длится миг, секунд реального времени.</summary>
        private const float Seconds = 3.5f;

        /// <summary>
        /// Насколько медленнее мир. Не <c>RefusalPresenter</c>-овские 0,3: тот
        /// миг отказа возвращает время, только если оно всё ещё его, — у нас своё
        /// число, и два замедления друг друга не отменят.
        /// </summary>
        private const float Slow = 0.35f;

        /// <summary>
        /// Удары мечом в щит от начала вызова, секунд: кадры 10, 19, 28 клипа
        /// при 30 в секунду и переход в клип (<c>WarriorAnimation.Blend</c>, 0,12).
        /// </summary>
        private static readonly float[] Strikes = { 0.42f, 0.72f, 1.02f };

        /// <summary>Ворон срывается с плеча — на первом ударе.</summary>
        private const float RavenAt = 0.45f;

        /// <summary>
        /// Показать миг. <paramref name="line"/> — реплика вида «Имя: «слова»»,
        /// как её пишет журнал; в журнал она уходит сразу, как у <see cref="Herald"/>.
        /// </summary>
        public static void Play(Warrior who, string line)
        {
            var camera = Dialogue.DialogueCameraController.Instance;
            if (who == null || who.IsDead || camera == null || camera.InDialogue
                || Core.GamePauseController.Stopped || Herald.Busy
                || !Herald.Split(line, out _, out string words))
            {
                Herald.Line(line);
                return;
            }

            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
            var host = new GameObject("Миг арьергарда").AddComponent<RearguardMoment>();
            host.StartCoroutine(host.Show(who, Core.Loc.Name(who.DisplayName), words));
        }

        private IEnumerator Show(Warrior who, string name, string words)
        {
            var camera = Dialogue.DialogueCameraController.Instance;
            var target = who.transform;

            // Мир — медленнее; запоминаем, что ставили мы: вернём, только если
            // время всё ещё наше (пауза или урок поверх — их хозяева сами).
            // Замедление отказа (RefusalPresenter, 0,3 на полсекунды) могло уже
            // стоять — наше его сменяет, а своё оно вернёт, только если 0,3 ещё стоит.
            bool slowed = Time.timeScale > 0f;
            if (slowed) Time.timeScale = Slow;

            var animator = who.GetComponentInChildren<Animator>();
            var mode = animator != null ? animator.updateMode : AnimatorUpdateMode.Normal;
            if (animator != null) animator.updateMode = AnimatorUpdateMode.UnscaledTime;

            var body = who.GetComponent<WarriorAnimation>();
            if (body != null) body.Challenge();
            Debug.Log($"[ПОБЕГ] Миг арьергарда: {who.DisplayName}");

            camera.SaveCameraPosition();
            var focus = StartCoroutine(camera.FocusOn(target));
            UI.Letterbox.Instance?.Say(name, words);
            if (who.TryGetComponent<Audio.VoiceGenerator>(out var voice))
                voice.Babble(words, Mathf.Min(Seconds - 0.8f, words.Length / 40f * 1.2f + 0.3f));

            // Железо в такт клипу и ворон — по реальному времени, как вызов.
            float t = 0f;
            int strike = 0;
            bool raven = false;
            while (t < Seconds)
            {
                if (target == null) break;
                if (strike < Strikes.Length && t >= Strikes[strike])
                    Audio.Sfx.ShieldStrike(target.position + Vector3.up * 1.1f, strike++);
                if (!raven && t >= RavenAt)
                {
                    raven = true;
                    Audio.Sfx.Caw(target.position + Vector3.up * 1.6f);
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (focus != null) StopCoroutine(focus);
            camera.StopSway();
            if (animator != null) animator.updateMode = mode;
            if (slowed && Mathf.Approximately(Time.timeScale, Slow)) Time.timeScale = 1f;

            yield return camera.RestoreCamera();
            Destroy(gameObject);
        }
    }
}
