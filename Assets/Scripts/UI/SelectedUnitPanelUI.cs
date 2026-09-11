// Assets/Scripts/UI/SelectedUnitPanelUI.cs
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Нижняя панель: кто выделен, чем он живёт, что делает сейчас.
    ///
    /// Три строки, и каждая отвечает на вопрос, который до сих пор
    /// оставался без ответа на экране:
    ///
    /// <list type="bullet">
    /// <item>имя — «кто это вообще»;</item>
    /// <item>главная шкала — «чего от него ждать»; это тот же ответ,
    ///       что даёт пророчество на совете, только доступный всегда
    ///       и про любого;</item>
    /// <item>текущее действие — «что он делает прямо сейчас», словами.</item>
    /// </list>
    ///
    /// Никого не выделено — показывается Греховод. Это не заглушка,
    /// а урок: игрок обязан увидеть, что он сам такой же строкой в этой
    /// панели, то есть фигура в мире, а не парящая камера.
    ///
    /// <b>Цифр здесь нет и быть не может</b> (00-GDD.md §7). Шкала
    /// названа словом, действие названо глаголом; насколько громко
    /// звучит грех — не показывается, потому что это число.
    /// Действие спрашивается только на первой ступени прозрачности
    /// и выше (<see cref="Transparency"/>): значок намерения и эта
    /// строка — одно и то же знание, поданное по-разному.
    /// </summary>
    public class SelectedUnitPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _nameLine;
        [SerializeField] private Text _sinLine;
        [SerializeField] private Text _actionLine;

        /// <summary>
        /// Как часто перечитывать действие. Каждый кадр не нужно:
        /// решение живёт заметно дольше кадра, а строка, меняющаяся
        /// шестьдесят раз в секунду, читается как мигание.
        /// </summary>
        [SerializeField] private float _refreshEvery = 0.25f;

        private float _next;
        private Warrior _shown;

        void Start()
        {
            if (_panel == null || _nameLine == null || _sinLine == null || _actionLine == null)
            {
                Debug.LogWarning("[ПАНЕЛЬ] Нижняя панель не связана в сцене: "
                               + "пересоберите сцены демо.");
                enabled = false;
                return;
            }

            Refresh();
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + _refreshEvery;

            Refresh();
        }

        private void Refresh()
        {
            var who = Current();

            if (who == null)
            {
                _panel.SetActive(false);
                _shown = null;
                return;
            }

            _panel.SetActive(true);

            // Имя и шкалу перечитываем только при смене героя: они
            // не меняются между кадрами, а строку действия — всегда.
            if (who != _shown)
            {
                _shown = who;
                _nameLine.text = who.DisplayName;
                _sinLine.text = SinLine(who);
            }

            _actionLine.text = ActionLine(who);
        }

        /// <summary>
        /// Кого показывать: первого выделенного, а если не выделен
        /// никто — самого Греховода.
        /// </summary>
        private Warrior Current()
        {
            var manager = SelectionManager.Instance;
            if (manager != null)
            {
                var picked = manager.GetSelectedUnits();
                for (int i = 0; i < picked.Count; i++)
                {
                    if (picked[i] == null) continue;

                    var warrior = picked[i].GetComponentInParent<Warrior>();
                    if (warrior != null) return warrior;
                }
            }

            return SinbinderPlayer.Instance;
        }

        /// <summary>
        /// Главная шкала словом. У Греховода она не значит ничего —
        /// он не голосует, — и врать про него «Гордыня» нельзя:
        /// игрок решит, что его собственный герой чем-то одержим.
        /// </summary>
        private string SinLine(Warrior who)
        {
            if (who is SinbinderPlayer) return "Греховод. Приказывает, но не решает";

            var soul = who.Soul;
            if (soul == null) return "Душа неизвестна";

            string line = $"Громче всего: {soul.GetSinName()}";

            // Братство — не шкала, а связь, и в панели она стоит рядом
            // со шкалой не для красоты: движок читает её в бою
            // (CombatDecisionContext.BrotherNearby), и игрок обязан
            // видеть то же, что видит движок.
            return Brother(soul) ? line + " · Брат по оружию" : line;
        }

        /// <summary>Носит ли душа перк братства.</summary>
        private static bool Brother(SoulData soul)
        {
            return soul.Memory?.NarrativePerks != null
                && soul.Memory.NarrativePerks.Exists(p => p.PerkName == "Брат по оружию");
        }

        /// <summary>
        /// Что делает сейчас. У отряда — последнее решение движка,
        /// у Греховода — идёт он или стоит: своих решений у него нет.
        /// </summary>
        private string ActionLine(Warrior who)
        {
            if (who is SinbinderPlayer)
            {
                var walk = who.GetComponent<PlayerWalk>();
                return walk != null && walk.Walking ? "Идёт" : "Стоит";
            }

            if (!Transparency.Shows(Clarity.Icons)) return string.Empty;

            var wrapper = who.GetComponent<AOS.AOSWarriorWrapper>();
            if (wrapper == null) return string.Empty;

            return "Сейчас: " + AOS.PhraseGenerator.Doing(wrapper.LastDecision);
        }
    }
}
