// Assets/Scripts/AOS Engine/DeedType.cs
namespace Sinbinder.AOS
{
    /// <summary>
    /// Тип деяния воина. Деяния копятся в ReputationData и превращаются
    /// в титулы через TitleManager. Это внешняя система: на голосование
    /// модулей AOS она не влияет.
    /// </summary>
    public enum DeedType
    {
        Kill,
        KillCommander,
        SaveAlly,
        ProtectCommander,
        CollectMostLoot,
        FindTreasure,
        DigMostSouls,
        SurviveMission,
        LastStand,
        NeverRetreat,
        Escape,
        ExecuteEnemy,
        RecruitWarrior,

        /// <summary>
        /// Был в бою и не ударил ни разу. Пишется в конце боя тому,
        /// кто выжил, не нанеся ни одного удара, — зеркало
        /// <see cref="NeverRetreat"/>. На нём стоит «Тень»: до 24 сентября
        /// она стояла на <see cref="SurviveMission"/>, которое конец боя
        /// пишет каждому уцелевшему, и после первого же боя «Тенью»
        /// становились все.
        /// </summary>
        StayedOut
    }
}
