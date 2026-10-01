// Assets/Scripts/UI/OverheadBuilder.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Надголовный интерфейс воина: полоса здоровья и значок намерения.
    ///
    /// <b>Первая ступень прозрачности</b> — «значок над головой, всегда,
    /// в момент решения» (00-GDD.md §7). И одновременно первый из четырёх
    /// обязательных скриншотов и ключевой кадр восьмисекундной гифки,
    /// единственного обязательного маркетингового материала (06-SELLING.md):
    /// «бой со значками намерений над головами — видно, что каждый решает
    /// сам».
    ///
    /// Собиралось это всё только в <see cref="UnitFactory"/>, которым
    /// пролог не пользуется: его спавнеры лепят воинов руками. Значит
    /// в демо не было ни значков, ни полос — и лестница прозрачности
    /// начиналась со второй ступени, а гифку снять было нельзя.
    /// Спрайты при этом лежали на месте, и код показа тоже: не хватало
    /// одного звена.
    ///
    /// Поэтому сборка вынесена сюда: одна на всех, кто делает воинов.
    /// </summary>
    public static class OverheadBuilder
    {
        /// <summary>На сколько поднять над головой.</summary>
        public const float Height = 2.5f;

        /// <summary>
        /// Повесить надголовный интерфейс, если его ещё нет.
        /// Возвращает корень или null, если вешать не на что.
        /// </summary>
        public static GameObject Attach(GameObject warrior, Damageable damageable)
        {
            if (warrior == null || damageable == null) return null;

            var existing = warrior.GetComponentInChildren<OverheadUI>();
            if (existing != null) return existing.gameObject;

            var pooled = OverheadUIPool.Get();
            if (pooled != null) return Reuse(pooled, warrior, damageable);

            return Build(warrior, damageable);
        }

        private static GameObject Reuse(GameObject root, GameObject warrior,
            Damageable damageable)
        {
            root.transform.SetParent(warrior.transform, false);
            root.transform.localPosition = new Vector3(0f, Height, 0f);

            var ui = root.GetComponent<OverheadUI>();
            if (ui?.HealthBar != null)
                ui.HealthBar.ManualInit(damageable, ui.HealthBar.Slider,
                                        ui.HealthBar.FillImage, root);

            var billboard = root.GetComponent<Billboard>();
            if (billboard != null) billboard.Retarget();

            // Набор из пула мог быть собран до слова над головой.
            if (root.GetComponent<OverheadWord>() == null) Word(root.transform);

            return root;
        }

        private static GameObject Build(GameObject warrior, Damageable damageable)
        {
            var root = new GameObject("Надголовное");
            root.transform.SetParent(warrior.transform, false);
            root.transform.localPosition = new Vector3(0f, Height, 0f);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;

            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120f, 30f);

            // Два с половиной метра над головой — столько занимала полоса
            // при прежнем масштабе, вдвое больше самого воина. Теперь
            // примерно в плечо шириной: видно, но смотрят на бойца.
            rect.localScale = Vector3.one * 0.008f;

            // Без разворота к камере холст в мире стоит как есть: значок
            // виден с одной стороны и исчезает с другой. Для главного
            // кадра игры это неприемлемо.
            root.AddComponent<Billboard>();

            var bar = HealthBar(root.transform, damageable, out var slider,
                                out var fill);
            var icon = Icon(root.transform);
            Word(root.transform);

            var ui = root.AddComponent<OverheadUI>();
            ui.HealthBar = bar;
            ui.DecisionIcon = icon;

            bar.ManualInit(damageable, slider, fill, root);

            return root;
        }

        private static HealthBarUI HealthBar(Transform parent, Damageable damageable,
            out Slider slider, out Image fill)
        {
            var go = new GameObject("Полоса здоровья");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 20f);
            rect.anchoredPosition = new Vector2(0f, -2f);

            slider = go.AddComponent<Slider>();
            slider.interactable = false;
            slider.minValue = 0f;
            slider.maxValue = damageable.MaxHP;
            slider.value = damageable.HP;

            Stretch("Подложка", go.transform, new Color(0.12f, 0.11f, 0.11f, 0.95f));

            var area = new GameObject("Заполнение", typeof(RectTransform));
            area.transform.SetParent(go.transform, false);
            var areaRect = (RectTransform)area.transform;
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = Vector2.zero;
            areaRect.offsetMax = Vector2.zero;

            fill = Stretch("Полоса", area.transform, new Color(0.55f, 0.16f, 0.14f));

            slider.fillRect = (RectTransform)fill.transform;
            slider.targetGraphic = fill;

            return go.AddComponent<HealthBarUI>();
        }

        private static Image Stretch(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return image;
        }

        /// <summary>
        /// Слово над отступившим (<see cref="OverheadWord"/>) — выше значка,
        /// без фона, с обводкой: рамка, летающая по полю, была бы тяжела.
        /// </summary>
        private static void Word(Transform parent)
        {
            var go = new GameObject("Слово", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(600f, 64f);
            rect.anchoredPosition = new Vector2(0f, 34f);

            var text = go.AddComponent<Text>();
            text.font = UiStyle.Title;      // слово — заглавными с засечками, как имена (42-INTERFACE §1, п. 9)
            text.fontSize = 44;
            text.alignment = TextAnchor.LowerCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.text = "";

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            parent.gameObject.AddComponent<OverheadWord>().SetText(text);
        }

        private static DecisionIconUI Icon(Transform parent)
        {
            var go = new GameObject("Значок намерения", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(48f, 48f);
            rect.anchoredPosition = new Vector2(0f, -20f);

            var image = go.AddComponent<Image>();
            image.raycastTarget = false;

            var icon = go.AddComponent<DecisionIconUI>();
            icon.SetIconImage(image);

            return icon;
        }
    }
}
