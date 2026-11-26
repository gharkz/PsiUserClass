using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PsionClass.Support
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using UnityEngine.UI;
    // Parchment table windows in the Logistics/Psion style: ink column dividers, inked value buttons, no key badges,
    // optional checkbox, section headings, column headers, notes, footer buttons. Rows update in place.
    internal sealed class UiRow
    {
        public string Label;
        public string[] Cells;           // extra columns after the label
        public bool Heading, ColumnHeader, Note, Info;
        public string[] Tabs;            // a tab bar: one inked button per entry, ActiveTab pressed
        public int ActiveTab;
        public Action<int> TabClick;
        public Action<int, bool> TabHover; // pointer entered (true) / left (false) tab i
        public bool Choice;              // one of a set of exclusive choices: button look, Active = pressed
        public bool Active;
        internal Image Fill;
        public bool? Checked;
        public Action<UiRow> Click;      // whole-row click
        public Action<bool> Hover;       // pointer entered (true) / left (false) the row
        public Action<UiRow, int>[] CellClick; // per-cell inked buttons (null entries = plain text)
        public Func<bool> Disabled;
        public Action<UiRow, ItemGeneral> Build; // extra content drawn into the row after the standard parts
        internal ItemGeneral Bound;
        internal Text LabelText;
        internal Text[] CellTexts;
        internal FilterCheckmark Mark;
        internal Color BaseColor;
    }

    internal static class UiTable
    {
        internal static readonly Color Ink = new Color(.39f, .31f, .22f), Warn = new Color(.62f, .20f, .12f);

        internal sealed class Window
        {
            public LayerList Layer;
            public List<UiRow> Rows;
            public float[] Edges; // column edges for the label + cells, e.g. {0, .5, .75, 1}
            public float Width;
            public float RowHeight = 32;
        }

        internal static Window Open(string title, List<UiRow> rows, float[] edges, float width = 760, float height = 560,
            List<(string label, Action click)> footer = null, bool floating = false, string footNote = null, List<(string label, Action click)> corner = null, float rowHeight = 32)
        {
            var w = new Window { Rows = rows, Edges = edges, Width = width - 60, RowHeight = rowHeight };
            // Floating layers (like the inventory) leave map input alone: camera pan, right-click cancel, clicks.
            var layer = floating ? EClass.ui.layerFloat.AddLayer<LayerList>() : EClass.ui.AddLayer<LayerList>();
            w.Layer = layer;
            if (floating)
            {   // no dimmer over the screen: the windows underneath (the inventory) stay usable
                var lockBtn = HarmonyLib.AccessTools.Field(typeof(Layer), "screenLock")?.GetValue(layer) as Component;
                if (lockBtn != null) lockBtn.gameObject.SetActive(false);
            }
            layer.SetHeader(title);
            layer.SetSize(width, height);
            layer.SetList2(rows, r => r.Label ?? "", (r, _) => { if (!Greyed(r)) r.Click?.Invoke(r); }, (r, item) => Format(w, r, item), autoClose: false);
            if (layer.list.gridLayout != null)
            {
                layer.list.gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layer.list.gridLayout.constraintCount = 1;
                layer.list.gridLayout.cellSize = new Vector2(w.Width, w.RowHeight);
            }
            if (footer != null)
                foreach (var (label, click) in footer)
                {
                    var b = layer.windows[0].AddBottomButton(label, () => Plugin.Guard(click));
                    b.mainText.text = label;
                }
            layer.RefreshSize();
            layer.scroll.vertical = true; layer.scroll.horizontal = false;
            float cornerW = corner != null && corner.Count > 0 ? Corner(layer, corner, !string.IsNullOrEmpty(footNote)) : 0f;
            if (!string.IsNullOrEmpty(footNote)) FootNote(layer, footNote, cornerW);
            layer.gameObject.AddComponent<HideNativeHeading>();
            return w;
        }

        // New rows (and title, size) in an open window, without closing it: no flicker, scroll position kept.
        internal static void Rebuild(Window w, string title, List<UiRow> rows, float[] edges, float width, float height)
        {
            var layer = w.Layer;
            w.Rows = rows; w.Edges = edges; w.Width = width - 60;
            layer.SetHeader(title);
            layer.SetSize(width, height);
            layer.SetList2(rows, r => r.Label ?? "", (r, _) => { if (!Greyed(r)) r.Click?.Invoke(r); }, (r, item) => Format(w, r, item), autoClose: false);
            if (layer.list.gridLayout != null) layer.list.gridLayout.cellSize = new Vector2(w.Width, w.RowHeight);
            layer.RefreshSize();
        }

        // A note pinned to the window's bottom edge; the list above gives up the room it needs.
        static void FootNote(LayerList layer, string text, float rightGap = 0f)
        {
            const float h = 52f, pad = 28f;
            var go = new GameObject("Layout foot note", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().ignoreLayout = true; // placed by hand, not by the window's layout group
            var r = (RectTransform)go.transform; r.SetParent(layer.windows[0].transform, false);
            r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(1f, 0f); r.pivot = new Vector2(.5f, 0f);
            // beside corner buttons: centred on the buttons' row (they sit at pad - 4, 30 high)
            float y0 = rightGap > 0 ? pad - 16f : pad - 6f, y1 = rightGap > 0 ? pad + 38f : pad - 6f + h;
            r.offsetMin = new Vector2(pad - 8f, y0); r.offsetMax = new Vector2(-pad + 4f - (rightGap > 0 ? rightGap + 16f : 0f), y1);
            var t = go.GetComponent<Text>(); t.raycastTarget = false; t.text = text;
            var any = layer.GetComponentsInChildren<Text>(true).FirstOrDefault(x => x != t && x.font != null);
            if (any != null) { t.font = any.font; t.fontSize = Mathf.Max(12, any.fontSize - 2); }
            t.color = Ink; t.alignment = rightGap > 0 ? TextAnchor.MiddleLeft : TextAnchor.LowerLeft; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.lineSpacing = 1.15f;
            var sr = (RectTransform)layer.scroll.transform;
            sr.offsetMin = new Vector2(sr.offsetMin.x, sr.offsetMin.y + h + 16f);
        }

        static void HideKey(UIButton button)
        {
            if (button.keyText != null) button.keyText.gameObject.SetActive(false);
            foreach (var image in button.GetComponentsInChildren<Image>(true))
                if (image.sprite != null && image.sprite.name == "bg_key") image.gameObject.SetActive(false);
        }

        static void Format(Window w, UiRow row, ItemGeneral item)
        {
            row.Bound = item;
            var hover = item.GetComponent<RowHover>();
            if (row.Hover != null) (hover ?? item.gameObject.AddComponent<RowHover>()).Hover = row.Hover;
            else if (hover != null) hover.Hover = null;
            var button = item.button1;
            var label = button.mainText;
            HideKey(button);
            var layout = item.GetComponent<LayoutElement>() ?? item.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = w.Width;
            layout.minHeight = layout.preferredHeight = Math.Max(w.RowHeight, row.Note ? 44 : 32);
            var fit = label.GetComponent<ContentSizeFitter>(); if (fit != null) fit.enabled = false;
            var labelLayout = label.GetComponent<LayoutElement>() ?? label.gameObject.AddComponent<LayoutElement>(); labelLayout.ignoreLayout = true;
            bool hasCells = row.Cells != null && row.Cells.Length > 0;
            float split = hasCells ? w.Edges[1] : 1f;
            var lr = label.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = new Vector2(split, 1);
            lr.offsetMin = new Vector2(row.Checked.HasValue ? 44 : row.Heading || row.Note ? 8 : 20, 0); lr.offsetMax = new Vector2(-6, 0);
            label.alignment = TextAnchor.MiddleLeft; label.fontSize = row.Note ? 13 : 14;
            label.horizontalOverflow = row.Note ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            label.text = row.Label ?? "";
            if (row.Note) { label.color = row.Info ? Ink : Warn; label.fontStyle = FontStyle.Normal; }
            else if (row.Heading || row.ColumnHeader) { label.color = Ink; label.fontStyle = FontStyle.Bold; }
            row.BaseColor = label.color;
            row.LabelText = label;
            if (row.Tabs != null) { Tabs(row, button, label); return; }
            if (row.Checked.HasValue) row.Mark = FilterCheckmark.Create(button.transform, new Vector2(24, 0), row.Checked.Value);
            if (row.Choice)
            {
                // Inked button inside the row: a light wash when inactive (click me), a solid fill when active.
                var fill = new GameObject("Layout choice fill", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                fill.GetComponent<LayoutElement>().ignoreLayout = true;
                var fr = (RectTransform)fill.transform; fr.SetParent(button.transform, false);
                fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(row.Cells != null && row.Cells.Length > 0 ? w.Edges[1] : 1f, 1f);
                fr.offsetMin = new Vector2(6, 3); fr.offsetMax = new Vector2(-4, -3);
                fr.SetAsFirstSibling();
                row.Fill = fill.GetComponent<Image>(); row.Fill.raycastTarget = false;
                var o = fill.AddComponent<Outline>(); o.effectColor = new Color(.38f, .28f, .16f, .35f); o.effectDistance = new Vector2(1, -1);
                lr.offsetMin = new Vector2(20, 0);
            }
            if (hasCells)
            {
                row.CellTexts = new Text[row.Cells.Length];
                for (int i = 0; i < row.Cells.Length; i++)
                {
                    int ci = i;
                    Action click = row.CellClick != null && ci < row.CellClick.Length && row.CellClick[ci] != null ? (() => { if (!Greyed(row)) row.CellClick[ci](row, ci); }) : (Action)null;
                    row.CellTexts[i] = Cell(button, label, w.Edges[i + 1], w.Edges[i + 2], row.Cells[i], row.ColumnHeader, click);
                }
            }
            ApplyState(row);
            StyleChoice(row);
            row.Build?.Invoke(row, item);
        }

        static Text Cell(UIButton button, Text template, float from, float to, string text, bool header, Action click)
        {
            var divider = new GameObject("Layout column divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            divider.GetComponent<LayoutElement>().ignoreLayout = true;
            var line = (RectTransform)divider.transform; line.SetParent(button.transform, false);
            line.anchorMin = new Vector2(from, 0); line.anchorMax = new Vector2(from, 1);
            line.offsetMin = new Vector2(-.5f, 1); line.offsetMax = new Vector2(.5f, -1);
            var ink = divider.GetComponent<Image>(); ink.color = new Color(.38f, .28f, .16f, .30f); ink.raycastTarget = false;
            if (click != null)
            {
                var cell = new GameObject("Layout value button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                cell.GetComponent<LayoutElement>().ignoreLayout = true;
                var cr = (RectTransform)cell.transform; cr.SetParent(button.transform, false);
                cr.anchorMin = new Vector2(from, 0); cr.anchorMax = new Vector2(to, 1); cr.offsetMin = new Vector2(4, 3); cr.offsetMax = new Vector2(-4, -3);
                var ci = cell.GetComponent<Image>(); ci.color = new Color(.38f, .28f, .16f, .14f);
                var co = cell.AddComponent<Outline>(); co.effectColor = new Color(.38f, .28f, .16f, .35f); co.effectDistance = new Vector2(1, -1);
                cell.GetComponent<Button>().targetGraphic = ci;
                cell.GetComponent<Button>().onClick.AddListener(() => Plugin.Guard(click));
            }
            var value = UnityEngine.Object.Instantiate(template, template.transform.parent);
            value.name = "Layout cell";
            var vr = value.rectTransform; vr.anchorMin = new Vector2(from, 0); vr.anchorMax = new Vector2(to, 1);
            vr.offsetMin = new Vector2(10, 0); vr.offsetMax = new Vector2(-8, 0);
            value.text = text ?? ""; value.fontStyle = header ? FontStyle.Bold : FontStyle.Normal; value.raycastTarget = false;
            if (header) value.color = Ink;
            value.transform.SetAsLastSibling();
            return value;
        }

        // Tab bar: equal-width inked buttons across the row; the active one pressed (dark fill, bold).
        static void Tabs(UiRow row, UIButton button, Text template)
        {
            template.text = "";
            button.interactable = false;
            int n = row.Tabs.Length;
            for (int i = 0; i < n; i++)
            {
                int k = i;
                bool on = k == row.ActiveTab;
                var go = new GameObject("Layout tab", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.GetComponent<LayoutElement>().ignoreLayout = true;
                var r = (RectTransform)go.transform; r.SetParent(button.transform, false);
                r.anchorMin = new Vector2((float)k / n, 0); r.anchorMax = new Vector2((float)(k + 1) / n, 1);
                r.offsetMin = new Vector2(k == 0 ? 6 : 3, 2); r.offsetMax = new Vector2(k == n - 1 ? -4 : -3, -2);
                var img = go.GetComponent<Image>(); img.color = on ? new Color(.38f, .28f, .16f, .42f) : new Color(.38f, .28f, .16f, .12f);
                var o = go.AddComponent<Outline>(); o.effectColor = new Color(.38f, .28f, .16f, .35f); o.effectDistance = new Vector2(1, -1);
                var b = go.GetComponent<Button>(); b.targetGraphic = img;
                b.onClick.AddListener(() => Plugin.Guard(() => row.TabClick?.Invoke(k)));
                if (row.TabHover != null) go.AddComponent<RowHover>().Hover = on2 => row.TabHover(k, on2);
                var t = UnityEngine.Object.Instantiate(template, go.transform);
                t.name = "Layout tab text";
                var tr = t.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
                t.alignment = TextAnchor.MiddleCenter; t.text = row.Tabs[k]; t.raycastTarget = false;
                t.fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
                t.color = on ? new Color(.20f, .14f, .08f) : Ink;
            }
        }

        internal static bool Greyed(UiRow row) => row.Disabled?.Invoke() ?? false;
        internal static void ApplyState(UiRow row)
        {
            if (row.Bound == null) return;
            bool grey = Greyed(row);
            row.Bound.button1.interactable = (row.Click != null || row.CellClick != null) && !grey;
            if (row.LabelText) row.LabelText.color = new Color(row.BaseColor.r, row.BaseColor.g, row.BaseColor.b, grey ? .38f : row.BaseColor.a);
            if (row.Mark) row.Mark.transform.parent.GetComponent<Image>().color = new Color(.85f, .77f, .60f, grey ? .30f : .70f);
        }
        internal static void SetCell(UiRow row, int i, string text) { row.Cells[i] = text; if (row.CellTexts != null && row.CellTexts[i]) row.CellTexts[i].text = text; }
        internal static void SetLabel(UiRow row, string text) { row.Label = text; if (row.LabelText) row.LabelText.text = text; }
        internal static void SetChecked(UiRow row, bool on) { row.Checked = on; if (row.Mark) row.Mark.gameObject.SetActive(on); }
        internal static void SetActive(UiRow row, bool on) { row.Active = on; StyleChoice(row); }
        static void StyleChoice(UiRow row)
        {
            if (!row.Choice || row.Fill == null) return;
            row.Fill.color = row.Active ? new Color(.38f, .28f, .16f, .42f) : new Color(.38f, .28f, .16f, .12f);
            if (row.LabelText) { row.LabelText.fontStyle = row.Active ? FontStyle.Bold : FontStyle.Normal; row.LabelText.color = row.Active ? new Color(.20f, .14f, .08f) : row.BaseColor; }
        }

        // Clear hover and press feedback for corner buttons: the face darkens a step on hover and two when pressed.
        internal sealed class Face : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
        {
            private Image img; private Color[] c; private bool over;
            internal void Init(Image image, Color[] colors) { img = image; c = colors; }
            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData _) { over = true; img.color = c[1]; }
            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData _) { over = false; img.color = c[0]; }
            public void OnPointerDown(UnityEngine.EventSystems.PointerEventData _) => img.color = c[2];
            public void OnPointerUp(UnityEngine.EventSystems.PointerEventData _) => img.color = over ? c[1] : c[0];
        }

        // Buttons inside the window's bottom-right corner (instead of the hanging tabs under the window).
        static float Corner(LayerList layer, List<(string label, Action click)> buttons, bool besideNote = false)
        {
            // Styled like the tab buttons: an ink wash with an ink outline; the primary (rightmost) action is the
            // pressed-tab look (deeper wash, bold dark ink), the others the plain-tab look. Each steps darker on hover/press.
            const float h = 30f, pad = 22f, gap = 6f;
            var font = layer.GetComponentsInChildren<Text>(true).FirstOrDefault(x => x.font != null);
            float x = -pad;
            for (int i = 0; i < buttons.Count; i++)
            {
                var (label, click) = buttons[buttons.Count - 1 - i];
                bool primary = i == 0;
                float wd = Mathf.Max(96f, 26f + label.Length * 9.5f);
                var go = new GameObject("Layout corner button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.GetComponent<LayoutElement>().ignoreLayout = true;
                var r = (RectTransform)go.transform; r.SetParent(layer.windows[0].transform, false);
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 0f);
                r.sizeDelta = new Vector2(wd, h); r.anchoredPosition = new Vector2(x, pad - 4f);
                x -= wd + gap;
                var face = primary
                    ? new[] { new Color(.38f, .28f, .16f, .42f), new Color(.38f, .28f, .16f, .52f), new Color(.38f, .28f, .16f, .62f) }
                    : new[] { new Color(.38f, .28f, .16f, .12f), new Color(.38f, .28f, .16f, .22f), new Color(.38f, .28f, .16f, .32f) };
                var img = go.GetComponent<Image>(); img.color = face[0];
                var o = go.AddComponent<Outline>(); o.effectColor = new Color(.38f, .28f, .16f, .35f); o.effectDistance = new Vector2(1, -1);
                var b = go.GetComponent<Button>(); b.targetGraphic = img; b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => Plugin.Guard(click));
                go.AddComponent<Face>().Init(img, face);
                var tg = new GameObject("text", typeof(RectTransform), typeof(Text)); var tr = (RectTransform)tg.transform; tr.SetParent(r, false);
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
                var t = tg.GetComponent<Text>(); t.raycastTarget = false; t.alignment = TextAnchor.MiddleCenter; t.text = label;
                if (font != null) { t.font = font.font; t.fontSize = font.fontSize; }
                t.fontStyle = primary ? FontStyle.Bold : FontStyle.Normal;
                t.color = primary ? new Color(.20f, .14f, .08f) : Ink;
            }
            // beside a foot note the buttons share its strip; otherwise the list gives up room for them
            if (!besideNote) { var sr = (RectTransform)layer.scroll.transform; sr.offsetMin = new Vector2(sr.offsetMin.x, sr.offsetMin.y + h + 10f); }
            return -x - pad;
        }

        // Small titled modal listing choices; the current one is checked; a click picks and closes.
        // toggles >= 0: only the first `toggles` options are on/off settings with a box; the rest are plain actions.
        internal static void Choose(string title, IList<(string label, Action pick)> options, int current, int toggles = -1)
        {
            Window modal = null;
            var rows = options.Select((o, i) => new UiRow { Label = o.label, Checked = toggles < 0 || i < toggles ? i == current : (bool?)null, Click = _ => { modal?.Layer.Close(); Plugin.Guard(o.pick); } }).ToList();
            modal = Open(title, rows, new[] { 0f, 1f }, 460, Math.Min(140 + rows.Count * 34, 560));
        }
    }

    // The generic list's "Name" heading row is replaced by our own headings.
    internal sealed class HideNativeHeading : MonoBehaviour
    {
        void LateUpdate()
        {
            foreach (var header in GetComponentsInChildren<UIHeader>(true))
                if (header.transform.parent != null && header.transform.parent.name == "HeaderRow")
                { header.transform.parent.gameObject.SetActive(false); enabled = false; }
        }
    }

    internal sealed class RowHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        internal Action<bool> Hover;
        bool inside;
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { inside = true; Plugin.Guard(() => Hover?.Invoke(true)); }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { if (!inside) return; inside = false; Plugin.Guard(() => Hover?.Invoke(false)); }
        void OnDisable() => OnPointerExit(null);
    }
}
