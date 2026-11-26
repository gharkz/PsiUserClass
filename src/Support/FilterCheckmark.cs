using UnityEngine;
using UnityEngine.UI;

namespace PsionClass.Support
{
    using UnityEngine;
    using UnityEngine.UI;
    // Small UI geometry keeps the check crisp without depending on a font glyph or game sprite.
    internal sealed class FilterCheckmark : MaskableGraphic
    {
        bool partial;
        internal bool Partial { set { if (partial == value) return; partial = value; SetVerticesDirty(); } }
        internal static FilterCheckmark Create(Transform parent, Vector2 position, bool selected)
        {
            var box = new GameObject("Parchment checkbox", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)box.transform; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, .5f); rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = new Vector2(14, 14);
            var image = box.GetComponent<Image>(); image.color = new Color(.85f, .77f, .60f, .70f); image.raycastTarget = false;
            var outline = box.AddComponent<Outline>(); outline.effectColor = new Color(.40f, .30f, .19f, .85f); outline.effectDistance = new Vector2(1, -1);
            var go = new GameObject("Inset checkmark", typeof(RectTransform));
            var markRect = (RectTransform)go.transform; markRect.SetParent(rect, false);
            markRect.anchorMin = Vector2.zero; markRect.anchorMax = Vector2.one;
            markRect.offsetMin = markRect.offsetMax = Vector2.zero;
            var mark = go.AddComponent<FilterCheckmark>(); mark.raycastTarget = false;
            mark.color = new Color(.24f, .18f, .10f, 1f); mark.gameObject.SetActive(selected);
            return mark;
        }
        internal static FilterCheckmark Replace(Graphic original)
        {
            original.enabled = false;
            // Native artwork occupies only the center of its 32px sprite rectangle.
            // Own a fixed 14px visible box instead of inheriting those transparent margins.
            var parent = (RectTransform)original.transform.parent;
            Vector2 center = (Vector2)original.transform.localPosition + original.rectTransform.rect.center;
            var mark = Create(parent, new Vector2(center.x - parent.rect.xMin, center.y - parent.rect.center.y), true);
            return mark;
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var r = rectTransform.rect;
            float stroke = Mathf.Min(2.5f, Mathf.Min(r.width, r.height) * .20f);
            // Include half the stroke in the inset so even the outer edge retains two pixels.
            float inset = 2f + stroke * .5f;
            float left = r.xMin + inset, right = r.xMax - inset;
            float bottom = r.yMin + inset, top = r.yMax - inset;
            if (right <= left || top <= bottom) return;
            if (partial) Segment(mesh, new Vector2(left, r.center.y), new Vector2(right, r.center.y), stroke);
            else
            {
                var joint = new Vector2(Mathf.Lerp(left, right, .34f), bottom);
                Segment(mesh, new Vector2(left, Mathf.Lerp(bottom, top, .45f)), joint, stroke);
                Segment(mesh, joint, new Vector2(right, top), stroke);
            }
        }
        void Segment(VertexHelper mesh, Vector2 from, Vector2 to, float width)
        {
            var d = (to - from).normalized;
            var side = new Vector2(-d.y, d.x) * (width * .5f);
            int start = mesh.currentVertCount;
            mesh.AddVert(from - side, color, Vector2.zero); mesh.AddVert(from + side, color, Vector2.zero);
            mesh.AddVert(to + side, color, Vector2.zero); mesh.AddVert(to - side, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
