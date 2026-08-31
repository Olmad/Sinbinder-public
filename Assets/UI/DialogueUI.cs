// Assets/UI/DialogueUI.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Dialogue;
using Sinbinder.Gameplay;
using Sinbinder.Audio;

namespace Sinbinder.UI
{
    public class DialogueUI : MonoBehaviour
    {
        [SerializeField] private GameObject _dialoguePanel;
        [SerializeField] private Text _speakerNameText;
        [SerializeField] private Text _dialogueText;
        [SerializeField] private float _fadeDelay = 0.5f;
        [SerializeField] private DialogueCameraController _cameraController;
        [SerializeField] private bool _useVoice = true; // Включить/выключить голос

        private Queue<DialogueLine> _queue = new();
        private bool _isShowing = false;
        private List<Warrior> _allWarriors;

        void Start()
        {
            var trigger = FindObjectOfType<DialogueTrigger>();
            if (trigger != null)
            {
                trigger.OnDialogueStart += OnDialogueStart;
                trigger.OnLineAdded += OnLineAdded;
            }

            if (_cameraController == null)
                _cameraController = FindObjectOfType<DialogueCameraController>();

            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(false);
        }

        void OnDestroy()
        {
            var trigger = FindObjectOfType<DialogueTrigger>();
            if (trigger != null)
            {
                trigger.OnDialogueStart -= OnDialogueStart;
                trigger.OnLineAdded -= OnLineAdded;
            }
        }

        private void OnDialogueStart(List<DialogueLine> lines)
        {
            _queue.Clear();
            foreach (var line in lines)
                _queue.Enqueue(line);

            if (!_isShowing)
                StartCoroutine(ShowDialogue());
        }

        private void OnLineAdded(DialogueLine line)
        {
            _queue.Enqueue(line);
            if (!_isShowing)
                StartCoroutine(ShowDialogue());
        }

        private IEnumerator ShowDialogue()
        {
            _isShowing = true;

            if (_cameraController != null)
                _cameraController.SaveCameraPosition();

            Core.GamePauseController.Instance?.Pause();

            _allWarriors = new List<Warrior>(FindObjectsOfType<Warrior>());

            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(true);

            while (_queue.Count > 0)
            {
                var line = _queue.Dequeue();

                if (_speakerNameText != null)
                    _speakerNameText.text = line.SpeakerName;
                if (_dialogueText != null)
                    _dialogueText.text = "";

                if (_cameraController != null)
                    _cameraController.StopSway();

                var speaker = _allWarriors.Find(w => w.Id == line.SpeakerId);

                foreach (var w in _allWarriors)
                {
                    var anim = w.GetComponent<DialogueAnimator>();
                    if (anim != null) anim.StopTalk();
                }

                if (speaker != null)
                {
                    var anim = speaker.GetComponent<DialogueAnimator>();
                    if (anim != null) anim.PlayTalk();

                    var voice = speaker.GetComponent<VoiceGenerator>();
                    if (_cameraController != null)
                        yield return _cameraController.FocusOn(speaker.transform);

                    // Печатаем текст с голосом
                    foreach (char c in line.Text)
                    {
                        if (_dialogueText != null)
                            _dialogueText.text += c;

                        if (_useVoice && voice != null)
                            voice.Speak();

                        yield return new WaitForSecondsRealtime(0.03f);
                    }
                }
                else
                {
                    _dialogueText.text = line.Text;
                }

                yield return new WaitForSecondsRealtime(line.Duration);
                yield return new WaitForSecondsRealtime(_fadeDelay);

                if (speaker != null)
                {
                    var anim = speaker.GetComponent<DialogueAnimator>();
                    if (anim != null) anim.StopTalk();
                }
            }

            if (_cameraController != null)
                _cameraController.StopSway();

            if (_cameraController != null)
                yield return _cameraController.RestoreCamera();

            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(false);

            _isShowing = false;

            Core.GamePauseController.Instance?.Resume();
        }
    }
}