"""Focused source-linked checks for event and combat dialogue grounding.

These checks do not claim to validate every player-facing sentence, nor to
verify Unity scene rendering. Full independent 5x audit remains required.
"""
import re
import unittest
from Tools.logic.check_whisper_semantics import ROOT


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


class PrisonerAndSharedHistoryText(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.events = read("Assets/Scripts/Gameplay/ExpeditionBook.cs")
        cls.map_events = read("Assets/Scripts/Gameplay/MapEvents.cs")
        cls.residents = read("Assets/Scripts/Gameplay/Residents.cs")
        cls.soul = read("Assets/Scripts/Core/Soul/SoulData.cs")
        cls.encounter = read("Assets/Scripts/Dialogue/EncounterLines.cs")
        cls.locale = read("Assets/Resources/Lang/en.txt")

    def test_legacy_event_id_preserved_and_story_no_longer_invents_female_healer(self):
        # The option has nested new[] { ... } initializers. Never parse it
        # with [^}]+: that stops at the first enum array's closing brace.
        start = self.events.index('new() { Id = "healer"')
        end = self.events.index('new() { Id = "relic"', start)
        event = self.events[start:end]
        m = re.search(r'Title = Loc\.N\("([^"]+)"\), Story = Loc\.N\("([^"]+)"\),', event)
        self.assertIsNotNone(m, "Check the title and story fields")
        title, story = m.groups()
        a, b = event.split('B = new() {', 1)
        self.assertIn("Guards = 3", a)
        self.assertEqual("Конвой с пленником", title)
        self.assertIn("связанного человека", story)
        self.assertNotIn("знахар", title.lower() + story.lower())
        self.assertNotIn("она", story.lower())
        self.assertIn("Recruits = 1", a)
        self.assertIn("Souls = 1", a)
        self.assertIn("CoinsMin = 10, CoinsMax = 10", b)
        self.assertIn("Забрать золото, пленника оставить", b)
        self.assertIn("_home.Newcomer(", self.map_events)
        self.assertIn("WarriorGenerator.Make(seed, SoulOrigin.Sortie, present)", self.residents)
        self.assertIn("Gender gender = Gender.Male", self.soul)

    def test_event_and_all_new_brother_texts_are_translated(self):
        strings = (
            "Конвой с пленником",
            "Стража ведёт связанного человека на суд. Вмешаться можно, но без боя не обойдётся.",
            "Освободить пленника",
            "Забрать золото, пленника оставить",
            "Свою долю я посчитаю. Твою не трону.",
            "При тебе отступить? Да мне потом самому в глаза не смотреть.",
            "Ты слева. Я справа. Только под руку не попадись.",
            "Сейчас не до зависти. Стань рядом.",
            "Не вздумай исчезнуть. Я должен тебя видеть.",
            "Доживём до ужина, тогда и о еде поспорим.",
            "Мне бы не шевелиться. А придётся тебя прикрывать.",
            "Постоим рядом. Так хоть молчать легче.",
            "Мне всё равно, чем кончится. Но в одиночку хуже.",
            "Будет еда, делим на двоих. Чужих не зови.",
        )
        for ru in strings:
            with self.subTest(ru=ru):
                hits = re.findall(r'^msgid "' + re.escape(ru) +
                                  r'"\r?\nmsgstr "([^"]+)"', self.locale, re.MULTILINE)
                self.assertEqual(1, len(hits), "Translation missing/duplicate for " + ru)
                self.assertFalse(re.search(r"[А-Яа-яЁё]", hits[0]))

    def test_brother_sin_moral_matrix_stays_7_by_3(self):
        before = 'private static readonly string[][] Brothers = new string[Sins][]'
        end = "// ---------- о легенде"
        start = self.encounter.index(before)
        stop = self.encounter.index(end, start)
        body = self.encounter[start:stop]
        self.assertEqual(21, len(re.findall(r'Loc\.N\("', body)))
        self.assertIn("if (Brother(speaker) && Brother(target)) return Brothers[me];", self.encounter)
        self.assertIn('p.PerkName == "Брат по оружию"', self.encounter)

    def test_no_unverified_pairwise_memories_in_brother_matrix(self):
        start = self.encounter.index("private static readonly string[][] Brothers")
        stop = self.encounter.index("// ---------- о легенде", start)
        body = self.encounter[start:stop]
        unsupported = (
            "как тогда", "как всегда", "Ты знаешь.",
            "Спасибо, что позвал", "Ты один меня с места",
            "Нам обоим всё равно",
        )
        for text in unsupported:
            with self.subTest(text=text):
                self.assertNotIn(text, body)

    def test_old_event_localization_cannot_be_displayed_from_book(self):
        self.assertNotIn('Loc.N("Пленённая знахарка")', self.events)
        self.assertNotIn('Loc.N("Её ведут на суд. Она знает травы — и души.")', self.events)
        self.assertNotIn('Loc.N("Отдать Ордену за награду")', self.events)


if __name__ == "__main__":
    unittest.main()
