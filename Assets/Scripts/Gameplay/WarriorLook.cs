using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Из чего сделан воин на экране.
    ///
    /// <b>Шов под модели.</b> До сих пор тело лепили кубом в четырёх
    /// местах подряд, каждое со своими числами. Поставить модель значило
    /// бы переписать все четыре и не забыть ни одного — а забыть одно
    /// проще всего, и тогда в отряде из девяти один остался бы кубом.
    ///
    /// Теперь одна правда: модель, если она есть, куб — если нет.
    /// Ни одна сцена от этого не ломается и сегодня выглядит как вчера.
    ///
    /// <b>Где лежит модель.</b> <c>Resources/Bodies/Skeleton</c>,
    /// <c>Zombie</c>, <c>Ghost</c>, <c>Golem</c> — по имени оболочки.
    /// Тот же уговор, что у конфига и оболочек: ассет кладут в Resources
    /// и он подхватывается сам. Аниматор на префабе обязан знать
    /// состояния из <see cref="BodyMotion"/>.
    ///
    /// <b>Отсутствие модели — событие, а не ноль.</b> Говорим об этом
    /// один раз за запуск, а не молчим и не кричим каждый раз: молчание
    /// прячет то, что художник положил файл не туда, а крик на каждого
    /// воина превращает консоль в мусор. Тот же приём, что у
    /// <c>AOSConfig.Load()</c>.
    /// </summary>
    public static class WarriorLook
    {
        private const string Folder = "Bodies/";

        private static bool _toldAboutMissing;
        private static bool _toldAboutMotion;

        /// <summary>Забыть, что уже жаловались. Для проверок.</summary>
        /// <summary>
        /// Сброс к началу игры. Звался ниоткуда с самого появления —
        /// правило orphans в check.py нашло это 17 сентября. Теперь
        /// зовёт сама Unity, и метод перестал быть публичным обещанием,
        /// которого никто не просил.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm()
        {
            _toldAboutMissing = false;
            _toldAboutMotion = false;
        }

        /// <summary>
        /// Собрать видимое тело и вернуть его. Рост и толщина остаются
        /// теми же, что были у куба: силуэт — это разница между оболочками
        /// и между опытным и рядовым, и терять её нельзя ни на модели,
        /// ни без неё.
        /// </summary>
        public static Transform Build(GameObject owner, ShellType shell,
                                      float height, float girth, float lift)
        {
            if (owner == null) return null;

            var prefab = Resources.Load<GameObject>(Folder + shell);

            if (prefab != null)
            {
                var model = Object.Instantiate(prefab, owner.transform);
                model.name = "Тело";

                // Модель стоит на ногах: у humanoid-рига опора внизу,
                // и поднимать его нечем и незачем. Подъём — это про куб,
                // у которого опора в середине.
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;

                // Модель приходит ростом в единицу, а нам нужен тот,
                // который задал спавнер: рост здесь — не украшение,
                // по нему видно опытного ещё до совета.
                //
                // Масштаб равномерный, и толщина в него не входит.
                // Толщина — это про куб: у куба она была шириной в метрах
                // (0,5), и приложенная к модели превратила бы скелета
                // в спичку вдвое у́же себя. У модели свои пропорции,
                // и портить их нечем, кроме как этим числом.
                // Толщина берётся из той же доли, что и радиус
                // капсулы-заглушки: половина — обычное сложение, больше —
                // кряжистый, меньше — сухой. До 20 сентября модели
                // ставился равномерный масштаб, и <c>girth</c> не делал
                // ничего вовсе: девять воинов у костра отличались только
                // ростом, и то двумя значениями.
                float thick = girth <= 0f ? 1f : girth / 0.5f;
                model.transform.localScale =
                    new Vector3(height * thick, height, height * thick);

                Animate(model, shell);

                // Грех горит в воине: цвет берётся из его же души.
                // Вешаем здесь, а не в спавнере, потому что гореть
                // должно на модели — там и материал, и голова.
                model.AddComponent<SinEyes>();

                // И одеваем: капюшон, колчан, наплечник, плащ из вороньих
                // перьев — по тому, кем воин является (22-LOOK.md).
                // Здесь, а не в спавнерах: спавнеров четыре, и одеть
                // в трёх из них — значит однажды забыть в четвёртом.
                Wardrobe.Dress(model, owner.GetComponent<Warrior>());

                return model.transform;
            }

            Missing(shell);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Тело";
            body.transform.SetParent(owner.transform);
            // Подъём передаёт вызывающий, а не считаем здесь: у куба
            // опора в середине, и каждый спавнер подобрал своё число
            // на глаз. Подменить их одной формулой значит сдвинуть
            // всех воинов во всех сценах — правка, которую из облака
            // не проверить ничем.
            body.transform.localPosition = new Vector3(0f, lift, 0f);
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = new Vector3(girth, height, girth);

            return body.transform;
        }

        /// <summary>
        /// Дать телу контроллер движений.
        ///
        /// Импортированная модель несёт <c>Animator</c> без контроллера:
        /// контроллер — редакторный ассет, в FBX его положить нельзя,
        /// и на префабе он оказался бы только если завести префаб.
        /// Префаб рядом с моделью носил бы то же имя в той же папке,
        /// и <c>Resources.Load</c> выбирал бы из двух — ровно та
        /// неоднозначность, которую потом ищут часами.
        ///
        /// Поэтому контроллер лежит отдельным именем и находится сам:
        /// <c>Bodies/SkeletonMotion</c> рядом с <c>Bodies/Skeleton</c>.
        /// Собирает его <c>BodyImport</c> при импорте модели.
        ///
        /// Без контроллера <c>Animator.Play</c> молчит, и тело стоит
        /// в позе привязки — руки в стороны. Поэтому говорим вслух,
        /// один раз: поза привязки на поле боя выглядит не как
        /// «анимации ещё нет», а как поломка.
        /// </summary>
        private static void Animate(GameObject model, ShellType shell)
        {
            var animator = model.GetComponentInChildren<Animator>();

            if (animator == null)
            {
                // Так уже было 12 сентября: импортёр не создал аватар,
                // Unity не повесила Animator, тело приехало без единого
                // движения — и выглядело это как «анимации не сделаны».
                // Молчать об этом нельзя: снаружи две причины неотличимы.
                Told($"[ВИД] На модели {shell} нет Animator — двигаться нечем. "
                   + "Аватар не создан при импорте; переимпортируйте модель.");
                return;
            }

            if (animator.runtimeAnimatorController != null) return;

            var controller = Resources.Load<RuntimeAnimatorController>(
                Folder + shell + "Motion");

            if (controller != null)
            {
                animator.runtimeAnimatorController = controller;
                return;
            }

            Told($"[ВИД] Движений нет ({Folder}{shell}Motion) — тело "
               + "останется в позе привязки. Переимпортируйте модель: "
               + "контроллер собирается сам, при импорте.");
        }

        /// <summary>Сказать один раз за запуск. Крик на каждого из девяти
        /// превращает консоль в мусор, а молчание прячет причину.</summary>
        private static void Told(string line)
        {
            if (_toldAboutMotion) return;
            _toldAboutMotion = true;

            Debug.LogWarning(line);
        }

        private static void Missing(ShellType shell)
        {
            if (_toldAboutMissing) return;
            _toldAboutMissing = true;

            Debug.Log($"[ВИД] Модели нет ({Folder}{shell}) — воины идут кубами. "
                    + "Это не поломка: шов работает, модель просто ещё не положена.");
        }
    }
}
