using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Sinbinder.Core;
using Sinbinder.Dialogue;

internal static class Program
{
    private static int count;
    private static void Check(bool good, string reason)
    {
        if (!good) throw new Exception("FAIL: " + reason);
        count++;
    }

    private static void Main()
    {
        var sins = (SinType[])Enum.GetValues(typeof(SinType));
        Check(sins.Length == 7, "exactly seven personality sins");
        foreach (var a in sins)
        {
            var replies = new HashSet<string>();
            foreach (var b in sins)
            {
                string reply = VillageDialogueRules.SinAnswer(a, b);
                Check(!string.IsNullOrWhiteSpace(reply), $"response to {a} by {b} is not empty");
                Check(!reply.StartsWith("["), "no placeholder responses");
                replies.Add(reply);
            }
            Check(replies.Count == sins.Length, $"different responders answer {a} differently");
        }
        Check(VillageDialogueRules.SinAnswer(SinType.Greed, SinType.Gluttony).Contains("хлеб"),
              "hungry villager answers the actual savings question");
        Check(VillageDialogueRules.SinAnswer(SinType.Pride, SinType.Gluttony).Contains("поесть"),
              "hungry villager challenges Pride, not the money question");
        Check(VillageDialogueRules.SinAnswer(SinType.Wrath, SinType.Gluttony).Contains("поешь"),
              "hungry villager responds to a threat with advice to eat");
        // A vice-conditioned reply must not claim witnessed reputation
        // or a recent meal without any memory/state predicate.
        Check(VillageDialogueRules.SinAnswer(SinType.Pride, SinType.Envy) ==
              "Думаешь, тебе первому место? С чего бы?",
              "Envy questions Pride without inventing community ranking");
        Check(VillageDialogueRules.SinAnswer(SinType.Gluttony, SinType.Pride) ==
              "Тебе только еда на уме?",
              "Pride answers Gluttony without inventing a recent meal");
        // These lines previously asserted events never supplied by the dialogue
        // context. Test the exact RU/EN runtime answers for both topics.
        var groundedSin = new (SinType Speaker, SinType Responder, string Ru, string En)[]
        {
            (SinType.Greed, SinType.Envy,
             "Сначала своим похвастайся, потом моё считай.",
             "Show off what you have first, then count mine."),
            (SinType.Lust, SinType.Envy,
             "А ко мне кто-нибудь подсесть захочет?",
             "And would anyone want to sit beside me?"),
            (SinType.Gluttony, SinType.Envy,
             "Тебе хочется, а мне что достанется?",
             "You're hungry, but what will I get?"),
            (SinType.Sloth, SinType.Envy,
             "Тебе бы всё сидеть. А мне что, за всех вставать?",
             "You'd rather sit all day. And I'm supposed to get up for everyone?")
        };
        var groundedLord = new (VillageLordTone Tone, string Ru, string En)[]
        {
            (VillageLordTone.Distrusted,
             "Пусть другим нравится. Я ему не верю.",
             "Others may like him. I don't trust him."),
            (VillageLordTone.Undecided,
             "Посмотрим, кому от его власти станет лучше.",
             "We'll see who benefits from his rule.")
        };
        foreach (var v in groundedSin)
            Check(VillageDialogueRules.SinAnswer(v.Speaker, v.Responder) == v.Ru,
                  "RU SinAnswer has no invented memory: " + v.Speaker + "/" + v.Responder);
        foreach (var v in groundedLord)
            Check(VillageDialogueRules.LordAnswer(SinType.Envy, v.Tone) == v.Ru,
                  "RU LordAnswer has no invented events: " + v.Tone);
        foreach (var sin in sins)
        {
            string positive = VillageDialogueRules.LordAnswer(sin, VillageLordTone.Respected);
            string negative = VillageDialogueRules.LordAnswer(sin, VillageLordTone.Distrusted);
            string invited = VillageDialogueRules.LordAnswer(sin, VillageLordTone.Undecided);
            Check(!string.IsNullOrWhiteSpace(positive) && !string.IsNullOrWhiteSpace(negative)
                  && !string.IsNullOrWhiteSpace(invited), "every reputation gets an answer");
            Check(positive != negative && positive != invited && negative != invited,
                  $"three different replies for Lord standing, sin={sin}");
            Check(!positive.StartsWith("Звал") && !negative.StartsWith("Звал")
                  && !invited.StartsWith("Звал"), "no phantom invitation in reputation lines");
        }
        foreach (int stamp in new[] { int.MinValue, -9, -1, 0, 1, 2, 3, 4, 5, int.MaxValue })
            Check(VillageDialogueRules.TopicIndex(stamp) >= 0 &&
                  VillageDialogueRules.TopicIndex(stamp) < 4, "topic index always 0..3");

        // Check real localisation for every production village reply.
        Loc.Use("en", File.ReadAllText("Assets/Resources/Lang/en.txt"));
        foreach (var a in sins)
        {
            foreach (var b in sins)
            {
                string reply = VillageDialogueRules.SinAnswer(a, b);
                Check(!Regex.IsMatch(reply, "[А-Яа-яЁё]"), "village reply translated: " + a + "/" + b);
            }
            foreach (VillageLordTone stance in Enum.GetValues(typeof(VillageLordTone)))
                Check(!Regex.IsMatch(VillageDialogueRules.LordAnswer(a, stance), "[А-Яа-яЁё]"),
                    "Lord reply translated: " + a + "/" + stance);
        }
        foreach (var v in groundedSin)
            Check(VillageDialogueRules.SinAnswer(v.Speaker, v.Responder) == v.En,
                  "EN SinAnswer matches verified RU: " + v.Speaker + "/" + v.Responder);
        foreach (var v in groundedLord)
            Check(VillageDialogueRules.LordAnswer(SinType.Envy, v.Tone) == v.En,
                  "EN LordAnswer matches verified RU: " + v.Tone);
        Loc.Use("ru", null);
        Console.WriteLine($"PASS: {count} production-linked village dialogue assertions");
    }
}
