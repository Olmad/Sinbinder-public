// Assets/Scripts/AOS Engine/IPersonalityModule.cs
// Перевод: текст через Loc
namespace Sinbinder.AOS
{
    public interface IPersonalityModule
    {
        string ModuleID { get; }
        float Evaluate(Soul soul, DecisionContext context, ActionType action);
    }
}