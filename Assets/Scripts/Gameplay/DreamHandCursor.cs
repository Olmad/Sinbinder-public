using System.Collections;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    // In-game virtual bone hand. Never moves the real OS pointer.
    public sealed class DreamHandCursor : MonoBehaviour
    {
        private Texture2D _texture;
        private Vector2 _position;
        private bool _visible;

        private void Awake() { _texture = PaintHand(); }
        private void OnDestroy() { if (_texture != null) Destroy(_texture); }

        public IEnumerator DragTo(Vector3 point, float seconds)
        {
            var cam = Camera.main;
            if (cam == null) yield break;
            var screen = cam.WorldToScreenPoint(point);
            if (screen.z <= 0f) yield break;
            var start = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            var end = new Vector2(screen.x, Screen.height - screen.y);
            _position = start;
            _visible = true;
            float elapsed = 0;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, seconds)));
                _position = Vector2.Lerp(start, end, t);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.16f); // The implied click.
            _visible = false;
        }

        private void OnGUI()
        {
            if (!_visible || _texture == null) return;
            GUI.depth = -500;
            GUI.DrawTexture(new Rect(_position.x - 20, _position.y - 12, 68, 80), _texture);
        }

        private static Texture2D PaintHand()
        {
            var t = new Texture2D(64, 76, TextureFormat.RGBA32, false);
            var clear = new Color(0, 0, 0, 0);
            var pixels = new Color[64 * 76];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
            t.SetPixels(pixels);
            var ivory = new Color(.83f, .80f, .71f, 1f);
            // Forefinger, knuckles and metacarpals; small, intentionally understated.
            Bone(t, 19, 68, 16, 37, ivory, 3);
            Bone(t, 16, 37, 27, 24, ivory, 3);
            Bone(t, 27, 24, 42, 23, ivory, 3);
            Bone(t, 42, 23, 49, 14, ivory, 3);
            Bone(t, 23, 32, 36, 48, ivory, 2);
            Bone(t, 28, 30, 42, 43, ivory, 2);
            Bone(t, 33, 28, 47, 35, ivory, 2);
            Bone(t, 27, 24, 29, 9, ivory, 4);
            t.Apply();
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        private static void Bone(Texture2D t, int x0, int y0, int x1, int y1, Color c, int r)
        {
            int dx = x1 - x0, dy = y1 - y0;
            int count = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
            for (int i = 0; i <= count; i++)
            {
                float f = count == 0 ? 0 : (float)i / count;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, f));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, f));
                for (int xx = -r; xx <= r; xx++)
                    for (int yy = -r; yy <= r; yy++)
                        if (xx * xx + yy * yy <= r * r && x + xx >= 0 && x + xx < t.width
                            && y + yy >= 0 && y + yy < t.height)
                            t.SetPixel(x + xx, y + yy, c);
            }
        }
    }
}
