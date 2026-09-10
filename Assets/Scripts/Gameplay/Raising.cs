// Assets/Scripts/Gameplay/Raising.cs
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Как из души и тела получается воин. Одно место на весь проект.
    ///
    /// Собиралось это до сих пор внутри <see cref="SoulBinding"/>, и пока
    /// поднимать умели только там, всё было честно. С появлением
    /// устройства связывания в склепе мест стало два, и вторая копия
    /// разошлась бы с первой на первой же правке: у одного поднятого
    /// был бы движок решений, у другого нет — и разницу заметили бы
    /// не сразу, а по «этот почему-то не думает».
    ///
    /// Правило проекта уже сформулировано в спавнерах: <b>движок обязан
    /// быть один на всех</b>. Здесь он и живёт.
    /// </summary>
    public static class Raising
    {
        /// <summary>
        /// Поднять воина. Возвращает его или <c>null</c>, если поднять
        /// было не из чего.
        /// </summary>
        public static Warrior Rise(SoulData soul, ShellType shell, Vector3 at,
            Quaternion rotation, RelationshipSystem relations)
        {
            if (soul == null) return null;

            var go = new GameObject(soul.Name);
            go.transform.position = at;
            go.transform.rotation = rotation;

            var warrior = go.AddComponent<Warrior>();
            warrior.Initialize(soul, shell, relations, false, Team.Player);

            // Оснастка та же, что у отряда: ноги, руки, выделение, показ
            // отказа. Поднятый, собранный иначе, вёл бы себя иначе — и это
            // читалось бы как поломка движка, а не как разная сборка.
            WarriorRig.Attach(go);
            go.AddComponent<SoulHarvester>();
            go.AddComponent<SoulBinding>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Тело";
            body.transform.SetParent(go.transform);
            body.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            body.transform.localScale = new Vector3(0.5f, 1.05f, 0.5f);

            // Поднятый вне старта сцены через настройку не проходит:
            // без этого вызова он остался бы телом без движка решений.
            var setup = Object.FindFirstObjectByType<AOS.AOSSceneSetup>();
            if (setup != null) setup.SetupWarrior(go);
            else Debug.LogWarning("[СВЯЗЫВАНИЕ] AOSSceneSetup в сцене нет: "
                                + "поднятый не будет ничего решать.");

            return warrior;
        }
    }
}
