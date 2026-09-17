// Assets/Scripts/Dev/CaptureMode.cs
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Dev
{
    /// <summary>
    /// Фото- и видеорежим: три рычага для съёмки, и ни одного лишнего.
    ///
    /// Заведён 17 сентября под показ в воскресенье. Автор поставил условие
    /// прямо: «если это всё лишь мишура для голой ветки — праздника
    /// не получится». Поэтому здесь ровно то, без чего съёмка дороже,
    /// и ничего, что просто приятно:
    ///
    /// <list type="bullet">
    /// <item><b>F9 — чистый кадр.</b> Гасит все холсты. Кадр с отладочной
    /// панелью в ролик не годится вовсе, а гасить их руками по одному —
    /// десять объектов в каждой сцене.</item>
    /// <item><b>F10 — замедление.</b> Главный кадр ролика (слово над
    /// воином и причина под ним) живёт около секунды. На 0.35 он читается,
    /// на единице — нет.</item>
    /// <item><b>F11 — следующий отказ.</b> Самый нужный рычаг.
    /// Отказ — событие вероятностное: по замеру стенда у Марги 39.8%,
    /// у Хоря 16.7%, у остальных ноль. Снимать продукт игры, надеясь
    /// на четыре шанса из десяти, — это десять дублей вместо одного.</item>
    /// </list>
    ///
    /// <b>Чего здесь нет нарочно.</b> Свободной камеры — <c>RTS_Camera</c>
    /// уже ездит и переключает два вида. Снимка экрана — это делает
    /// система. Полос кадра по кнопке — они и так приходят на наезде.
    /// Всё это было бы мишурой.
    ///
    /// <b>Отказ не подделывается.</b> F11 не выдумывает причину: он берёт
    /// <b>настоящего второго</b> из того же бюллетеня. Воин отказывается
    /// по своей причине, просто гарантированно — и причина под словом
    /// остаётся правдой. Подделанный отказ в ролике был бы обманом
    /// зрителя, а вся игра про то, что решает воин.
    ///
    /// Режим доступен только при <see cref="Transparency.DeveloperUnlocked"/>:
    /// у игрока этих клавиш нет.
    /// </summary>
    public class CaptureMode : MonoBehaviour
    {
        /// <summary>Гасить ли интерфейс. Читает <see cref="Update"/>.</summary>
        public static bool CleanFrame { get; private set; }

        /// <summary>
        /// Следующий отказ гарантирован. Снимается сам, как только отказ
        /// случился: рычаг на один дубль, иначе весь отряд перестанет
        /// слушаться и кадр развалится.
        /// </summary>
        public static bool NextRefusal { get; private set; }

        public static void RefusalTaken() => NextRefusal = false;

        [SerializeField] private KeyCode _clean = KeyCode.F9;
        [SerializeField] private KeyCode _slow = KeyCode.F10;
        [SerializeField] private KeyCode _refuse = KeyCode.F11;

        [SerializeField] private float _slowScale = 0.35f;

        private bool _slowed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm()
        {
            CleanFrame = false;
            NextRefusal = false;
        }

        void Update()
        {
            if (!Transparency.DeveloperUnlocked) return;

            if (Input.GetKeyDown(_clean))
            {
                CleanFrame = !CleanFrame;
                foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.InstanceID))
                    canvas.enabled = !CleanFrame;

                Say(CleanFrame ? "интерфейс скрыт" : "интерфейс возвращён");
            }

            if (Input.GetKeyDown(_slow))
            {
                _slowed = !_slowed;

                // Паузу не трогаем: игра на паузе ставит timeScale в ноль,
                // и наша единица разбудила бы её посреди совета.
                if (GamePauseController.Instance == null
                    || !GamePauseController.Instance.IsPaused)
                    Time.timeScale = _slowed ? _slowScale : 1f;

                Say(_slowed ? "замедление " + _slowScale.ToString("0.00") : "обычный ход");
            }

            if (Input.GetKeyDown(_refuse))
            {
                NextRefusal = true;
                Say("следующий отказ гарантирован — снимается после первого же");
            }
        }

        private static void Say(string what)
            => Debug.Log("[СЪЁМКА] " + what);
    }
}
