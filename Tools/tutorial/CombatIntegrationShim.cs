// Minimal collaborators for the production combat and prologue scripts in TutorialChecks.
// The fixture explicitly drives Unity callbacks and the prologue's delayed continuation.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sinbinder.Gameplay;

namespace UnityEngine
{
    internal sealed class SerializeField : Attribute { }
    internal sealed class TooltipAttribute(string text) : Attribute
    {
        public string Text { get; } = text;
    }
    internal sealed class TextAreaAttribute(int minLines, int maxLines) : Attribute
    {
        public int MinLines { get; } = minLines;
        public int MaxLines { get; } = maxLines;
    }
    internal sealed class RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType mode) : Attribute
    {
        public RuntimeInitializeLoadType Mode { get; } = mode;
    }
    internal enum RuntimeInitializeLoadType { SubsystemRegistration }

    public class Object
    {
        public static T FindFirstObjectByType<T>() where T : MonoBehaviour => GameObject.FindComponent<T>();
        public static void DontDestroyOnLoad(Object target) { }
        public static void Destroy(Object target) { }
    }

    public class MonoBehaviour : Object
    {
        public GameObject gameObject { get; internal set; }
        public Transform transform => gameObject.transform;
        public bool isActiveAndEnabled => gameObject.Active;
        public T GetComponent<T>() where T : MonoBehaviour => gameObject.GetComponent<T>();
        public bool TryGetComponent<T>(out T value) where T : MonoBehaviour => gameObject.TryGetComponent(out value);

        private readonly List<IEnumerator> _coroutines = new();

        protected void StartCoroutine(IEnumerator routine)
        {
            if (routine.MoveNext()) _coroutines.Add(routine);
        }

        public void ContinueCoroutines()
        {
            _coroutines.RemoveAll(routine => !routine.MoveNext());
        }
    }

    public sealed class GameObject : Object
    {
        private static readonly List<GameObject> Objects = new();
        private readonly List<MonoBehaviour> _components = new();

        public GameObject(string name)
        {
            Name = name;
            Objects.Add(this);
        }

        public string Name { get; }
        public bool Active { get; set; } = true;
        public Transform transform { get; } = new();

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

        public static GameObject Find(string name) => Objects.Find(go => go.Active && go.Name == name);
        internal static T FindComponent<T>() where T : MonoBehaviour =>
            Objects.Where(go => go.Active).Select(go => go.GetComponent<T>()).FirstOrDefault(component => component != null);

        internal static void ClearScene() => Objects.Clear();
    }

    public sealed class Transform { public Vector3 position { get; set; } }
    public readonly record struct Vector3(float X, float Y, float Z)
    {
        public static float Distance(Vector3 a, Vector3 b) =>
            MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
    }

    internal sealed class WaitForSecondsRealtime(float delay)
    {
        public float Delay { get; } = delay;
    }

    internal static class Mathf
    {
        public static float Abs(float value) => MathF.Abs(value);
        public static float Max(float a, float b) => MathF.Max(a, b);
        public static float Min(float a, float b) => MathF.Min(a, b);
        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
    }

    internal static class Time { public static float unscaledDeltaTime => 0f; }
    internal static class AudioListener { public static float volume { get; set; } }
    internal static class Debug
    {
        public static void Log(string line) { }
        public static void LogWarning(string line) { }
        public static void LogError(string line) { }
    }
}

namespace UnityEngine.SceneManagement
{
    public readonly struct Scene { }

    internal static class SceneManager
    {
        public static event Action<Scene> sceneUnloaded;
        public static List<string> Loads { get; } = new();
        public static void LoadScene(string name) => Loads.Add(name);
        public static void Unload() => sceneUnloaded?.Invoke(default);
    }
}

namespace Sinbinder.Gameplay
{
    using UnityEngine;

    public enum Team { Player, Enemy, Neutral }
    public class Warrior : MonoBehaviour
    {
        public string DisplayName => gameObject.Name;
        public string Id => DisplayName;
        public Team Team { get; set; }
        public float ShellHP => 30f;
        public float Defense => 0f;
        public bool IsLord { get; set; }
        public object Shell => null;
        public object Soul => null;
    }

    internal sealed class SinbinderPlayer : Warrior
    {
        public static bool Exists => false;
        public static Vector3 Where => default;
    }

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
        public static int Shares { get; set; }
        public static void Share(List<Damageable> allies, List<HarvestableBody> bodies) => Shares++;
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
    internal static class PersonalExpedition
    {
        public static void OnGuardAttacked(Warrior warrior, GameObject attacker) { }
        public static void Forget() { }
    }
    internal static class Favor { public static void Killed(Warrior victim, Warrior by) { } }
    internal sealed class CardTable
    {
        public static CardTable Instance => null;
        public void CancelForDeath(Warrior warrior) { }
    }
    internal static class FallenBag
    {
        public static void CreateFor(Damageable killed, HarvestableBody body) { }
        public static void Forget() { }
    }

