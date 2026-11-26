// Unity callbacks are driven by the fixture. Combat and damage use production code.
using System;
using System.Collections.Generic;
using System.Linq;
using Sinbinder.Gameplay;
using UnityEngine.SceneManagement;

namespace UnityEngine
{
    internal sealed class SerializeField : Attribute { }
    internal sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        // Без первичного конструктора (C# 12): местный Roslyn Unity 6 его не знает (Tools/ci/unity-dotnet.ps1).
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType mode) { Mode = mode; }
        public RuntimeInitializeLoadType Mode { get; }
    }
    internal enum RuntimeInitializeLoadType { SubsystemRegistration }

    public class Object
    {
        public static void DontDestroyOnLoad(Object target) { }
        public static void Destroy(Object target) { }
    }

    public class MonoBehaviour : Object
    {
        public GameObject gameObject { get; internal set; }
        public Transform transform => gameObject.transform;
        public bool isActiveAndEnabled => gameObject.activeInHierarchy;
        public T GetComponent<T>() where T : MonoBehaviour => gameObject.GetComponent<T>();
        public bool TryGetComponent<T>(out T value) where T : MonoBehaviour => gameObject.TryGetComponent(out value);
    }

    public sealed class GameObject : Object
    {
        private readonly List<MonoBehaviour> _components = new();

        public GameObject(string name) => this.name = name;

        public string name { get; }
        public Scene scene { get; } = SceneManager.GetActiveScene();
        public Transform transform { get; } = new();
        public bool activeInHierarchy { get; private set; } = true;

        public void SetActive(bool active) => activeInHierarchy = active;

        public T AddComponent<T>() where T : MonoBehaviour, new()
        {
            var component = new T { gameObject = this };
            _components.Add(component);
            return component;
        }

        public T GetComponent<T>() where T : MonoBehaviour => _components.OfType<T>().FirstOrDefault();
        public bool TryGetComponent<T>(out T value) where T : MonoBehaviour
        {
            value = GetComponent<T>();
            return value != null;
        }
    }

    public sealed class Transform { public Vector3 position { get; set; } }

    public readonly record struct Vector3(float X, float Y, float Z)
    {
        public static float Distance(Vector3 a, Vector3 b) =>
            MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
    }

    internal static class Mathf
    {
        public static float Abs(float value) => MathF.Abs(value);
        public static float Max(float a, float b) => MathF.Max(a, b);
        public static float Min(float a, float b) => MathF.Min(a, b);
        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
    }

    internal static class Debug { public static void Log(string line) { } }
}

namespace UnityEngine.SceneManagement
{
    public readonly record struct Scene(int handle, string name);

    internal static class SceneManager
    {
        private static Scene _active = new(1, "Combat fixture");

        public static Scene GetActiveScene() => _active;
        public static void LoadScene(string name) => _active = new Scene(_active.handle + 1, name);
    }
}

namespace Sinbinder.Gameplay
{
    using UnityEngine;

    public enum Team { Player, Enemy, Neutral }

    public class Warrior : MonoBehaviour
    {
        public string DisplayName => gameObject.name;
        public string Id { get; set; }
        public Team Team { get; set; }
        public bool IsLord { get; set; }
        public float ShellHP => 30f;
        public float Defense => 0f;
        public object Shell => null;
        public object Soul => null;
    }

    internal sealed class SinbinderPlayer : Warrior { }

    public sealed class HarvestableBody : MonoBehaviour
    {
        public bool Foe { get; set; }
        public void Initialize(object shell, object soul) { }
    }

    internal static class Lords
    {
        public static bool Active { get; set; }
        public static string Of(Warrior warrior) => warrior.IsLord ? warrior.Id : null;
    }

    internal static class Hostility
    {
        public static void Struck(Warrior victim, Warrior by) { }
        public static void NoteKill(Warrior victim, Warrior by) { }
        public static List<Damageable> EnemiesOf(Damageable asker, List<Damageable> allies,
            List<Damageable> enemies, List<Damageable> neutrals) => enemies;
        public static List<Damageable> FriendsOf(Damageable asker, List<Damageable> neutrals) => neutrals;
    }

    internal static class TugMatch
    {
        public static List<Warrior> Knockdowns { get; } = new();
        public static void LordDown(Warrior victim) => Knockdowns.Add(victim);
    }

    internal static class LootChain
    {
        public static List<HarvestableBody[]> Shares { get; } = new();
        public static void Share(List<Damageable> allies, List<HarvestableBody> bodies) => Shares.Add(bodies.ToArray());
    }

    internal static class Bodyguard { public static bool Absorb(Damageable victim, GameObject attacker, float damage) => false; }
    internal static class Gift { public static float Modify(float damage, GameObject attacker, Warrior victim) => damage; }
    internal static class Facing
    {
        public static float DamageMultiplier(Transform victim, Vector3 attacker) => 1f;
        public static bool IsFromBehind(Transform victim, Vector3 attacker) => false;
    }

    internal sealed class FirstPersonCombat : MonoBehaviour { public float ReduceIncoming(float damage, GameObject attacker) => damage; }
    internal sealed class Engagement : MonoBehaviour { public float IncomingMultiplier => 1f; }
    internal sealed class WarriorAnimation : MonoBehaviour { public void Struck() { } }
    internal static class HitBurst { public static void At(Warrior warrior, Vector3 position) { } }
    internal static class DreamTutorialDirector { public static bool ProtectingHero => false; }

    internal sealed class PersonalExpedition
    {
        public static PersonalExpedition DialogueVisit { get; set; }
        public HashSet<Warrior> Travellers { get; } = new();
        public HashSet<Warrior> Guards { get; } = new();
        public static bool Travels(Warrior warrior) => DialogueVisit?.Travellers.Contains(warrior) == true;
        public bool HasGuard(Warrior warrior) => Guards.Contains(warrior);
        public static void OnGuardAttacked(Warrior warrior, GameObject attacker) { }
    }

    internal static class Favor { public static void Killed(Warrior victim, Warrior by) { } }

    internal sealed class CardTable
    {
        public static CardTable Instance => null;
        public void CancelForDeath(Warrior warrior) { }
    }

    internal static class FallenBag { public static void CreateFor(Damageable killed, HarvestableBody body) { } }
    internal static class Lesson { public static void BackStruck(GameObject attacker) { } }
}

namespace Sinbinder.Core
{
    internal static class Cost
    {
        public static bool On => false;
        public static IDisposable Of(string label) => null;
    }
}

namespace Sinbinder.AOS
{
    using UnityEngine;

    internal sealed class AOSEventHub
    {
        public static AOSEventHub Instance { get; set; }
        public List<(BattleResult Result, Damageable[] Survivors)> Battles { get; } = new();

        public void OnBattleEnd(BattleResult result, List<Damageable> survivors) =>
            Battles.Add((result, survivors.ToArray()));
    }

    internal sealed class AOSWarriorWrapper : MonoBehaviour { public void MarkStruck() { } }
}

namespace Sinbinder.Audio
{
    internal static class Sfx { public static void Hit(Warrior warrior, UnityEngine.Vector3 position) { } }
}
