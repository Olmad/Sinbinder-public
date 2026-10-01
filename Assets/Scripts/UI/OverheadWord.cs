// Assets/Scripts/UI/OverheadWord.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.AOS;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Одно слово над отступившим (docs/42-INTERFACE.md §1, пп. 2–5).
    ///
    /// <b>Норма тихая, отклонение громкое.</b> Послушный — без подписи;
    /// над тем, кто отказал, колеблется, бежит, грабит или перешёл к чужим, —
    /// слово: «Грабит», «Колеблется…».
    ///
    /// <b>Вспышка, потом тление.</b> Новое отступление горит цветом греха
    /// две с половиной секунды и уходит в тихий вид — точка греха и слово
    /// цвета кости, — пока воин не вернулся к приказу. Постоянная яркость
    /// перестаёт замечаться, вспышка — нет.
    ///
    /// <b>Ярких — не больше двух.</b> Вспыхнуло третье — первое гаснет:
    /// яркость — расход, а не украшение.
    ///
    /// Причины над головой нет: она в голосах выбранного и в журнале
    /// (§1, п. 4, — одно решение, одни слова, но не в пяти местах).
    /// Пока слова над головами живы, общая подпись момента
    /// (<see cref="MomentCaption"/>) над головой не пишет — второе слово
    /// над тем же воином.
    /// </summary>
    public class OverheadWord : MonoBehaviour
    {
        [SerializeField] private Text _text;

        private const float FlashSeconds = 2.5f;
        private const int MostLit = 2;
        private const float RefreshEvery = 0.25f;

        private static readonly List<OverheadWord> Lit = new List<OverheadWord>();
        private static int _alive;

        /// <summary>Слова над головами есть в сцене — подписи момента молчать.</summary>
        public static bool Active => _alive > 0;

        private float _next;
        private float _flashUntil;
        private string _shown;

        public void SetText(Text text) => _text = text;

        void OnEnable() => _alive++;

        void OnDisable()
        {
            _alive--;
            Lit.Remove(this);
        }

        void Update()
        {
            if (_text == null || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + RefreshEvery;

            var warrior = GetComponentInParent<Warrior>();
            string word = Word(warrior);

            if (word == null)
            {
                _text.text = "";
                _shown = null;
                Lit.Remove(this);
                return;
            }

            if (word != _shown)
            {
                _shown = word;
                Flash();
            }

            bool bright = Time.unscaledTime < _flashUntil;
            if (!bright) Lit.Remove(this);

            var sin = warrior.Soul != null ? SinPalette.Of(warrior.Soul.Sin) : Color.gray;
            var dot = bright ? sin : sin * 0.72f;
            _text.text = $"<color=#{ColorUtility.ToHtmlStringRGB(dot)}>●</color> {word}";
            _text.color = bright ? sin : new Color(0.90f, 0.86f, 0.78f, 0.85f);
            _text.fontSize = bright ? 46 : 38;
        }

        private void Flash()
        {
            _flashUntil = Time.unscaledTime + FlashSeconds;

            Lit.Remove(this);
            while (Lit.Count >= MostLit)
            {
                var oldest = Lit[0];
                Lit.RemoveAt(0);
                if (oldest != null) oldest._flashUntil = 0f;
            }
            Lit.Add(this);
        }

        /// <summary>
        /// Слово, если воин отступил; пусто — норма. Только свой отряд: над
        /// десятью охотниками слова были бы кашей, а их поступки — в журнале.
        /// </summary>
        private static string Word(Warrior w)
        {
            if (w == null || w.IsDead || w.Team != Team.Player || w is SinbinderPlayer) return null;
            if (FogOfWar.Hides(w)) return null;
            if (!Transparency.Shows(Detail.Moments)) return null;

            var d = w.GetComponent<AOSWarriorWrapper>();
            if (d == null || d.LastContext == null) return null;

            var detail = d.LastDecisionDetail;
            if (detail.Hesitated) return Loc.T("Колеблется…");

            bool deviates = detail.RefusedCommand
                         || detail.Action == ActionType.Flee
                         || detail.Action == ActionType.Loot
                         || detail.Action == ActionType.AcceptBribe;
            if (!deviates) return null;

            string word = PhraseGenerator.Short(detail.Action, d.LastContext);
            if (string.IsNullOrEmpty(word)) return null;
            return char.ToUpper(word[0]) + word.Substring(1);
        }
    }
}
