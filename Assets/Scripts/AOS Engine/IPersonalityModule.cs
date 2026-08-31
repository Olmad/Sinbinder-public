// Assets/_Project/Scripts/AOS/IPersonalityModule.cs
namespace Sinbinder.AOS
{
    public interface IPersonalityModule
    {
        string ModuleID { get; }
        float Evaluate(Soul soul, DecisionContext context, ActionType action);
    }
}