// Assets/Scripts/Gameplay/Raising.cs
// Перевод: текст через Loc
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
        /// <summary>Кожа свежего мертвеца: серая, без крови, чуть в зелень.
        /// Живая — 0,435/0,378/0,335 (bodies.py); эта темнее и холоднее.</summary>
        private static readonly Color DeadSkin = new Color(0.34f, 0.355f, 0.335f);

        /// <summary>
        /// Поднять воина. Возвращает его или <c>null</c>, если поднять
        /// было не из чего.
        /// </summary>
        public static Warrior Rise(SoulData soul, ShellType shell, Vector3 at,
            Quaternion rotation, RelationshipSystem relations, Warrior corpse = null, string words = null)
        {
            if (soul == null) return null;

            var go = new GameObject(soul.Name);
            go.transform.position = at;
            go.transform.rotation = rotation;

            var warrior = go.AddComponent<Warrior>();
            // «Перетягивание»: своих отрядов нет — поднятый ничей, но верен поднявшему (BindingDevice).
            warrior.Initialize(soul, shell, relations, false, Lords.Active ? Team.Neutral : Team.Player);
            AOS.MemoryProcessor.Instance?.InstallRaisedMemories(warrior);

            // Душа донесла имя через смерть (SoulData.EarnedTitle) — и в новом
            // теле его носят снова. Первая фраза так и просит: «Зовите снова».
            AOS.TitleManager.Bestow(warrior, soul.EarnedTitle);

            // Оснастка та же, что у отряда: ноги, руки, выделение, показ
            // отказа. Поднятый, собранный иначе, вёл бы себя иначе — и это
            // читалось бы как поломка движка, а не как разная сборка.
            WarriorRig.Attach(go);
            go.AddComponent<SoulHarvester>();

            // Оболочка видна: поднятый в големе не должен выглядеть
            // как поднятый в скелете — иначе выбор тела у устройства
            // ничего не значит на глаз.
            //
            // Свежее тело — другое дело (автор, 08.11: «только что умерший воин
            // становится разлагающимся зомби, уже потеряв куски плоти и даже всё
            // мясо с руки»). Оболочка та же — душа в мёртвом теле, — но тело его
            // же: рост, лицо и одежда того, кто только что лежал, кожа мёртвая,
            // а в глазах грех (SinEyes). Гниль приходит со временем, не сразу.
            if (corpse != null)
            {
                var old = corpse.transform.Find("Тело");
                float tall = old != null && old.localScale.y > 0f ? old.localScale.y : 1.05f;
                float thick = old != null && old.localScale.y > 0f ? old.localScale.x / tall * 0.5f : 0.5f;
                var body = WarriorLook.Build(go, corpse.Shell, tall, thick, 0.65f, dressAs: corpse);
                if (body != null) Wardrobe.Tint(body.gameObject, "Skin", DeadSkin);
            }
            else WarriorLook.Build(go, shell, 1.05f, 0.5f, 0.65f);

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
                setup.StartCoroutine(Speak(warrior, soul, shell, words));   // words — первая фраза, если её выбрал зовущий (печати сна)
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
        private static string _lastOpening;
        private static float _lastOpeningAt = -999f;

        private static System.Collections.IEnumerator Speak(
            Warrior warrior, SoulData soul, ShellType shell, string words = null)
        {
            if (warrior == null) yield break;

            string line = words ?? RisingWords.OnRising(soul, shell);

            // Пятеро стражей у печатей сна поднимаются подряд, и каждый начинал одним и тем же
            // «Голодно. С самого начала — голодно.» (кадры 25.11). Слова тела у всех в оболочке одни:
            // прозвучали только что — поднятый говорит своё, то, что от души и ремесла.
            string opening = RisingWords.Opening(soul, shell);
            if (!string.IsNullOrEmpty(opening) && opening == _lastOpening && Time.unscaledTime - _lastOpeningAt < 90f
                && line.StartsWith(opening + " "))
                line = line.Substring(opening.Length + 1);
            _lastOpening = opening;
            _lastOpeningAt = Time.unscaledTime;

            var camera = Object.FindFirstObjectByType<Dialogue.DialogueCameraController>();
            if (camera != null)
            {
                // Поднятому спешить некуда: он ждал дольше. Если кадр
                // занят разговором у устройства связывания, дождёмся —
                // иначе первая фраза легла бы поверх чужой и забрала
                // бы себе дом камеры.
                yield return camera.WaitUntilFree();

                // Ждали в живой игре, и ждать могли до двенадцати секунд:
                // за это время поднятый мог исчезнуть вместе со сценой.
                // Дальше идут warrior.transform и warrior.DisplayName,
                // а обращение к уничтоженному MonoBehaviour бросает
                // MissingReferenceException. Та же перепроверка стоит
                // в церемонии; здесь её сперва не было — ожидание
                // я добавил в оба места, а перепроверку в одно.
                if (warrior == null) yield break;

                camera.SaveCameraPosition();
                yield return camera.FocusOn(warrior.transform);
            }

            UI.Letterbox.Instance?.Say(Core.Loc.Name(warrior.DisplayName), line);
            Debug.Log($"[ПОДНЯТИЕ] {warrior.DisplayName}: {line}");

            // Столько, чтобы успеть прочесть: печать (40 букв в секунду) и чтение (~12).
            // Было ровно 2,6 с на любую строку (автор, 26.10: «фраза при поднятии
            // воина быстро пропадает»).
            int letters = string.IsNullOrEmpty(line) ? 0 : line.Length;
            yield return UI.Letterbox.Hold(Mathf.Clamp(1.8f + letters / 12f, 3.5f, 9f));

            if (camera != null) yield return camera.RestoreCamera();
        }
    }
}
