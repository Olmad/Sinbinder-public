// Assets/Scripts/Editor/DemoSmoke.cs
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sinbinder.EditorTools
{
    /// <summary>
    /// Прогон сцен демо в игре, без человека: каждая сцена играется
    /// несколько секунд, и всё, что упало, записывается.
    ///
    /// Заведён 13 сентября перед показом демо большому числу людей.
    /// Самопроверка движка проверяет логику, <c>check.py</c> — код,
    /// но ни то ни другое не запускает сцену: исключение в <c>Start</c>
    /// у спавнера или у интерфейса видно только в игре. А на показе оно
    /// видно всем.
    ///
    /// Прогон не заменяет прохождения руками: он не нажимает кнопок
    /// совета и не водит отряд. Он ловит то, что падает само.
    ///
    /// Запуск: <c>-executeMethod Sinbinder.EditorTools.DemoSmoke.Run</c>
    /// без <c>-quit</c> — выходит сам, когда сцены кончатся.
    /// Итог — в <c>Logs/demo-smoke.txt</c>.
    /// </summary>
    [InitializeOnLoad]
    public static class DemoSmoke
    {
        private static readonly string[] Scenes =
        {
            "Assets/Scenes/Prologue_Camp.unity",
            "Assets/Scenes/Prologue_Raid.unity",
            "Assets/Scenes/Crypt_Entrance.unity",
        };

        private const float Seconds = 40f;

        // Всё состояние — в SessionState, а не в полях. Вход в игру
        // перезагружает скрипты, и статические поля с подписками
        // обнуляются: первая версия так и застряла в первой сцене,
        // забыв, что ждёт.
        private const string Active = "Sinbinder.DemoSmoke.Active";
        private const string Index = "Sinbinder.DemoSmoke.Index";
        private const string Until = "Sinbinder.DemoSmoke.Until";

        private const string Report = "Logs/demo-smoke.txt";

        private static readonly string NL = System.Environment.NewLine;

        static DemoSmoke()
        {
            if (!SessionState.GetBool(Active, false)) return;

            Application.logMessageReceived += Catch;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            File.WriteAllText(Report, "");
            SessionState.SetBool(Active, true);
            SessionState.SetInt(Index, 0);
            SessionState.SetFloat(Until, 0f);

            Application.logMessageReceived += Catch;
            EditorApplication.update += Tick;

            Open(0);
        }

        private static void Write(string line) => File.AppendAllText(Report, line + NL);

        private static void Open(int i)
        {
            if (i >= Scenes.Length)
            {
                Write("=== КОНЕЦ ===");
                SessionState.SetBool(Active, false);
                EditorApplication.Exit(0);
                return;
            }

            _frameStep = 0;
            EditorSceneManager.OpenScene(Scenes[i]);
            Write("=== " + Scenes[i] + " ===");
            SessionState.SetFloat(Until, 0f);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Active, false)) return;

            if (EditorApplication.isPlaying)
            {
                float until = SessionState.GetFloat(Until, 0f);
                if (until <= 0f)
                {
                    SessionState.SetFloat(Until, (float)EditorApplication.timeSinceStartup + Seconds);
                    return;
                }

                Frame(until - (float)EditorApplication.timeSinceStartup);

                if (EditorApplication.timeSinceStartup < until) return;

                Write("  секунд игры: " + Time.time.ToString("F1"));
                SessionState.SetFloat(Until, -1f);
                EditorApplication.isPlaying = false;
                return;
            }

            // Вышли из игры после отсчёта — следующая сцена.
            if (SessionState.GetFloat(Until, 0f) < 0f && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                int next = SessionState.GetInt(Index, 0) + 1;
                SessionState.SetInt(Index, next);
                Open(next);
            }
        }

        // Внутри одной игры скрипты не перезагружаются, так что фаза
        // проверки полос может жить в поле.
        private static int _frameStep;
        private static float _frameAt;
        private static bool _sawTalk;

        /// <summary>
        /// Проверка полос кадра: наехать на первого воина с подписью,
        /// замерить, что полосы поднялись и слово на месте, вернуть
        /// камеру и замерить, что всё убралось.
        ///
        /// Сам по себе разговор за двадцать секунд может и не случиться,
        /// и тогда полосы в прогоне не показались бы ни разу — «ошибок
        /// нет» значило бы «не проверяли». Поэтому наезд вызывается
        /// принудительно, тем же путём, что у побега.
        /// </summary>
        private static void Frame(float left)
        {
            float played = Seconds - left;

            // Отвечаем на вопрос о сохранении так же, как игрок: пока
            // на него не ответили, игра стоит на паузе и проверять нечего.
            Sinbinder.UI.StartPanel.ChooseFresh();

            var box = Sinbinder.UI.Letterbox.Instance;
            var cam = Sinbinder.Dialogue.DialogueCameraController.Instance;

            // Идёт настоящий разговор — ждём, как ждёт MomentCamera:
            // наезд поверх печатающейся реплики дописал бы в одну строку
            // два текста. Первая версия теста так и сделала в набеге
            // («Сбегает сделан.»), и это была ошибка теста, а не игры.
            if (cam != null && cam.InDialogue && _frameStep == 0)
            {
                if (!_sawTalk && box != null && box.Shown)
                {
                    _sawTalk = true;
                    Write("  [ПОЛОСЫ] в разговоре подняты сами");
                }
                return;
            }

            if (_frameStep == 0 && played > 6f)
            {
                SelfDisabled();
                Dressed();

                _frameStep = 1;
                _frameAt = played;
                var w = Object.FindFirstObjectByType<Sinbinder.Gameplay.Warrior>();
                if (box == null || cam == null || w == null)
                {
                    Write("  [ОШИБКА] полосы: нечего проверять — полосы " + (box != null)
                          + ", камера " + (cam != null) + ", воин " + (w != null));
                    _frameStep = 9;
                    return;
                }

                cam.SaveCameraPosition();
                box.StartCoroutine(cam.FocusOn(w.transform, "Сбегает"));
            }
            else if (_frameStep == 1 && played > _frameAt + 3f)
            {
                _frameStep = 2;
                var top = (RectTransform)box.transform.Find("Полоса сверху");
                var line = box.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                string text = "";
                foreach (var t in line) if (t.name == "Строка") text = t.text;

                bool ok = box.Shown && top != null && top.sizeDelta.y > 50f && text == "Сбегает";
                Write("  " + (ok ? "[ПОЛОСЫ] подняты" : "[ОШИБКА] полосы не поднялись")
                      + ": высота " + (top != null ? top.sizeDelta.y.ToString("F0") : "?")
                      + ", строка «" + text + "»");

                box.StartCoroutine(cam.RestoreCamera());
            }
            else if (_frameStep == 2 && played > _frameAt + 6f)
            {
                _frameStep = 3;
                var top = (RectTransform)box.transform.Find("Полоса сверху");
                bool ok = !box.Shown && top != null && top.sizeDelta.y < 1f;
                Write("  " + (ok ? "[ПОЛОСЫ] убраны" : "[ОШИБКА] полосы остались")
                      + ": высота " + (top != null ? top.sizeDelta.y.ToString("F0") : "?"));
            }
        }

        /// <summary>
        /// Интерфейс, который выключил сам себя.
        ///
        /// Класс ошибок, найденный 14 сентября трижды за ночь: компонент
        /// висит на своей же панели и в Awake/Start её прячет. Дальше
        /// у выключенного объекта не идёт Update, не стартует корутина
        /// и его не находит FindFirstObjectByType. Так в демо не было
        /// ни подсказки при наведении, ни вопроса о плате в склепе,
        /// ни экрана конца демо — и ни одна проверка этого не видела,
        /// потому что ничего не падало: всё просто молчало.
        ///
        /// Ищем выключенный объект, у компонента которого поле-панель
        /// указывает на него же.
        /// </summary>
        private static void SelfDisabled()
        {
            var all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,
                                                              FindObjectsSortMode.None);
            int found = 0;

            foreach (var mb in all)
            {
                if (mb == null || mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Namespace == null || !mb.GetType().Namespace.StartsWith("Sinbinder")) continue;

                foreach (var f in mb.GetType().GetFields(System.Reflection.BindingFlags.Instance
                                                         | System.Reflection.BindingFlags.NonPublic
                                                         | System.Reflection.BindingFlags.Public))
                {
                    if (f.Name != "_panel") continue;

                    var value = f.GetValue(mb) as Object;
                    GameObject target = value as GameObject;
                    if (target == null && value is Component c) target = c.gameObject;

                    if (target != null && target == mb.gameObject)
                    {
                        found++;
                        Write("  [ОШИБКА] " + mb.GetType().Name + " висит на своей же панели «"
                              + mb.gameObject.name + "» и выключил себя: Update, корутины и поиск "
                              + "для него мертвы. Перевесить на холст.");
                    }
                }
            }

            if (found == 0) Write("  [ИНТЕРФЕЙС] сам себя не выключил никто");
        }

        /// <summary>
        /// Одеты ли воины и село ли надетое на место.
        ///
        /// Часть гардероба висит на кости с матрицей привязки
        /// (<see cref="Sinbinder.Gameplay.Wardrobe"/>), и ошибка в этой
        /// матрице не роняет ничего: капюшон просто уезжает в сторону
        /// или под землю. Глазами в пакетном режиме этого не увидеть,
        /// поэтому меряем: далеко ли надетое от своей кости.
        /// </summary>
        private static void Dressed()
        {
            int worn = 0;
            float worst = 0f;
            string where = "";

            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (mf == null || mf.transform.parent == null) continue;
                if (!Sinbinder.Gameplay.Wardrobe.Knows(mf.gameObject.name)) continue;

                worn++;

                // Меряем от кости, на которой висит, до середины надетого.
                var bone = mf.transform.parent;
                var centre = mf.GetComponent<Renderer>() != null
                    ? mf.GetComponent<Renderer>().bounds.center
                    : mf.transform.position;

                float gap = Vector3.Distance(bone.position, centre);
                if (gap <= worst) continue;

                worst = gap;

                // Не только «далеко», но и куда именно уехало: своим
                // местом в кости, своим размером и серединой в мире.
                // Без этих трёх чисел причину ищут перезапусками.
                var t = mf.transform;
                where = mf.gameObject.name + " на " + bone.name
                      + $" (в кости {t.localPosition.x:0.00} {t.localPosition.y:0.00} "
                      + $"{t.localPosition.z:0.00}, масштаб {t.localScale.x:0.##}, "
                      + $"общий {t.lossyScale.x:0.##}, кость ×{bone.lossyScale.x:0.###}, "
                      + $"хозяин {mf.transform.root.name}, "
                      + $"середина {centre.x:0.0} {centre.y:0.0} {centre.z:0.0}, "
                      + $"кость {bone.position.x:0.0} {bone.position.y:0.0} {bone.position.z:0.0})";
            }

            if (worn == 0)
            {
                Write("  [ГАРДЕРОБ] на воинах ничего не надето");
                return;
            }

            // Полметра — это уже не «сидит», а «висит рядом»: самая
            // длинная часть, плащ, укладывается в треть метра от кости.
            bool ok = worst < 0.5f;
            Write("  " + (ok ? "[ГАРДЕРОБ] надето " + worn + " частей, дальше всех "
                             : "[ОШИБКА] гардероб уехал: ")
                  + where + " — " + worst.ToString("F2") + " м от кости");
        }

        private static void Catch(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(Active, false)) return;
            if (type == LogType.Log) return;

            string first = (message ?? "").Split('\n')[0];

            if (type == LogType.Warning)
            {
                Write("  [ПРЕДУПРЕЖДЕНИЕ] " + first);
                return;
            }

            Write("  [ОШИБКА] " + first);
            Write("      " + (stack ?? "").Split('\n')[0]);
        }
    }
}
