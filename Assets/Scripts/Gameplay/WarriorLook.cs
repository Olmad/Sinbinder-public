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
        public static void Forget()
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
                model.transform.localScale = Vector3.one * height;

                Animate(model, shell);

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
