// Assets/_Project/Scripts/Dialogue/DialogueAnimator.cs
using UnityEngine;

namespace Sinbinder.Dialogue
{
    public class DialogueAnimator : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private string _talkAnimation = "Talk";
        [SerializeField] private string _idleAnimation = "Idle";

        public void PlayTalk()
        {
            if (_animator != null)
            {
                _animator.SetBool("IsTalking", true);
                _animator.Play(_talkAnimation);
            }
        }

        public void StopTalk()
        {
            if (_animator != null)
            {
                _animator.SetBool("IsTalking", false);
                _animator.Play(_idleAnimation);
            }
        }
    }
}