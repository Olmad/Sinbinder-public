// Assets/_Project/Scripts/AOS Engine/Decision.cs
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

        /// <summary>Разрыв между первым и вторым. Мера уверенности.</summary>
        public float Gap;

        /// <summary>Разрыв меньше порога: воин колеблется, действие — Idle.</summary>
        public bool Hesitated;

        /// <summary>Приказ был отдан и проиграл голосование.</summary>
        public bool RefusedCommand;
    }
}
