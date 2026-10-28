// Assets/Scripts/Gameplay/TugMatch.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Партия «Перетягивания» (54-TUG.md). Сторона выбывает, когда разрушен её трон; пустые места
    /// лобби выбыли с начала. Выбыл игрок — поражение; осталась одна сторона, и это игрок, — победа.
    /// Экран конца: «Ещё партию» (то же поле, новые люди) и «В меню».
    ///
    /// Гибель Греховода — не конец (автор, 28.10: «как условность — при гибели Греховод
    /// восстанавливается на троне через 1 минуту; при смерти не оставляет свою душу, только весь
    /// инвентарь»): кошель партии падает, где он пал (<see cref="LordPurse"/>), души и тела для
    /// жатвы нет, через <see cref="RiseAfter"/> секунд он встаёт у своего трона — если трон цел.
    /// </summary>
    public class TugMatch : MonoBehaviour
    {
        public static TugMatch Instance { get; private set; }

        /// <summary>Склеп каждой стороны (корень префаба Crypt); нет склепа — null.</summary>
        public readonly List<Transform> Crypts = new();

        private readonly List<bool> _out = new();
        private int _player = -1;
        private string _title, _body;

        public static bool Over => Instance != null && Instance._title != null;
        public static bool Won { get; private set; }

        void Awake() => Instance = this;

        void OnEnable() => Sturdy.Broke += OnBroke;

        void OnDisable()
        {
            Sturdy.Broke -= OnBroke;
            if (_title != null) Time.timeScale = 1f;
            if (Instance == this) Instance = null;
        }

        public void Begin(int player)
        {
            _player = player;
            Won = false;
            _out.Clear();
            for (int i = 0; i < Crypts.Count; i++)
            {
                var seat = i < MapSession.Seats.Count ? MapSession.Seats[i] : MapSession.Seat.Empty;
                _out.Add(seat == MapSession.Seat.Empty);
            }
        }

        /// <summary>Сторона игрока.</summary>
        public int PlayerSide => _player;

        public bool Out(int side) => side < 0 || side >= _out.Count || _out[side];

        /// <summary>Склеп стороны: дверь и трон.</summary>
        public (CryptDoor door, Sturdy throne) PartsOf(int side)
        {
            if (side < 0 || side >= Crypts.Count || Crypts[side] == null) return (null, null);
            var door = Crypts[side].GetComponentInChildren<CryptDoor>();
            Sturdy throne = null;
            foreach (var s in Crypts[side].GetComponentsInChildren<Sturdy>())
                if (s.GetComponentInParent<CryptDoor>() == null) throne = s;
            return (door, throne);
        }

        private void OnBroke(Sturdy s)
        {
            if (s == null || s.GetComponentInParent<CryptDoor>() != null) return;   // дверь — не конец
            Fall(s.Side, Loc.F("Трон склепа {0} разрушен.", s.Side + 1));
        }

        /// <summary>Через сколько секунд павший Греховод встаёт у своего трона.</summary>
        public const float RiseAfter = 60f;

        private readonly Dictionary<int, float> _down = new();

        /// <summary>Лежит ли Греховод стороны — ждёт, когда встанет.</summary>
        public bool Down(int side) => _down.ContainsKey(side);

        /// <summary>
        /// Пал Греховод: кошель — на землю, души нет (CombatManager её не заводит), через минуту — у трона.
        /// </summary>
        public static void LordDown(Warrior body)
        {
            var m = Instance;
            if (m == null || body == null || Over) return;
            string lord = Lords.Of(body);
            if (lord == null) return;
            var rival = body.GetComponent<RivalLord>();
            int side = body is SinbinderPlayer ? m._player : rival != null ? rival.Side : -1;
            if (side < 0 || m._down.ContainsKey(side)) return;

            LordPurse.Drop(body.transform.position, Favor.EmptyPurse(lord), lord);
            if (body is SinbinderPlayer) Remains.Drop();   // ноша с плеч — пропала вместе с ним
            m._down[side] = Time.time + RiseAfter;
            // Корона на пустом троне — видна всем; полоса до восстания — только своей стороне.
            ThroneVigil.Begin(m.PartsOf(side).throne, Time.time + RiseAfter, side == m._player);
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(
                Loc.F("{0} пал. Встанет у трона через минуту; кошель остался, где он пал.", lord));
            Debug.Log($"[ПАРТИЯ] пал {lord}: встанет через {RiseAfter:F0} с");
        }

        void Update()
        {
            if (_down.Count == 0 || Over) return;
            foreach (var kv in new List<KeyValuePair<int, float>>(_down))
                if (Time.time >= kv.Value) Rise(kv.Key);
        }

        /// <summary>Встать у трона — сейчас (по часам; прогон зовёт сразу).</summary>
        public void Rise(int side)
        {
            _down.Remove(side);
            if (Out(side) || side >= Crypts.Count || Crypts[side] == null) return;
            var (_, throne) = PartsOf(side);
            if (throne != null && throne.TryGetComponent<ThroneVigil>(out var vigil)) vigil.End();
            var at = throne != null ? throne.transform.position + throne.transform.forward * 1.8f : Crypts[side].position;
            if (UnityEngine.AI.NavMesh.SamplePosition(at, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) at = hit.position;

            if (side == _player)
            {
                var old = SinbinderPlayer.Instance;
                if (old != null) Destroy(old.gameObject);
                StartCoroutine(Hero(at));
            }
            else
            {
                foreach (var r in RivalLord.All)
                    if (r != null && r.Side == side) { Destroy(r.gameObject); break; }
                MapLoader.Rival(side, MapSession.Seats[side], at, Crypts[side].rotation);
            }
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(Loc.F("Греховод стороны {0} встал у трона.", side + 1));
        }

        /// <summary>Новое тело Греховода игрока — кадром позже: старое уходит в конце кадра.</summary>
        private System.Collections.IEnumerator Hero(Vector3 at)
        {
            yield return null;
            SinbinderPlayer.Spawn(at);
        }

        private void Fall(int side, string why)
        {
            if (Over || Out(side)) return;
            _out[side] = true;
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(why);
            Debug.Log($"[ПАРТИЯ] выбыла сторона {side + 1}: {why}");

            if (side == _player) { End(false, why); return; }
            int left = 0;
            for (int i = 0; i < _out.Count; i++) if (!_out[i]) left++;
            if (left == 1 && !Out(_player)) End(true, why);
        }

        private void End(bool won, string why)
        {
            Won = won;
            _title = won ? Loc.T("Победа") : Loc.T("Поражение");
            _body = why + "\n" + (won ? Loc.T("Ваш трон стоит. Других — нет.") : Loc.T("Ваша сторона выбыла из партии."));
            Time.timeScale = 0f;
            Debug.Log($"[ПАРТИЯ] {_title}: {why}");
        }

        void OnGUI()
        {
            if (_title == null) return;
            float w = 420f, h = 200f;
            var box = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUILayout.BeginArea(box, GUI.skin.box);
            var big = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            GUILayout.Label(_title, big);
            GUILayout.Label(_body, new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = TextAnchor.MiddleCenter });
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Loc.T("Ещё партию"))) { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
            if (GUILayout.Button(Loc.T("В меню"))) { Time.timeScale = 1f; SceneManager.LoadScene(0); }
            GUILayout.EndArea();
        }
    }
}
