// Assets/Scripts/UI/GameCursor.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Свой курсор — скелетная рука, и она говорит, что под ней. Автор,
    /// 26 октября: «вариант 3 на основе костяного пальца, и банка с прыгающей
    /// душой при ожидании»; «было бы хорошо, если бы курсор отображал какие-нибудь
    /// статусы — при наведении на воина рука держит банку, соответствующую душе
    /// воина».
    ///
    /// <list type="bullet">
    /// <item>обычно — костяной палец;</item>
    /// <item>свой воин — палец держит банку, душа в ней цветом его греха
    ///   (<see cref="SinPalette"/> — та же, что огонь в глазах и рамка подсказки);</item>
    /// <item>свой, но далеко — банка тусклая: приказ дойдёт тише
    ///   (<see cref="Voice.MuffleFor"/>). Карта обучения, 36-LESSONS §7: «голос
    ///   тише издали» не было видно до отказа;</item>
    /// <item>враг — клешня; угасающая душа — пустая банка; вещь на F — ладонь;</item>
    /// <item>кат-сцена (полосы, а ответа выбирать не нужно) — банка, в которой
    ///   подпрыгивает душа: «ждите».</item>
    /// </list>
    ///
    /// Курсор системный (<c>Cursor.SetCursor</c>): не отстаёт от мыши и есть
    /// в меню. Картинки — <c>Resources/Cursors</c> (<c>Tools/cursors/cursors.py</c>);
    /// нет их — курсор Windows, как прежде. Душа красится здесь, один раз на грех.
    /// </summary>
    public class GameCursor : MonoBehaviour
    {
        private const string Folder = "Cursors/";
        private static readonly Vector2 Hot = new Vector2(2f, 2f);
        private const int WaitFrames = 8;
        private const float WaitFps = 10f;

        /// <summary>Приглушённость, с которой банка тускнеет: четверть пути к «не слышно».</summary>
        private const float DimFrom = 0.25f;

        private static GameCursor _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_instance != null || Application.isBatchMode) return;
            if (Resources.Load<Texture2D>(Folder + "pointer") == null) return;

            var go = new GameObject("Курсор");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GameCursor>();
        }

        private readonly Dictionary<string, Texture2D> _made = new();
        private string _shown;
        private float _nextLook;
        private string _want = "pointer";

        void Update()
        {
            if (Time.unscaledTime >= _nextLook)
            {
                _nextLook = Time.unscaledTime + 0.05f;
                _want = Decide();
            }

            string key = _want;
            if (key == "wait")
                key = "wait_" + ((int)(Time.unscaledTime * WaitFps) % WaitFrames);

            if (key == _shown) return;
            var tex = Get(key);
            if (tex == null) return;
            Cursor.SetCursor(tex, key.StartsWith("wait") ? new Vector2(24f, 24f) : Hot, CursorMode.Auto);
            _shown = key;
        }

        void OnDestroy()
        {
            if (_instance == this) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        /// <summary>Что под курсором — именем картинки (душа — «warrior:Pride»).</summary>
        private static string Decide()
        {
            // Ждать: идёт кат-сцена, и выбирать в ней нечего.
            bool letterbox = Letterbox.Instance != null && Letterbox.Instance.Shown;
            if ((letterbox || OpeningReveal.Busy) && !TalkView.Shown) return "wait";

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return "pointer";

            var cam = Camera.main;
            if (cam == null) return "pointer";
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 300f, ~0, QueryTriggerInteraction.Collide)) return "pointer";

            var w = hit.collider.GetComponentInParent<Warrior>();
            if (w != null && !w.IsDead && !(w is SinbinderPlayer))
            {
                if (w.Team != Team.Player) return "enemy";
                string sin = w.Soul != null ? w.Soul.Sin.ToString() : "";
                return (Voice.MuffleFor(w) >= DimFrom ? "warrior_far:" : "warrior:") + sin;
            }

            if (hit.collider.GetComponentInParent<Inspectable>() != null
                || hit.collider.GetComponentInParent<TrophyChest>() != null
                || hit.collider.GetComponentInParent<WeaponPickup>() != null
                || hit.collider.GetComponentInParent<CardTable>() != null)
                return "interact";

            // Угасающая душа — её огонёк у точки на земле, куда смотрит курсор.
            foreach (var soul in Object.FindObjectsByType<SoulIndicator>(FindObjectsSortMode.None))
                if (soul != null && CampFocus.GroundDistance(soul.transform.position, hit.point) < 1.3f)
                    return "harvest";

            return "pointer";
        }

        /// <summary>Картинка по имени; «имя:Грех» — с душой, докрашенной цветом греха.</summary>
        private Texture2D Get(string key)
        {
            if (_made.TryGetValue(key, out var done)) return done;

            string name = key, sin = null;
            int colon = key.IndexOf(':');
            if (colon >= 0) { name = key.Substring(0, colon); sin = key.Substring(colon + 1); }

            var basePic = Resources.Load<Texture2D>(Folder + name);
            if (basePic == null) { _made[key] = null; return null; }

            var soul = Resources.Load<Texture2D>(Folder + name + "_soul");
            if (soul == null) { _made[key] = basePic; return basePic; }

            // Ожидание и душа без греха — цвета пепла (SinPalette по умолчанию);
            // воин — цвета его греха.
            var colour = !string.IsNullOrEmpty(sin) && System.Enum.TryParse<SinType>(sin, out var s)
                ? SinPalette.Of(s) : SinPalette.Of((SinType)(-1));
            _made[key] = Compose(basePic, soul, colour);
            return _made[key];
        }

        /// <summary>
        /// Душа поверх банки: светлая середина цвета греха. Поверх, а не под:
        /// под стеклом цвет тускнел до серого, и Гордыню не отличить от Уныния.
        /// </summary>
        private static Texture2D Compose(Texture2D basePic, Texture2D soul, Color colour)
        {
            var a = basePic.GetPixels32();
            var m = soul.GetPixels32();
            var glow = Color.Lerp(colour, Color.white, 0.25f);
            for (int i = 0; i < a.Length && i < m.Length; i++)
            {
                float k = m[i].a / 255f;
                if (k <= 0f) continue;
                Color c = a[i];
                Color mixed = Color.Lerp(c, glow, k * 0.9f);
                mixed.a = Mathf.Max(c.a, k);
                a[i] = mixed;
            }
            var tex = new Texture2D(basePic.width, basePic.height, TextureFormat.RGBA32, false);
            tex.SetPixels32(a);
            tex.Apply();
            return tex;
        }
    }
}
