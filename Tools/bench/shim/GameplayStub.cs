// Заглушка воина: только те члены, которых касается логика голосования.
// Настоящий Warrior — MonoBehaviour с боем, навигацией и сценой; для
// подсчёта голосов ничего этого не нужно.
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    public enum Team { Player, Enemy }

    public class Warrior
    {
        public string Id = System.Guid.NewGuid().ToString();
        public SoulData Soul;
        public string DisplayName => Soul != null ? Soul.Name : "";
        public float Loyalty = 50f;
        public int UnpaidMissions;
        public float HP = 30f, MaxHP = 30f;
        public bool IsDead;
        public Team Team = Team.Player;
    }
}
