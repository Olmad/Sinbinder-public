// Assets/Scripts/Editor/BindingCheck.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Sinbinder.Core;
using Sinbinder.Crypt;
using Sinbinder.Gameplay;

namespace Sinbinder.EditorTools
{
    /// <summary>
    /// Связывание души с телом — проверка запуском.
    ///
    /// `11-MISSING.md` п. 6 называл его «не проверено ничем», и это было
    /// верно: вся цепочка живёт в полигоне `Crypt_Test`, который
    /// <b>не входит в сборку</b>, а в склепе демо мастерская — примитивы.
    /// Читать код мало: цепочка из шести предметов, и каждый передаёт
    /// следующему то, чего у него может не оказаться.
    ///
    /// Идём путём игрока, а не мимо него: подводим Греховода и нажимаем
    /// тем же <c>Use()</c>, которым отвечает предмет на клавишу. Обход
    /// через <c>BindingDevice.PutSoul</c> проверял бы устройство,
    /// а не мастерскую.
    ///
    /// Что проверяем (`14-HANDOFF.md` §110):
    /// <list type="number">
    /// <item>опись — что стоит на полке и стойках и что на них написано;</item>
    /// <item>связывание — свежая душа и скелет, и кто в итоге встал;</item>
    /// <item>отказы — голем без оков и истлевшая душа в тяжёлом теле,
    /// дословно;</item>
    /// <item>кадр поднятого.</item>
    /// </list>
    ///
    /// Отчёт — <c>Logs/binding-check.txt</c>. Окон не открываем и звук
    /// глушим: ПК рабочий, и проверка идёт, пока автор занят другим.
    /// </summary>
    [InitializeOnLoad]
    public static class BindingCheck
    {
        private const string Scene = "Assets/Scenes/Crypt_Test.unity";
        private const string Report = "Logs/binding-check.txt";
        private const string Shots = "Docs/Образцы/связывание";

        private const string Active = "Sinbinder.BindingCheck.Active";
        private const string Index = "Sinbinder.BindingCheck.Index";
        private const string Failed = "Sinbinder.BindingCheck.Failed";

        private sealed class Step
        {
            public string Name;
            public Action Do;
            public Func<bool> Done;
            public float Limit = 10f;
        }

        private static List<Step> _steps;
        private static int _index;
        private static bool _entered;
        private static float _startedAt;
        private static int _failed;

        private static BindingDevice _device;
        private static Warrior _risen;
        private static readonly HashSet<int> _before = new();

        /// <summary>
        /// Возвращение после перезагрузки домена.
        ///
        /// Вход в Play перезагружает домен: всё статическое обнуляется,
        /// подписка на тик пропадает. Первый запуск 25 сентября так
        /// и повис — заголовок отчёта записан, а дальше тишина до утра:
        /// тик после входа в Play не звал никто. Прогон демо это знал
        /// давно (тот же приём), проверка связывания — нет.
        ///
        /// Что пережить обязано, лежит в SessionState: идёт ли проверка,
        /// на каком она шаге и сколько провалов набрала.
        /// </summary>
        static BindingCheck()
        {
            if (!SessionState.GetBool(Active, false)) return;

            _steps = Plan();
            _index = SessionState.GetInt(Index, 0);
            _failed = SessionState.GetInt(Failed, 0);
            _entered = false;

            EditorApplication.update += Tick;
        }

        [MenuItem("Sinbinder/Проверить связывание")]
        public static void Run()
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Report, "");

            // Та же причина, что и у прогона демо: проверка входит в Play
            // со всем звуком игры, а идёт в любое время суток.
            EditorUtility.audioMasterMute = true;

            SessionState.SetBool(Active, true);
            SessionState.SetInt(Index, 0);
            SessionState.SetInt(Failed, 0);

            Write("=== СВЯЗЫВАНИЕ ===");

            if (!File.Exists(Scene))
            {
                Write("[НЕТ СЦЕНЫ] " + Scene + " — проверять нечего.");
                Finish();
                return;
            }

            EditorSceneManager.OpenScene(Scene);
            EditorApplication.isPlaying = true;

            _steps = Plan();
            _index = 0;
            _entered = false;
            _failed = 0;

