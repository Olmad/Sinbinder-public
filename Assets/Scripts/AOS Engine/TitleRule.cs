// Assets/Scripts/AOS Engine/TitleRule.cs
using Sinbinder.Core;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Условие получения титула. Проверяется в TitleManager против
    /// накопленных DeedRecord воина. Флаги Requires* помечают легендарные
    /// титулы — те, что требуют особых обстоятельств, а не счётчика.
    /// </summary>
    [System.Serializable]
    public class TitleRule
    {
        public string Title;

        /// <summary>
        /// Женская форма того же слова. Пусто — слово одно на оба пола
        /// («Тень», «Гроза Охотников», «Берсерк»).
        ///
        /// Заведено 17 сентября: в базе стояла <b>«Защитница»</b> как
        /// самостоятельный титул на двенадцать спасений, и доставалась
        /// она кому угодно — мужчина с двенадцатью спасениями становился
        /// «Защитница Марга». Пол титул не спрашивал, потому что
        /// спрашивать было негде.
        ///
        /// Деянию всё равно, чьё это тело, — <b>слову нет</b>. Поэтому
        /// пол живёт не в условии, а в самом имени: одно правило, две
        /// формы. Порог у них общий, и женских титулов, до которых
        /// мужчине не дойти, в игре нет.
        ///
        /// Формы пишутся <b>парами на одной строке</b> — тот же приём,
        /// что у <see cref="Grammar.Pick"/>: правило по окончанию здесь
        /// не работает («Беглец» даёт «Беглянка», «Скупой» — «Скупая»),
        /// а правило, которое почти работает, хуже отсутствующего.
        /// </summary>
        public string Female;

        public DeedType MainDeed;
        public int RequiredCount;
        public float RequiredImportance;
        public float RequiredRespect;
        public float RequiredFear;

        public bool RequiresSoulCollector;
        public bool RequiresNearAltar;
        public bool RequiresLastAlive;
        public bool RequiresCoreMemory;

        /// <summary>
        /// Слово, каким зовут именно этого. Мужской формой остаётся
        /// <see cref="Title"/> всегда: пара может быть не написана,
        /// и тогда слово одно на обоих.
        ///
        /// Чистое — стенд проверяет его без Unity.
        /// </summary>
        public string For(Gender gender)
            => Grammar.Pick(gender, Title,
                            string.IsNullOrEmpty(Female) ? Title : Female);
    }
}
