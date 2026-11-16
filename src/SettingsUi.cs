using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PsionClass
{
    // Psion settings window, styled after the Logistics Computer tables: parchment checkboxes, ink column dividers,
    // no key-letter badges, in-place updates, a choice popup and a validated text input over a visible window.
    internal static class SettingsUi
    {
        sealed class Row
        {
            public string Label, Value;
            public bool Heading, Note;
            public bool? Checked;
            public Action<Row> Click, ValueClick;
            public bool NeedsAuto, Indent;
            public Func<bool> Disabled;
            public int Group; // 1 = box top, 2 = inside, 3 = box bottom
            public ItemGeneral Bound;
            public FilterCheckmark Mark;
            public Text ValueText;
        }

        static readonly Color Ink = new Color(.39f, .31f, .22f), Warn = new Color(.62f, .20f, .12f);

        internal static void Open()
        {
            var pc = EClass.pc;
            var rows = new List<Row>
            {
                new Row { Note = true, Label = UiText.Pick("Back up your save before changing powers. Turning a power back on clears its spell stock.",
                    "能力を変更する前にセーブをバックアップしてください。能力を再びオンにするとその呪文のストックは消えます。",
                    "更改能力前请先备份存档。重新开启能力会清除其法术储备。") },
                new Row { Heading = true, Label = UiText.Pick("Telekinesis", "テレキネシス", "念动术") },
                new Row { Label = UiText.Pick("Mana cost multiplier (0 = free)", "マナ消費の倍率(0=無料)", "魔力消耗倍率(0=免费)"), Value = "x" + Fmt(PsionCombat.TkMpMult(pc)), Click = EditMp },
                new Row { Label = UiText.Pick("Collision dice sides multiplier", "衝突ダイス面数の倍率", "碰撞骰面数倍率"), Value = "x" + Fmt(PsionCombat.TkDiceMult(pc)), Click = EditDice },
                new Row { Group = 1, Heading = true, Label = UiText.Pick("Auto-combat Telekinesis", "自動戦闘でのテレキネシス", "自动战斗中的念动术") },
                new Row { Group = 2, Label = UiText.Pick("Use", "使用", "使用"), Value = PsionCombat.Label(PsionCombat.Tk), Click = ChooseTkMode },
                new Row { Group = 2, NeedsAuto = true, Label = UiText.Pick("Only when it deals collision damage", "衝突ダメージを与える時のみ", "仅在造成碰撞伤害时"), Checked = PsionCombat.DamageOnly(pc),
                    Click = r => { PsionCombat.SetDamageOnly(pc, !PsionCombat.DamageOnly(pc)); Reopen(); } },
                new Row { Group = 2, NeedsAuto = true, Indent = true, Label = UiText.Pick("Allow a pull that sets up a hurl", "放り飛ばしにつながる引き寄せは許可", "允许为抛飞铺垫的拉近"), Checked = PsionCombat.Combo(pc),
                    Disabled = () => PsionCombat.Tk != PsionCombat.TkMode.Both || !PsionCombat.DamageOnly(pc),
                    Click = r => { PsionCombat.SetCombo(pc, !PsionCombat.Combo(pc)); r.Checked = PsionCombat.Combo(pc); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); } },
                new Row { Group = 2, NeedsAuto = true, Label = UiText.Pick("Don't use if its path holds:", "経路に次がいれば使わない:", "路径上有以下对象时不使用:") },
            };
            var kinds = (PsionCombat.Bystander[])Enum.GetValues(typeof(PsionCombat.Bystander));
            for (int i = 0; i < kinds.Length; i++)
            {
                var kind = kinds[i];
                rows.Add(new Row { Group = i == kinds.Length - 1 ? 3 : 2, NeedsAuto = true, Indent = true, Label = PsionCombat.BystanderLabel(kind), Checked = PsionCombat.Blocks(pc, kind),
                    Click = r => { PsionCombat.ToggleBlock(pc, kind); r.Checked = PsionCombat.Blocks(pc, kind); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); } });
            }
            rows.Add(new Row { Heading = true, Label = UiText.Pick("Powers (checked = innate, no spell stock; click a level to change it)", "能力(チェック=生得、ストック不要。レベルをクリックで変更)", "能力(勾选=天生,无需储备;点击等级可修改)") });
            foreach (var (lv, id) in PsionData.Unlocks.GroupBy(u => u.id).Select(g => g.First()))
                rows.Add(new Row
                {
                    Label = EClass.sources.elements.map[id].GetName().ToTitleCase(),
                    Value = UiText.Pick("Psicraft ", "サイクラフト", "灵能术") + PowerConfig.Level(pc, id),
                    ValueClick = r => EditLevel(r, id),
                    Checked = PowerConfig.Enabled(pc, id),
                    Click = r => { PowerConfig.Toggle(pc, id); r.Checked = PowerConfig.Enabled(pc, id); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); },
                });

            var layer = EClass.ui.AddLayer<LayerList>();
            current = layer;
            layer.SetHeader(UiText.Pick("Psion settings", "サイオン設定", "灵能者设置"));
            layer.SetSize(720, 560);
            layer.SetList2(rows, r => r.Label, (r, _) => r.Click?.Invoke(r), (r, item) => Format(r, item), autoClose: false);
            if (layer.list.gridLayout != null)
            {
                layer.list.gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layer.list.gridLayout.constraintCount = 1;
                layer.list.gridLayout.cellSize = new Vector2(660, 32);
            }
            layer.RefreshSize();
            layer.scroll.vertical = true; layer.scroll.horizontal = false;
        }

        static void HideKey(UIButton button)
        {
            if (button.keyText != null) button.keyText.gameObject.SetActive(false);
            foreach (var image in button.GetComponentsInChildren<Image>(true))
                if (image.sprite != null && image.sprite.name == "bg_key") image.gameObject.SetActive(false);
        }

        static void Format(Row row, ItemGeneral item, float width = 660)
        {
            row.Bound = item;
            var button = item.button1;
            var label = button.mainText;
            HideKey(button);
            bool autoOff = (row.NeedsAuto && PsionCombat.Tk == PsionCombat.TkMode.Off) || (row.Disabled?.Invoke() ?? false);
            button.interactable = row.Click != null && !autoOff;
            var layout = item.GetComponent<LayoutElement>() ?? item.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = width;
            layout.minHeight = layout.preferredHeight = row.Note ? 44 : 32;
            var fit = label.GetComponent<ContentSizeFitter>(); if (fit != null) fit.enabled = false;
            var labelLayout = label.GetComponent<LayoutElement>() ?? label.gameObject.AddComponent<LayoutElement>(); labelLayout.ignoreLayout = true;
            var lr = label.rectTransform; lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(row.Value != null ? .62f : 1, 1);
            float left = (row.Checked.HasValue ? 44 : row.Heading || row.Note ? 8 : 20) + (row.Indent ? 24 : 0) + (row.Group > 0 ? 8 : 0);
            lr.offsetMin = new Vector2(left, 0); lr.offsetMax = new Vector2(-6, 0);
            label.alignment = TextAnchor.MiddleLeft; label.fontSize = row.Note ? 13 : 14;
            label.horizontalOverflow = row.Note ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            label.text = row.Label;
            if (row.Note) { label.color = Warn; label.fontStyle = FontStyle.Normal; }
            else if (row.Heading) { label.color = Ink; label.fontStyle = FontStyle.Bold; }
            if (row.Checked.HasValue) row.Mark = FilterCheckmark.Create(button.transform, new Vector2(24 + (row.Indent ? 24 : 0) + (row.Group > 0 ? 8 : 0), 0), row.Checked.Value);
            if (row.Group > 0) GroupBox(button.transform, row.Group);
            if (autoOff) { label.color = new Color(label.color.r, label.color.g, label.color.b, .38f); if (row.Mark) row.Mark.transform.parent.GetComponent<Image>().color = new Color(.85f, .77f, .60f, .30f); }
            if (row.Value == null) return;
            var divider = new GameObject("Psion column divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            divider.GetComponent<LayoutElement>().ignoreLayout = true;
            var line = (RectTransform)divider.transform; line.SetParent(button.transform, false);
            line.anchorMin = new Vector2(.62f, 0); line.anchorMax = new Vector2(.62f, 1);
            line.offsetMin = new Vector2(-.5f, 1); line.offsetMax = new Vector2(.5f, -1);
            var ink = divider.GetComponent<Image>(); ink.color = new Color(.38f, .28f, .16f, .30f); ink.raycastTarget = false;
            var value = UnityEngine.Object.Instantiate(label, label.transform.parent);
            value.name = "Psion value";
            var vr = value.rectTransform; vr.anchorMin = new Vector2(.62f, 0); vr.anchorMax = new Vector2(1, 1);
            vr.offsetMin = new Vector2(10, 0); vr.offsetMax = new Vector2(-8, 0);
            value.text = row.Value; value.fontStyle = FontStyle.Normal;
            row.ValueText = value;
            if (row.ValueClick != null)
            {
                // Separate click target over the value cell (inked panel like Logistics action buttons).
                var cell = new GameObject("Psion value button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                cell.GetComponent<LayoutElement>().ignoreLayout = true;
                var cr = (RectTransform)cell.transform; cr.SetParent(button.transform, false);
                cr.anchorMin = new Vector2(.62f, 0); cr.anchorMax = new Vector2(1, 1); cr.offsetMin = new Vector2(4, 3); cr.offsetMax = new Vector2(-4, -3);
                var ci = cell.GetComponent<Image>(); ci.color = new Color(.38f, .28f, .16f, .14f);
                var co = cell.AddComponent<Outline>(); co.effectColor = new Color(.38f, .28f, .16f, .35f); co.effectDistance = new Vector2(1, -1);
                cell.GetComponent<Button>().targetGraphic = ci;
                cell.GetComponent<Button>().onClick.AddListener(() => row.ValueClick(row));
                value.transform.SetAsLastSibling(); value.raycastTarget = false;
            }
        }

        static LayerList current;
        static void Reopen() { EClass.core.actionsNextFrame.Add(() => { if (current != null) current.Close(); Open(); }); }
        // Group box: light panel with ink edges; top edge on the first row, bottom edge on the last.
        static void GroupBox(Transform parent, int part)
        {
            var color = new Color(.38f, .28f, .16f, .45f);
            var bg = new GameObject("Psion group panel", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            bg.GetComponent<LayoutElement>().ignoreLayout = true;
            var r = (RectTransform)bg.transform; r.SetParent(parent, false); r.SetAsFirstSibling();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(2, part == 3 ? 2 : 0); r.offsetMax = new Vector2(-2, part == 1 ? -2 : 0);
            var img = bg.GetComponent<Image>(); img.color = new Color(.56f, .50f, .40f, .10f); img.raycastTarget = false;
            void Edge(Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
            {
                var e = new GameObject("Psion group edge", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                e.GetComponent<LayoutElement>().ignoreLayout = true;
                var er = (RectTransform)e.transform; er.SetParent(r, false);
                er.anchorMin = aMin; er.anchorMax = aMax; er.offsetMin = oMin; er.offsetMax = oMax;
                var ei = e.GetComponent<Image>(); ei.color = color; ei.raycastTarget = false;
            }
            Edge(new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(1, 0));
            Edge(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-1, 0), Vector2.zero);
            if (part == 1) Edge(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -1), Vector2.zero);
            if (part == 3) Edge(new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
        }

        static void SetValue(Row row, string text) { row.Value = text; if (row.ValueText) row.ValueText.text = text; }

        // Small modal window listing every mode (same table styling); the current one is checked, a click picks and closes.
        static void ChooseTkMode(Row row)
        {
            LayerList modal = null;
            var options = ((PsionCombat.TkMode[])Enum.GetValues(typeof(PsionCombat.TkMode))).Select(m => new Row
            {
                Label = PsionCombat.Label(m),
                Checked = PsionCombat.Tk == m,
                Click = _ =>
                {
                    bool was = PsionCombat.Tk == PsionCombat.TkMode.Off;
                    PsionCombat.Tk = m;
                    SetValue(row, PsionCombat.Label(m));
                    modal?.Close();
                    Reopen();
                },
            }).ToList();
            modal = EClass.ui.AddLayer<LayerList>();
            modal.SetHeader(UiText.Pick("Auto-combat Telekinesis", "自動戦闘でのテレキネシス", "自动战斗中的念动术"));
            modal.SetSize(440, 300);
            modal.SetList2(options, r => r.Label, (r, _) => r.Click?.Invoke(r), (r, item) => Format(r, item, 380), autoClose: false);
            if (modal.list.gridLayout != null)
            {
                modal.list.gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                modal.list.gridLayout.constraintCount = 1;
                modal.list.gridLayout.cellSize = new Vector2(380, 32);
            }
            modal.RefreshSize();
        }

        // Positive number (max 10). Invalid input is dropped and the old value kept.
        static void EditDice(Row row)
        {
            var pc = EClass.pc;
            Modals.Input(UiText.Pick("Collision dice sides multiplier (positive number, e.g. 1.5)", "衝突ダイス面数の倍率(正の数、例: 1.5)", "碰撞骰面数倍率(正数,例如1.5)"),
                Fmt(PsionCombat.TkDiceMult(pc)), (cancel, text) =>
                {
                    if (cancel) return;
                    var s = (text ?? "").Trim().TrimStart('x', 'X');
                    if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && !float.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out v)
                        || float.IsNaN(v) || float.IsInfinity(v) || v <= 0 || v > 10)
                    {
                        Msg.Say(UiText.Pick("Not a valid multiplier (a positive number up to 10). Kept x", "無効な倍率です(10以下の正の数)。x", "倍率无效(不超过10的正数)。保留x") + Fmt(PsionCombat.TkDiceMult(pc))
                            + UiText.Pick(".", "のままです。", "。"));
                        return;
                    }
                    PsionCombat.SetTkDiceMult(pc, v);
                    SetValue(row, "x" + Fmt(v));
                }, Dialog.InputType.Default);
        }

        // Zero or positive number (max 10). Invalid input is dropped and the old value kept.
        static void EditMp(Row row)
        {
            var pc = EClass.pc;
            Modals.Input(UiText.Pick("Telekinesis mana cost multiplier (0 = free, e.g. 0.5)", "テレキネシスのマナ消費倍率(0=無料、例: 0.5)", "念动术魔力消耗倍率(0=免费,例如0.5)"),
                Fmt(PsionCombat.TkMpMult(pc)), (cancel, text) =>
                {
                    if (cancel) return;
                    var s = (text ?? "").Trim().TrimStart('x', 'X');
                    if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && !float.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out v)
                        || float.IsNaN(v) || float.IsInfinity(v) || v < 0 || v > 10)
                    {
                        Msg.Say(UiText.Pick("Not a valid multiplier (0 to 10). Kept x", "無効な倍率です(0～10)。x", "倍率无效(0至10)。保留x") + Fmt(PsionCombat.TkMpMult(pc))
                            + UiText.Pick(".", "のままです。", "。"));
                        return;
                    }
                    PsionCombat.SetTkMpMult(pc, v);
                    SetValue(row, "x" + Fmt(v));
                }, Dialog.InputType.Default);
        }

        // Whole number 1-999. Invalid input is dropped and the old value kept.
        static void EditLevel(Row row, int id)
        {
            var pc = EClass.pc;
            Modals.Input(UiText.Pick("Psicraft level that unlocks ", "解放するサイクラフトのレベル: ", "解锁所需灵能术等级: ") + EClass.sources.elements.map[id].GetName() + " (1-999)",
                PowerConfig.Level(pc, id).ToString(), (cancel, text) =>
                {
                    if (cancel) return;
                    if (!int.TryParse((text ?? "").Trim(), out int lv) || lv < 1 || lv > 999)
                    {
                        Msg.Say(UiText.Pick("Not a valid level (1 to 999). Kept ", "無効なレベルです(1～999)。", "等级无效(1至999)。保留") + PowerConfig.Level(pc, id) + UiText.Pick(".", "のままです。", "。"));
                        return;
                    }
                    PowerConfig.SetLevel(pc, id, lv);
                    SetValue(row, UiText.Pick("Psicraft ", "サイクラフト", "灵能术") + lv);
                }, Dialog.InputType.Default);
        }

        static string Fmt(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
