// Assets/Scripts/UI/WorldPlate.cs
using UnityEngine;

namespace Sinbinder.UI
{
    /// <summary>
    /// Надпись на предмете, которая молчит, пока на предмет не смотрят.
    ///
    /// До сих пор таблички висели всегда: подставки с телами, гнёзда,
    /// рычаги, банки на полке, колышки павших — всё подписано разом.
    /// В комнате из десятка предметов это стена текста, сквозь которую
    /// не видно самой комнаты, и читать её никто не станет — ровно как
    /// не читают одиннадцать строк подряд в журнале.
    ///
    /// Показывает <see cref="PlateSight"/>, один на сцену: он и решает,
    /// на что нацелен игрок. Сама табличка только гаснет и загорается.
    ///
    /// Гасим рисователь, а не объект: выключенный объект перестал бы
    /// поворачиваться к камере (<see cref="Billboard"/>), и загоревшись,
    /// первый кадр показал бы надпись боком.
    /// </summary>
    public class WorldPlate : MonoBehaviour
    {
        private Renderer _renderer;

        void Awake()
        {
            _renderer = GetComponent<Renderer>();

            if (_renderer == null)
            {
                Debug.LogWarning($"[ТАБЛИЧКА] У «{name}» нечего гасить: "
                               + "нет рисователя. Компонент вешается на объект "
                               + "с TextMesh.");
                enabled = false;
                return;
            }

            Show(false);
        }

        public void Show(bool on)
        {
            if (_renderer != null) _renderer.enabled = on;
        }
    }
}
