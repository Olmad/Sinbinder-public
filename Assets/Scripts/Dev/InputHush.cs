// Assets/Scripts/Dev/InputHush.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sinbinder.Dev
{
    /// <summary>
    /// Заглушить клавиши игры, пока ими распоряжается кто-то другой:
    /// консоль (<see cref="CheatConsole"/>) или съёмка (<see cref="Shooting"/>).
    ///
    /// Ввод в проекте — старый <c>Input</c>, и он не знает, кто сейчас
    /// «владеет» клавиатурой: набирая в консоли «aos», игрок водил бы
    /// камеру на A и S и открывал бы ступени ясности на O. Поэтому
    /// слушателей клавиш на время выключают, а потом возвращают такими,
    /// какими были: выключенный до нас остаётся выключенным.
    ///
    /// Хозяев может быть двое сразу — консоль открыта поверх съёмки.
    /// Компонент молчит, пока его глушит хоть один.
    /// </summary>
    public static class InputHush
    {
        /// <summary>Кто глушит и заодно ли меню паузы.</summary>
        private static readonly Dictionary<object, bool> Holders = new();

        /// <summary>Каким компонент был до нас.</summary>
        private static readonly Dictionary<Behaviour, bool> Was = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm()
        {
            Holders.Clear();
            Was.Clear();
            SceneManager.sceneLoaded -= Forget;
            SceneManager.sceneLoaded += Forget;
        }

        /// <summary>Сцена сменилась — прежние компоненты ушли вместе с ней.</summary>
        private static void Forget(Scene scene, LoadSceneMode mode)
        {
            Holders.Clear();
            Was.Clear();
        }

        /// <summary>Заглушить игру. <paramref name="menuToo"/> — и меню паузы на Esc.</summary>
        public static void Hold(object owner, bool menuToo)
        {
            Holders[owner] = menuToo;
            Apply();
        }

        public static void Release(object owner)
        {
            if (Holders.Remove(owner)) Apply();
        }

        private static void Apply()
        {
            bool any = Holders.Count > 0;
            bool menu = false;
            foreach (var pair in Holders) menu |= pair.Value;

            foreach (var b in Listeners(menu: false)) Set(b, any);
            foreach (var b in Listeners(menu: true)) Set(b, menu);
        }

        private static void Set(Behaviour b, bool hush)
        {
            if (b == null) return;

            if (hush)
            {
                if (!Was.ContainsKey(b)) Was[b] = b.enabled;
                b.enabled = false;
                return;
            }

            if (Was.TryGetValue(b, out bool was))
            {
                b.enabled = was;
                Was.Remove(b);
            }
        }

        /// <summary>
        /// Кто в игре слушает клавиши. Список короткий и явный: новый
        /// слушатель, не внесённый сюда, будет слышать набор в консоли —
        /// заметно сразу, при первой же попытке.
        /// </summary>
        private static IEnumerable<Behaviour> Listeners(bool menu)
        {
            if (menu)
            {
                foreach (var b in Find<UI.PauseMenu>()) yield return b;
                yield break;
            }

            foreach (var b in Find<Gameplay.RTS_Camera>()) yield return b;
            foreach (var b in Find<Gameplay.SelectionManager>()) yield return b;
            foreach (var b in Find<Gameplay.PlayerWalk>()) yield return b;
            foreach (var b in Find<Gameplay.SatchelHands>()) yield return b;
            foreach (var b in Find<Gameplay.SoulHarvester>()) yield return b;
            foreach (var b in Find<UI.SquadStrategyUI>()) yield return b;
            foreach (var b in Find<UI.ClarityPanel>()) yield return b;
            foreach (var b in Find<UI.CheatSpawner>()) yield return b;
            foreach (var b in Find<UI.GearPanel>()) yield return b;
            foreach (var b in Find<AOS.DebugAOS>()) yield return b;
            foreach (var b in Find<Crypt.CryptInteractable>()) yield return b;
        }

        private static T[] Find<T>() where T : Behaviour
            => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }
}
