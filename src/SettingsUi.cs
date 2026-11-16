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
            public string Label, Value, Value2;
            public bool ColumnHeader;
            public bool Heading, Note;
            public bool? Checked;
            public Action<Row> Click, ValueClick, Value2Click;
            public bool NeedsAuto, Indent;
            public Func<bool> Disabled;
            public int Group; // 1 = box top, 2 = inside, 3 = box bottom
            public ItemGeneral Bound;
            public FilterCheckmark Mark;
            public Text ValueText, LabelText, Value2Text;
            public Color BaseColor;
        }

        static LayerList current;
        static List<Row> shown = new List<Row>();
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
                new Row { Label = UiText.Pick("Use a fixed mana cost (ignores spell level)", "固定マナ消費を使う(呪文レベル無関係)", "使用固定魔力消耗(不受法术等级影响)"), Checked = PsionCombat.TkFixedOn(pc),
                    Click = r => { PsionCombat.SetTkFixedOn(pc, !PsionCombat.TkFixedOn(pc)); r.Checked = PsionCombat.TkFixedOn(pc); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); RefreshStates(); } },
                new Row { Indent = true, Label = UiText.Pick("Fixed mana cost", "固定マナ消費", "固定魔力消耗"), Value = FixedText(pc), Click = EditFixed,
                    Disabled = () => !PsionCombat.TkFixedOn(EClass.pc) },
                new Row { Label = UiText.Pick("Mana cost multiplier (0 = free)", "マナ消費の倍率(0=無料)", "魔力消耗倍率(0=免费)"), Value = "x" + Fmt(PsionCombat.TkMpMult(pc)), Click = EditMp,
                    Disabled = () => PsionCombat.TkFixed(EClass.pc).HasValue },
                new Row { Label = UiText.Pick("Collision dice sides multiplier", "衝突ダイス面数の倍率", "碰撞骰面数倍率"), Value = "x" + Fmt(PsionCombat.TkDiceMult(pc)), Click = EditDice },
                new Row { Group = 1, Heading = true, Label = UiText.Pick("Auto-combat Telekinesis", "自動戦闘でのテレキネシス", "自动战斗中的念动术") },
                new Row { Group = 2, Label = UiText.Pick("Use", "使用", "使用"), Value = PsionCombat.Label(PsionCombat.Tk), Click = ChooseTkMode },
                new Row { Group = 2, NeedsAuto = true, Label = UiText.Pick("Only when it deals collision damage", "衝突ダメージを与える時のみ", "仅在造成碰撞伤害时"), Checked = PsionCombat.DamageOnly(pc),
                    Click = r => { PsionCombat.SetDamageOnly(pc, !PsionCombat.DamageOnly(pc)); r.Checked = PsionCombat.DamageOnly(pc); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); RefreshStates(); } },
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
            rows.Add(new Row { Heading = true, Label = UiText.Pick("Powers", "能力", "能力") });
            rows.Add(new Row { Note = true, Label = UiText.Pick("Checked = innate (no spell stock). Click a number to change it. AutoCombat bonus applies with the Psion strategy.",
                "チェック=生得(ストック不要)。数値をクリックで変更。自動戦闘ボーナスはサイオン作戦で有効。", "勾选=天生(无需储备)。点击数值可修改。自动战斗加成在灵能者战术下生效。") });
            rows.Add(new Row { ColumnHeader = true, Label = UiText.Pick("Power", "能力", "能力"), Value = UiText.Pick("Unlocks at", "解放レベル", "解锁等级"), Value2 = UiText.Pick("AutoCombat bonus", "自動戦闘ボーナス", "自动战斗加成") });
            foreach (var (lv, id) in PsionData.Unlocks.GroupBy(u => u.id).Select(g => g.First()))
                rows.Add(new Row
                {
                    Label = EClass.sources.elements.map[id].GetName().ToTitleCase(),
                    Value = UiText.Pick("Psicraft ", "サイクラフト", "灵能术") + PowerConfig.Level(pc, id),
                    ValueClick = r => EditLevel(r, id),
                    Value2 = Signed(PowerConfig.Bonus(pc, id)),
                    Value2Click = r => EditBonus(r, id),
                    Checked = PowerConfig.Enabled(pc, id),
                    Click = r => { PowerConfig.Toggle(pc, id); r.Checked = PowerConfig.Enabled(pc, id); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); },
                });

            shown = rows;
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
            row.LabelText = label;
            var layout = item.GetComponent<LayoutElement>() ?? item.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = width;
            layout.minHeight = layout.preferredHeight = row.Note ? 44 : 32;
            var fit = label.GetComponent<ContentSizeFitter>(); if (fit != null) fit.enabled = false;
            var labelLayout = label.GetComponent<LayoutElement>() ?? label.gameObject.AddComponent<LayoutElement>(); labelLayout.ignoreLayout = true;
            float split = row.Value2 != null ? .46f : .62f, split2 = .68f;
            var lr = label.rectTransform; lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(row.Value != null ? split : 1, 1);
            float left = (row.Checked.HasValue ? 44 : row.Heading || row.Note ? 8 : 20) + (row.Indent ? 24 : 0) + (row.Group > 0 ? 8 : 0);
            lr.offsetMin = new Vector2(left, 0); lr.offsetMax = new Vector2(-6, 0);
            label.alignment = TextAnchor.MiddleLeft; label.fontSize = row.Note ? 13 : 14;
            label.horizontalOverflow = row.Note ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            label.text = row.Label;
            if (row.Note) { label.color = Warn; label.fontStyle = FontStyle.Normal; }
            else if (row.Heading || row.ColumnHeader) { label.color = Ink; label.fontStyle = FontStyle.Bold; }
            row.BaseColor = label.color;
            if (row.Checked.HasValue) row.Mark = FilterCheckmark.Create(button.transform, new Vector2(24 + (row.Indent ? 24 : 0) + (row.Group > 0 ? 8 : 0), 0), row.Checked.Value);
            if (row.Group > 0) GroupBox(button.transform, row.Group);
            if (row.Value == null) { ApplyState(row); return; }
            float valueEnd = row.Value2 != null ? split2 : 1f;
            row.ValueText = Cell(button, label, split, valueEnd, row.Value, row.ColumnHeader, row.ValueClick == null ? null : (Action)(() => row.ValueClick(row)));
            if (row.Value2 != null)
                row.Value2Text = Cell(button, label, split2, 1f, row.Value2, row.ColumnHeader, row.Value2Click == null ? null : (Action)(() => row.Value2Click(row)));
            ApplyState(row);
        }

        // One table cell: ink divider on its left edge, text, and (if clickable) an inked button panel behind it.
        static Text Cell(UIButton button, Text template, float from, float to, string text, bool header, Action click)
        {
            var divider = new GameObject("Psion column divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            divider.GetComponent<LayoutElement>().ignoreLayout = true;
            var line = (RectTransform)divider.transform; line.SetParent(button.transform, false);
            line.anchorMin = new Vector2(from, 0); line.anchorMax = new Vector2(from, 1);
            line.offsetMin = new Vector2(-.5f, 1); line.offsetMax = new Vector2(.5f, -1);
            var ink = divider.GetComponent<Image>(); ink.color = new Color(.38f, .28f, .16f, .30f); ink.raycastTarget = false;
            if (click != null)
            {
                var cell = new GameObject("Psion value button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                cell.GetComponent<LayoutElement>().ignoreLayout = true;
                var cr = (RectTransform)cell.transform; cr.SetParent(button.transform, false);
                cr.anchorMin = new Vector2(from, 0); cr.anchorMax = new Vector2(to, 1); cr.offsetMin = new Vector2(4, 3); cr.offsetMax = new Vector2(-4, -3);
                var ci = cell.GetComponent<Image>(); ci.color = new Color(.38f, .28f, .16f, .14f);
                var co = cell.AddComponent<Outline>(); co.effectColor = new Color(.38f, .28f, .16f, .35f); co.effectDistance = new Vector2(1, -1);
                cell.GetComponent<Button>().targetGraphic = ci;
                cell.GetComponent<Button>().onClick.AddListener(() => click());
            }
            var value = UnityEngine.Object.Instantiate(template, template.transform.parent);
            value.name = "Psion value";
            var vr = value.rectTransform; vr.anchorMin = new Vector2(from, 0); vr.anchorMax = new Vector2(to, 1);
            vr.offsetMin = new Vector2(10, 0); vr.offsetMax = new Vector2(-8, 0);
            value.text = text; value.fontStyle = header ? FontStyle.Bold : FontStyle.Normal; value.raycastTarget = false;
            value.transform.SetAsLastSibling();
            return value;
        }

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

        static bool Greyed(Row row) => (row.NeedsAuto && PsionCombat.Tk == PsionCombat.TkMode.Off) || (row.Disabled?.Invoke() ?? false);
        // Update a row's enabled look in place (no window rebuild).
        static void ApplyState(Row row)
        {
            if (row.Bound == null) return;
            bool grey = Greyed(row);
            row.Bound.button1.interactable = row.Click != null && !grey;
            if (row.LabelText) row.LabelText.color = new Color(row.BaseColor.r, row.BaseColor.g, row.BaseColor.b, grey ? .38f : row.BaseColor.a);
            if (row.Mark) row.Mark.transform.parent.GetComponent<Image>().color = new Color(.85f, .77f, .60f, grey ? .30f : .70f);
        }
        static void RefreshStates() { foreach (var r in shown) ApplyState(r); }

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
                    RefreshStates();
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

        static string FixedText(Chara pc) => PsionCombat.TkFixedValue(pc) + " MP";

        // Whole number 0-999 (decimals truncated). Anything else, including empty, is dropped and the old value kept.
        static void EditFixed(Row row)
        {
            var pc = EClass.pc;
            Modals.Input(UiText.Pick("Fixed Telekinesis mana cost (whole number 0-999)", "テレキネシスの固定マナ消費(0～999の整数)", "念动术固定魔力消耗(0-999的整数)"),
                PsionCombat.TkFixedValue(pc).ToString(), (cancel, text) =>
                {
                    if (cancel) return;
                    var t = (text ?? "").Trim();
                    if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && !double.TryParse(t, NumberStyles.Float, CultureInfo.CurrentCulture, out v)
                        || double.IsNaN(v) || double.IsInfinity(v) || v < 0 || v >= 1000)
                    {
                        Msg.Say(UiText.Pick("Not a valid cost (a whole number 0 to 999). Kept ", "無効な値です(0～999の整数)。", "数值无效(0至999的整数)。保留") + FixedText(pc) + UiText.Pick(".", "のままです。", "。"));
                        return;
                    }
                    PsionCombat.SetTkFixedValue(pc, (int)Math.Truncate(v));
                    SetValue(row, FixedText(pc));
                }, Dialog.InputType.Default);
        }

        static string Signed(int v) => v > 0 ? "+" + v : v.ToString();

        // Whole number -100..200. Invalid input is dropped and the old value kept.
        static void EditBonus(Row row, int id)
        {
            var pc = EClass.pc;
            Modals.Input(UiText.Pick("Auto-combat priority bonus for ", "自動戦闘の優先ボーナス: ", "自动战斗优先加成: ") + EClass.sources.elements.map[id].GetName() + " (-100 to 200)",
                PowerConfig.Bonus(pc, id).ToString(), (cancel, text) =>
                {
                    if (cancel) return;
                    if (!int.TryParse((text ?? "").Trim().TrimStart('+'), out int v) || v < -100 || v > 200)
                    {
                        Msg.Say(UiText.Pick("Not a valid bonus (-100 to 200). Kept ", "無効な値です(-100～200)。", "数值无效(-100至200)。保留") + Signed(PowerConfig.Bonus(pc, id)) + UiText.Pick(".", "のままです。", "。"));
                        return;
                    }
                    PowerConfig.SetBonus(pc, id, v);
                    row.Value2 = Signed(v); if (row.Value2Text) row.Value2Text.text = row.Value2;
                }, Dialog.InputType.Default);
        }

        static string Fmt(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