            EditorApplication.update += Tick;
        }

        // ────────────────────────────── ход ──────────────────────────────

        private static void Tick()
        {
            if (!SessionState.GetBool(Active, false)) return;
            if (!EditorApplication.isPlaying) return;
            if (_steps == null || _index >= _steps.Count) { Finish(); return; }

            var step = _steps[_index];

            if (!_entered)
            {
                _entered = true;
                _startedAt = Time.realtimeSinceStartup;

                try { step.Do?.Invoke(); }
                catch (Exception e)
                {
                    // Провал, а не сноска: первый прогон записал исключение
                    // и следом «ГОТОВО» тому же шагу — и итог «пройдено
                    // целиком», хотя кадра поднятого так и не было.
                    _failed++;
                    SessionState.SetInt(Failed, _failed);
                    Write("  [ОШИБКА ШАГА] " + step.Name + ": "
                        + (e.InnerException ?? e).Message);
                }
            }

            bool done;
            try { done = step.Done == null || step.Done(); }
            catch (Exception e)
            {
                Write("  [ОШИБКА ПРОВЕРКИ] " + step.Name + ": " + e.Message);
                done = false;
            }

            float spent = Time.realtimeSinceStartup - _startedAt;

            if (done)
            {
                Write("  [ГОТОВО] " + step.Name);
                Next();
                return;
            }

            if (spent < step.Limit) return;

            _failed++;
            SessionState.SetInt(Failed, _failed);
            Write("  [ЗАСТРЯЛО] " + step.Name + " — не дождались за "
                + step.Limit.ToString("F0") + " с");
            Next();
        }

        private static void Next()
        {
            _index++;
            _entered = false;
            SessionState.SetInt(Index, _index);
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Active, false);

            Write(_failed == 0 ? "=== ПРОЙДЕНО ЦЕЛИКОМ ===" : $"=== ПРОВАЛОВ: {_failed} ===");

            EditorApplication.isPlaying = false;
            EditorApplication.Exit(_failed == 0 ? 0 : 1);
        }

        private static void Write(string line) => File.AppendAllText(Report, line + "\n");

        // ────────────────────────────── план ──────────────────────────────

        private static List<Step> Plan()
        {
            return new List<Step>
            {
                new Step
                {
                    Name = "полигон загрузился",

                    // В полигоне тот же вопрос о сохранении, что и в лагере,
                    // и пока на него не ответили, игра стоит на паузе.
                    // Отвечаем, как ответил бы игрок, — каждый кадр, пока висит.
                    Done = () =>
                    {
                        Sinbinder.UI.StartPanel.ChooseFresh();

                        return !Sinbinder.UI.StartPanel.Waiting
                            && SinbinderPlayer.Exists
                            && UnityEngine.Object.FindFirstObjectByType<BindingDevice>() != null;
                    },
                    Limit = 30f,
                },

                new Step
                {
                    Name = "опись: что стоит в мастерской",
                    Do = Inventory,
                    Done = () => true,
                },

                new Step
                {
                    Name = "отказы названы словами, а не серой кнопкой",
                    Do = Refusals,
                    Done = () => true,
                },

                new Step
                {
                    Name = "душа взята с полки",
                    Do = TakeSoul,
                    Done = () => CryptHands.HasSoul,
                },

                new Step
                {
                    Name = "душа положена в гнездо",
                    Do = () => Press(Socket(BindingSocket.Slot.Soul)),
                    Done = () => Device() != null && Device().HasSoul,
                },

                new Step
                {
                    Name = "тело взято со стойки",
                    Do = TakeShell,
                    Done = () => CryptHands.HasShell,
                },

                new Step
                {
                    Name = "тело положено в ложе",
                    Do = () => Press(Socket(BindingSocket.Slot.Shell)),
                    Done = () => Device() != null && Device().HasShell,
                },

                new Step
                {
                    Name = "устройство готово и говорит, что выйдет",
                    Do = Foretell,
                    Done = () => Device() != null && string.IsNullOrEmpty(Device().NotReady),
                },

                new Step
                {
                    Name = "рычаг дёрнут",
                    Do = Pull,
                    Done = () => Risen() != null,
                    Limit = 15f,
                },

                new Step
                {
                    Name = "поднятый осмотрен",
                    Do = Examine,
                    Done = () => true,
                    Limit = 15f,
                },

                new Step
                {
                    Name = "поднятый идёт по приказу",
                    Do = Order,
                    Done = Walked,
                    Limit = 12f,
                },
            };
        }

        // ───────────────────────────── шаги ─────────────────────────────

        /// <summary>
        /// Опись: что игрок увидит, подойдя. Надписи берём те самые,
        /// которые читает и табличка, и подсказка, — не пересказ.
        /// </summary>
        private static void Inventory()
        {
            var jars = UnityEngine.Object.FindObjectsByType<SoulJar>(FindObjectsSortMode.None);
            Write($"  полка: банок {jars.Length}");

            foreach (var jar in jars)
            {
                string what = jar.Soul == null
                    ? "пусто"
                    : $"{jar.Soul.Name}, {SoulData.GetSinName(jar.Soul.Sin)}, {jar.Quality}";

                Write("    • " + what);
            }

            var stands = UnityEngine.Object.FindObjectsByType<ShellStand>(FindObjectsSortMode.None);
            Write($"  стойки тел: {stands.Length}");
            foreach (var stand in stands) Write("    • " + Flat(stand.Label));

            var things = UnityEngine.Object.FindObjectsByType<CryptInteractable>(FindObjectsSortMode.None);
            Write($"  всего предметов под рукой: {things.Length}");

            // Запомним, кто был до связывания: поднятого узнаём по тому,
            // что его раньше не было, а не по имени — имя мы и проверяем.
            _before.Clear();
            foreach (var w in UnityEngine.Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                if (w != null) _before.Add(w.GetInstanceID());

            Write($"  воинов в сцене до связывания: {_before.Count}");
        }

        /// <summary>
        /// Отказы — дословно. Их ценность в том, что они названы словами:
        /// «нельзя» без причины игрок читает как поломку игры.
        /// </summary>
        private static void Refusals()
        {
            var device = Device();
            if (device == null) { Write("  [НЕТ УСТРОЙСТВА]"); return; }

            Write("  пустое устройство говорит: «" + device.NotReady + "»");

            // Голем без оков. Спрашиваем то же правило, что спросит
            // устройство, — не копию.
            string golem = CryptUpgrades.WhyNot(ShellType.Golem);
            Write("  голем без оков: «" + (string.IsNullOrEmpty(golem) ? "разрешён" : golem) + "»");

            // Истлевшая душа в тяжёлом теле: «одна воля» — самая слабая.
            var heavy = ShellLibrary.Get(ShellType.Golem) ?? ShellLibrary.Get(ShellType.Zombie);
            if (heavy != null)
            {
                string no = ShellChoice.Refusal(heavy, SoulQuality.Dissolved);
                Write("  истлевшая душа в тяжёлом теле: «"
                    + (string.IsNullOrEmpty(no) ? "пускает" : no) + "»");
            }
        }

        private static void TakeSoul()
        {
            var jars = UnityEngine.Object.FindObjectsByType<SoulJar>(FindObjectsSortMode.None);

            SoulJar best = null;
            foreach (var jar in jars)
            {
                if (jar.Soul == null) continue;
                if (best == null || jar.Quality < best.Quality) best = jar;   // Shock — самая свежая
            }

            if (best == null) { Write("  [ПОЛКА ПУСТА] брать нечего"); return; }

            Write($"  берём: {best.Soul.Name} ({best.Quality})");
            Press(best);
        }

        private static void TakeShell()
        {
            var stands = UnityEngine.Object.FindObjectsByType<ShellStand>(FindObjectsSortMode.None);

            ShellStand skeleton = null;
            foreach (var stand in stands)
                if (stand.Shell == ShellType.Skeleton) { skeleton = stand; break; }

            if (skeleton == null && stands.Length > 0) skeleton = stands[0];
            if (skeleton == null) { Write("  [НЕТ СТОЕК]"); return; }

            Write("  берём тело: " + skeleton.Shell);
            Press(skeleton);
        }

        private static void Foretell()
        {
            var device = Device();
            if (device == null) return;

            string say = device.Foretell();
            Write("  предсказание: «" + (string.IsNullOrEmpty(say) ? "молчит" : say) + "»");

            string no = device.NotReady;
            if (!string.IsNullOrEmpty(no)) Write("  устройство не готово: «" + no + "»");
        }

        private static void Pull()
        {
            var handle = UnityEngine.Object.FindFirstObjectByType<BindingHandle>();
            if (handle == null) { Write("  [НЕТ РЫЧАГА]"); return; }

            Press(handle);
        }

        /// <summary>
        /// Кто встал. Проверяем не «появился объект», а то, что делает
        /// его воином: облик, навмеш, выделение, приказ. Поднятый,
        /// который стоит и не слушается, — не воин, а декорация.
        /// </summary>
        private static void Examine()
        {
            var risen = Risen();
            if (risen == null) { Write("  [НИКТО НЕ ВСТАЛ]"); return; }

            _risen = risen;

            Write($"  встал: {risen.DisplayName}, команда {risen.Team}");

            var soul = risen.Soul;
            if (soul != null)
                Write($"    душа: {soul.Name}, {SoulData.GetSinName(soul.Sin)}");

            var skin = risen.GetComponentInChildren<SkinnedMeshRenderer>();
            Write("    облик: " + (skin == null ? "НЕТ МОДЕЛИ" : skin.name));

            var agent = risen.GetComponent<NavMeshAgent>();
            Write("    навмеш: " + (agent == null
                ? "нет агента"
                : agent.isOnNavMesh ? "стоит на нём" : "АГЕНТ ВНЕ НАВМЕША"));

            var damage = risen.GetComponent<Damageable>();
            Write("    здоровье: " + (damage == null ? "нет" : $"{damage.HP:0}/{damage.MaxHP:0}"));

            // Выделение и приказ — тем же способом, что и у игрока.
            var picker = UnityEngine.Object.FindFirstObjectByType<SelectionManager>();
            var mark = risen.GetComponent<SelectionComponent>();

            if (mark == null)
            {
                Write("    выделение: НЕЛЬЗЯ — нет SelectionComponent, рамкой его не взять");
            }
            else if (picker != null)
            {
                var select = typeof(SelectionManager).GetMethod("SelectUnit",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                select?.Invoke(picker, new object[] { mark });
                Write("    выделение: " + (select == null ? "способа нет" : "выделен"));
            }

            Shot(risen);
        }

        private static Vector3 _orderedFrom;

        /// <summary>
        /// Приказ «иди туда» — тем же вызовом, что и правая кнопка игрока
        /// (<c>SelectionManager</c>, <c>IssueCommand</c>).
        ///
        /// Приказ здесь — голос, а не команда: воин вправе отказаться,
        /// и отказ — это игра, а не поломка. Поэтому отчёт говорит, что
        /// случилось, а решает читающий. Но свежеподнятый, которому
        /// не грозит ничто, стоять на месте не должен: такое чаще значит
        /// сломанный агент, чем характер.
        /// </summary>
        private static void Order()
        {
            var risen = _risen != null ? _risen : Risen();
            if (risen == null) { Write("  [НЕКОМУ ПРИКАЗЫВАТЬ]"); return; }

            _risen = risen;
            _orderedFrom = risen.transform.position;

            var to = _orderedFrom + risen.transform.forward * 3f;
            risen.IssueCommand(CommandKind.Move, to);
            Write("  приказ: идти на три метра вперёд");
        }

        private static bool Walked()
        {
            if (_risen == null) return false;

            float gone = Vector3.Distance(_risen.transform.position, _orderedFrom);
            if (gone < 1f) return false;

            Write($"    прошёл {gone:0.0} м");
            return true;
        }

        // ──────────────────────────── помощники ────────────────────────────

        private static BindingDevice Device()
            => _device != null ? _device
             : _device = UnityEngine.Object.FindFirstObjectByType<BindingDevice>();

        private static BindingSocket Socket(BindingSocket.Slot which)
        {
            foreach (var socket in UnityEngine.Object.FindObjectsByType<BindingSocket>(
                         FindObjectsSortMode.None))
            {
                var field = typeof(BindingSocket).GetField("_slot",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (field != null && field.GetValue(socket) is BindingSocket.Slot slot
                    && slot == which) return socket;
            }

            return null;
        }

        /// <summary>
        /// Нажать предмет так, как нажал бы игрок: подвести Греховода
        /// вплотную и позвать его же <c>Use()</c>. Дальность предмет
        /// проверяет сам в <c>Update</c>, и подходить всё равно нужно —
        /// иначе проверка доказывала бы работу метода, а не мастерской.
        /// </summary>
        private static void Press(CryptInteractable thing)
        {
            if (thing == null) { Write("  [НЕТ ПРЕДМЕТА]"); return; }

            HeroTo(thing.transform);

            var use = thing.GetType().GetMethod("Use",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (use == null) { Write("  [НЕТ Use] " + thing.name); return; }

            use.Invoke(thing, null);
        }

        private static void HeroTo(Transform what)
        {
            var hero = SinbinderPlayer.Instance;
            if (hero == null || what == null) return;

            var to = what.position;
            var from = hero.transform.position;

            var step = (from - to);
            step.y = 0f;

            if (step.sqrMagnitude < 0.01f) step = Vector3.back;

            hero.transform.position = to + step.normalized * 1.2f;
            hero.transform.rotation = Quaternion.LookRotation((to - hero.transform.position).normalized);
        }

        private static Warrior Risen()
        {
            foreach (var w in UnityEngine.Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                if (w != null && !_before.Contains(w.GetInstanceID())) return w;

            return null;
        }

        private static void Shot(Warrior who)
        {
            try
            {
                Sinbinder.Utilets.Snapshot.Portrait(Shots, "поднятый — " + who.DisplayName,
                                                    who.transform);
            }
            catch (Exception e)
            {
                Write("  [СНИМОК НЕ ВЫШЕЛ] " + e.Message);
            }
        }

        private static string Flat(string text)
            => string.IsNullOrEmpty(text) ? "" : text.Replace("\n", " · ");
    }
}
