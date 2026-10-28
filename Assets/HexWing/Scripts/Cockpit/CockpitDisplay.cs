using TMPro;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Text on a cockpit display or menu panel (diegetic: it lives on the surface, never on the player's
    /// face). Uses the TextMeshPro font Resources/Fonts/Rajdhani SDF (sharp at any distance, Polish letters;
    /// made by HexWing > Build Game) and falls back to the built-in font with a stereo-safe text shader.
    /// Set the text only when it changes. Size: a line is about lineHeight metres tall (1.4 cm reads well
    /// at the console's ~0.65 m).
    /// </summary>
    public class CockpitDisplay : MonoBehaviour
    {
        const string FontPath = "Fonts/Rajdhani SDF";

        static Font _font;
        static Material _material;
        static TMP_FontAsset _tmpFont;
        static bool _checked;
        static readonly System.Collections.Generic.Dictionary<int, Material> Queued = new();

        TextMesh _text;
        TextMeshPro _tmp;
        string _current = "";
        float _size;

        /// <summary>Puts a display text on the screen anchor (its +Z points away from the eye).</summary>
        /// <param name="renderQueue">0 = the font's own; else a shared copy of the font material in that queue
        /// (the menu panel draws its layers in a fixed order: plate, tiles, edges, texts).</param>
        public static CockpitDisplay Create(Transform screen, Color color, float lineHeight = 0.014f, int renderQueue = 0)
        {
            EnsureFont();
            var go = new GameObject("Text");
            go.transform.SetParent(screen, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.008f);   // just in front of the glass
            var display = go.AddComponent<CockpitDisplay>();
            if (_tmpFont != null)
            {
                var tmp = go.AddComponent<TextMeshPro>();
                tmp.font = _tmpFont;
                tmp.fontSize = lineHeight * 10f / 1.2f;   // TMP: size 1 = 0.1 m per line of text, with its line spacing
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.color = color;
                tmp.text = "";
                tmp.rectTransform.sizeDelta = new Vector2(0.5f, 0.3f);
                if (renderQueue > 0) tmp.fontSharedMaterial = InQueue(_tmpFont.material, renderQueue);
                var r = tmp.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                display._tmp = tmp;
                display._size = tmp.fontSize;
                return display;
            }
            // TextMesh: about 12 units per line at font size 120 and character size 1
            go.transform.localScale = Vector3.one * (lineHeight / 12f);
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.fontSize = 120;
            tm.fontStyle = FontStyle.Bold;
            tm.characterSize = 1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.text = "";
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = renderQueue > 0 ? InQueue(_material, renderQueue) : _material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            display._text = tm;
            return display;
        }

        static void EnsureFont()
        {
            if (!_checked)
            {
                _checked = true;
                _tmpFont = Resources.Load<TMP_FontAsset>(FontPath);
                Debug.Log(_tmpFont != null
                    ? "[HexWing] Texts: TextMeshPro (Rajdhani SDF)."
                    : "[HexWing] Texts: built-in font (Resources/Fonts/Rajdhani SDF missing; run HexWing > Build Game twice after the TMP import).");
            }
            if (_tmpFont != null || _font != null) return;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _material = new Material(Mats.Get(Mats.Text)) { mainTexture = _font.material.mainTexture };
            Font.textureRebuilt += f => { if (f == _font && _material != null) _material.mainTexture = _font.material.mainTexture; };
        }

        static Material InQueue(Material source, int queue)
        {
            if (Queued.TryGetValue(queue, out var m) && m != null) return m;
            m = new Material(source) { name = source.name + " " + queue, renderQueue = queue };
            Queued[queue] = m;
            return m;
        }

        /// <summary>Keeps the text inside a box this wide (metres): TMP shrinks long texts down to 60%.</summary>
        public void FitWidth(float width)
        {
            if (_tmp == null) return;
            float size = _size;
            _tmp.rectTransform.sizeDelta = new Vector2(width, _tmp.rectTransform.sizeDelta.y);
            _tmp.overflowMode = TextOverflowModes.Ellipsis;
            _tmp.enableAutoSizing = true;
            _tmp.fontSizeMax = size;
            _tmp.fontSizeMin = size * 0.6f;
        }

        public void SetText(string text)
        {
            if (text == _current) return;
            _current = text ?? "";
            if (_tmp != null) _tmp.text = _current;
            else _text.text = _current;
        }

        public void SetColor(Color color)
        {
            if (_tmp != null) _tmp.color = color;
            else _text.color = color;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _font = null; _material = null; _tmpFont = null; _checked = false; Queued.Clear(); }
    }
}
