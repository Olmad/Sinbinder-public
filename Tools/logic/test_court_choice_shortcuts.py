"""Guard the actual Court onboarding copy and its manual shortcut contract.

Run from repository root: python -m unittest Tools.logic.test_court_choice_shortcuts
This is a source-linked regression guard, not a Unity UI test.
"""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
DREAM = ROOT / "Assets/Scripts/Gameplay/DreamTutorialDirector.cs"
EN = ROOT / "Assets/Resources/Lang/en.txt"
SELECTION = ROOT / "Assets/Scripts/Gameplay/SelectionManager.cs"
RU = "Не показывать автоматически (U — краткий Суд, Shift+U — полный)"
ENGLISH = "Do not show automatically (U: brief Court, Shift+U: full Court)"


class CourtChoiceShortcutCopyTests(unittest.TestCase):
    def test_choice_and_translation_match_real_shortcuts(self):
        dream = DREAM.read_text(encoding="utf-8")
        english = EN.read_text(encoding="utf-8")
        selection = SELECTION.read_text(encoding="utf-8")
        self.assertIn('Loc.T("' + RU + '")', dream)
        self.assertIn('msgid "' + RU + '"\nmsgstr "' + ENGLISH + '"', english)
        self.assertIn("bool showFullCourt = Input.GetKey(KeyCode.LeftShift)", selection)
        self.assertIn("Input.GetKey(KeyCode.RightShift)", selection)
        self.assertIn("brief: !showFullCourt", selection)
        self.assertIn("force: true", selection)

    def test_interrupted_choice_releases_input_lock(self):
        dream = DREAM.read_text(encoding="utf-8")
        disable = dream.split("private void OnDisable()", 1)[1].split("private void OnDestroy()", 1)[0]
        self.assertIn("_courtChoicePending = false;", disable)
        self.assertIn("ReleaseCourtChoicePause();", disable)
        self.assertIn("_handBusy = false;", disable)
        # Normal completion must still restore the pre-modal lock state.
        preview = dream.split("private IEnumerator OfferShortCourtPreview", 1)[1].split("private void ChooseAutomaticCourts", 1)[0]
        self.assertIn("bool wasHandBusy = _handBusy;", preview)
        self.assertIn("_handBusy = wasHandBusy;", preview)
        self.assertIn("while (_courtChoicePending) yield return null;", preview)

    def test_disabling_cancels_pending_coroutines_before_unlock(self):
        dream = DREAM.read_text(encoding="utf-8")
        disable = dream.split("private void OnDisable()", 1)[1].split("private void OnDestroy()", 1)[0]
        # Negative regression: enabled=false alone does not stop Unity coroutines.
        # A pending choice must not later launch a brief Court or restore stale input.
        self.assertIn("StopAllCoroutines();", disable)
        self.assertLess(disable.index("StopAllCoroutines();"), disable.index("_handBusy = false;"))
        self.assertIn("_courtRunning = false;", disable)
        preview = dream.split("private IEnumerator OfferShortCourtPreview", 1)[1].split("private void ChooseAutomaticCourts", 1)[0]
        self.assertIn("while (_courtChoicePending) yield return null;", preview)
        self.assertIn("originalVotes: originalVotes", preview)
        self.assertNotIn("AOS.Decide(", preview)

    def test_old_misleading_copy_is_absent(self):
        self.assertNotIn("Не показывать автоматически (U — Суд вручную)",
                         DREAM.read_text(encoding="utf-8"))
        self.assertNotIn('msgstr "Do not show automatically (U: open Court manually)"',
                         EN.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
