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

        /// <summary>Забыть, что уже жаловались. Для проверок.</summary>
        public static void Forget() => _toldAboutMissing = false;

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

                // Модель приходит своего роста, а нам нужен тот, который
                // задал спавнер: рост здесь — не украшение, по нему видно
                // опытного ещё до совета.
                model.transform.localScale = new Vector3(girth, height, girth);

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

        private static void Missing(ShellType shell)
        {
            if (_toldAboutMissing) return;
            _toldAboutMissing = true;

            Debug.Log($"[ВИД] Модели нет ({Folder}{shell}) — воины идут кубами. "
                    + "Это не поломка: шов работает, модель просто ещё не положена.");
        }
    }
}
