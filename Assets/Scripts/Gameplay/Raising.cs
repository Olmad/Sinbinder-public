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

            // Оболочка видна: поднятый в големе не должен выглядеть
            // как поднятый в скелете — иначе выбор тела у устройства
            // ничего не значит на глаз.
            WarriorLook.Build(go, shell, 1.05f, 0.5f, 0.65f);

            // Поднятый вне старта сцены через настройку не проходит:
            // без этого вызова он остался бы телом без движка решений.
            var setup = Object.FindFirstObjectByType<AOS.AOSSceneSetup>();
            if (setup != null)
            {
                setup.SetupWarrior(go);

                // Поднятие — событие, а игрок видел его молча: тело
                // появлялось, и всё. Просьба автора от 15 сентября —
                // показывать это явно. Слова берутся из души и оболочки
                // (<see cref="RisingWords"/>), жребия там нет.
                //
                // Ведём корутину на настройке сцены: Raising статичен,
                // а наезд камеры — это ожидание. Тот же приём, что
                // у церемонии титула.
                setup.StartCoroutine(Speak(warrior, soul, shell));
            }
            else Debug.LogWarning("[СВЯЗЫВАНИЕ] AOSSceneSetup в сцене нет: "
                                + "поднятый не будет ничего решать.");

            return warrior;
        }

        /// <summary>
        /// Наезд на поднятого и его первая фраза.
        ///
        /// Камера не обязательна: в склепе она есть, на полигоне может
        /// не быть. Нет её — фраза всё равно звучит на нижней полосе,
        /// потому что молчаливое поднятие и было тем, что чинится.
        /// </summary>
        private static System.Collections.IEnumerator Speak(
            Warrior warrior, SoulData soul, ShellType shell)
        {
            if (warrior == null) yield break;

            string line = RisingWords.OnRising(soul, shell);

            var camera = Object.FindFirstObjectByType<Dialogue.DialogueCameraController>();
            if (camera != null)
            {
                camera.SaveCameraPosition();
                yield return camera.FocusOn(warrior.transform);
            }

            UI.Letterbox.Instance?.Say(warrior.DisplayName, line);
            Debug.Log($"[ПОДНЯТИЕ] {warrior.DisplayName}: {line}");

            yield return new WaitForSecondsRealtime(2.6f);

            if (camera != null) yield return camera.RestoreCamera();
        }
    }
}
