// Assets/Scripts/Gameplay/RavenPerch.cs
// Перевод: текст через Loc
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Ворон на плече Каргана (примета облачной, 26.10: «ворон прибился к нему ещё при жизни
    /// и не ушёл после смерти»; кадр «Приметы: ворон на плече Каргана»). Сидит на левом плече,
    /// клювом по ходу. Модель — своя (raven.py; автор, 27.10: «ворон у Каргана странный»),
    /// и он летает (<see cref="RavenFlight"/>; «ворон не улетает и не прилетает»).
    ///
    /// Ставит себя сам: раз в две секунды ищет Старого Ворона (<see cref="CrowStep.Raven"/>)
    /// без птицы. Метка «Ворон» на корне воина — птица у него уже есть, где бы она ни летала.
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

            // Своя модель с крыльями; нет её — прежняя, сидячая.
            var prefab = Resources.Load<GameObject>("Props/Raven");
            if (prefab == null) prefab = Resources.Load<GameObject>("Props/Crow");
            if (prefab == null) return;

            var mark = new GameObject("Ворон").transform;
            mark.SetParent(w.transform, false);

            // Своя опора птицы: вперёд — клювом, вверх — спиной. Модель внутри — со своим поворотом
            // импорта (корень FBX переводит −Y Blender во «вперёд», как у дерева с отметинами).
            var rig = new GameObject("Ворон на плече").transform;
            rig.SetParent(shoulder, false);
            var model = Object.Instantiate(prefab, rig, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = prefab.transform.rotation;
            model.transform.localScale = prefab.transform.localScale;
            foreach (var c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);

            rig.position = shoulder.position + w.transform.up * 0.03f - w.transform.right * 0.03f;
            rig.rotation = w.transform.rotation;
            float size = Mathf.Max(0.6f, w.transform.lossyScale.y);
            rig.localScale = Vector3.one * (size / Mathf.Max(0.01f, shoulder.lossyScale.x));

            rig.gameObject.AddComponent<RavenFlight>().Configure(w, shoulder);
        }
    }
}
