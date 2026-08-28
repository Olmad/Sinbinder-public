// Assets/_Project/Scripts/AOS Engine/IMissionModule.cs
namespace Sinbinder.AOS
{
    /// <summary>
    /// Необязательное расширение личностного модуля: голос в мирных миссиях.
    ///
    /// Боевой голос обязателен — его объявляет IPersonalityModule.
    /// Мирный голос — нет: модулю Страха нечего сказать о том, обложить
    /// деревню данью или помочь ей. Такой модуль просто не реализует
    /// этот интерфейс и молчит на совете.
    ///
    /// BehaviourResolver.DecideMission опрашивает только тех, кто его реализует.
    /// </summary>
    public interface IMissionModule
    {
        float EvaluateMission(Soul soul, MissionContext context, MissionAction action);
    }
}
