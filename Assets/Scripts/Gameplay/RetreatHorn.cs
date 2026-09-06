// Assets/Scripts/Gameplay/RetreatHorn.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Рог отхода. Сцена 5 пролога (docs/09-PROLOGUE.md §4).
    ///
    /// Приказ отходить — это первая половина сцены; вторая в том, что один
    /// человек его не выполнит. Рог нужен именно поэтому: пока приказ был
    /// только строкой в журнале, отказ не с чем было сравнивать. Рог
    /// слышат все, и то, что Карган остался, слышно тоже.
    ///
    /// Трубит один раз за сцену. Рог, трубящий на каждый отход, к третьему
    /// разу перестаёт что-либо значить — та же причина, по которой тишина
    /// в <see cref="UI.RefusalSilence"/> случается только на первом отказе.
    ///
    /// Звук — работа автора. Если его нет, компонент говорит об этом вслух
    /// и не молчит впустую: отсутствие данных здесь событие, а не ноль.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class RetreatHorn : MonoBehaviour
    {
        [Tooltip("Звук рога. Пусто — компонент поищет Resources/Sounds/Horn, "
               + "чтобы файл можно было просто положить в папку.")]
        [SerializeField] private AudioClip _horn;

        [Tooltip("Где искать звук, если поле пустое.")]
        [SerializeField] private string _fallbackPath = "Sounds/Horn";

        [Tooltip("Что пишет журнал, когда трубит рог. Пусто — не пишет.")]
        [SerializeField] private string _line = "Рог трубит отход.";

        [Tooltip("Только первый приказ отходить. Снимать не советую.")]
        [SerializeField] private bool _onlyFirst = true;

        private AudioSource _source;
        private bool _spent;
        private bool _complained;

        void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;

            if (_horn == null) _horn = Resources.Load<AudioClip>(_fallbackPath);
        }

        void OnEnable() => SelectionManager.OnPlayerOrder += OnOrder;
        void OnDisable() => SelectionManager.OnPlayerOrder -= OnOrder;

        private void OnOrder(CommandKind kind, int listeners)
        {
            if (kind != CommandKind.FallBack) return;
            if (_onlyFirst && _spent) return;
            _spent = true;

            Blow();
        }

        /// <summary>Протрубить. Публично: сцену может вести и не игрок.</summary>
        public void Blow()
        {
            if (_horn != null)
            {
                _source.PlayOneShot(_horn);
            }
            else if (!_complained)
            {
                // Один раз, не каждый кадр: ругань, повторённая сто раз,
                // прячет остальную консоль.
                _complained = true;
                Debug.LogWarning($"[РОГ] Звука нет: положить файл "
                               + $"в Assets/Resources/{_fallbackPath} или задать "
                               + "поле в инспекторе. Сцена 5 пройдёт молча.");
            }

            if (string.IsNullOrEmpty(_line)) return;

            var log = Object.FindFirstObjectByType<UI.BattleLogUI>();
            log?.Write(_line);
        }
    }
}
