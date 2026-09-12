// Assets/Scripts/UI/OverheadUIPool.cs
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.UI
{
    /// <summary>
    /// Склад надголовных панелей: снятую с погибшего можно отдать живому,
    /// а не создавать заново.
    ///
    /// <b>Склад статический, а сцены — нет.</b> Отсюда и вся беда, которую
    /// он однажды устроил: панель, положенная сюда в одной сцене, вместе
    /// со сценой уничтожается, а очередь об этом не знает. Следующий
    /// <see cref="Get"/> доставал покойника и дёргал ему <c>SetActive</c> —
    /// то есть каждое поднятие воина на полигоне падало исключением.
    ///
    /// Та же болезнь, что у менеджера выделения и камеры разговора:
    /// долгожитель держит ссылку на то, что смены сцен не переживает.
    /// Лечится здесь, а не у зовущих: их пятеро, и проверять за складом
    /// пришлось бы каждому.
    /// </summary>
    public static class OverheadUIPool
    {
        private static readonly Queue<GameObject> _pool = new();

        /// <summary>
        /// Панель со склада или <c>null</c>, если склада нет.
        ///
        /// Уничтоженные пропускаем молча: они не ошибка склада, а обычная
        /// судьба его содержимого. Ошибкой было бы отдать такую наружу.
        /// </summary>
        public static GameObject Get()
        {
            while (_pool.Count > 0)
            {
                var obj = _pool.Dequeue();

                // Сравнение с null у Unity ловит и уничтоженные объекты —
                // именно ради этого случая оно и перегружено.
                if (obj == null) continue;

                obj.SetActive(true);
                return obj;
            }

            return null;
        }

        /// <summary>Вернуть панель на склад. Уничтоженную не берём.</summary>
        public static void Return(GameObject obj)
        {
            if (obj == null) return;

            obj.SetActive(false);
            _pool.Enqueue(obj);
        }

        /// <summary>Опустошить склад. Уничтоженных не трогаем.</summary>
        public static void Clear()
        {
            while (_pool.Count > 0)
            {
                var obj = _pool.Dequeue();
                if (obj != null) Object.Destroy(obj);
            }
        }
    }
}