    internal static class SquadRoster
    {
        public static string CommanderName => "";
        public static void Clear() { }
    }
    internal static class RaidEvent
    {
        public const string SceneName = "Raid";
        public static bool Stage(PrologueDirector director) => false;
        public static void Forget() { }
    }
    internal static class CampFocus { public static float GroundDistance(Vector3 a, Vector3 b) => Vector3.Distance(a, b); }
    internal static class CryptArrival { public static bool Running => false; }
    internal static class CrystalBall { public static bool Leading => false; }
    internal static class CameraPullback { public static void Forget() { } }
    internal static class ExpeditionReplayArchive { public static void Forget() { } }
    internal static class Lesson
    {
        public static void BackStruck(GameObject attacker) { }
        public static void Forget() { }
    }
    internal static class SquadOrders { public static void Reset() { } }
    internal static class WeaponPickup { public static void Forget() { } }
    internal static class Signature { public static void Forget() { } }
    internal static class CampLife { public static void Forget() { } }
    internal static class TrophyChest { public static void Forget() { } }
    internal static class CrowStep { public static void Forget() { } }
    internal static class Provocation { public static void Clear() { } }
    internal static class RatBanner { public static void Reset() { } }
    internal static class SinbinderTrail { public static void Forget() { } }
}

namespace Sinbinder.Core
{
    internal static class Loc { public static string T(string line) => line; }
    internal static class Cost
    {
        public static bool On => false;
        public static IDisposable Of(string label) => null;
    }
    internal static class SaveSystem { public static bool Arriving => false; }
    internal static class Satchel { public static void Forget() { } }
    internal static class Records { public static void ForgetHidden() { } }
    internal sealed class GamePauseController
    {
        public static GamePauseController Instance => null;
        public static bool Stopped => false;
        public bool Held => false;
        public void Resume() { }
    }
}

namespace Sinbinder.AOS
{
    using UnityEngine;

    internal sealed class AOSEventHub
    {
        public static AOSEventHub Instance { get; set; }
        public List<BattleResult> Battles { get; } = new();
        public List<Damageable> Deaths { get; } = new();
        public void OnBattleEnd(BattleResult result) => Battles.Add(result);
        public void OnUnitDied(Damageable victim, GameObject killer, bool lordDown = false) => Deaths.Add(victim);
    }
    internal sealed class AOSWarriorWrapper : MonoBehaviour { public void MarkStruck() { } }
    internal static class MemoryProcessor { public static void Forget() { } }
    internal static class EmotionSystem { public static void Forget() { } }
    internal static class RumourManager { public static void Clear() { } }
}

namespace Sinbinder.Audio
{
    internal static class Sfx { public static void Hit(Warrior warrior, UnityEngine.Vector3 position) { } }
    internal static class Music
    {
        public static bool Silenced { get; set; }
        public static void Stop(float delay) { }
    }
}

namespace Sinbinder.UI
{
    using UnityEngine;

    internal sealed class BattleLogUI : MonoBehaviour
    {
        public List<string> Lines { get; } = new();
        public void Write(string line) => Lines.Add(line);
    }
    internal sealed class DemoEndUI : MonoBehaviour
    {
        public List<bool> Wiped { get; } = new();
        public void Show(bool wiped) => Wiped.Add(wiped);
    }
    internal sealed class SalaryPanelUI : MonoBehaviour
    {
        public static bool Answered => false;
        public static bool Payday => false;
    }
    internal static class GameOverUI { public static void Show(string title, string body) { } }
    internal static class MovementHintUI { public static void Forget() { } }
    internal static class HarvestHintUI { public static void Forget() { } }
    internal static class CommandHintUI { public static void Forget() { } }
    internal static class SurroundedMarkUI { public static void Forget() { } }
    internal static class ViewHintUI { public static void Forget() { } }
    internal static class StaminaHintUI { public static void Forget() { } }
}

namespace Sinbinder.Crypt
{
    internal static class CryptWorkshop { public static bool Waiting => false; }
    internal static class CryptHands { public static void Forget() { } }
    internal static class MissionBoard { public static void Forget() { } }
}

namespace Sinbinder.Inventory
{
    internal sealed class PlayerInventory
    {
        public static PlayerInventory Instance => null;
        public void Forget() { }
    }
}
