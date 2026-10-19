// Assets/Scripts/UI/PlateLook.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.UI;

namespace Sinbinder.UI
{
    /// <summary>
    /// Табличка (<see cref="UiStyle.Plate"/>) на панели, которую строит
    /// сборщик сцен. Табличка рисуется кодом и в сцену не сохраняется,
    /// поэтому её ставят при запуске — тем же <see cref="UiStyle.Dress"/>,
    /// что и панели кода: облик меняется в одном месте для всех разом
    /// (46-UI §0, п. 2).
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class PlateLook : MonoBehaviour
    {
        [SerializeField] private UiStyle.PlateWeight _weight = UiStyle.PlateWeight.Screen;

        /// <summary>Задать вес. Зовёт сборщик сцен.</summary>
        public void Set(UiStyle.PlateWeight weight) => _weight = weight;

        void Awake() => UiStyle.Dress(GetComponent<Image>(), _weight);
    }
}
