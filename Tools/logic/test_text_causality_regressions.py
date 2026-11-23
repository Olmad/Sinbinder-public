"""Source-linked checks for corrected player-facing text, NOT Unity playtests.

Checks exact text contracts and paired RU/EN messages on the game branch.
Independent manual semantic coverage of *all* game texts is tracked in 88.
"""
from __future__ import annotations

import re
import unittest
from pathlib import Path

from Tools.logic.check_whisper_semantics import KEYS, ROOT, extract, scan


def read(relative: str) -> str:
    return (ROOT / relative).read_text(encoding="utf-8")


class CorrectedTextRegressions(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.whisper = read("Assets/Scripts/Gameplay/Whisper.cs")
        cls.court = read("Assets/Scripts/Gameplay/SoulCourtReasons.cs")
        cls.tutorial = read("Assets/Scripts/Gameplay/DreamTutorialDirector.cs")
        cls.encounter = read("Assets/Scripts/Dialogue/EncounterLines.cs")
        cls.locale = read("Assets/Resources/Lang/en.txt")
        cls.approvals = __import__("json").loads(read("Tools/logic/whisper_semantics_approved.json"))["approved"]

    def test_whisper_all_seven_pairs_source_reviewed_and_translated(self):
        result = scan(self.whisper, self.locale, self.approvals)
        self.assertEqual(set(KEYS), {item["sin"] for item in result})
        self.assertEqual([], [(item["sin"], item["status"]) for item in result
                              if item["status"] != "SOURCE_REVIEWED"])

    def test_whisper_has_no_unverified_specific_people_history_or_rewards(self):
        phrases = ("Она смотрит.", "Он смеялся над тобой у огня.",
                   "Карган бы смог.", "Половина добычи — твоя.",
                   "После — котёл твой.", "до утра никто не тронет")
        for phrase in phrases:
            with self.subTest(phrase=phrase):
                self.assertNotIn(phrase, self.whisper)
        pairs = extract(self.whisper)
        self.assertIn("желание", pairs["Lust"][0])
        self.assertIn("гнев", pairs["Wrath"][0])
        self.assertIn("покоя", pairs["Sloth"][0])

    def test_whisper_payment_promises_exact_amount_spent(self):
        m = re.search(r"private const int GreedCoins = (\d+);", self.whisper)
        self.assertIsNotNone(m)
        self.assertEqual(3, int(m.group(1)),
                         "When price changes, update localized offer and tests")
        self.assertIn("За твой ответ я заплачу три монеты.", self.whisper)
        self.assertIn("!bag.SpendGold(GreedCoins)", self.whisper)
        self.assertIn("w.Pocket(GreedCoins)", self.whisper)
        self.assertIn("canPay = false;", self.whisper)
        self.assertIn("w.CommandRevision == agreedOrder", self.whisper)

    def test_whisper_insufficient_funds_not_described_as_empty_wallet(self):
        self.assertIn("Gold < GreedCoins", self.whisper)
        for text in ("Трёх монет у тебя нет.",
                     "Монет на уговор больше не хватает.",
                     "Уговор не состоялся: не хватает монет.",
                     "На уговор жадного не хватило монет."):
            self.assertIn(text, self.whisper)
            self.assertIn('msgid "' + text + '"', self.locale)
        self.assertNotIn("Кошель уже пуст.", self.whisper)

    def test_court_gendered_virtue_explanations_are_explicit_pairs(self):
        for phrase in ("Добрых черт в ней больше, чем грешных. А добрая слушается.",
                       "Грешных черт в ней больше, чем добрых. А грешница слушаться не привыкла."):
            self.assertIn("Loc.T(" + '"' + phrase + '"' + ")", self.court)
            self.assertIn('msgid "' + phrase + '"', self.locale)
        # Grammar.For changes pronouns but not 'добрый' or 'грешник'.
        self.assertRegex(self.court, r': P\(w, Loc\.T\("Добрых черт в нём')
        self.assertRegex(self.court, r': P\(w, Loc\.T\("Грешных черт в нём')

    def test_court_does_not_call_one_coin_a_full_purse(self):
        self.assertNotIn("У него полный кошель.", self.court)
        self.assertIn('if (c.PocketGold > 0) return Loc.T("В кошеле есть золото.', self.court)
        self.assertIn('return wants ? (c.PocketGold > 0', self.court)

    def test_court_memory_no_longer_invents_a_specific_personal_event(self):
        for phrase in ("Его однажды спасли.", "Его однажды предали."):
            self.assertNotIn(phrase, self.court)
        self.assertIn("Память тянет помочь союзнику.", self.court)
        self.assertIn("Память удерживает от помощи.", self.court)

    def test_tutorial_does_not_claim_sole_cause_of_changed_decision(self):
        self.assertNotIn("Причины больше нет: убийцы мертвы.", self.tutorial)
        self.assertNotIn("у его души нашлась другая причина.", self.tutorial)
        self.assertIn("Теперь он послушался приказа.", self.tutorial)
        self.assertIn("Приказ снова не выполнен. Обстановка изменилась.", self.tutorial)

    def test_combat_dialogue_does_not_invent_tasting_specific_vials(self):
        self.assertNotIn("Твои склянки на вкус пробовал.", self.encounter)
        self.assertIn("Алхимик. От твоих зелий хоть сытым станешь?", self.encounter)

    def test_court_attack_reasons_do_not_invent_unobserved_events(self):
        # Wrath/Patience choose explanations from the judge's preference,
        # not from a checked enemy range or the first actual attack tick.
        attack = self.court.split("private static string Attack(", 1)[1].split(
            "private static string Idle(", 1)[0]
        for old in ("В нём гнев. Гнев хочет крови, а враг рядом.",
                    "Терпения в нём нет. Ждать он не умеет — бьёт первым."):
            self.assertNotIn(old, attack)
            self.assertNotIn('msgid "' + old + '"', self.locale)
        revisions = (
            ("В нём гнев. Гнев требует драки.",
             "Wrath burns within. Wrath demands a fight."),
            ("В нём нет терпения. Ему не терпится вступить в бой.",
             "Patience is gone. This warrior is eager to fight."),
        )
        for ru, en in revisions:
            with self.subTest(ru=ru):
                self.assertIn('Loc.T("' + ru + '")', attack)
                self.assertEqual(1, self.locale.count('msgid "' + ru + '"'))
                self.assertIn('msgid "' + ru + '"\nmsgstr "' + en + '"',
                              self.locale)

    def test_court_abstention_avoids_unsupported_world_facts(self):
        # A small order/rival net is not evidence that the world contains
        # no threats, debts, loot or memories. Inspect the production path,
        # not a second hand-written model of the AOS rules.
        court_scene = read("Assets/Scripts/Gameplay/SoulCourt.cs")
        self.assertIn("Mathf.Abs(Net) < Audible", court_scene)
        self.assertIn("order.FindAll(x => x.Abstains && !shortly)", court_scene)
        self.assertIn("SoulCourtReasons.Idle(j.Module, w)", court_scene)
        idle = self.court.split("public static string Idle(string module, Warrior w)", 1)[1].split(
            "// ── что в душе ──", 1)[0]
        safe = {
            "Loyalty": "Воздерживаюсь: верность не выбрала сторону.",
            "Fear": "Воздерживаюсь: страх не выбрал сторону.",
            "Memory": "Воздерживаюсь: память не выбрала сторону.",
            "Morality": "Воздерживаюсь: совесть не выбрала сторону.",
            "Virtue": "Воздерживаюсь: добродетель не выбрала сторону.",
            "Patience": "Воздерживаюсь: терпение не выбрало сторону.",
            "Greed": "Воздерживаюсь: жадность не выбрала сторону.",
            "Pride": "Воздерживаюсь: гордыня не выбрала сторону.",
            "Wrath": "Воздерживаюсь: гнев не выбрал сторону.",
            "Envy": "Воздерживаюсь: зависть не выбрала сторону.",
            "Lust": "Воздерживаюсь: страсть не выбрала сторону.",
            "Gluttony": "Воздерживаюсь: чревоугодие не выбрало сторону.",
            "Sloth": "Воздерживаюсь: уныние не выбрало сторону.",
        }
        self.assertEqual(set(safe), set(re.findall(r'case "([^"]+)":', idle)))
        for module, phrase in safe.items():
            with self.subTest(module=module):
                self.assertRegex(idle, r'case "' + re.escape(module) +
                                 r'":\s*return Loc\.T\("' + re.escape(phrase) + r'"\);')
                self.assertEqual(1, self.locale.count('msgid "' + phrase + '"'))
                match = re.search(r'^msgid "' + re.escape(phrase) +
                                  r'"\s*\nmsgstr "([^"]+)"', self.locale, re.MULTILINE)
                self.assertIsNotNone(match)
                self.assertFalse(re.search(r"[А-Яа-яЁё]", match.group(1)))
        for unproved in (
            "обид и долгов тут нет", "его жизни этот приказ не грозит",
            "делить тут нечего", "мстить тут некому",
            "честь его тут не задета", "сравнивать тут не с кем",
        ):
            self.assertNotIn(unproved, idle)

    def test_all_revised_ru_lines_have_single_english_translation(self):
        phrases = (
            "За твой ответ я заплачу три монеты.",
            "Направь своё желание на этот приказ.",
            "В кошеле есть золото. Жалко рисковать им в драке.",
            "В кошеле есть золото. Лучше переждать в стороне.",
            "Жадность велит держаться в стороне.",
            "Память тянет помочь союзнику.",
            "Память удерживает от помощи.",
            "Добрых черт в ней больше, чем грешных. А добрая слушается.",
            "Грешных черт в ней больше, чем добрых. А грешница слушаться не привыкла.",
            "Теперь он послушался приказа. Убийц рядом больше нет.",
            "Теперь она послушалась приказа. Убийц рядом больше нет.",
            "Приказ снова не выполнен. Обстановка изменилась. Наведите на воина мышь, чтобы узнать, что повлияло на решение.",
            "Алхимик. От твоих зелий хоть сытым станешь?",
        )
        for phrase in phrases:
            with self.subTest(phrase=phrase):
                self.assertEqual(1, self.locale.count('msgid "' + phrase + '"'))
                m = re.search(r'^msgid "' + re.escape(phrase) +
                              r'"\s*\nmsgstr "([^"]+)"', self.locale, re.MULTILINE)
                self.assertIsNotNone(m, "Missing/empty EN for " + phrase)
                self.assertFalse(re.search(r"[А-Яа-яЁё]", m.group(1)))

if __name__ == "__main__":
    unittest.main()
