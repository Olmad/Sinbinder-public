// Assets/Scripts/Gameplay/RavenPerch.cs
// Перевод: текст через Loc
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Ворон на плече Каргана (примета облачной, 26.10: «ворон прибился к нему ещё при жизни
    /// и не ушёл после смерти»; кадр «Приметы: ворон на плече Каргана»). Сидит на левом плече,
    /// клювом по ходу; Карган пал — ворон слетает к огню (строка гибели — <see cref="Signature"/>).
    ///
    /// Ставит себя сам: раз в две секунды ищет Старого Ворона (<see cref="CrowStep.Raven"/>)
    /// без птицы. Вороньим следом Карган рассыпается — ворон пропадает вместе с телом, потому
    /// что сидит на кости, а не в мире.
    /// </summary>
    public class RavenPerch : MonoBehaviour
    {
        private float _next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindFirstObjectByType<RavenPerch>() != null) return;
            var go = new GameObject("Ворон Каргана");
            DontDestroyOnLoad(go);
            go.AddComponent<RavenPerch>();
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 2f;

            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (!CrowStep.Raven(w) || w.IsDead) continue;
                if (w.transform.Find("Ворон") != null) continue;
                Perch(w);
            }
        }

        private static void Perch(Warrior w)
        {
            Transform shoulder = null;
            foreach (var t in w.GetComponentsInChildren<Transform>(true))
                if (t.name == "LeftUpperArm") { shoulder = t; break; }
            if (shoulder == null) return;   // тела ещё нет — в следующий раз

            var prefab = Resources.Load<GameObject>("Props/Crow");
            if (prefab == null) return;

            // Метка «Ворон» — на корне воина: по ней видно, что птица уже сидит.
            var mark = new GameObject("Ворон").transform;
            mark.SetParent(w.transform, false);

            var bird = Object.Instantiate(prefab, shoulder, false);
            bird.name = "Ворон на плече";
            // Лапами на плечо, клювом по ходу: опора модели — у лап, нос — по −Y Blender, а корень
            // FBX переводит −Y в «вперёд» (как у дерева с отметинами). Был разворот на 180° и
            // подъём на 10 см — ворон висел над плечом клювом вниз (кадр 27.10).
            bird.transform.position = shoulder.position + w.transform.up * 0.03f - w.transform.right * 0.03f;
            bird.transform.rotation = w.transform.rotation * prefab.transform.rotation;
            float size = Mathf.Max(0.6f, w.transform.lossyScale.y);
            bird.transform.localScale = prefab.transform.localScale * (1.0f / Mathf.Max(0.01f, shoulder.lossyScale.x)) * size;
            foreach (var c in bird.GetComponentsInChildren<Collider>()) Object.Destroy(c);
        }
    }
}
