public static class MissionDatabase
{
    public static MissionAction GetAction(SinType sin, MoralType moral)
    {
        // Возвращает MissionAction для каждой комбинации
        // Пример для Гнева:
        if (sin == SinType.Wrath && moral == MoralType.Vicious) return MissionAction.KillEveryone;
        if (sin == SinType.Wrath && moral == MoralType.Pious) return MissionAction.KillTraveler;
        // ...
    }
}