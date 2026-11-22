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
            public string Label, Value, Value2, Value3;
            public bool? Auto; public FilterCheckmark AutoMark; public int AutoId;
            public bool ColumnHeader;
            public bool Heading, Note, Info;
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
                new Row { Label = UiText.Pick("Class name (this language)", "クラス名(この言語)", "职业名称(当前语言)"), Value = ClassName.Current, Click = EditClassName },
                new Row { Label = UiText.Pick("Get a new copy of the primer (once a day)", "入門書をもう一冊もらう(1日1回)", "再领取一本入门书(每天一次)"), Click = _ => NewPrimer() },
                new Row { Heading = true, Label = UiText.Pick("Telekinesis", "テレキネシス", "念动术") },
                new Row { Label = UiText.Pick("Use a fixed mana cost (ignores spell level)", "固定マナ消費を使う(呪文レベル無関係)", "使用固定魔力消耗(不受法术等级影响)"), Checked = PsionCombat.TkFixedOn(pc),
                    Click = r => { PsionCombat.SetTkFixedOn(pc, !PsionCombat.TkFixedOn(pc)); r.Checked = PsionCombat.TkFixedOn(pc); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); RefreshStates(); } },
                new Row { Indent = true, Label = UiText.Pick("Fixed mana cost", "固定マナ消費", "固定魔力消耗"), Value = FixedText(pc), Click = EditFixed,
                    Disabled = () => !PsionCombat.TkFixedOn(EClass.pc) },
                new Row { Label = UiText.Pick("Mana cost multiplier (0 = free)", "マナ消費の倍率(0=無料)", "魔力消耗倍率(0=免费)"), Value = "x" + Fmt(PsionCombat.TkMpMult(pc)), Click = EditMp,
                    Disabled = () => PsionCombat.TkFixed(EClass.pc).HasValue },
                new Row { Label = UiText.Pick("Collision dice sides multiplier", "衝突ダイス面数の倍率", "碰撞骰面数倍率"), Value = "x" + Fmt(PsionCombat.TkDiceMult(pc)), Click = EditDice },
                new Row { Heading = true, Label = UiText.Pick("Sound", "音", "声音") },
                new Row { Label = UiText.Pick("Telekinesis sound effect", "テレキネシスの効果音", "念动术音效"), Checked = PsionCombat.TkSoundOn(pc),
                    Click = r => { PsionCombat.SetTkSoundOn(pc, !PsionCombat.TkSoundOn(pc)); r.Checked = PsionCombat.TkSoundOn(pc); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); RefreshStates(); } },
            };
            foreach (var sid in PsionCombat.SoundPowers)
            {
                int id = sid;
                rows.Add(new Row { Indent = true, Label = EClass.sources.elements.map[id].GetName().ToTitleCase(), Value = PsionCombat.SoundLabel(PsionCombat.TkSound(pc, id)),
                    Disabled = () => !PsionCombat.TkSoundOn(EClass.pc),
                    Click = r => Choose(r, EClass.sources.elements.map[id].GetName().ToTitleCase() + UiText.Pick(" sound", "の音", "的声音"),
                        PsionCombat.SoundChoices, PsionCombat.TkSound(EClass.pc, id), PsionCombat.SoundLabel, v => PsionCombat.SetTkSound(EClass.pc, id, v)) });
            }
            rows.AddRange(new[]
            {
                new Row { Heading = true, Label = UiText.Pick("Psi-Hurl", "サイハール", "灵能投掷") },
                new Row { Label = UiText.Pick("Mana cost", "マナ消費", "魔力消耗"), Value = PsionCombat.HurlCostLabel(PsionCombat.HurlCostMode(pc)), Click = ChooseHurlCost },
                new Row { Indent = true, Label = UiText.Pick("Multiplier (0 = free)", "倍率(0=無料)", "倍率(0=免费)"), Value = "x" + Fmt(PsionCombat.HurlMult(pc)), Click = EditHurlMult,
                    Disabled = () => PsionCombat.HurlCostMode(EClass.pc) == PsionCombat.HurlCost.Fixed },
                new Row { Indent = true, Label = UiText.Pick("Fixed mana cost", "固定マナ消費", "固定魔力消耗"), Value = PsionCombat.HurlFixed(pc) + " MP", Click = EditHurlFixed,
                    Disabled = () => PsionCombat.HurlCostMode(EClass.pc) != PsionCombat.HurlCost.Fixed },
                new Row { Label = UiText.Pick("AutoCombat: use Telekinesis for adjacent enemies (cheaper)", "自動戦闘: 隣接する敵にはテレキネシスを使う(安価)", "自动战斗: 对相邻敌人使用念动术(更省)"), Checked = PsionCombat.PreferTk(pc),
                    Click = r => { PsionCombat.SetPreferTk(pc, !PsionCombat.PreferTk(pc)); r.Checked = PsionCombat.PreferTk(pc); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); } },
                new Row { Label = UiText.Pick("AutoCombat direction when hurling an enemy", "自動戦闘で敵を投げる方向", "自动战斗投掷敌人的方向"), Value = PsionCombat.HurlDirLabel(PsionCombat.HurlDir(pc)), Click = ChooseHurlDir },
                new Row { Indent = true, Label = UiText.Pick("Prefer it over throwing into other enemies", "他の敵へぶつけるより優先", "优先于砸向其他敌人"), Checked = PsionCombat.HurlDirFirst(pc),
                    Disabled = () => PsionCombat.HurlDir(EClass.pc) == PsionCombat.HurlDirection.Random,
                    Click = r => { PsionCombat.SetHurlDirFirst(pc, !PsionCombat.HurlDirFirst(pc)); r.Checked = PsionCombat.HurlDirFirst(pc); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); } },
                new Row { Label = UiText.Pick("Full flight distance (20 tiles, ignores Telekinesis level)", "最大飛距離(20マス、テレキネシスのレベル無関係)", "完整飞行距离(20格,不受念动术等级影响)"), Checked = PsionCombat.HurlFull(pc),
                    Click = r => { PsionCombat.SetHurlFull(pc, !PsionCombat.HurlFull(pc)); r.Checked = PsionCombat.HurlFull(pc); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); } },
                new Row { Label = UiText.Pick("Telekinetic Control also protects allies you throw", "念動制御は投げた味方も守る", "念动控制也保护你投掷的友方"), Checked = PsionCombat.TkControlAllies(pc),
                    Click = r => { PsionCombat.SetTkControlAllies(pc, !PsionCombat.TkControlAllies(pc)); r.Checked = PsionCombat.TkControlAllies(pc); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); } },
                new Row { Label = UiText.Pick("Telekinetic Control also shields you and allies in the path", "念動制御は経路上の自分と味方も守る", "念动控制也保护路径上的你和友方"), Checked = PsionCombat.TkControlPath(pc),
                    Click = r => { PsionCombat.SetTkControlPath(pc, !PsionCombat.TkControlPath(pc)); r.Checked = PsionCombat.TkControlPath(pc); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); RefreshStates(); } },
                new Row { Label = UiText.Pick("Telekinetic Control rank 2: very heavy bodies still hurt (20%)", "念動制御ランク2: 非常に重い相手は20%受ける", "念动控制等级2: 非常沉重的目标仍造成20%伤害"), Checked = PsionCombat.HeavyMatters(pc),
                    Click = r => { PsionCombat.SetHeavyMatters(pc, !PsionCombat.HeavyMatters(pc)); r.Checked = PsionCombat.HeavyMatters(pc); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); RefreshStates(); } },
                new Row { Indent = true, Label = UiText.Pick("Very heavy means", "非常に重いとは", "非常沉重指"), Value = HeavyBasisText(pc),
                    Disabled = () => !PsionCombat.HeavyMatters(EClass.pc),
                    Click = r => { PsionCombat.SetHeavyByWeight(EClass.pc, !PsionCombat.HeavyByWeight(EClass.pc)); SetValue(r, HeavyBasisText(EClass.pc)); } },
                new Row { Indent = true, Label = UiText.Pick("Multiplier (of yours)", "倍率(自分の)", "倍率(相对于你)"), Value = "x" + Fmt(PsionCombat.HeavyFactor(pc)), Click = EditHeavyFactor,
                    Disabled = () => !PsionCombat.HeavyMatters(EClass.pc) },
                new Row { Label = UiText.Pick("AutoCombat may throw allies with Psi-Hurl", "自動戦闘でサイハールに味方を使う", "自动战斗可用灵能投掷投出友方"), Checked = PsionCombat.AllyAmmo(pc),
                    Click = r => { PsionCombat.SetAllyAmmo(pc, !PsionCombat.AllyAmmo(pc)); r.Checked = PsionCombat.AllyAmmo(pc); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); RefreshStates(); } },
                new Row { Indent = true, Label = UiText.Pick("Only when Telekinetic Control is at least", "念動制御が次以上の時のみ", "仅当念动控制至少为"), Checked = PsionCombat.AllyReqOn(pc),
                    Value = RankText(PsionCombat.AllyReqRank(pc)),
                    ValueClick = r => { PsionCombat.ToggleAllyReqRank(EClass.pc); r.Value = RankText(PsionCombat.AllyReqRank(EClass.pc)); if (r.ValueText) r.ValueText.text = r.Value; },
                    Disabled = () => !PsionCombat.AllyAmmo(EClass.pc),
                    Click = r => { PsionCombat.SetAllyReqOn(pc, !PsionCombat.AllyReqOn(pc)); r.Checked = PsionCombat.AllyReqOn(pc); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); } },
                new Row { Note = true, Info = true, Label = UiText.Pick("Collision damage is the dice roll x (50% + 25% per tile flown): 4 tiles = 150%, 8 = 250%, 20 = 550%.",
                    "衝突ダメージはダイス×(50%+飛んだマスごとに25%): 4マス=150%、8マス=250%、20マス=550%。",
                    "碰撞伤害为骰值×(50%+每飞行一格25%):4格=150%,8格=250%,20格=550%。") },
                new Row { Heading = true, Label = UiText.Pick("Psi-Strike", "サイストライク", "灵能打击") },
                new Row { Label = UiText.Pick("Fixed mana cost", "固定マナ消費", "固定魔力消耗"), Value = PsionCombat.StrikeCost(pc) + " MP", Click = EditStrikeCost },
                new Row { Group = 1, Heading = true, Label = UiText.Pick("Auto-combat Telekinesis", "自動戦闘でのテレキネシス", "自动战斗中的念动术") },
                new Row { Group = 2, Label = UiText.Pick("Use", "使用", "使用"), Value = PsionCombat.Label(PsionCombat.Tk), Click = ChooseTkMode },
                new Row { Group = 2, NeedsAuto = true, Label = UiText.Pick("Only when it deals collision damage", "衝突ダメージを与える時のみ", "仅在造成碰撞伤害时"), Checked = PsionCombat.DamageOnly(pc),
                    Click = r => { PsionCombat.SetDamageOnly(pc, !PsionCombat.DamageOnly(pc)); r.Checked = PsionCombat.DamageOnly(pc); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); RefreshStates(); } },
                new Row { Group = 2, NeedsAuto = true, Indent = true, Label = UiText.Pick("Allow a pull that sets up a hurl", "放り飛ばしにつながる引き寄せは許可", "允许为抛飞铺垫的拉近"), Checked = PsionCombat.Combo(pc),
                    Disabled = () => PsionCombat.Tk != PsionCombat.TkMode.Both || !PsionCombat.DamageOnly(pc),
                    Click = r => { PsionCombat.SetCombo(pc, !PsionCombat.Combo(pc)); r.Checked = PsionCombat.Combo(pc); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); } },
                new Row { Group = 2, NeedsAuto = true, Label = UiText.Pick("Don't use if its path holds:", "経路に次がいれば使わない:", "路径上有以下对象时不使用:") },
            });
            var kinds = (PsionCombat.Bystander[])Enum.GetValues(typeof(PsionCombat.Bystander));
            for (int i = 0; i < kinds.Length; i++)
            {
                var kind = kinds[i];
                rows.Add(new Row { Group = i == kinds.Length - 1 ? 3 : 2, NeedsAuto = true, Indent = true, Label = PsionCombat.BystanderLabel(kind), Checked = PsionCombat.Blocks(pc, kind),
                    Click = r => { PsionCombat.ToggleBlock(pc, kind); r.Checked = PsionCombat.Blocks(pc, kind); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); } });
            }
            rows.Add(new Row { Heading = true, Label = UiText.Pick("Powers", "能力", "能力") });
            rows.Add(new Row { Label = UiText.Pick("Apply AutoCombat bonuses (on top of any strategy)", "自動戦闘ボーナスを適用(どの作戦にも上乗せ)", "应用自动战斗加成(叠加于任何战术)"), Checked = PsionCombat.BonusOn(pc),
                Click = r => { PsionCombat.SetBonusOn(pc, !PsionCombat.BonusOn(pc)); r.Checked = PsionCombat.BonusOn(pc); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); } });
            rows.Add(new Row { Note = true, Info = true, Label = UiText.Pick("Checked = innate (no spell stock). Click a number to change it.",
                "チェック=生得(ストック不要)。数値をクリックで変更。", "勾选=天生(无需储备)。点击数值可修改。") });
            rows.Add(new Row { Note = true, Info = true, Label = UiText.Pick("AutoCombat column: unchecked means AutoCombat never uses that power.",
                "自動戦闘の列: チェックを外すと自動戦闘はその能力を使わない。", "自动战斗列: 取消勾选后自动战斗不会使用该能力。") });
            rows.Add(new Row { Note = true, Info = true, Label = UiText.Pick("AutoCombat bonus: the higher it is, the more often AutoCombat picks that power when it's available.",
                "自動戦闘ボーナス: 高いほど、使用可能な時に自動戦闘がその能力を選びやすくなる。",
                "自动战斗加成: 数值越高,自动战斗在可用时越常选择该能力。") });
            rows.Add(new Row { ColumnHeader = true, Label = UiText.Pick("Power", "能力", "能力"), Value = UiText.Pick("Unlocks at", "解放レベル", "解锁等级"), Value2 = UiText.Pick("AutoCombat bonus", "自動戦闘ボーナス", "自动战斗加成"), Value3 = UiText.Pick("AutoCombat", "自動戦闘", "自动战斗") });
            foreach (var (lv, id) in PsionData.Unlocks.GroupBy(u => u.id).Select(g => g.First()))
                rows.Add(new Row
                {
                    Label = EClass.sources.elements.map[id].GetName().ToTitleCase(),
                    Value = UiText.Pick("Psicraft ", "サイクラフト", "灵能术") + PowerConfig.Level(pc, id),
                    ValueClick = r => EditLevel(r, id),
                    Value2 = Signed(PowerConfig.Bonus(pc, id)),
                    Value2Click = r => EditBonus(r, id),
                    Auto = PowerConfig.AutoOn(pc, id), AutoId = id,
                    Checked = PowerConfig.Enabled(pc, id),
                    Click = r => { PowerConfig.Toggle(pc, id); r.Checked = PowerConfig.Enabled(pc, id); if (r.Mark) r.Mark.gameObject.SetActive(r.Checked.Value); },
                });

            shown = rows;
            var layer = EClass.ui.AddLayer<LayerList>();
            current = layer;
            layer.SetHeader(ClassName.Current + UiText.Pick(" settings", "設定", "设置"));
            layer.SetSize(720, 560);
            PsionData.RefreshLiveTexts();
            layer.SetList2(rows, r => r.Label, (r, _) => { r.Click?.Invoke(r); PsionData.RefreshLiveTexts(); }, (r, item) => Format(r, item), autoClose: false);
            if (layer.list.gridLayout != null)
            {
                layer.list.gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layer.list.gridLayout.constraintCount = 1;
                layer.list.gridLayout.cellSize = new Vector2(660, 32);
            }
            layer.RefreshSize();
            layer.scroll.vertical = true; layer.scroll.horizontal = false;
            layer.gameObject.AddComponent<HideNativeHeading>();
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
            bool four = row.Value3 != null || row.Auto.HasValue;
            float split = four ? .40f : row.Value2 != null ? .46f : .62f, split2 = four ? .60f : .68f, split3 = .82f;
            var lr = label.rectTransform; lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(row.Value != null ? split : 1, 1);
            float left = (row.Checked.HasValue ? 44 : row.Heading || row.Note ? 8 : 20) + (row.Indent ? 24 : 0) + (row.Group > 0 ? 8 : 0);
            lr.offsetMin = new Vector2(left, 0); lr.offsetMax = new Vector2(-6, 0);
            label.alignment = TextAnchor.MiddleLeft; label.fontSize = row.Note ? 13 : 14;
            label.horizontalOverflow = row.Note ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            label.text = row.Label;
            if (row.Note) { label.color = row.Info ? Ink : Warn; label.fontStyle = FontStyle.Normal; }
            else if (row.Heading || row.ColumnHeader) { label.color = Ink; label.fontStyle = FontStyle.Bold; }
            row.BaseColor = label.color;
            if (row.Checked.HasValue) row.Mark = FilterCheckmark.Create(button.transform, new Vector2(24 + (row.Indent ? 24 : 0) + (row.Group > 0 ? 8 : 0), 0), row.Checked.Value);
            if (row.Group > 0) GroupBox(button.transform, row.Group);
            if (row.Value == null) { ApplyState(row); return; }
            float valueEnd = row.Value2 != null ? split2 : 1f;
            float value2End = four ? split3 : 1f;
            row.ValueText = Cell(button, label, split, valueEnd, row.Value, row.ColumnHeader, row.ValueClick == null ? null : (Action)(() => row.ValueClick(row)));
            if (row.Value2 != null)
                row.Value2Text = Cell(button, label, split2, value2End, row.Value2, row.ColumnHeader, row.Value2Click == null ? null : (Action)(() => row.Value2Click(row)));
            if (row.Value3 != null) Cell(button, label, split3, 1f, row.Value3, true, null);
            if (row.Auto.HasValue)
            {
                var pc = EClass.pc;
                Cell(button, label, split3, 1f, "", false, () =>
                {
                    int id = row.AutoId; PowerConfig.ToggleAuto(pc, id);
                    row.Auto = PowerConfig.AutoOn(pc, id); if (row.AutoMark) row.AutoMark.gameObject.SetActive(row.Auto.Value);
                });
                row.AutoMark = FilterCheckmark.Create(button.transform, Vector2.zero, row.Auto.Value);
                var box = (RectTransform)row.AutoMark.transform.parent;
                box.anchorMin = box.anchorMax = new Vector2((split3 + 1f) / 2, .5f); box.anchoredPosition = Vector2.zero;
                box.SetAsLastSibling();
            }
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
                cell.GetComponent<Button>().onClick.AddListener(() => { click(); PsionData.RefreshLiveTexts(); });
            }
            var value = UnityEngine.Object.Instantiate(template, template.transform.parent);
            value.name = "Psion value";
            var vr = value.rectTransform; vr.anchorMin = new Vector2(from, 0); vr.anchorMax = new Vector2(to, 1);
            vr.offsetMin = new Vector2(10, 0); vr.offsetMax = new Vector2(-8, 0);
            value.text = text; value.fontStyle = header ? FontStyle.Bold : FontStyle.Normal; value.raycastTarget = false;
            if (header) value.color = Ink;
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

        static void SetValue(Row row, string text) { row.Value = text; if (row.ValueText) row.ValueText.text = text; PsionData.RefreshLiveTexts(); }

        // Small modal window listing every mode (same table styling); the current one is checked, a click picks and closes.
        static string RankText(int rank) => UiText.Pick("rank ", "ランク", "等级") + rank;

        static void Choose<T>(Row row, string title, T[] values, T current, Func<T, string> label, Action<T> set)
        {
            LayerList modal = null;
            var options = values.Select(v => new Row
            {
                Label = label(v), Checked = Equals(v, current),
                Click = _ => { set(v); SetValue(row, label(v)); modal?.Close(); RefreshStates(); },
            }).ToList();
            modal = EClass.ui.AddLayer<LayerList>();
            modal.SetHeader(title);
            modal.SetSize(440, Math.Min(420, 120 + values.Length * 34));
            modal.SetList2(options, r => r.Label, (r, _) => r.Click?.Invoke(r), (r, item) => Format(r, item, 380), autoClose: false);
            if (modal.list.gridLayout != null)
            {
                modal.list.gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                modal.list.gridLayout.constraintCount = 1;
                modal.list.gridLayout.cellSize = new Vector2(380, 32);
            }
            modal.RefreshSize();
            modal.gameObject.AddComponent<HideNativeHeading>();
        }

        static void ChooseHurlCost(Row row) => Choose(row, UiText.Pick("Psi-Hurl mana cost", "サイハールのマナ消費", "灵能投掷魔力消耗"),
            (PsionCombat.HurlCost[])Enum.GetValues(typeof(PsionCombat.HurlCost)), PsionCombat.HurlCostMode(EClass.pc), PsionCombat.HurlCostLabel, m => PsionCombat.SetHurlCostMode(EClass.pc, m));

        static void ChooseHurlDir(Row row) => Choose(row, UiText.Pick("AutoCombat Psi-Hurl direction", "自動戦闘でのサイハールの方向", "自动战斗灵能投掷方向"),
            (PsionCombat.HurlDirection[])Enum.GetValues(typeof(PsionCombat.HurlDirection)), PsionCombat.HurlDir(EClass.pc), PsionCombat.HurlDirLabel, d => PsionCombat.SetHurlDir(EClass.pc, d));

        static void EditHurlMult(Row row)
        {
            var pc = EClass.pc;
            Modals.Input(UiText.Pick("Psi-Hurl cost multiplier (0 = free, e.g. 1.5)", "サイハールの消費倍率(0=無料、例: 1.5)", "灵能投掷消耗倍率(0=免费,例如1.5)"),
                Fmt(PsionCombat.HurlMult(pc)), (cancel, text) =>
                {
                    if (cancel) return;
                    var s = (text ?? "").Trim().TrimStart('x', 'X');
                    if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && !float.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out v)
                        || float.IsNaN(v) || float.IsInfinity(v) || v < 0 || v > 10)
                    {
                        Msg.Say(UiText.Pick("Not a valid multiplier (0 to 10). Kept x", "無効な倍率です(0～10)。x", "倍率无效(0至10)。保留x") + Fmt(PsionCombat.HurlMult(pc)) + UiText.Pick(".", "のままです。", "。"));
                        return;
                    }
                    PsionCombat.SetHurlMult(pc, v);
                    SetValue(row, "x" + Fmt(v));
                }, Dialog.InputType.Default);
        }

        // Mana cost entry: a number 0-999 (decimals truncated by the caller), either decimal mark, an optional "MP" suffix.
        // Empty, non-numeric, NaN, infinite, negative or 1000+ is rejected and the old value kept.
        static bool ParseCost(string text, out double v)
        {
            var t = (text ?? "").Trim();
            if (t.EndsWith("MP", StringComparison.OrdinalIgnoreCase)) t = t.Substring(0, t.Length - 2).Trim();
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && !double.TryParse(t, NumberStyles.Float, CultureInfo.CurrentCulture, out v)) return false;
            return !double.IsNaN(v) && !double.IsInfinity(v) && v >= 0 && v < 1000;
        }

        static void EditStrikeCost(Row row)
        {
            var pc = EClass.pc;
            Modals.Input(UiText.Pick("Psi-Strike mana cost (whole number 0-999)", "サイストライクのマナ消費(0～999の整数)", "灵能打击魔力消耗(0-999的整数)"),
                PsionCombat.StrikeCost(pc).ToString(), (cancel, text) =>
                {
                    if (cancel) return;
                    if (!ParseCost(text, out double v))
                    {
                        Msg.Say(UiText.Pick("Not a valid cost (a whole number 0 to 999). Kept ", "無効な値です(0～999の整数)。", "数值无效(0至999的整数)。保留") + PsionCombat.StrikeCost(pc) + " MP" + UiText.Pick(".", "のままです。", "。"));
                        return;
                    }
                    PsionCombat.SetStrikeCost(pc, (int)Math.Truncate(v));
                    SetValue(row, PsionCombat.StrikeCost(pc) + " MP");
                }, Dialog.InputType.Default);
        }

        static string HeavyBasisText(Chara pc) => PsionCombat.HeavyByWeight(pc)
            ? UiText.Pick("its weight vs. yours", "重さの比較", "重量比较") : UiText.Pick("its level vs. yours", "レベルの比較", "等级比较");

        // Positive number up to 100. Invalid input is dropped and the old value kept.
        static void EditHeavyFactor(Row row)
        {
            var pc = EClass.pc;
            Modals.Input(UiText.Pick("Very heavy: at least this many times your level or weight (e.g. 2)", "非常に重い: 自分のレベルまたは重さの何倍以上か(例: 2)", "非常沉重: 至少为你等级或重量的几倍(例如2)"),
                Fmt(PsionCombat.HeavyFactor(pc)), (cancel, text) =>
                {
                    if (cancel) return;
                    var s = (text ?? "").Trim().TrimStart('x', 'X');
                    if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && !float.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out v)
                        || float.IsNaN(v) || float.IsInfinity(v) || v <= 0 || v > 100)
                    {
                        Msg.Say(UiText.Pick("Not a valid multiplier (a positive number up to 100). Kept x", "無効な倍率です(100以下の正の数)。x", "倍率无效(不超过100的正数)。保留x") + Fmt(PsionCombat.HeavyFactor(pc)) + UiText.Pick(".", "のままです。", "。"));
                        return;
                    }
                    PsionCombat.SetHeavyFactor(pc, v);
                    SetValue(row, "x" + Fmt(v));
                }, Dialog.InputType.Default);
        }

        // A lost primer can be replaced, at most once per in-game day (no farming).
        const string KeyPrimerDay = "psion_primer_day";
        static void NewPrimer()
        {
            var pc = EClass.pc;
            int today = EClass.world.date.GetRawDay();
            if (int.TryParse(pc.GetStr(KeyPrimerDay), out int last) && last == today)
            {
                Msg.Say(UiText.Pick("You already received a copy today.", "今日はもう受け取った。", "今天已经领取过了。"));
                return;
            }
            pc.SetStr(KeyPrimerDay, today.ToString());
            pc.Pick(ThingGen.CreateRedBook("guide_psion"));
        }

        static void EditHurlFixed(Row row)
        {
            var pc = EClass.pc;
            Modals.Input(UiText.Pick("Fixed Psi-Hurl mana cost (whole number 0-999)", "サイハールの固定マナ消費(0～999の整数)", "灵能投掷固定魔力消耗(0-999的整数)"),
                PsionCombat.HurlFixed(pc).ToString(), (cancel, text) =>
                {
                    if (cancel) return;
                    if (!ParseCost(text, out double v))
                    {
                        Msg.Say(UiText.Pick("Not a valid cost (a whole number 0 to 999). Kept ", "無効な値です(0～999の整数)。", "数值无效(0至999的整数)。保留") + PsionCombat.HurlFixed(pc) + " MP" + UiText.Pick(".", "のままです。", "。"));
                        return;
                    }
                    PsionCombat.SetHurlFixed(pc, (int)Math.Truncate(v));
                    SetValue(row, PsionCombat.HurlFixed(pc) + " MP");
                }, Dialog.InputType.Default);
        }

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
            modal.gameObject.AddComponent<HideNativeHeading>();
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

        // 1-24 characters; empty restores the default for this language.
        static void EditClassName(Row row)
        {
            Modals.Input(UiText.Pick("Class name for this language (empty = default)", "この言語でのクラス名(空欄=初期値)", "当前语言的职业名称(留空=默认)"),
                ClassName.Current, (cancel, text) =>
                {
                    if (cancel) return;
                    var t = (text ?? "").Trim();
                    if (t.Length > 24) { Msg.Say(UiText.Pick("Too long (24 characters max). Kept ", "長すぎます(最大24文字)。", "太长(最多24个字符)。保留") + ClassName.Current + UiText.Pick(".", "のままです。", "。")); return; }
                    ClassName.Set(ClassName.LangKey, t);
                    SetValue(row, ClassName.Current);
                    if (current) current.SetHeader(ClassName.Current + UiText.Pick(" settings", "設定", "设置"));
                }, Dialog.InputType.Default);
        }

        static string Fmt(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}

namespace PsionClass
{
    // The generic list's single "Name" heading row is replaced by our own headings (as in the Logistics tables).
    internal sealed class HideNativeHeading : UnityEngine.MonoBehaviour
    {
        void LateUpdate()
        {
            foreach (var header in GetComponentsInChildren<UIHeader>(true))
                if (header.transform.parent != null && header.transform.parent.name == "HeaderRow")
                { header.transform.parent.gameObject.SetActive(false); enabled = false; }
        }
    }
}
