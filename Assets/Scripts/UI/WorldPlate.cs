// Assets/Scripts/UI/WorldPlate.cs
// Перевод: текст через Loc
using UnityEngine;

using Sinbinder.Core;
namespace Sinbinder.UI
{
    /// <summary>
    /// Подпись предмета. Хранит слова и молчит.
    ///
    /// Показывает её не она сама, а <see cref="PlateSight"/> — одной
    /// строкой у нижней панели. Так подпись всегда в одном месте экрана,
    /// её не приходится искать взглядом и она не наезжает ни на панель,
    /// ни на другие предметы.
    ///
    /// Сам текст в мире гасится навсегда: висящие над каждым предметом
    /// надписи — это стена, сквозь которую не видно комнаты. Но объект
    /// с <c>TextMesh</c> остаётся, потому что в нём и лежат слова: сборщик
    /// сцены пишет их туда, а не в отдельное поле, и второй правды
    /// о подписи заводить не нужно.
    /// </summary>
    public class WorldPlate : MonoBehaviour
    {
        private TextMesh _mesh;

        /// <summary>
        /// Что здесь написано — на языке игрока. Пусто — показывать нечего.
        ///
        /// Слова вписывает в сцену сборщик (DemoSceneBuilder), и лежат они
        /// в ней по-русски, мимо Loc. Переводятся здесь, при каждом чтении:
        /// сменил игрок язык — табличка уже на новом (docs/38-LANG.md §6).
        /// Строки нет в таблице — это имя (павший на колышке): Loc.Name.
        /// </summary>
        public string Text => _mesh != null ? Shown(_mesh.text) : string.Empty;

        private static string Shown(string baked)
            => Loc.Has(baked) ? Loc.T(baked) : Loc.Name(baked);

        void Awake()
        {
            _mesh = GetComponent<TextMesh>();

            if (_mesh == null)
            {
                Debug.LogWarning($"[ТАБЛИЧКА] У «{name}» нет TextMesh: "
                               + "подписи взяться неоткуда.");
                enabled = false;
                return;
            }

            // Гасим рисователь, а не объект: выключенный объект перестал бы
            // поворачиваться к камере, а он ещё нужен как носитель слов.
            var renderer = GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;
        }
    }
}
