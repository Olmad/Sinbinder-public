// Assets/Scripts/UI/DialogueUI.cs
// Перевод: текст через Loc
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Dialogue;
using Sinbinder.Gameplay;
using Sinbinder.Audio;

using Sinbinder.Core;
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
        private readonly Queue<DialogueLine> _ambientQueue = new();
        private bool _isShowing = false;
        private List<Warrior> _allWarriors;
        private Coroutine _ambientRunning;
        private int _ambientBubble;

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
            CutAmbient();
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
            if (this == null || lines.Count == 0) return;

            if (!ReferenceEquals(lines[0].Expedition, null))
            {
                foreach (var line in lines) QueueAmbient(line);
                return;
            }

            _queue.Clear();
            foreach (var line in lines)
                _queue.Enqueue(line);

            if (!_isShowing)
                _running = StartCoroutine(ShowDialogue());
        }

        private void OnLineAdded(DialogueLine line)
        {
            if (this == null) return;

            if (!ReferenceEquals(line.Expedition, null))
            {
                QueueAmbient(line);
                return;
            }

            _queue.Enqueue(line);
            if (!_isShowing)
                _running = StartCoroutine(ShowDialogue());
        }

        private Coroutine _running;

        /// <summary>Идёт ли разговор сейчас.</summary>
        public bool Showing => _isShowing;

        /// <summary>
        /// Оборвать разговор на полуслове — конец игры.
        ///
        /// Разговор, начатый рядом со смертью Греховода, допечатывал реплику
        /// по букве с голосом уже под экраном «Греховод пал», потом сам
        /// уводил камеру — а на кадре конца на нижней полосе висел огрызок
        /// строки: «Ры» у облачной сессии 25 сентября, «М» у локальной
        /// 26-го. Павший не договаривает, и живые под экраном конца —
        /// тоже: мир остановлен, разговор вместе с ним.
        ///
        /// Паузу не снимает: её держит конец игры.
        /// </summary>
        public void Cut()
        {
            CutAmbient();

            if (!_isShowing) return;

            if (_running != null) StopCoroutine(_running);
            _running = null;
            _queue.Clear();
            _isShowing = false;

            if (_dialogueText != null) _dialogueText.text = "";
            if (_speakerNameText != null) _speakerNameText.text = "";
            if (_dialoguePanel != null) _dialoguePanel.SetActive(false);

            // Камера и полосы — на место: за экраном конца виден мир таким,
            // каким его оставили, а не крупный план на полуслове.
            if (_cameraController != null)
                _cameraController.StartCoroutine(_cameraController.RestoreCamera());
        }

        private void CutAmbient()
        {
            if (_ambientRunning != null) StopCoroutine(_ambientRunning);
            _ambientRunning = null;
            _ambientQueue.Clear();
            SpeechBubbles.Withdraw(_ambientBubble);
            _ambientBubble = 0;
        }

        private void QueueAmbient(DialogueLine line)
        {
            if (!ReferenceEquals(line.Expedition, PersonalExpedition.DialogueVisit)) return;

            _ambientQueue.Enqueue(line);
            if (_ambientRunning == null)
                _ambientRunning = StartCoroutine(ShowAmbientDialogue());
        }

        private IEnumerator ShowAmbientDialogue()
        {
            yield return null;
            while (_ambientQueue.Count > 0)
            {
                var line = _ambientQueue.Dequeue();
                var visit = line.Expedition;
                while (ReferenceEquals(visit, PersonalExpedition.DialogueVisit) &&
                       (GamePauseController.Stopped || PersonalExpedition.HoldBanter))
                    yield return null;
                if (!ReferenceEquals(visit, PersonalExpedition.DialogueVisit)) continue;

                Warrior speaker = null;
                foreach (var warrior in FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                    if (warrior != null && warrior.Id == line.SpeakerId &&
                        warrior.gameObject.scene == visit.gameObject.scene)
                    {
                        speaker = warrior;
                        break;
                    }
                if (speaker == null || speaker.IsDead || FogOfWar.Hides(speaker)) continue;

                string full = Loc.T(line.Text);
                if (string.IsNullOrEmpty(full)) continue;
                float duration = Mathf.Max(line.Duration, 1.4f + full.Length / 15f);
                _ambientBubble = SpeechBubbles.SayTracked(speaker, full, duration);
                FindFirstObjectByType<BattleLogUI>()?.Write(Loc.F("{0}: «{1}»", Loc.Name(speaker.DisplayName), full));

                float until = Time.unscaledTime + duration;
                while (ReferenceEquals(visit, PersonalExpedition.DialogueVisit) && Time.unscaledTime < until &&
                       speaker != null && !speaker.IsDead && !FogOfWar.Hides(speaker))
                    yield return null;

                SpeechBubbles.Withdraw(_ambientBubble);
                _ambientBubble = 0;
            }
            _ambientRunning = null;
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

                // Павший не договаривает. Погибший лежит, а не исчезает,
                // и проверка выше его не вычёркивает: строка звучала бы
                // из трупа, с наездом на него.
                if (speaker != null && speaker.IsDead) continue;

                // Имя — через полосы. Они прячут строку имени, когда говорящего
                // нет (слово поступка «Сбегает», конец наезда), и включить её
                // обратно может только их Say. Прямой записью имя ставилось
                // в выключенную строку — автор, 26 сентября: «когда воины перед
                // боем переговариваются, не всегда видно имена».
                if (Letterbox.Instance != null) Letterbox.Instance.Say(Loc.Name(line.SpeakerName), "");
                else
                {
                    if (_speakerNameText != null) _speakerNameText.text = Loc.Name(line.SpeakerName);
                    if (_dialogueText != null) _dialogueText.text = "";
                }

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
                    {
                        var listener = string.IsNullOrEmpty(line.ListenerId) ? null
                            : _allWarriors.Find(w => w.Id == line.ListenerId && !w.IsDead);
                        yield return _cameraController.FocusConversation(speaker.transform,
                            listener != null ? listener.transform : null);
                    }

                    // Печатаем текст с голосом. Щелчок — строка целиком
                    // (Letterbox.Nexts), дальше срок чтения, щелчок — следующая.
                    string full = Loc.T(line.Text);
                    int hurry = Letterbox.Nexts;
                    foreach (char c in full)
                    {
                        if (Letterbox.Nexts != hurry || Letterbox.Skipping)
                        {
                            if (_dialogueText != null) _dialogueText.text = full;
                            break;
                        }

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
                    _dialogueText.text = Loc.T(line.Text);
                }

                // Щелчок «дальше» — следующая реплика сразу, без срока и паузы.
                yield return Letterbox.Hold(line.Duration + _fadeDelay);

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
            _running = null;

            Core.GamePauseController.Instance?.Resume();
        }
    }
}
