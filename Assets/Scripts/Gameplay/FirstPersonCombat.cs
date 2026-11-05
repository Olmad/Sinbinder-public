// Assets/Scripts/Gameplay/FirstPersonCombat.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Личный ближний бой Греховода. Не заменяет обычный AutoAttack:
    /// обращается к его ForceAttack, используя ту же анимацию, урон и задержку.
    /// Команды остальным воинам принадлежат SelectionManager.
    /// </summary>
    [RequireComponent(typeof(SinbinderPlayer))]
    [RequireComponent(typeof(AutoAttack))]
    public sealed class FirstPersonCombat : MonoBehaviour
    {
        [SerializeField] private float _blockMultiplier = 0.4f;
        [SerializeField] private float _blockStaminaCost = 18f;
        [SerializeField] private float _holdStaminaPerSecond = 16f;
        [SerializeField] private float _minimumBlockStamina = 6f;
        [SerializeField] private float _manualSwingCooldown = 0.6f;
        [SerializeField] private float _blockFrontDot = 0.35f;

        private AutoAttack _auto;
        private Fatigue _fatigue;
        private SinbinderPlayer _player;
        private bool _manual;
        private bool _blocking;
        private float _nextSwing;
        private Camera _cachedCamera;

        public bool Blocking => _blocking;

        // Monotonic counters for real, accepted actions. Optional dream QA reads these;
        // key presses rejected by a menu, cooldown or stowed weapon do not count.
        public int AcceptedSwings { get; private set; }
        public int AcceptedBlockFrames { get; private set; }

        private void Awake()
        {
            _auto = GetComponent<AutoAttack>();
            _fatigue = GetComponent<Fatigue>();
            _player = GetComponent<SinbinderPlayer>();
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus) _blocking = false;
        }

        private void OnDisable()
        {
            _blocking = false;
            if (_auto != null) _auto.SetManualOnly(false);
            _manual = false;
        }

        private void Update()
        {
            if (_cachedCamera == null) _cachedCamera = Camera.main;
            var view = _cachedCamera != null ? _cachedCamera.GetComponent<RTS_Camera>() : null;
            // Роль отвечает за то, разрешена ли АВТОатака. Это состояние
            // нельзя сбрасывать при открытии консоли, диалога или меню:
            // иначе герой начнёт бить сам, пока игрок занят интерфейсом.
            bool manualMode = view != null && view.WarriorModeNow &&
                              _player != null && !_player.IsDead;
            if (_manual != manualMode)
            {
                _manual = manualMode;
                _auto.SetManualOnly(manualMode);
            }

            // No attacks/blocks during the short in-game bone-hand takeover.
            if (DreamTutorialDirector.InputLocked) { _blocking = false; return; }

            // Доступ к клавишам — отдельное условие. Роль остаётся Воином,
            // даже если ввод временно забрала отладочная консоль / съёмка.
            _blocking = false;
            if (!manualMode || Sinbinder.Dev.InputHush.Held ||
                Core.GamePauseController.Stopped || UI.Modal.Any ||
                OpeningReveal.Busy || Sinbinder.Dev.PhotoMode.Active ||
                (Dialogue.DialogueCameraController.Instance != null &&
                 Dialogue.DialogueCameraController.Instance.InDialogue) ||
                !view.MouseActionsReady || Time.deltaTime <= 0f) return;

            if (Input.GetMouseButton(1) && (_fatigue == null || _fatigue.Current > _minimumBlockStamina))
            {
                _blocking = true;
                AcceptedBlockFrames++;
                if (_fatigue != null)
                    _fatigue.Spend(_holdStaminaPerSecond * Time.deltaTime);
            }

            if (Input.GetMouseButtonDown(0) && !Input.GetMouseButton(1) && Time.time >= _nextSwing)
            {
                // Взятое со стойки (WeaponPickup) бьёт только наголо — B. Без него
                // в руке посох Греховода, он всегда при нём.
                var arms = GetComponentInChildren<Armament>(true);
                if (arms != null && !arms.Drawn) return;
                _nextSwing = Time.time + _manualSwingCooldown;
                Strike();
            }
        }

        private void Strike()
        {
            if (_cachedCamera == null || _auto == null) return;
            AcceptedSwings++;
            float reach = Mathf.Max(1f, _auto.AttackRange);
            var ray = _cachedCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, reach + 0.25f,
                                ~0, QueryTriggerInteraction.Ignore))
            {
                var target = hit.collider.GetComponentInParent<Damageable>();
                if (target != null && !target.IsDead && target.gameObject != gameObject &&
                    target.Warrior != null && target.Warrior.Team == Team.Enemy)
                {
                    _auto.ForceAttack(target); // existing damage, timing and sound
                    return;
                }
            }

            // Промах тоже выглядит как удар и утомляет героя.
            var animation = GetComponent<WarriorAnimation>();
            if (animation != null) animation.Struck();
            _fatigue?.SpendForAttack();
        }

        /// <summary>Вызывается Damageable уже после брони, до снятия HP.</summary>
        public float ReduceIncoming(float damage, GameObject attacker)
        {
            if (!_blocking || attacker == null || _cachedCamera == null) return damage;
            if (_fatigue != null && _fatigue.Current < _blockStaminaCost) return damage;

            Vector3 toward = attacker.transform.position - transform.position;
            Vector3 facing = _cachedCamera.transform.forward;
            toward.y = 0f;
            facing.y = 0f;
            if (toward.sqrMagnitude < 0.01f || facing.sqrMagnitude < 0.01f) return damage;
            if (Vector3.Dot(toward.normalized, facing.normalized) < _blockFrontDot) return damage;

            _fatigue?.Spend(_blockStaminaCost);
            return Mathf.Max(0f, damage * _blockMultiplier);
        }
    }
}
