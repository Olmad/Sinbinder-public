// Assets/Scripts/AOS Engine/Decision.cs
namespace Sinbinder.AOS
{
    /// <summary>
    /// Итог голосования вместе с причиной.
    ///
    /// Раньше резолвер возвращал только ActionType и выбрасывал всё
    /// остальное в Debug.Log. Из-за этого объяснить игроку решение было
    /// нечем: причина существовала полсекунды и исчезала. Теперь причина
    /// уезжает наверх вместе с действием — из неё строится подсказка
    /// при наведении и строка журнала.
    /// </summary>
    public struct Decision
    {
        /// <summary>Что воин будет делать.</summary>
        public ActionType Action;

        /// <summary>ModuleID модуля, который дал победившему действию больше всех очков.</summary>
        public string TopModule;

        /// <summary>
        /// Что победило до порога колебания. При Hesitated поле Action
        /// становится Idle, а здесь остаётся настоящий лидер — иначе
        /// нечем объяснить, между чем именно воин выбирал.
        /// </summary>
        public ActionType TopContender;

        /// <summary>Что было вторым — то, чем воин пожертвовал.</summary>
        public ActionType RunnerUp;

        /// <summary>Разрыв между первым и вторым, в очках.</summary>
        public float Gap;

        /// <summary>
        /// Уверенность: разрыв в долях от громкости победившего голоса.
        /// Ноль — полная мука выбора, единица — решение без спора.
        ///
        /// Голыми очками уверенность измерять нельзя: в тихом лагере весь
        /// счёт лежит около нуля, и отрыв в семь очков там — уверенная
        /// победа, а в гуще боя те же семь очков при счёте под сотню —
        /// настоящее колебание. Один и тот же разрыв значит разное.
        /// </summary>
        public float Confidence;

        /// <summary>Уверенность ниже порога: воин колеблется, действие — Idle.</summary>
        public bool Hesitated;

        /// <summary>Приказ был отдан и проиграл голосование.</summary>
        public bool RefusedCommand;

        /// <summary>
        /// Отказ взвешен «от противного» (<see cref="Counterfactual"/>):
        /// поле <see cref="Decisive"/> — правда, а не пусто по умолчанию.
        /// </summary>
        public bool Weighed;

        /// <summary>
        /// Причина, без которой приказ был бы исполнен. None при Weighed —
        /// ни одна поодиночке не решает: решил характер.
        /// </summary>
        public Counterfactual.Factor Decisive;

        /// <summary>
        /// Вторая причина пары: отказ держался на двух причинах сразу,
        /// без обеих вместе приказ был бы исполнен. None — не пара.
        /// </summary>
        public Counterfactual.Factor DecisiveAlso;

        /// <summary>
        /// Голос души, без которого приказ был бы исполнен (второй круг,
        /// когда ни одна причина положения не решает). Null — всё разом.
        /// </summary>
        public string DecisiveVoice;
    }
}
