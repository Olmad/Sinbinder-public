using System;
using System.Reflection;
using Sinbinder.AOS;
using Sinbinder.Core;
using Sinbinder.Gameplay;

// 26.11 (author: "it should depend on the sin"): an ally's death is taken by the witness's dominant sin or virtue.
internal static class WitnessGriefChecks
{
    internal static void Run(Action<bool, string> check)
    {
        check(Dominant(SinType.Wrath, 70f) == EmotionType.Anger, "a wrathful witness is angered by an ally's death");
        check(Dominant(SinType.Pride, 70f) == EmotionType.Anger, "a proud witness is angered by an ally's death");
        check(Dominant(SinType.Sloth, 70f) == EmotionType.Sadness, "a slothful witness despairs at an ally's death");
        check(Dominant(SinType.Gluttony, 70f) == EmotionType.Fear, "a gluttonous witness is frightened for his own flesh");
        check(Dominant(SinType.Wrath, -70f) == EmotionType.Sadness, "a patient witness grieves without fury");

        bool differs = false;
        foreach (SinType sin in Enum.GetValues(typeof(SinType)))
        {
            var g = WitnessGrief.For(Soul(sin, 70f));
            var v = WitnessGrief.For(Soul(sin, -70f));
            check(g.Sadness + g.Anger + g.Fear > 0f && v.Sadness + v.Anger + v.Fear > 0f,
                $"{sin} and its virtue both react to an ally's death");
            if (g.Dominant != WitnessGrief.Plain.Dominant || v.Dominant != WitnessGrief.Plain.Dominant) differs = true;
        }
        check(differs, "witness reactions are no longer one and the same for every soul");
        check(WitnessGrief.For(null).Dominant == EmotionType.Sadness, "a soulless witness keeps the old sorrow");

        // The reaction reaches the decision weights through the real emotion manager.
        EmotionSystem.ForgetAll();
        var system = new EmotionSystem();
        typeof(EmotionSystem).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(system, null);
        var angry = new Warrior { Id = "wrathful witness" };
        var sad = new Warrior { Id = "slothful witness" };
        Feel(system, angry, WitnessGrief.For(Soul(SinType.Wrath, 70f)));
        Feel(system, sad, WitnessGrief.For(Soul(SinType.Sloth, 70f)));
        check(system.GetEmotionWeight(angry, "Wrath") > 1f && system.GetEmotionWeight(angry, "Fear") <= 1f,
            "after a death the wrathful witness hears Wrath louder");
        check(system.GetEmotionWeight(sad, "Fear") > 1f && system.GetEmotionWeight(sad, "Wrath") < 1f,
            "after a death the slothful witness hears Fear louder");
        EmotionSystem.ForgetAll();
    }

    private static EmotionType Dominant(SinType sin, float value) => WitnessGrief.For(Soul(sin, value)).Dominant;

    private static SoulData Soul(SinType sin, float value)
    {
        var spectra = new float[SoulData.SpectrumCount];
        spectra[(int)sin] = value;
        return new SoulData("свидетель", MoralType.Neutral, 1, spectra);
    }

    private static void Feel(EmotionSystem system, Warrior w, Grief g)
    {
        if (g.Sadness > 0f) system.TriggerEmotion(w, EmotionType.Sadness, g.Sadness);
        if (g.Anger > 0f) system.TriggerEmotion(w, EmotionType.Anger, g.Anger);
        if (g.Fear > 0f) system.TriggerEmotion(w, EmotionType.Fear, g.Fear);
    }
}
