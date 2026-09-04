using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Gameplay;
using Sinbinder.Inventory;

namespace Sinbinder.UI
{
    /// <summary>
    /// Восьмой рычаг игрока: вложить вещь в руки выбранному воину.
    ///
    /// «Джинн не переписывает душу — он вкладывает в руку золотой клинок
    /// и смотрит, что будет» (08-FLOOR §3.3). Механика была написана
    /// целиком — <c>TemptationResolver</c> подключён к голосованию, поля
    /// у предмета есть, — и не имела ни одного предмета и ни одного
    /// способа его дать. Рычаг существовал в виде труб без воды.
    ///
    /// Панель намеренно скупа: список вещей, выбранный воин, щелчок.
    /// Ни чисел, ни шкал — только название и строка о том, к чему вещь
    /// тянет. Что из этого выйдет, игрок узнает из поступка.
    /// </summary>
    public class TemptationPanelUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _rows;
        [SerializeField] private Text _hint;
        [SerializeField] private Font _font;

        private readonly List<InventoryItem> _items = new();

        void Start()
        {
            _items.AddRange(TemptationCatalog.Demo());

            float y = 0f;
            foreach (var item in _items)
            {
                Row(item, y);
                y -= 74f;
            }

            Hint();
        }

        private void Row(InventoryItem item, float y)
        {
            var go = new GameObject(item.Name, typeof(RectTransform));
            go.transform.SetParent(_rows, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(0f, 66f);
            rt.anchoredPosition = new Vector2(0f, y);

            var plate = go.AddComponent<Image>();
            plate.color = new Color(0.11f, 0.10f, 0.09f, 0.90f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = plate;
            button.onClick.AddListener(() => Give(item));

            var label = new GameObject("Подпись", typeof(RectTransform));
            label.transform.SetParent(rt, false);

            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(14f, 6f);
            lrt.offsetMax = new Vector2(-14f, -6f);

            var text = label.AddComponent<Text>();
            text.font = _font;
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(0.90f, 0.88f, 0.84f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = $"{item.Name} — {Pull(item)}";
        }

        /// <summary>К чему тянет вещь. Словами: чисел игрок не видит.</summary>
        private static string Pull(InventoryItem item)
        {
            string sin = Core.SoulData.GetSinName(item.TemptationSin);
            return item.TemptationValue >= 0f ? $"тянет к: {sin}" : $"отнимает: {sin}";
        }

        private void Give(InventoryItem item)
        {
            var warrior = Selected();
            if (warrior == null) { Hint("Сначала выберите воина."); return; }

            if (!warrior.Give(item))
            {
                Hint($"{warrior.DisplayName} уже несёт это.");
                return;
            }

            Hint($"{warrior.DisplayName} принял: {item.Name}.");

            var log = Object.FindFirstObjectByType<BattleLogUI>();
            log?.Write($"{warrior.DisplayName} взял: {item.Name}.");
        }

        private static Warrior Selected()
        {
            var selection = SelectionManager.Instance;
            if (selection == null) return null;

            foreach (var unit in selection.GetSelectedUnits())
            {
                if (unit == null) continue;
                var warrior = unit.GetComponent<Warrior>();
                if (warrior != null && !warrior.IsDead && warrior.Team == Team.Player)
                    return warrior;
            }
            return null;
        }

        private void Hint(string text = null)
        {
            if (_hint == null) return;
            _hint.text = text ?? "Выберите воина и дайте ему вещь.";
        }
    }
}
