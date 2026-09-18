// Assets/Scripts/UI/DialogueUI.cs
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

        /// <summary>
        /// От кого подписались. Хранится, а не ищется при отписке.
        ///
        /// Триггер разговора живёт на Managers и переживает смену сцен,
        /// а этот экран — нет. Раньше отписка искала триггер заново
        /// в OnDestroy, посреди выгрузки сцены, и не находила: подписка
        /// мёртвого экрана оставалась на живом триггере, и первый же
        /// разговор следующей доли падал в уничтоженный объект.
        /// Нашёл прогон DemoWalkthrough 13 сентября, на переходе
        /// из лагеря в набег.
        /// </summary>
        private DialogueTrigger _trigger;

        void Start()
        {
            _trigger = FindFirstObjectByType<DialogueTrigger>();
            if (_trigger != null)
            {
                _trigger.OnDialogueStart += OnDialogueStart;
                _trigger.OnLineAdded += OnLineAdded;
            }

            if (_cameraController == null)
                _cameraController = FindFirstObjectByType<DialogueCameraController>();

            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(false);
        }

        void OnDestroy()
        {
            if (_trigger != null)
            {
                _trigger.OnDialogueStart -= OnDialogueStart;
                _trigger.OnLineAdded -= OnLineAdded;
            }
        }

        private void OnDialogueStart(List<DialogueLine> lines)
        {
            // Вторая линия обороны: если подписка всё же пережила экран,
            // уничтоженный экран молчит, а не роняет разговор.
            if (this == null) return;

            _queue.Clear();
            foreach (var line in lines)
                _queue.Enqueue(line);

            if (!_isShowing)
                StartCoroutine(ShowDialogue());
        }

        private void OnLineAdded(DialogueLine line)
        {
            if (this == null) return;

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

            _allWarriors = new List<Warrior>(FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID));

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

                // Убитых вычёркиваем перед каждой строкой.
                //
                // Список набран один раз, до цикла, а цикл долгий: он ждёт
                // наводки камеры и печатает текст по букве. За это время
                // воин успевает погибнуть — а погибший уничтожается, и
                // GetComponent у него бросает MissingReferenceException.
                // Разговор при этом рвался посреди фразы.
                //
                // Заметить это чтением кода было почти нельзя: строчка
                // выше, `w.Id`, у мёртвого работает — управляемый объект
                // цел, и лишь обращение к нативной части падает.
                _allWarriors.RemoveAll(w => w == null);

                var speaker = _allWarriors.Find(w => w.Id == line.SpeakerId);

                foreach (var w in _allWarriors)
                {
                    var anim = w.GetComponent<Gameplay.WarriorAnimation>();
                    if (anim != null) anim.Talk(false);
                }

                if (speaker != null)
                {
                    var anim = speaker.GetComponent<Gameplay.WarriorAnimation>();
                    if (anim != null) anim.Talk(true);

                    var voice = speaker.GetComponent<VoiceGenerator>();
                    if (_cameraController != null)
                        yield return _cameraController.FocusOn(speaker.transform);

                    // Печатаем текст с голосом
                    foreach (char c in line.Text)
                    {
                        if (_dialogueText != null)
                            _dialogueText.text += c;

                        // Букву передаём нарочно: от неё дрожит высота,
                        // и реплика звучит одинаково при каждом прочтении.
                        if (_useVoice && voice != null)
                            voice.Speak(c);

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
                    var anim = speaker.GetComponent<Gameplay.WarriorAnimation>();
                    if (anim != null) anim.Talk(false);
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