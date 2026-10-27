using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Text on a cockpit display (diegetic: it lives on the screen surface, never on the player's face).
    /// Uses the built-in font with a stereo-safe text shader; set the text only when it changes.
    /// Size: a line is about lineHeight metres tall (1.4 cm reads well at the console's ~0.65 m).
    /// </summary>
    public class CockpitDisplay : MonoBehaviour
    {
        static Font _font;
        static Material _material;

        TextMesh _text;
        string _current = "";

        /// <summary>Puts a display text on the screen anchor (its +Z points away from the eye).</summary>
        public static CockpitDisplay Create(Transform screen, Color color, float lineHeight = 0.014f)
        {
            EnsureFont();
            var go = new GameObject("Text");
            go.transform.SetParent(screen, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.008f);   // just in front of the glass
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
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var display = go.AddComponent<CockpitDisplay>();
            display._text = tm;
            return display;
        }

        static void EnsureFont()
        {
            if (_font != null) return;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _material = new Material(Mats.Get(Mats.Text)) { mainTexture = _font.material.mainTexture };
            Font.textureRebuilt += f => { if (f == _font && _material != null) _material.mainTexture = _font.material.mainTexture; };
        }

        public void SetText(string text)
        {
            if (text == _current) return;
            _current = text ?? "";
            _text.text = _current;
        }

        public void SetColor(Color color) => _text.color = color;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _font = null; _material = null; }
    }
}
