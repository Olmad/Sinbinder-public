public MissionAction DecideMission(Warrior warrior, MissionContext context)
{
    Dictionary<MissionAction, float> scores = new();
    // ...
    // Голосование модулей
    return scores.OrderByDescending(kv => kv.Value).First().Key;
}