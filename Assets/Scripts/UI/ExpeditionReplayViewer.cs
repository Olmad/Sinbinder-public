// Complete, step-by-step playback of the ACTUAL auto-battle event stream.
// The little tactical diagram is explicitly schematic: old BattleRecord
// contains decisions, actors, targets and outcomes, but NO world positions.
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    public sealed class ExpeditionReplayViewer : MonoBehaviour
    {
        private static ExpeditionReplayViewer _open;
        private static int _closedFrame = -1;
        public static bool IsOpen => _open != null;
        public static bool OwnsEscape => IsOpen || Time.frameCount == _closedFrame;

        public static void Open(ExpeditionReplayData data)
        {
            if (data == null || data.Frames.Count == 0) return;
            if (_open != null) Destroy(_open.gameObject);
            var go = new GameObject("Просмотр полной записи экспедиции");
            _open = go.AddComponent<ExpeditionReplayViewer>();
            _open._data = data;
            _open._index = 0;
            _open._running = true;
        }

        private ExpeditionReplayData _data;
        private int _index;
        private bool _running;
        private float _clock, _speed = 1f;
        private Vector2 _scroll;
        private GUIStyle _caption, _item;

        private void Update()
        {
            if (_data == null) { Close(); return; }
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            if (!_running || _index >= _data.Frames.Count - 1) return;
            _clock += Time.unscaledDeltaTime * _speed;
            if (_clock >= 1.1f)
            {
                _clock = 0f;
                _index++;
                if (_index == _data.Frames.Count - 1) _running = false;
            }
        }

        private void OnDestroy()
        {
            if (_open == this) _open = null;
        }

        private void Close()
        {
            if (_open != this) return;
            _closedFrame = Time.frameCount;
            _open = null;
            Destroy(gameObject);
        }

        private void OnGUI()
        {
            if (_data == null || _data.Frames.Count == 0) return;
            float w = Mathf.Clamp(Screen.width - 40f, 430f, 1180f);
            float h = Mathf.Clamp(Screen.height - 40f, 355f, 760f);
            float left = (Screen.width - w) * 0.5f, top = (Screen.height - h) * 0.5f;

            GUI.Box(new Rect(left, top, w, h), "");
            if (_caption == null)
            {
                _caption = new GUIStyle(GUI.skin.label)
                    { fontSize = 19, fontStyle = FontStyle.Bold, wordWrap = true };
                _item = new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true};
            }
            GUI.Label(new Rect(left+15f, top+9f,w-170f,34f),
                "ПОЛНЫЙ ПРОТОКОЛ · "+_data.MissionName,_caption);
            if(GUI.Button(new Rect(left+w-142f,top+8f,130f,32f),"Закрыть (Esc)"))Close();
            if (_data == null) return;

            var frame = _data.Frames[_index];
            float mapWidth = Mathf.Min(w*0.58f,w-205f);
            float mapHeight = Mathf.Min(350f,h*0.48f);
            var mapRect=new Rect(left+15f,top+50f,mapWidth,mapHeight);
            DrawSchematicMap(mapRect,frame);

            float sideX=mapRect.xMax+12f,sideW=Mathf.Max(150f,w-mapWidth-44f);
            GUI.Box(new Rect(sideX,top+50f,sideW,mapHeight),"");
            GUI.Label(new Rect(sideX+9f,top+58f,sideW-18f,85f),
                "Событие "+(_index+1)+" из "+_data.Frames.Count+"\n"+
                StageName(frame.Kind),_caption);
            GUI.Label(new Rect(sideX+9f,top+155f,sideW-18f,mapHeight-115f),
                (string.IsNullOrEmpty(frame.Actor)?"":frame.Actor+": ") + frame.Description,
                _item);

            GUI.Label(new Rect(mapRect.x,top+58f+mapHeight,w-30f,23f),
                "Схематическая реконструкция: боевые решения из движка, точные координаты в старой записи отсутствуют.",
                _item);

            float y=top+89f+mapHeight;
            float buttonW=Mathf.Min(150f,(w-40f)/5f);
            if(GUI.Button(new Rect(left+15f,y,buttonW,34f),"|<")){_index=0;_running=false;}
            if(GUI.Button(new Rect(left+18f+buttonW,y,buttonW,34f),"< Назад"))
            { _index=Mathf.Max(0,_index-1);_running=false; }
            if(GUI.Button(new Rect(left+21f+2*buttonW,y,buttonW,34f),_running?"Пауза":"Играть"))
            { _running=!_running;_clock=0f; }
            if(GUI.Button(new Rect(left+24f+3*buttonW,y,buttonW,34f),"Вперёд >"))
            { _index=Mathf.Min(_data.Frames.Count-1,_index+1);_running=false; }
            if(GUI.Button(new Rect(left+27f+4*buttonW,y,buttonW,34f),"x"+_speed.ToString("0")))
                _speed=_speed>=4f?1f:_speed*2f;

            float logY=y+43f, logH=Mathf.Max(80f,top+h-logY-17f);
            float rowH=27f;
            var view=new Rect(left+15f,logY,w-30f,logH);
            var content=new Rect(0f,0f,view.width-22f,_data.Frames.Count*rowH+4f);
            _scroll=GUI.BeginScrollView(view,_scroll,content);
            int first=Mathf.Max(0,(int)(_scroll.y/rowH)-1);
            int last=Mathf.Min(_data.Frames.Count,first+(int)(logH/rowH)+5);
            for(int i=first;i<last;i++)
            {
                var step=_data.Frames[i];
                if(i==_index)
                    GUI.Box(new Rect(0f,i*rowH,content.width,rowH),"");
                string line=(i+1)+". "+StageName(step.Kind)+" · "+
                    (string.IsNullOrEmpty(step.Actor)?"":step.Actor+": ")+step.Description;
                if(GUI.Button(new Rect(0f,i*rowH,content.width,rowH),line,GUI.skin.label))
                { _index=i;_running=false;_clock=0f; }
            }
            GUI.EndScrollView();
        }

        private void DrawSchematicMap(Rect rect,ExpeditionReplayFrame frame)
        {
            GUI.Box(rect,"СХЕМА МАРШРУТА");
            Rect ground=new Rect(rect.x+12f,rect.y+27f,rect.width-24f,rect.height-40f);
            GUI.DrawTexture(ground,Texture2D.whiteTexture,ScaleMode.StretchToFill);
            var previous=GUI.color;
            GUI.color=frame.Kind==ExpeditionReplayKind.CombatAction
                ? new Color(0.14f,0.19f,0.16f,0.99f)
                : new Color(0.18f,0.24f,0.2f,0.99f);
            GUI.DrawTexture(ground,Texture2D.whiteTexture);
            GUI.color=previous;

            // Deliberate schematic: the record does NOT contain meters.
            float progress=frame.Kind switch {
                ExpeditionReplayKind.Departure => 0.06f,
                ExpeditionReplayKind.March => 0.30f,
                ExpeditionReplayKind.Return => 0.07f,
                _ => 0.76f
            };
            float sx=ground.x+ground.width*0.15f;
            float ex=ground.x+ground.width*0.85f;
            float y=ground.y+ground.height*(1f-progress);
            GUI.Label(new Rect(sx-20f,ground.y+5f,140f,20f),"Цель");
            GUI.Label(new Rect(sx-20f,ground.yMax-24f,160f,20f),"Выход / возврат");
            var a=new Rect(sx,y-8f,22f,22f);
            GUI.color=new Color(0.28f,0.82f,0.91f);
            GUI.DrawTexture(a,Texture2D.whiteTexture);
            GUI.color=new Color(0.88f,0.32f,0.28f);
            GUI.DrawTexture(new Rect(ex-40f,ground.y+ground.height*0.23f,22f,22f),
                Texture2D.whiteTexture);
            GUI.color=previous;
            GUI.Label(new Rect(a.x+26f,a.y-2f,ground.width-60f,26f),
                frame.Kind==ExpeditionReplayKind.CombatAction
                ? frame.Actor : _data.Leader);
        }

        private static string StageName(ExpeditionReplayKind stage) => stage switch
        {
            ExpeditionReplayKind.Departure => "Выступление",
            ExpeditionReplayKind.March => "Маршрут",
            ExpeditionReplayKind.CommandDecision => "Решение командира",
            ExpeditionReplayKind.CombatAction => "Боевой эпизод",
            ExpeditionReplayKind.Loot => "Добыча",
            ExpeditionReplayKind.Return => "Возвращение",
            _ => "Событие"
        };
    }
}
