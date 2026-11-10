// Only used by .NET CI to compile/check ServerWarriorOrderBridge
// against the real, stable public Warrior API. This is NOT shipped in Unity.
using UnityEngine;

namespace Sinbinder.Gameplay
{
    public enum Team { Player, Enemy, Neutral }
    public enum CommandKind
    {
        None, Move, Attack, Hold, Defend, FallBack, Patrol, AttackMove
    }

    public class Damageable : MonoBehaviour
    {
        public float HP { get; set; } = 30f;
        public float MaxHP { get; set; } = 30f;
    }

    public class UnitMover : MonoBehaviour
    {
        public Vector3 Destination;
        public GameObject AttackTarget;
        public void CommandMove(Vector3 point) { Destination = point; }
        public void CommandAttack(GameObject target) { AttackTarget = target; }
    }

    public class SinbinderPlayer : Warrior { }
    public class RivalLord : MonoBehaviour { public int Side { get; set; } }

    public static class MapLoader
    {
        public static Sinbinder.Core.MapData Current { get; set; }
    }

    public class Warrior : MonoBehaviour
    {
        public Team Team { get; set; }
        public bool IsDead { get; set; }
        public CommandKind LastCommand;
        public Vector3 LastPoint;
        public GameObject LastTarget;
        public string LastLord;
        public float LastMuffle;
        public UnitMover TestMover;

        public new T GetComponent<T>() where T : class
        {
            if (typeof(T) == typeof(UnitMover))
                return TestMover as T;
            return base.GetComponent<T>();
        }

        public void IssueCommand(CommandKind kind, Vector3 point, GameObject target = null,
            float muffle = 0f, string lord = null)
        {
            LastCommand = kind;
            LastPoint = point;
            LastTarget = target;
            LastMuffle = muffle;
            LastLord = lord;
        }

        public void IssuePatrol(Vector3 to, float muffle = 0f, string lord = null)
        {
            IssueCommand(CommandKind.Patrol, to, null, muffle, lord);
        }
    }

    public static class Voice
    {
        public static bool Heard(float muffle) { return muffle < 1f; }
        public static float Muffle(float distance)
        { return distance <= 100f ? 0.2f : 1f; }
    }
}
