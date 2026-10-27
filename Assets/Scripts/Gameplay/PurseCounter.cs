// Assets/Scripts/Gameplay/PurseCounter.cs
// Перевод: текст через Loc
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Примета Лиски на слух: он держится у кошеля Греховода и пересчитывает его.
    ///
    /// Автор, отзыв 26.10, п. 21: «меня почему-то преследовал Лиска». Ходит он
    /// за Греховодом из верности (<c>LoyaltyModule.EvaluateSpot</c>: верный
    /// в лагере держится у владыки, у Лиски этот голос громче всех) — это
    /// движок, и он прав. Непонятно было другое: <i>зачем</i>. Предложение D
    /// («у кошеля») облачная приняла: Лиска «чужое пересчитывает — на всякий
    /// случай» (<see cref="Signature"/>), и теперь это слышно. Кошель стал
    /// другим, пока Лиска рядом, — он говорит, сколько было и сколько стало.
    ///
    /// Примета не врёт: «было» — то, что он сам видел в последний раз, когда
    /// стоял рядом; не видел перемены — и не скажет, пока снова не подойдёт.
    /// Монеты — числом: это то, чем Греховод владеет (кодекс §8). Не чаще
    /// раза в <see cref="Every"/> секунд; в разгроме молчит.
    /// </summary>
    public class PurseCounter : MonoBehaviour
    {
        private static readonly string Counter = Loc.N("Лиска");

        /// <summary>С какого расстояния он видит кошель, м.</summary>
        private const float Sees = 6f;

        /// <summary>Не чаще, секунд игры: счёт — привычка, а не бухгалтерия вслух.</summary>
        private const float Every = 25f;

        private static readonly string Less = Loc.N("Было {0} — стало {1}, владыка. Я считаю.");
        private static readonly string More = Loc.N("Было {0} — стало {1}. Откуда, владыка?");

        private Warrior _liska;
        private float _look;
        private float _find;
        private float _quietUntil;
        private int _seen = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Object.FindFirstObjectByType<PurseCounter>() != null) return;
            var go = new GameObject("Счёт Лиски");
            DontDestroyOnLoad(go);
            go.AddComponent<PurseCounter>();
        }

        void Update()
        {
            if (Time.unscaledTime < _look) return;
            _look = Time.unscaledTime + 0.5f;

            var purse = Inventory.PlayerInventory.Instance;
            if (purse == null || !SinbinderPlayer.Exists || RaidEvent.Running) return;

            // Искать — раз в три секунды: в склепе Лиски может и не быть,
            // а обход всех воинов дважды в секунду — лишний (замер кадра D, 26.10).
            if ((_liska == null || _liska.IsDead) && Time.unscaledTime >= _find)
            {
                _find = Time.unscaledTime + 3f;
                _liska = Find();
            }
            if (_liska == null) { _seen = -1; return; }

            if (CampFocus.GroundDistance(_liska.transform.position, SinbinderPlayer.Where) > Sees) return;

            int now = purse.Gold;
            if (_seen < 0) { _seen = now; return; }
            if (now == _seen || Time.time < _quietUntil) return;

            var pause = GamePauseController.Instance;
            if (pause != null && pause.IsPaused) return;
            if (UI.TalkView.Shown || Herald.Busy) return;

            string line = Loc.F(now < _seen ? Less : More, Grammar.Coins(_seen), Grammar.Coins(now));
            UI.SpeechBubbles.Say(_liska, line);
            Debug.Log($"[ПРИМЕТА] Лиска считает кошель: {_seen} → {now}");
            _seen = now;
            _quietUntil = Time.time + Every;
        }

        private static Warrior Find()
        {
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
                if (w != null && !w.IsDead && w.Team == Team.Player && w.DisplayName == Counter) return w;
            return null;
        }
    }
}
