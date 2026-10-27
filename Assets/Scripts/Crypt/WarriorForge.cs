// Assets/Scripts/Crypt/WarriorForge.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Создатель воинов на полигоне — как в ролевых играх, без ресурсов.
    ///
    /// Автор, 27 октября: «Нужно в полигоне сделать быстрого создателя воинов,
    /// как во всяких РПГ. Без ресурсов. И место проверки уникальности».
    /// Зачем: генератор (docs/53-GENERATOR.md) меряется слепой пробой —
    /// отличит ли автор сгенерированного от ручного. Для этого нужно место,
    /// где воина собирают за секунды и сразу смотрят, как он живёт рядом
    /// с другими.
    ///
    /// N — открыть и закрыть. Строки щелчком, как дверь в игру: грех, второй
    /// грех, сила, мораль, ремесло, тело, верность. «Создать» — воин встаёт
    /// на площадке тем же путём, что в лагере (<see cref="PrologueCampSpawner.Forge"/>):
    /// голосует, ходит, говорит. Ни души, ни монеты не тратится.
    ///
    /// Цифр души здесь нет, как и везде: сила и верность — словами.
    /// Женщин создатель не делает (правило <c>unique_women</c>): женщины
    /// в этом мире уникальны и не собираются из строк.
    ///
    /// Ставит себя сам — только в сцену полигона (там, где <see cref="TestChamber"/>).
    /// </summary>
    public class WarriorForge : MonoBehaviour
    {
        private const KeyCode Key = KeyCode.N;

        /// <summary>Имена — те же, что у генератора: одна правда на двоих.</summary>
        private static string[] Names => WarriorGenerator.Names;

        private static readonly Trade[] TradeList =
        {
            Trade.None, Trade.Hunter, Trade.Peasant, Trade.Archer, Trade.Alchemist, Trade.Mage,
            Trade.OrderHunter, Trade.Tracker, Trade.Inquisitor, Trade.Catcher,
        };

        private static readonly ShellType[] ShellList =
            { ShellType.Skeleton, ShellType.Zombie, ShellType.Ghost, ShellType.Golem };

        /// <summary>Сила главного греха и верность — три ступени словами.</summary>
        private static readonly float[] Strength = { 30f, 55f, 80f };
        private static readonly float[] Faith = { 30f, 60f, 90f };

        private int _number = 1, _sin, _second = 1, _strength = 1, _moral = 1, _trade, _shell, _faith = 1, _origin;
        private readonly List<Warrior> _made = new();

        private GameObject _canvas, _panel;
        private RectTransform _rows;
        private readonly List<GameObject> _spawned = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void First() => Install();

        private static void OnScene(Scene scene, LoadSceneMode mode) => Install();

        private static void Install()
        {
            if (Object.FindFirstObjectByType<TestChamber>() == null) return;
            if (Object.FindFirstObjectByType<WarriorForge>() != null) return;
            new GameObject("Создатель воинов").AddComponent<WarriorForge>();
        }

        public static bool Open { get; private set; }

        void OnDestroy()
        {
            if (Open) Close();
        }

        void Update()
        {
            if (Dev.CheatConsole.Open) return;
            if (Open && Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            if (!Input.GetKeyDown(Key)) return;
            if (Open) Close();
            else if (!UI.Modal.Any) Show();
        }

        private void Show()
        {
            if (_panel == null) Build();
            Open = true;
            UI.Modal.Open(_panel);
            GamePauseController.Instance?.Pause();
            Draw();
        }

        private void Close()
        {
            Open = false;
            if (_panel != null) UI.Modal.Close(_panel);
            GamePauseController.Instance?.Resume();
        }

        // ──────────────────────────────────
        // Строки
        // ──────────────────────────────────

        private void Draw()
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();

            var font = UI.UiStyle.Body;
            float y = 0f, h = UI.MenuRows.Height * 1.3f;

            void Row(string title, string second, System.Action click)
            {
                _spawned.Add(UI.MenuRows.Row(_rows, font, y, title, second, click));
                y -= h;
            }

            var sin = (SinType)_sin;
            Row(Loc.F("Имя: {0}", Name()), Loc.T("Щелчок — следующее имя."), () => { _number++; Draw(); });
            Row(Loc.F("Грех: {0}", SoulData.GetSinName(sin)), Loc.T("Что ведёт его в спорах души."),
                () => { _sin = (_sin + 1) % SoulData.SpectrumCount; if (_second == _sin) _second = (_second + 1) % SoulData.SpectrumCount; Draw(); });
            Row(Loc.F("Второй грех: {0}", SoulData.GetSinName((SinType)_second)), Loc.T("Слабее главного — с ним он спорит сам с собой."),
                () => { do _second = (_second + 1) % SoulData.SpectrumCount; while (_second == _sin); Draw(); });
            Row(Loc.F("Сила греха: {0}", StrengthWord()), Loc.T("Насколько грех громче остальных голосов."),
                () => { _strength = (_strength + 1) % Strength.Length; Draw(); });
            Row(Loc.F("Мораль: {0}", MoralWord()), Loc.T("Чего он не сделает никогда — или сделает охотно."),
                () => { _moral = (_moral + 1) % 3; Draw(); });
            Row(Loc.F("Ремесло: {0}", TradeWord()), Loc.T("Кем был до смерти: тянет душу, даёт оружие и слова."),
                () => { _trade = (_trade + 1) % TradeList.Length; Draw(); });
            Row(Loc.F("Тело: {0}", ShellWord()), Loc.T("Скелет, зомби, призрак или голем."),
                () => { _shell = (_shell + 1) % ShellList.Length; Draw(); });
            Row(Loc.F("Верность: {0}", FaithWord()), Loc.T("Насколько ваш приказ весит в его душе."),
                () => { _faith = (_faith + 1) % Faith.Length; Draw(); });

            y -= h * 0.3f;
            Row(Loc.T("Создать"), Loc.T("Встанет на площадке и будет жить, как в лагере."), Make);

            // Генератор (docs/53-GENERATOR.md): человек целиком — откуда душа, ремесло,
            // два греха, мораль, примета, имя, история. Строки выше он не читает.
            y -= h * 0.3f;
            Row(Loc.F("Откуда душа: {0}", OriginWord()), Loc.T("Из этого генератор выводит всё остальное."),
                () => { _origin = (_origin + 1) % 4; Draw(); });
            Row(Loc.T("Собрать генератором"), Loc.T("Человек целиком: ремесло, грехи, примета, история."), MakeGenerated);
            if (_made.Count > 0)
                Row(Loc.T("Убрать созданных"), Loc.T("Площадка снова пуста — девять лагеря остаются."), Clear);
            Row(Loc.T("Закрыть"), Loc.F("{0} или Esc.", Key), Close);
        }

        private string Name()
        {
            string name = Names[(_number - 1) % Names.Length];
            // Круг имён пройден — тот же человек стал бы двойником: второй
            // по счёту носит прозвище «Младший», и это тоже часть имени.
            return _number > Names.Length ? name + " " + Loc.T("Младший") : name;
        }

        private string OriginWord() => (SoulOrigin)_origin switch
        {
            SoulOrigin.Pact => Loc.T("сделка — отдал душу сам"),
            SoulOrigin.Jar => Loc.T("банка с полки — собран давно"),
            SoulOrigin.Sortie => Loc.T("вылазка — принесли с места"),
            _ => Loc.T("жатва — пал в бою, где были мы"),
        };

        private string StrengthWord() => _strength switch
        {
            0 => Loc.T("едва заметен"),
            1 => Loc.T("заметен"),
            _ => Loc.T("силён"),
        };

        private string FaithWord() => _faith switch
        {
            0 => Loc.T("себе на уме"),
            1 => Loc.T("верен"),
            _ => Loc.T("предан"),
        };

        private string MoralWord() => (MoralType)_moral switch
        {
            MoralType.Pious => Loc.T("благочестивый"),
            MoralType.Vicious => Loc.T("порочный"),
            _ => Loc.T("ни то ни сё"),
        };

        private string TradeWord()
        {
            var t = TradeList[_trade];
            return t == Trade.None ? Loc.T("без ремесла") : Trades.Name(t);
        }

        private string ShellWord() => ShellList[_shell] switch
        {
            ShellType.Zombie => Loc.T("зомби"),
            ShellType.Ghost => Loc.T("призрак"),
            ShellType.Golem => Loc.T("голем"),
            _ => Loc.T("скелет"),
        };

        // ──────────────────────────────────
        // Создать и убрать
        // ──────────────────────────────────

        private void Make()
        {
            var spawner = Object.FindFirstObjectByType<PrologueCampSpawner>();
            if (spawner == null)
            {
                Debug.LogWarning("[СОЗДАТЕЛЬ] На полигоне нет отряда (PrologueCampSpawner) — ставить некуда.");
                return;
            }

            float strength = Strength[_strength];
            var member = new SquadRoster.Member
            {
                Name = Name(),
                Sin = (SinType)_sin,
                Moral = (MoralType)_moral,
                Gender = Gender.Male,
                Intensity = strength,
                Loyalty = Faith[_faith],
                Trade = TradeList[_trade],
                Shell = ShellList[_shell],
            };

            // Встаёт во дворе уникальности, если он есть, и живёт им (CampYard).
            var yard = CampYard.Instance;
            var warrior = spawner.Forge(member, Spot(yard != null ? yard.Fire + new Vector3(0f, 0f, -6f) : spawner.transform.position, _made.Count));
            if (warrior == null) return;
            CampYard.Admit(warrior);

            // Второй грех — слабее главного, но слышен.
            if (warrior.Soul != null)
                warrior.Soul.Set((SinType)_second, Mathf.Max(warrior.Soul.Get((SinType)_second), strength * 0.6f));

            _made.Add(warrior);
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(
                Loc.F("{0} встал на площадке.", Loc.Name(member.Name)));
            _number++;
            Draw();
        }

        /// <summary>
        /// Сложить человека генератором и поставить на площадку. Номер — тот же счёт,
        /// что у имён: одинаковый номер и происхождение — один и тот же человек.
        /// </summary>
        private void MakeGenerated()
        {
            var spawner = Object.FindFirstObjectByType<PrologueCampSpawner>();
            if (spawner == null)
            {
                Debug.LogWarning("[СОЗДАТЕЛЬ] На полигоне нет отряда (PrologueCampSpawner) — ставить некуда.");
                return;
            }

            var present = new List<string>();
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
                if (w != null && !w.IsDead && w.Team == Team.Player && !(w is SinbinderPlayer))
                    present.Add(w.DisplayName);

            var k = WarriorGenerator.Make(_number, (SoulOrigin)_origin, present);
            var member = new SquadRoster.Member
            {
                Name = k.Name,
                Sin = k.Sin,
                Moral = k.Moral,
                Gender = Gender.Male,
                Intensity = k.Strength,
                Loyalty = Faith[_faith],
                Trade = k.Trade,
                Shell = k.Shell,
                Seed = k.Seed,
            };

            var warrior = spawner.Forge(member, Spot(spawner.transform.position, _made.Count));
            if (warrior == null) return;
            if (warrior.Soul != null)
                warrior.Soul.Set(k.Second, Mathf.Max(warrior.Soul.Get(k.Second), k.Strength * 0.6f));
            Signature.Give(k.Name, k.Why, k.First, k.Death);

            _made.Add(warrior);
            var log = Object.FindFirstObjectByType<UI.BattleLogUI>();
            log?.Write(Loc.F("{0} встал на площадке.", Loc.Name(k.Name)));
            if (k.Seed != null && !string.IsNullOrEmpty(k.Seed.Story)) log?.Write(k.Seed.Story);
            _number++;
            Draw();
        }

        private void Clear()
        {
            foreach (var w in _made) if (w != null) Destroy(w.gameObject);
            _made.Clear();
            Draw();
        }

        /// <summary>Место на площадке: ряды по шесть перед отрядом, на земле, где можно ходить.</summary>
        private static Vector3 Spot(Vector3 squad, int k)
        {
            var at = squad + new Vector3((k % 6 - 2.5f) * 1.6f, 0f, 3f + k / 6 * 1.6f);
            return NavMesh.SamplePosition(at, out var hit, 3f, NavMesh.AllAreas) ? hit.position : at;
        }

        // ──────────────────────────────────
        // Панель
        // ──────────────────────────────────

        private void Build()
        {
            _canvas = new GameObject("Создатель воинов — экран", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UI.UiStyle.Reference;

            _panel = new GameObject("Создатель воинов", typeof(RectTransform));
            _panel.transform.SetParent(_canvas.transform, false);
            var rt = (RectTransform)_panel.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(40f, 0f);
            rt.sizeDelta = new Vector2(760f, 1200f);
            var back = _panel.AddComponent<Image>();
            UI.UiStyle.Dress(back, UI.UiStyle.PlateWeight.Screen);

            var title = new GameObject("Заголовок", typeof(RectTransform), typeof(Text));
            title.transform.SetParent(_panel.transform, false);
            var trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -24f); trt.sizeDelta = new Vector2(-48f, 60f);
            var text = title.GetComponent<Text>();
            text.font = UI.UiStyle.Title;
            text.fontSize = 40;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.94f, 0.92f, 0.86f);
            text.text = Loc.T("Создатель воинов");

            var rows = new GameObject("Строки", typeof(RectTransform));
            rows.transform.SetParent(_panel.transform, false);
            _rows = (RectTransform)rows.transform;
            _rows.anchorMin = new Vector2(0f, 1f); _rows.anchorMax = new Vector2(1f, 1f); _rows.pivot = new Vector2(0.5f, 1f);
            _rows.anchoredPosition = new Vector2(0f, -100f); _rows.sizeDelta = new Vector2(-48f, 1080f);

            _panel.SetActive(false);
        }
    }
}
