// Castle and fort meshes: three true gate gaps for castle, walkable ramparts and access ramps.
// Primitive meshes have colliders and participate in GroundNavMesh.Start() baking.
using UnityEngine;
using System.Collections.Generic;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    public static class MapFortifications
    {
        public static List<GameObject> Build(MapData map, Transform parent, bool preview)
        {
            var roots = new List<GameObject>();
            if (map == null || map.Fortifications == null) return roots;
            foreach (var f in map.Fortifications)
            {
                if (f == null) continue;
                float half = Mathf.Clamp(f.HalfSize,12f,38f);
                bool castle = f.Kind == "castle";
                int gates = castle ? 3 : 1;
                var root = new GameObject((castle ? "Замок" : "Форт") + " — " + f.X + "," + f.Z);
                root.transform.SetParent(parent, false);
                roots.Add(root);
                root.transform.SetPositionAndRotation(new Vector3(f.X,MapTerrain.HeightAt(map,f.X,f.Z),f.Z),Quaternion.Euler(0f,f.Yaw,0f));
                float walk = castle ? 6.3f : 4.8f;
                float wall = walk - .25f; // stone parapet is separate, leaving firing gaps
                float gateWidth = castle ? 8.5f : 7f;
                Material stone = MaterialOf(new Color(castle ? .36f:.42f,.37f,.36f));
                Material timber = MaterialOf(new Color(.28f,.19f,.13f));
                // Local forward is +Z. Open forward gate; castles also have left and right gates.
                for(int side=0;side<4;side++)
                {
                    bool gate = side == 0 || (castle && (side==1 || side==2));
                    float length = 2f*half;
                    Transform section = new GameObject("Стена " + side).transform;
                    section.SetParent(root.transform,false);
                    section.localRotation = Quaternion.Euler(0f,side==0?0f:side==1?90f:side==2?-90f:180f,0f);
                    section.localPosition = Vector3.zero;
                    if (gate)
                    {
                        float remain = (length-gateWidth)/2f;
                        Block("Стена слева",section,new Vector3(-(gateWidth+remain)/2f,wall/2f,half),new Vector3(remain,wall,2.6f),stone);
                        Block("Стена справа",section,new Vector3((gateWidth+remain)/2f,wall/2f,half),new Vector3(remain,wall,2.6f),stone);
                        Block("Перемычка над воротами",section,new Vector3(0f,wall-1.1f,half),new Vector3(gateWidth,2.2f,2.6f),stone);
                        Block("Сторожевой настил над воротами",section,new Vector3(0f,walk-.18f,half-2.4f),new Vector3(gateWidth+1f,.36f,2.8f),timber);
                    }
                    else Block("Глухая стена",section,new Vector3(0f,wall/2f,half),new Vector3(length,wall,2.6f),stone);
                    // Walkways run around perimeter; no blocking fence on inner side.
                    Block("Проход по стене",section,new Vector3(0f,walk-.23f,half-2.5f),new Vector3(length,.45f,2.8f),timber);
                    for(int i=0;i<4;i++)
                    {
                        float x=-half + 2f + i*(length-4f)/3f;
                        if (gate && Mathf.Abs(x) < gateWidth*.6f) continue;
                        Block("Зубец",section,new Vector3(x,walk+.65f,half+.18f),new Vector3(2.2f,1.3f,1f),stone);
                    }
                }
                // Two ascending interior ramps. NavMesh can connect ground to rampart.
                Ramp(root.transform,new Vector3(-half+4f,0f,half-18f),new Vector3(-half+4f,walk,half-2.5f),timber);
                Ramp(root.transform,new Vector3(half-4f,0f,half-18f),new Vector3(half-4f,walk,half-2.5f),timber);
                foreach(var corner in new[] { new Vector3(-half,0f,-half),new Vector3(half,0f,-half),new Vector3(-half,0f,half),new Vector3(half,0f,half) })
                {
                    Block("Башня",root.transform,corner+Vector3.up*(wall+.8f)*.5f,
                        new Vector3(5f,wall+.8f,5f),stone);
                }
                if (!preview && !castle && (f.Guards > 0 || f.Archers > 0))
                {
                    var spawn = root.AddComponent<FortGarrison>();
                    spawn.Configure(Mathf.Clamp(f.Guards,0,12),Mathf.Clamp(f.Archers,0,8),half,walk);
                }
            }
            return roots;
        }
        private static void Ramp(Transform parent,Vector3 from,Vector3 to,Material material)
        {
            var dir=to-from;
            float length = dir.magnitude;
            var go=Block("Пологий подъём на стену",parent,(from+to)*.5f,new Vector3(2.8f,.48f,length),material);
            go.transform.localRotation = Quaternion.LookRotation(dir.normalized,Vector3.up);
        }
        private static Material MaterialOf(Color color)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader==null) return null;
            var mat=new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor",color);
            else if (mat.HasProperty("_Color")) mat.color=color;
            return mat;
        }
        private static GameObject Block(string name,Transform parent,Vector3 pos,Vector3 scale,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name=name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition=pos;go.transform.localScale=scale;
            if (mat!=null) go.GetComponent<MeshRenderer>().sharedMaterial=mat;
            return go;
        }
    }
}
