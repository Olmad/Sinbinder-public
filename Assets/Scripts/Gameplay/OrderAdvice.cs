// Assets/Scripts/Gameplay/OrderAdvice.cs
// No Unity dependency. Same advice catalogue is used by actual UI and CI.
namespace Sinbinder.Gameplay
{
    /// <summary>
    /// One practical next step when a warrior refuses an order.
    /// Advice is deliberately probabilistic: changing circumstances helps
    /// the player, but cannot force a soul to obey.
    /// No new action, button or core mechanic is invented here.
    /// </summary>
    public static class OrderAdvice
    {
        public static string Next(string factor, string voice = null)
        {
            switch (factor)
            {
                case "Distance": return "Подойдите ближе и повторите приказ.";
                case "Debt": return "Поговорите с воином и выплатите долг.";
                case "Pocket": return "Проверьте его добычу и договоритесь о доле.";
                case "Temptation": return "Проверьте предмет-искуситель и попробуйте другое снаряжение.";
                case "Loot": return "Измените маршрут, чтобы увести его от добычи.";
                case "Patrol": return "Попробуйте более короткий приказ или другого воина.";
                case "AllyInDanger": return "Сначала обезопасьте раненого товарища.";
                case "Wounds": return "Дайте раненому передышку или выберите здорового воина.";
                case "Surrounded": return "Помогите ему выйти из окружения.";
                case "Fatigue": return "Дайте ему передышку или выберите свежего воина.";
                case "Murderer": return "Уберите угрозу его убийц или выберите другого воина.";
            }

            switch (voice)
            {
                case "Greed": return "Проверьте долги, добычу и снаряжение.";
                case "Pride": return "Попробуйте другую задачу или другого воина.";
                case "Wrath": return "Измените обстановку боя или выберите спокойного воина.";
                case "Sloth": return "Дайте передышку или поручите дело более деятельному.";
                case "Fear": return "Снизьте опасность или выберите смелого воина.";
                case "Memory": return "Уберите напоминание о прошлом или выберите другую душу.";
                case "Loyalty": return "Попробуйте укрепить доверие к командиру.";
                case "Morality":
                case "Virtue": return "Попробуйте приказ, который не идёт против его убеждений.";
            }

            return "Попробуйте изменить обстоятельства или выбрать другого воина.";
        }
    }
}
