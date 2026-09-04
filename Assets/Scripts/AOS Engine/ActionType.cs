// Assets/Scripts/AOS Engine/ActionType.cs
namespace Sinbinder.AOS
{
    public enum ActionType
    {
        // Базовые действия
        Attack,
        SaveAlly,
        Loot,
        Flee,
        Idle,
        ObeyCommand,
        
        // Подкуп и переманивание
        BribeEnemy,
        AcceptBribe,
        
        // Навыки Гнева (Wrath)
        Berserk,
        PowerStrike,
        
        // Навыки Терпения (Patience)
        IronStance,
        CounterAttack,
        SecondWind,
        Unshakable,
        
        // Навыки Жадности (Greed)
        GoldRush,
        ShareGold,
        
        // Навыки Гордыни (Pride)
        DuelChallenge,
        HeroicPose,
        Inspiration,
        LastStand,
        
        // Навыки Смирения (Humility)
        ShieldOfFaith,
        Cleanse,
        AuraOfHumility,
        Sacrifice,
        
        // Навыки Зависти (Envy)
        StealWeapon,
        CopySkill,
        DrainHealth,
        StealEssence,
        
        // Навыки Доброжелательности (Goodwill)
        Gift,
        Encourage,
        ShareHealth,
        CollectiveStrength,
        
        // Навыки Похоти (Lust)
        Charm,
        KissOfDeath,
        Seduce,
        FatalPassion,
        
        // Навыки Целомудрия (Chastity)
        VowOfPurity,
        Clarity,
        Mentor,
        SpiritualShield,
        
        // Навыки Чревоугодия (Gluttony)
        Devour,
        BellyArmor,
        Vomit,
        InsatiableHunger,
        
        // Навыки Умеренности (Temperance)
        Ration,
        StarvationResist,
        CleansePoison,
        CommonTreasury,
        
        // Навыки Уныния (Sloth)
        Yawn,
        LazyHeal,
        AuraOfApathy,
        EternalSleep,
        
        // Навыки Усердия (Diligence)
        WorkSurge,
        Sleepless,
        WorkInspiration,
        Tireless,
        
        // Навыки Алхимика
        HealAlly,
        AcidBomb,
        SmokeScreen,
        
        // Навыки Лучника
        PowerShot,
        ArrowRain,
        RetreatShot,
    }
}