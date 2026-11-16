// Assets/Scripts/Gameplay/HeroArm.cs
// Перевод: текста нет
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Рука Греховода для первого лица — из его же модели (автор, 16.11: «руки выглядят так себе.
    /// почему не использовать руку самого Греховода»). До того кисть была шаром-«варежкой»,
    /// рукав — цилиндром (кадры прогона 16.11: «душа в руке», «оружие в руке»).
    ///
    /// Кисть и предплечье вырезаются из сеток тела и облачения в нынешней позе: та же кость,
    /// то же сукно, те же цвета. Две части, обе в рамке кисти — начало в кости кисти, оси —
    /// её поворот, масштаб единичный, метры: <see cref="Hand"/> (кость кисти и всё, что под ней)
    /// и <see cref="Forearm"/> (кость предплечья). Кисть ставит вид — как держать; предплечье
    /// тянется от запястья к локтю за краем кадра (<see cref="Reach"/>).
    /// </summary>
    public sealed class HeroArm
    {
        public Transform Hand { get; private set; }
        public Transform Forearm { get; private set; }

        /// <summary>Рамка кисти: локоть (кость предплечья), середина кисти, к пальцам, нормаль ладони.</summary>
        public Vector3 Elbow { get; private set; }
        public Vector3 Centre { get; private set; }
        public Vector3 Fingers { get; private set; }
        public Vector3 Palm { get; private set; }

        private Transform _stretch;
        private float _length;

        /// <summary>
        /// Вырезать руку (правую или левую) из модели Греховода. Части — дети <paramref name="parent"/>,
        /// пока стоят там, где рука сейчас в мире: держащий может взять их в свою рамку.
        /// null — модели нет или сетки не читаются; тогда вид остаётся при своих примитивах.
        /// </summary>
        public static HeroArm Cut(SinbinderPlayer hero, bool right, Transform parent)
        {
            if (hero == null) return null;
            string side = right ? "Right" : "Left";
            Transform hand = null, lower = null;
            var skins = hero.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var s in skins)
            {
                if (s.bones == null) continue;
                foreach (var b in s.bones)
                {
                    if (b == null) continue;
                    if (b.name == side + "Hand") hand = b;
                    else if (b.name == side + "LowerArm") lower = b;
                }
                if (hand != null && lower != null) break;
            }
            if (hand == null || lower == null) return null;

            // Рамка кисти без масштаба: сетки — в метрах мира.
            var toFrame = Matrix4x4.TRS(hand.position, hand.rotation, Vector3.one).inverse;
            var arm = new HeroArm();
            arm.Hand = new GameObject("Кисть Греховода").transform;
            arm.Forearm = new GameObject("Предплечье Греховода").transform;
            arm.Hand.SetParent(parent, false);
            arm.Forearm.SetParent(parent, false);
            arm._stretch = new GameObject("Вдоль").transform;
            arm._stretch.SetParent(arm.Forearm, false);

            var handPoints = new List<Vector3>();
            var elbowDir = toFrame.MultiplyPoint3x4(lower.position).normalized;
            foreach (var skin in skins)
            {
                // Надетое (мягкая ткань под сеткой тела, Wardrobe.Soften) в кисть не идёт, и рукав
                // режется по запястью: прогон 16.11 — край рукава лёг на ладонь багровым куском,
                // кисти за ним видно не было.
                bool worn = skin.transform.parent != null && skin.transform.parent.GetComponent<SkinnedMeshRenderer>() != null;
                var mesh = skin.sharedMesh;
                if (mesh == null || !mesh.isReadable || skin.bones == null) continue;
                var bones = skin.bones;
                var binds = mesh.bindposes;
                var weights = mesh.boneWeights;
                var v = mesh.vertices;
                if (binds.Length != bones.Length || weights.Length != v.Length) continue;

                // 1 — кисть (кость кисти и пальцы под ней), 2 — предплечье, 0 — прочее.
                var part = new int[bones.Length];
                var skinM = new Matrix4x4[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    var b = bones[i];
                    if (b == null) { skinM[i] = Matrix4x4.identity; continue; }
                    part[i] = b.IsChildOf(hand) ? 1 : b == lower ? 2 : 0;
                    skinM[i] = toFrame * b.localToWorldMatrix * binds[i];
                }

                var n = mesh.normals;
                var uv = mesh.uv;
                var owner = new int[v.Length];
                var at = new Vector3[v.Length];
                var nm = new Vector3[v.Length];
                bool any = false;
                for (int k = 0; k < v.Length; k++)
                {
                    var w = weights[k];
                    float h = 0f, f = 0f;
                    void Add(int bi, float wt) { if (wt <= 0f) return; if (part[bi] == 1) h += wt; else if (part[bi] == 2) f += wt; }
                    Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1);
                    Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
                    owner[k] = h >= 0.5f ? 1 : f >= 0.5f || h + f >= 0.5f ? 2 : 0;
                    if (owner[k] == 0) continue;
                    at[k] = Skin(skinM, w, v[k], true);
                    if (worn)
                    {
                        if (Vector3.Dot(at[k], elbowDir) < 0.01f) { owner[k] = 0; continue; }
                        owner[k] = 2;
                    }
                    any = true;
                    nm[k] = k < n.Length ? Skin(skinM, w, n[k], false).normalized : Vector3.up;
                    if (owner[k] == 1) handPoints.Add(at[k]);
                }
                if (!any) continue;

                // Треугольник — части большинства; чужие вершины стыка копируются: на запястье нет щели.
                foreach (int which in new[] { 1, 2 })
                {
                    var map = new Dictionary<int, int>();
                    var pv = new List<Vector3>(); var pn = new List<Vector3>(); var puv = new List<Vector2>();
                    var subs = new List<int>[mesh.subMeshCount];
                    bool got = false;
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        subs[s] = new List<int>();
                        var tris = mesh.GetTriangles(s);
                        for (int t = 0; t + 2 < tris.Length; t += 3)
                        {
                            int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                            if (owner[a] == 0 || owner[b] == 0 || owner[c] == 0) continue;
                            int mine = (owner[a] == which ? 1 : 0) + (owner[b] == which ? 1 : 0) + (owner[c] == which ? 1 : 0);
                            if (mine < 2) continue;
                            foreach (int idx in new[] { a, b, c })
                            {
                                if (!map.TryGetValue(idx, out int to))
                                {
                                    to = pv.Count;
                                    map[idx] = to;
                                    pv.Add(at[idx]); pn.Add(nm[idx]);
                                    puv.Add(idx < uv.Length ? uv[idx] : Vector2.zero);
                                }
                                subs[s].Add(to);
                            }
                            got = true;
                        }
                    }
                    if (!got) continue;

                    var cut = new Mesh { name = skin.name + (which == 1 ? " — кисть" : " — предплечье") };
                    cut.SetVertices(pv); cut.SetNormals(pn); cut.SetUVs(0, puv);
                    cut.subMeshCount = subs.Length;
                    for (int s = 0; s < subs.Length; s++) cut.SetTriangles(subs[s], s);
                    cut.RecalculateBounds();

                    var go = new GameObject(cut.name);
                    go.transform.SetParent(which == 1 ? arm.Hand : arm._stretch, false);
                    go.AddComponent<MeshFilter>().sharedMesh = cut;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterials = skin.sharedMaterials;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }
            if (arm.Hand.childCount == 0) { arm.Destroy(); return null; }

            arm.Elbow = toFrame.MultiplyPoint3x4(lower.position);
            arm._length = Mathf.Max(0.05f, arm.Elbow.magnitude);

            // Середина кисти, к пальцам, ладонь — по облаку вершин кисти: плоскость кисти —
            // две длинные оси, ладонь — третья. Сторона ладони — к телу (в покое ладонь смотрит
            // в бедро): так у правой и левой не перепутаются тыл и ладонь.
            var centre = Vector3.zero;
            foreach (var p in handPoints) centre += p;
            centre /= Mathf.Max(1, handPoints.Count);
            arm.Centre = centre;
            arm.Fingers = centre.sqrMagnitude > 1e-6f ? centre.normalized : -arm.Elbow.normalized;
            var palm = Thinnest(handPoints, centre, arm.Fingers);
            var inward = toFrame.MultiplyVector(right ? -hero.transform.right : hero.transform.right);
            if (Vector3.Dot(palm, inward) < 0f) palm = -palm;
            arm.Palm = palm;
            arm.Hand.SetPositionAndRotation(hand.position, hand.rotation);
            arm.Forearm.SetPositionAndRotation(hand.position, hand.rotation);
            arm._stretch.localRotation = Quaternion.identity;   // в мире — как есть; Reach повернёт
            arm._stretch.localScale = Vector3.one;
            return arm;
        }

        /// <summary>Кость тела Греховода по имени («RightHand») — из костей его сетки, не из вещей.</summary>
        public static Transform Bone(SinbinderPlayer hero, string name)
        {
            if (hero == null) return null;
            foreach (var s in hero.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (s.bones != null)
                    foreach (var b in s.bones)
                        if (b != null && b.name == name) return b;
            return null;
        }

        /// <summary>Предплечье — от запястья к локтю; длина своя, тянется не больше чем в 1,6 раза.</summary>
        public void Reach(Vector3 wrist, Vector3 elbow)
        {
            if (Forearm == null) return;
            var d = elbow - wrist;
            if (d.sqrMagnitude < 1e-6f) return;
            float k = Mathf.Clamp(d.magnitude / _length, 0.8f, 1.6f);
            Forearm.SetPositionAndRotation(wrist, Quaternion.FromToRotation(Vector3.up, d));
            Forearm.localScale = new Vector3(1f, k, 1f);
            _stretch.localRotation = Quaternion.FromToRotation(Elbow.normalized, Vector3.up);
            _stretch.localScale = Vector3.one;
        }

        /// <summary>Поворот, который кладёт рамку кисти так: пальцы — <paramref name="fingers"/>, ладонь — <paramref name="palm"/>.</summary>
        public Quaternion Pose(Vector3 fingers, Vector3 palm)
            => Quaternion.LookRotation(fingers, palm) * Quaternion.Inverse(Quaternion.LookRotation(Fingers, Palm));

        public void Show(bool on)
        {
            if (Hand != null && Hand.gameObject.activeSelf != on) Hand.gameObject.SetActive(on);
            if (Forearm != null && Forearm.gameObject.activeSelf != on) Forearm.gameObject.SetActive(on);
        }

        public void Destroy()
        {
            foreach (var t in new[] { Hand, Forearm })
            {
                if (t == null) continue;
                foreach (var f in t.GetComponentsInChildren<MeshFilter>(true))
                    if (f.sharedMesh != null) Object.Destroy(f.sharedMesh);
                Object.Destroy(t.gameObject);
            }
            Hand = Forearm = null;
        }

        private static Vector3 Skin(Matrix4x4[] m, BoneWeight w, Vector3 p, bool point)
        {
            Vector3 One(int i, float wt) => wt <= 0f ? Vector3.zero
                : (point ? m[i].MultiplyPoint3x4(p) : m[i].MultiplyVector(p)) * wt;
            var sum = One(w.boneIndex0, w.weight0) + One(w.boneIndex1, w.weight1)
                    + One(w.boneIndex2, w.weight2) + One(w.boneIndex3, w.weight3);
            float total = w.weight0 + w.weight1 + w.weight2 + w.weight3;
            return total > 1e-5f ? sum / total : (point ? p : p);
        }

        /// <summary>Ось, вдоль которой кисть тоньше всего, поперёк пальцев: ладонь (или тыл).</summary>
        private static Vector3 Thinnest(List<Vector3> points, Vector3 centre, Vector3 along)
        {
            // Поперёк пальцев ищем ось наибольшего разброса — ширину кисти; ладонь — третья ось.
            var a = Vector3.Cross(along, Vector3.up);
            if (a.sqrMagnitude < 1e-4f) a = Vector3.Cross(along, Vector3.right);
            a.Normalize();
            var b = Vector3.Cross(along, a).normalized;
            float aa = 0f, bb = 0f, ab = 0f;
            foreach (var p in points)
            {
                var q = p - centre;
                float x = Vector3.Dot(q, a), y = Vector3.Dot(q, b);
                aa += x * x; bb += y * y; ab += x * y;
            }
            // Главная ось 2×2: угол θ = ½·atan2(2ab, aa − bb).
            float th = 0.5f * Mathf.Atan2(2f * ab, aa - bb);
            var width = a * Mathf.Cos(th) + b * Mathf.Sin(th);
            return Vector3.Cross(along, width).normalized;
        }
    }
}
