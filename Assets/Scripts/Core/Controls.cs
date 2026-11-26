// Assets/Scripts/Core/Controls.cs
// Перевод: текст через Loc
//
// Настройка управления (автор, 26.11: «необходима настройка управления, чтобы можно было переназначить кнопки»).
// Одно место, где живут клавиши игровых действий: код спрашивает Controls.Down(Act.Interact), а не
// Input.GetKeyDown(KeyCode.F). Выбор игрока — в Prefs (на флешке — файлом, см. DataFolder); страница «Управление» в меню паузы (PauseMenu).
// Подсказки вида «F — …» переписываются под выбранную клавишу (Controls.Relabel).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Core
{
    /// <summary>Игровые действия, которые можно переназначить. Порядок — порядок строк на странице.</summary>
    public enum Act
    {
        Forward, Back, Left, Right,
        Interact, Harvest, Raise, NextJar, Drop, Inventory, Map,
        View, Role, Whisper, Skill,
        OrderMove, OrderAttack, OrderFallBack, OrderPatrol, StanceHold, StanceDefend, StanceFree, Review,
    }

    public static class Controls
    {
        private static readonly Dictionary<Act, KeyCode> Defaults = new()
        {
            { Act.Forward, KeyCode.W }, { Act.Back, KeyCode.S }, { Act.Left, KeyCode.A }, { Act.Right, KeyCode.D },
            { Act.Interact, KeyCode.F }, { Act.Harvest, KeyCode.E }, { Act.Raise, KeyCode.R }, { Act.NextJar, KeyCode.J },
            { Act.Drop, KeyCode.G }, { Act.Inventory, KeyCode.I }, { Act.Map, KeyCode.K },
            { Act.View, KeyCode.Tab }, { Act.Role, KeyCode.V }, { Act.Whisper, KeyCode.Z }, { Act.Skill, KeyCode.Q },
            { Act.OrderMove, KeyCode.M }, { Act.OrderAttack, KeyCode.T }, { Act.OrderFallBack, KeyCode.X },
            { Act.OrderPatrol, KeyCode.P }, { Act.StanceHold, KeyCode.H }, { Act.StanceDefend, KeyCode.G },
            { Act.StanceFree, KeyCode.C }, { Act.Review, KeyCode.U },
        };

        /// <summary>Действия одной клавиши в разных обстоятельствах — не спор: G бросает вещь из рук, а без вещи — «защищать».</summary>
        private static readonly HashSet<(Act, Act)> Shared = new() { (Act.Drop, Act.StanceDefend), (Act.StanceDefend, Act.Drop) };

        private static readonly Dictionary<Act, KeyCode> Keys = new();
        private static bool _loaded;
        private static bool _moved;   // хоть одна клавиша не как задумано — подсказкам есть что менять

        /// <summary>Клавиши сменились — подсказкам перерисоваться.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { _loaded = false; _moved = false; Keys.Clear(); Pretend.Clear(); Changed = null; _lastIn = _lastOut = null; }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            Keys.Clear();
            foreach (var kv in Defaults)
            {
                int saved = -1;
                try { saved = Prefs.GetInt(Pref(kv.Key), -1); } catch { }
                Keys[kv.Key] = saved > 0 && Enum.IsDefined(typeof(KeyCode), saved) ? (KeyCode)saved : kv.Value;
            }
            Moved();
        }

        private static string Pref(Act a) => "keys." + a;

        private static void Moved()
        {
            _moved = false;
            foreach (var kv in Defaults) if (Keys[kv.Key] != kv.Value) { _moved = true; break; }
            _lastIn = _lastOut = null;
        }

        public static KeyCode Key(Act a) { Load(); return Keys.TryGetValue(a, out var k) ? k : KeyCode.None; }
        public static KeyCode Default(Act a) => Defaults.TryGetValue(a, out var k) ? k : KeyCode.None;
        public static bool Down(Act a) => Input.GetKeyDown(Key(a));
        public static bool Held(Act a) => Input.GetKey(Key(a)) || Pretend.Contains(a);

        /// <summary>Рука проверок (Editor/CourtHands): «держит» клавиши хода, как игрок. В игре пусто.</summary>
        public static readonly HashSet<Act> Pretend = new();

        /// <summary>
        /// Назначить. Клавиша занята другим действием — они меняются местами: двух действий на одной клавише
        /// без нужды не бывает, а оставить одно без клавиши — потерять его. Вернёт действие, с которым поменялись.
        /// </summary>
        public static Act? Bind(Act a, KeyCode key)
        {
            Load();
            if (!Bindable(key)) return null;
            Act? swapped = null;
            var was = Key(a);
            foreach (var other in (Act[])Enum.GetValues(typeof(Act)))
                if (other != a && Keys[other] == key && !Shared.Contains((a, other)))
                { Keys[other] = was; Save(other); swapped = other; }
            Keys[a] = key;
            Save(a);
            Moved();
            Changed?.Invoke();
            return swapped;
        }

        public static void Reset()
        {
            Load();
            foreach (var kv in Defaults) { Keys[kv.Key] = kv.Value; try { Prefs.DeleteKey(Pref(kv.Key)); } catch { } }
            Moved();
            Changed?.Invoke();
        }

        private static void Save(Act a) { try { Prefs.SetInt(Pref(a), (int)Keys[a]); Prefs.Save(); } catch { } }

        /// <summary>Esc — меню и «пропустить», цифры — ответы в разговорах, F1–F12 — записи и служебное: не отдаём.</summary>
        public static bool Bindable(KeyCode k)
        {
            if (k == KeyCode.None || k == KeyCode.Escape) return false;
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return false;
            if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return false;
            if (k >= KeyCode.F1 && k <= KeyCode.F15) return false;
            if (k >= KeyCode.Mouse0 && k <= KeyCode.Mouse1) return false;   // щелчки — выбор и приказ
            if (k >= KeyCode.JoystickButton0) return false;
            return true;
        }

        /// <summary>Что игрок видит на клавише.</summary>
        public static string Name(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.None: return "—";
                case KeyCode.Space: return Loc.T("Пробел");
                case KeyCode.Tab: return "Tab";
                case KeyCode.Return: return "Enter";
                case KeyCode.LeftShift: case KeyCode.RightShift: return "Shift";
                case KeyCode.LeftControl: case KeyCode.RightControl: return "Ctrl";
                case KeyCode.LeftAlt: case KeyCode.RightAlt: return "Alt";
                case KeyCode.Mouse2: return Loc.T("Колесо");
                case KeyCode.Mouse3: return Loc.T("Мышь 4");
                case KeyCode.Mouse4: return Loc.T("Мышь 5");
                case KeyCode.BackQuote: return "`";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Slash: return "/";
                case KeyCode.Backslash: return "\\";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.UpArrow: return "↑";
                case KeyCode.DownArrow: return "↓";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.RightArrow: return "→";
            }
            return k.ToString();
        }

        public static string Name(Act a) => Name(Key(a));

        /// <summary>Строка страницы «Управление».</summary>
        public static string Title(Act a)
        {
            switch (a)
            {
                case Act.Forward: return Loc.T("Вперёд");
                case Act.Back: return Loc.T("Назад");
                case Act.Left: return Loc.T("Влево");
                case Act.Right: return Loc.T("Вправо");
                case Act.Interact: return Loc.T("Взаимодействие");
                case Act.Harvest: return Loc.T("Собрать душу");
                case Act.Raise: return Loc.T("Поднять из банки");
                case Act.NextJar: return Loc.T("Следующая банка");
                case Act.Drop: return Loc.T("Бросить вещь из рук");
                case Act.Inventory: return Loc.T("Вещи и снаряжение");
                case Act.Map: return Loc.T("Мини-карта");
                case Act.View: return Loc.T("Сверху или глазами");
                case Act.Role: return Loc.T("Командир или Воин");
                case Act.Whisper: return Loc.T("Шёпот");
                case Act.Skill: return Loc.T("Умение выбранного");
                case Act.OrderMove: return Loc.T("Приказ: идти");
                case Act.OrderAttack: return Loc.T("Приказ: атаковать");
                case Act.OrderFallBack: return Loc.T("Приказ: отступить");
                case Act.OrderPatrol: return Loc.T("Приказ: дозор");
                case Act.StanceHold: return Loc.T("Стойка: стоять");
                case Act.StanceDefend: return Loc.T("Стойка: защищать");
                case Act.StanceFree: return Loc.T("Стойка: свободно");
                case Act.Review: return Loc.T("Пересмотреть суд");
            }
            return a.ToString();
        }

        private static string _lastIn, _lastOut;

        /// <summary>
        /// Подсказка с клавишей по умолчанию («F — говорить», «(J)», «· K», «W, A, S, D — идти») — под выбранную.
        /// Меняется только буква-клавиша отдельным словом: «F — …» в начале, «(F)» в скобках, «· K» в конце,
        /// «W, A, S, D» и «WASD» целиком. Пока клавиши как задуманы — строка та же.
        /// </summary>
        public static string Relabel(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            Load();
            if (!_moved) return text;
            if (ReferenceEquals(text, _lastIn) || text == _lastIn) return _lastOut;
            string src = text;
            // Одним проходом по словам-клавишам: при обмене (F↔E) поочерёдная замена вернула бы F на место.
            int dash = text.IndexOf(" — ", StringComparison.Ordinal);
            if (dash > 0 && dash <= 6) text = KeyWord(text.Substring(0, dash)) + text.Substring(dash);
            var sb = new System.Text.StringBuilder(text.Length + 8);
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('(', i);
                if (open < 0) { sb.Append(text, i, text.Length - i); break; }
                int close = text.IndexOf(')', open + 1);
                if (close < 0 || close - open - 1 > 6) { sb.Append(text, i, open + 1 - i); i = open + 1; continue; }
                sb.Append(text, i, open + 1 - i).Append(KeyWord(text.Substring(open + 1, close - open - 1))).Append(')');
                i = close + 1;
            }
            text = sb.ToString();
            int dot = text.LastIndexOf("· ", StringComparison.Ordinal);
            if (dot >= 0 && text.Length - dot - 2 <= 6) text = text.Substring(0, dot + 2) + KeyWord(text.Substring(dot + 2));
            if (text.Contains("W, A, S, D")) text = text.Replace("W, A, S, D", Walk(", "));
            _lastIn = src;
            _lastOut = text;
            return text;
        }

        /// <summary>Четыре клавиши хода через разделитель («W, A, S, D»).</summary>
        private static string Walk(string sep)
            => Name(Act.Forward) + sep + Name(Act.Left) + sep + Name(Act.Back) + sep + Name(Act.Right);

        /// <summary>Слово-клавиша по умолчанию («F», «Tab», «WASD») → нынешняя. Не клавиша — как было.</summary>
        public static string KeyWord(string token)
        {
            if (string.IsNullOrEmpty(token)) return token;
            Load();
            if (!_moved) return token;
            if (token == "WASD")
            {
                string w = Walk("");
                return w.Length == 4 ? w : Walk(" ");
            }
            foreach (var kv in Defaults) if (Name(kv.Value) == token) return Name(Keys[kv.Key]);
            return token;
        }
    }
}