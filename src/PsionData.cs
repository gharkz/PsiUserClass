using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PsionClass
{
    // Registers the Psion's source rows in code (standalone; cloned from vanilla rows so every field is sane).
    internal static class PsionData
    {
        internal const string JobId = "psion";
        internal const int Psicraft = 79400, FeatPsion = 79401, FeatNecropathy = 79402;
        internal const int KineticArrow = 79410, KineticBolt = 79411, KineticBall = 79412, KineticFunnel = 79413;
        internal const int NecropathyEnchant = 79403, PsiShield = 79404, PsiTeleport = 79405, PsiEdge = 79406, PsiHurl = 79407, FeatTkControl = 79408, PsiStrike = 79409;
        internal static readonly int[] KineticIds = { KineticArrow, KineticBolt, KineticBall, KineticFunnel };
        internal const int Telekinesis = 8215;
        internal const int PsiArrow = 50504, PsiBolt = 50304, PsiBall = 50104, PsiFunnel = 50604; // vanilla *_Mind rows
        internal static readonly int[] OwnElementIds = { Psicraft, FeatPsion, FeatNecropathy, KineticArrow, KineticBolt, KineticBall, KineticFunnel, PsiShield, NecropathyEnchant, PsiTeleport, PsiEdge, PsiHurl, FeatTkControl, PsiStrike };

        // Psicraft (base) level -> power. Irregular on purpose; Necropathy gating lives in the feat's req.
        internal static readonly (int lv, int id)[] Unlocks =
        {
            (1, Telekinesis), (5, PsiArrow), (5, PsiHurl), (10, PsiBolt), (15, PsiStrike), (15, KineticArrow), (20, KineticBolt), (25, PsiShield),
            (35, PsiBall), (45, PsiFunnel), (45, KineticBall), (50, PsiTeleport), (55, KineticFunnel),
        };
        // Kinetic clone -> vanilla Impact row it copies (and absorbs on unlock).
        internal static readonly Dictionary<int, string> KineticSource = new Dictionary<int, string>
        {
            { KineticArrow, "arrow_Impact" }, { KineticBolt, "bolt_Impact" }, { KineticBall, "ball_Impact" }, { KineticFunnel, "funnel_Impact" },
        };

        static T Clone<T>(T row) => (T)typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(row, null);

        static void SetText(object row, string field, string en, string jp, string cn)
        {
            var t = row.GetType();
            t.GetField(field)?.SetValue(row, en);
            t.GetField(field + "_JP")?.SetValue(row, jp);
            t.GetField(field + "_L")?.SetValue(row, Lang.langCode == "CN" ? cn : Lang.langCode == "JP" ? jp : en);
        }

        internal static bool Registered => EClass.sources.elements.map.ContainsKey(Psicraft) && EClass.sources.jobs.map.ContainsKey(JobId);

        internal static void Register()
        {
            var E = EClass.sources.elements;
            if (!E.map.ContainsKey(Psicraft))
            {
                foreach (var id in OwnElementIds)
                    if (E.map.ContainsKey(id)) throw new InvalidOperationException("Element id already taken: " + id);
                AddElement(MakePsicraft(E));
                AddElement(MakeFeatPsion(E));
                AddElement(MakeNecropathy(E));
                foreach (var kv in KineticSource) AddElement(MakeKinetic(E, kv.Key, kv.Value));
                AddElement(MakePsiShield(E));
                AddElement(MakeNecropathyEnchant(E));
                AddElement(MakePsiTeleport(E));
                AddElement(MakePsiEdge(E));
                AddElement(MakePsiHurl(E));
                AddElement(MakeTkControl(E));
                AddElement(MakePsiStrike(E));
            }
            if (!EClass.sources.tactics.map.ContainsKey(JobId)) AddTactics();
            if (!EClass.sources.jobs.map.ContainsKey(JobId)) AddJob();
            ClassName.Apply();
            RefreshLiveTexts();
        }

        static void AddElement(SourceElement.Row row)
        {
            var E = EClass.sources.elements;
            row.OnImportData(E);
            E.rows.Add(row);
            E.map[row.id] = row;
            E.alias[row.alias] = row;
            if (row.group == "ABILITY" || row.group == "SPELL") ACT.dict[row.alias] = ACT.Create(row); // as ACT.Init does
        }

        static SourceElement.Row MakePsicraft(SourceElement E)
        {
            var r = Clone(E.alias["casting"]);
            r.id = Psicraft; r.alias = "psicraft";
            SetText(r, "name", "Psicraft", "サイクラフト", "灵能术");
            SetText(r, "detail", "The exotic discipline of the Psion. Raises the power of psionic abilities and unlocks new ones.",
                "サイオンの異端の術。サイ能力を強め、新たな能力を目覚めさせる。", "灵能者的奇异之术。强化灵能并解锁新的能力。");
            SetText(r, "textPhase", "Your mind sharpens.", "精神が研ぎ澄まされる。", "你的精神更加敏锐。");
            r.aliasParent = "CHA";
            r.geneSlot = -1;
            r.chance = 0; // never a random skill reward
            return r;
        }

        static SourceElement.Row MakeFeatPsion(SourceElement E)
        {
            var r = Clone(E.alias["featWizard"]);
            r.id = FeatPsion; r.alias = "featPsion";
            SetText(r, "name", "Psionic Mind", "サイオンの素質", "灵能天赋");
            SetText(r, "textExtra", "Psi powers awaken as Psicraft grows",
                "サイクラフトの成長でサイ能力が目覚める",
                "随灵能术成长觉醒灵能");
            SetText(r, "textPhase", "You have psychic powers.", "あなたにはサイの力がある。", "你拥有灵能之力。");
            SetText(r, "textInc", "You have psychic powers.", "あなたにはサイの力がある。", "你拥有灵能之力。");
            r.max = 1;
            r.geneSlot = -1;
            return r;
        }

        static SourceElement.Row MakeNecropathy(SourceElement E)
        {
            var r = Clone(E.alias["featLooter"]);
            r.id = FeatNecropathy; r.alias = "featNecropathy";
            SetText(r, "name", "Necropathy", "ネクロパシー", "死灵感应");
            SetText(r, "detail", "Attune your mind to the undead.", "アンデッドに心を通わせる。", "让心灵与不死者相通。");
            SetText(r, "textPhase", "Your mind is attuned to the undead.", "あなたの心はアンデッドに通じている。", "你的心灵与不死者相通。"); // feat list line (was Looter's) // the clone kept Looter's line
            // One line per rank (the hover card shows the current rank and the next); commas separate a rank's points.
            SetText(r, "textExtra",
                "Shrug off psychic backlash from the undead\n" +
                "Shrug off psychic backlash from the undead,Mind attacks reach the undead at 50% effect\n" +
                "Shrug off psychic backlash from the undead,Mind attacks reach the undead at 100% effect",
                "アンデッドからの精神反動を受けない\n" +
                "アンデッドからの精神反動を受けない,精神攻撃がアンデッドに50%の効果\n" +
                "アンデッドからの精神反動を受けない,精神攻撃がアンデッドに100%の効果",
                "不再受到不死者的精神反噬\n" +
                "不再受到不死者的精神反噬,精神攻击对不死者有50%效果\n" +
                "不再受到不死者的精神反噬,精神攻击对不死者有100%效果");
            r.req = new[] { "psicraft", "10", "20", "30" };
            r.cost = new[] { 1, 2, 3 };
            r.max = 3;
            r.geneSlot = -1;
            return r;
        }

        static SourceElement.Row MakeKinetic(SourceElement E, int id, string vanillaAlias)
        {
            var r = Clone(E.alias[vanillaAlias]);
            r.id = id;
            r.alias = vanillaAlias.Split('_')[0] + "_PsiKinetic"; // "arrow_..." keeps the generic arrow_/bolt_ dice key
            r.aliasParent = "CHA";
            r.geneSlot = -1;
            r.chance = 0; // no spellbooks for these
            return r;
        }

        static SourceElement.Row MakePsiShield(SourceElement E)
        {
            var r = Clone(E.alias["SpHolyShield"]);
            r.id = PsiShield; r.alias = "SpPsiShield";
            SetText(r, "name", "Psi-Shield", "サイシールド", "灵能护盾");
            r.aliasParent = "CHA";
            r.geneSlot = -1;
            r.chance = 0; // no spellbooks
            return r; // same proc (Buff, ConHolyShield): never stacks with Holy Shield, strongest wins
        }

        static SourceElement.Row MakeNecropathyEnchant(SourceElement E)
        {
            var r = Clone(E.alias["negateTeleport"]);
            r.id = NecropathyEnchant; r.alias = "encNecropathy";
            SetText(r, "name", "Necropathy", "ネクロパシー", "死灵感应");
            SetText(r, "textExtra", "Necropathy rank (Psions; stacks with the feat, max 3)", "ネクロパシーのランク(サイオン用、特技と合算、最大3)", "死灵感应等级(灵能者;与专长叠加,最多3)");
            r.encSlot = "all";
            r.chance = 20;  // rare roll
            r.LV = 20;
            r.tag = new string[0];
            r.geneSlot = -1;
            return r;
        }

        static SourceElement.Row MakePsiTeleport(SourceElement E)
        {
            var r = Clone(E.alias["SpTeleport"]);
            r.id = PsiTeleport; r.alias = "SpPsiTeleport";
            SetText(r, "name", "Psi-Teleport", "サイテレポート", "灵能传送");
            r.type = "ActPsiTeleport";
            r.proc = new string[0];
            r.cost = new[] { 24 };          // 2x Teleport
            r.abilityType = new string[0];  // AI never uses it
            r.aliasParent = "CHA";
            r.geneSlot = -1;
            r.chance = 0;
            return r;
        }

        static SourceElement.Row MakePsiEdge(SourceElement E)
        {
            var r = Clone(E.alias["featPsion"]);
            r.id = PsiEdge; r.alias = "featPsiEdge";
            SetText(r, "name", "Psionic Edge", "サイオニック・エッジ", "灵能锋刃");
            SetText(r, "textExtra", "+#1 hardness to harvesting tools,+#1 to crafting and utility skills",
                "採取道具の硬度+#1,製作・技術系スキル+#1", "采集工具硬度+#1,制作与技巧类技能+#1");
            SetText(r, "textPhase", "Your mind sharpens your hands.", "精神が手先を研ぎ澄ます。", "精神使你的双手更加灵巧。");
            SetText(r, "textInc", "Your psionic edge sharpens.", "サイの刃が研ぎ澄まされた。", "你的灵能锋刃更加锐利了。");
            r.max = 99;
            r.geneSlot = -1;
            return r;
        }

        static SourceElement.Row MakePsiHurl(SourceElement E)
        {
            var r = Clone(E.alias["SpTelekinesis"]);
            r.id = PsiHurl; r.alias = "SpPsiHurl";
            SetText(r, "name", "Psi-Hurl", "サイハール", "灵能投掷");
            SetText(r, "detail", "Hurl any object or creature you can see, yourself included, in a direction you choose. Shares Telekinesis's level. Flies 4 tiles, +1 per 3 Telekinesis levels (up to 20; settings can make it always 20). Collision damage is the dice roll x (50% + 25% per tile flown), so long flights hit far harder than the dice suggest. Costs 1.5x Telekinesis. Heavy or distant targets resist more.",
                "見えている物や生き物(自分も可)を好きな方向へ放り飛ばす。レベルはテレキネシスと共通。飛距離は4マス、テレキネシス3レベルごとに+1(最大20、設定で常に20も可)。衝突ダメージはダイス×(50%+飛んだマスごとに25%)なので、長く飛ぶほどダイス以上に強い。消費はテレキネシスの1.5倍。重い・遠い対象ほど抵抗する。",
                "将视野内的任何物体或生物(包括自己)朝你选择的方向抛出。等级与念动术共享。飞行4格,念动术每3级+1(最多20,可在设置中改为始终20)。碰撞伤害为骰值×(50%+每飞行一格25%),飞得越远伤害远超骰值。消耗为念动术的1.5倍。沉重或远处的目标更难抛动。");
            SetText(r, "textExtra", "Can be used on creatures, items on the ground and yourself,Higher power allows heavier objects to be moved,Hurls the target along a line you aim from its tile,The target pushes aside any creatures along its path,Invoke a #calc #ele collision,Collision damage increases with target weight and distance",
                "生物、地面のアイテム、自分を対象にできる,パワーが上がるほどより重い対象を動かせる,対象の位置から狙った方向へ放り飛ばす,対象は移動経路にいる生物を押しのける,#calcの#eleの衝突,衝突ダメージは対象の重量と移動距離に比例する",
                "可对生物、地面物品及自己使用,威力越高可移动越重的目标,将目标从其所在位置沿瞄准方向抛出,目标会推开路径上的生物,造成#calc的#ele碰撞,碰撞伤害随目标重量和移动距离增加");
            r.type = "ActPsiHurl";
            r.aliasParent = "CHA";
            r.geneSlot = -1;
            r.chance = 0; // no spellbooks
            return r;
        }

        static SourceElement.Row MakePsiStrike(SourceElement E)
        {
            var r = Clone(E.alias["SpTelekinesis"]);
            r.id = PsiStrike; r.alias = "SpPsiStrike";
            SetText(r, "name", "Psi-Strike", "サイストライク", "灵能打击");
            SetText(r, "detail", "Carry your ordinary melee attack to a target up to 8 tiles away with your mind, like a blade thrown and called back. It uses your equipped weapons (or martial arts) exactly as a normal attack does: same damage, same skill experience. Psicraft and this ability's level add no power to it. Fixed mana cost.",
                "精神の力で通常の近接攻撃を最大8マス先の対象へ届ける。投げて呼び戻す刃のように。装備中の武器(または格闘)を通常攻撃と同じく使い、ダメージもスキル経験も同じ。サイクラフトやこの能力のレベルで威力は上がらない。マナ消費は固定。",
                "以精神之力将普通近战攻击送达至多8格外的目标,如掷出又召回的刀刃。与普通攻击一样使用已装备的武器(或格斗):伤害与技能经验相同。灵能术与该能力等级不增加威力。魔力消耗固定。");
            SetText(r, "textExtra", "Invoke your melee attack on the target,Reaches up to 8 tiles; needs line of sight,Uses your equipped weapons or martial arts like a normal attack,Psicraft and ability level add no power",
                "対象に近接攻撃を行う,最大8マスまで届く(視線が必要),通常攻撃と同じく装備中の武器か格闘を使う,サイクラフトや能力レベルで威力は上がらない",
                "对目标发动近战攻击,最远8格(需要视线),与普通攻击一样使用已装备的武器或格斗,灵能术与能力等级不增加威力");
            r.type = "ActPsiStrike";
            r.aliasParent = "CHA";
            r.geneSlot = -1;
            r.chance = 0;
            return r;
        }

        static SourceElement.Row MakeTkControl(SourceElement E)
        {
            var r = Clone(E.alias["featNecropathy"]);
            r.id = FeatTkControl; r.alias = "featTkControl";
            SetText(r, "name", "Telekinetic Control", "念動制御", "念动控制");
            SetText(r, "detail", "Use your own telekinesis to steady your landing.", "自らの念動力で着地を整える。", "用自己的念动力稳住落地。");
            SetText(r, "textPhase", "You can catch yourself mid-flight.", "飛ばされても自分を受け止められる。", "你能在飞行中稳住自己。");
            SetText(r, "textExtra",
                "Self-hurl collision damage -50% (allies in your hurls too),Collision damage when hurled by others -10%\n" +
                "Self-hurl collision damage -100% (allies in your hurls too; very heavy bodies still bruise),Collision damage when hurled by others -20%",
                "自分を投げた時の衝突ダメージ-50%(投げに巻き込んだ味方も),他者に投げられた時の衝突ダメージ-10%\n" +
                "自分を投げた時の衝突ダメージ-100%(投げに巻き込んだ味方も、非常に重い相手は別),他者に投げられた時の衝突ダメージ-20%",
                "自我投掷碰撞伤害-50%(你投掷中波及的友方同样),被他人投掷时碰撞伤害-10%\n" +
                "自我投掷碰撞伤害-100%(你投掷中波及的友方同样;非常沉重的目标除外),被他人投掷时碰撞伤害-20%");
            r.req = new[] { "psicraft", "5", "10" };
            r.cost = new[] { 1, 1 };
            r.max = 2;
            r.geneSlot = -1;
            return r;
        }

        static void AddTactics()
        {
            var T = EClass.sources.tactics;
            var r = Clone(T.map["wizard"]);
            r.id = JobId;
            SetText(r, "name", "Psion", "サイオン", "灵能者");
            r.dist = 2; r.move = 50; r.movePC = 0; r.taunt = -1;
            r.melee = 55; r.range = 50; r.spell = 70; r.heal = 60; r.summon = 55; r.buff = 50; r.debuff = 45; r.party = -20;
            r.tag = (r.tag ?? new string[0]).Where(t => t != "pc").ToArray(); // NPC tactics only; players overlay bonuses on any strategy
            T.rows.Add(r);
            T.map[r.id] = r;
        }

        static void AddJob()
        {
            var J = EClass.sources.jobs;
            var r = Clone(J.map["wizard"]);
            r.id = JobId;
            SetText(r, "name", "psion", "サイオン", "灵能者");
            SetText(r, "detail", "An exotic mystic whose power comes from the mind itself, not from magic. Psionic powers awaken as Psicraft grows, but mind attacks used on the undead lash back.",
                "魔力ではなく精神そのものから力を引き出す異端の神秘家。サイクラフトの成長と共にサイ能力が目覚めるが、アンデッドへの精神攻撃は自らに跳ね返る。",
                "力量源自精神本身而非魔力的奇异秘术师。灵能随灵能术成长而觉醒,但对不死者使用精神攻击会反噬自身。");
            r.playable = 1;
            r.STR = 0; r.END = 0; r.DEX = 0; r.PER = 4; r.LER = 5; r.WIL = 3; r.MAG = 3; r.CHA = 10; r.SPD = 0;
            // staff/blunt/short sword 4 (140), throwing 3, casting 4, control magic 2 (120), Psicraft 4 (140), class feat.
            // 103 weaponStaff, 111 weaponBlunt, 107 weaponDagger (short sword), 108 throwing, 304 casting, 302 controlmana.
            r.elements = new[] { 103, 4, 111, 4, 107, 4, 108, 3, 304, 4, 302, 2, Psicraft, 4, FeatPsion, 1 };
            r.weapon = new[] { "staff", "blunt", "dagger" };
            r.equip = "mage";
            r.domain = new[] { 914, 1, 918, 1, 917, 1 };
            var map = Element.GetElementMap(r.elements);
            map[70] = r.STR; map[71] = r.END; map[72] = r.DEX; map[73] = r.PER; map[74] = r.LER;
            map[75] = r.WIL; map[76] = r.MAG; map[77] = r.CHA; map[79] = r.SPD;
            r.elementMap = map;
            J.rows.Add(r);
            J.map[r.id] = r;
        }

        internal static bool IsPsion(Chara c) => c != null && c.job?.id == JobId;

        // Feat and power texts that quote Psion settings, rebuilt from the player's current settings
        // (on load, when the settings window opens, and after each change in it).
        internal static void RefreshLiveTexts()
        {
            try
            {
                var E = EClass.sources?.elements;
                var pc = EClass.core.IsGameStarted ? EClass.pc : null;
                if (E == null || !E.map.ContainsKey(FeatTkControl)) return;
                string X(float v) => "x" + v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

                // Telekinetic Control: allies line only while that setting is on.
                bool allies = pc == null || PsionCombat.TkControlAllies(pc);
                bool heavy = pc == null || PsionCombat.HeavyMatters(pc);
                bool path = pc != null && PsionCombat.TkControlPath(pc);
                // Short comma-separated points per rank (the feat list shows the current rank on one line).
                bool byW = pc != null && PsionCombat.HeavyByWeight(pc);
                string f = "x" + (pc != null ? PsionCombat.HeavyFactor(pc) : 2f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                string extraEn = (allies ? ",Same for allies you throw" : "") + (path ? ",Shields you and allies in the path" : "");
                string extraJp = (allies ? ",投げた味方も同様" : "") + (path ? ",経路上の自分と味方も守る" : "");
                string extraCn = (allies ? ",你投掷的友方同样" : "") + (path ? ",保护路径上的你和友方" : "");
                string heavyEn = heavy ? ",Bodies of " + (byW ? "weight" : "Lv") + " " + f + "+ still deal 20%" : "";
                string heavyJp = heavy ? "," + (byW ? "重さ" : "Lv") + f + "以上の相手からは20%受ける" : "";
                string heavyCn = heavy ? "," + (byW ? "重量" : "等级") + f + "以上的目标仍造成20%" : "";
                SetText(E.map[FeatTkControl], "textExtra",
                    "Self-hurl collisions -50%,Hurled by others -10%" + extraEn + "\n" +
                    "Self-hurl collisions -100%" + heavyEn + ",Hurled by others -20%" + extraEn,
                    "自分を投げた衝突-50%,他者に投げられた時-10%" + extraJp + "\n" +
                    "自分を投げた衝突-100%" + heavyJp + ",他者に投げられた時-20%" + extraJp,
                    "自我投掷碰撞-50%,被他人投掷-10%" + extraCn + "\n" +
                    "自我投掷碰撞-100%" + heavyCn + ",被他人投掷-20%" + extraCn);

                // Psi-Hurl: flight and cost.
                bool full = pc != null && PsionCombat.HurlFull(pc);
                var mode = pc != null ? PsionCombat.HurlCostMode(pc) : PsionCombat.HurlCost.CurrentTk;
                float hm = pc != null ? PsionCombat.HurlMult(pc) : 1.5f;
                int hf = pc != null ? PsionCombat.HurlFixed(pc) : 5;
                string fEn = full ? "Always flies the full 20 tiles." : "Flies 4 tiles, +1 per 3 Telekinesis levels (up to 20).";
                string fJp = full ? "常に最大の20マス飛ぶ。" : "飛距離は4マス、テレキネシス3レベルごとに+1(最大20)。";
                string fCn = full ? "始终飞行完整的20格。" : "飞行4格,念动术每3级+1(最多20)。";
                string cEn = mode == PsionCombat.HurlCost.Fixed ? "Costs a fixed " + hf + " mana." : hm <= 0 ? "Costs no mana." : mode == PsionCombat.HurlCost.NaturalTk ? "Costs " + X(hm) + " Telekinesis's natural mana cost." : "Costs " + X(hm) + " Telekinesis's mana.";
                string cJp = mode == PsionCombat.HurlCost.Fixed ? "消費は固定" + hf + "マナ。" : hm <= 0 ? "マナを消費しない。" : mode == PsionCombat.HurlCost.NaturalTk ? "消費はテレキネシス本来の消費の" + X(hm) + "。" : "消費はテレキネシスの" + X(hm) + "。";
                string cCn = mode == PsionCombat.HurlCost.Fixed ? "固定消耗" + hf + "点魔力。" : hm <= 0 ? "不消耗魔力。" : mode == PsionCombat.HurlCost.NaturalTk ? "消耗为念动术原始消耗的" + X(hm) + "。" : "消耗为念动术的" + X(hm) + "。";
                SetText(E.map[PsiHurl], "detail",
                    "Hurl any object or creature you can see, yourself included, in a direction you choose. Shares Telekinesis's level and grants Telekinesis experience. " + fEn + " Collision damage is the dice roll x (50% + 25% per tile flown), so long flights hit far harder than the dice suggest. " + cEn + " Heavy or distant targets resist more.",
                    "見えている物や生き物(自分も可)を好きな方向へ放り飛ばす。レベルはテレキネシスと共通で、テレキネシスの経験を得る。" + fJp + "衝突ダメージはダイス×(50%+飛んだマスごとに25%)なので、長く飛ぶほどダイス以上に強い。" + cJp + "重い・遠い対象ほど抵抗する。",
                    "将视野内的任何物体或生物(包括自己)朝你选择的方向抛出。等级与念动术共享,并获得念动术经验。" + fCn + "碰撞伤害为骰值×(50%+每飞行一格25%),飞得越远伤害远超骰值。" + cCn + "沉重或远处的目标更难抛动。");

                // Psi-Strike: cost.
                int sc = pc != null ? PsionCombat.StrikeCost(pc) : 12;
                SetText(E.map[PsiStrike], "detail",
                    "Carry your ordinary melee attack to a target up to 8 tiles away with your mind, like a blade thrown and called back. It uses your equipped weapons (or martial arts) exactly as a normal attack does: same damage, same skill experience. Psicraft and this ability's level add no power to it. Costs a fixed " + sc + " mana.",
                    "精神の力で通常の近接攻撃を最大8マス先の対象へ届ける。投げて呼び戻す刃のように。装備中の武器(または格闘)を通常攻撃と同じく使い、ダメージもスキル経験も同じ。サイクラフトやこの能力のレベルで威力は上がらない。消費は固定" + sc + "マナ。",
                    "以精神之力将普通近战攻击送达至多8格外的目标,如掷出又召回的刀刃。与普通攻击一样使用已装备的武器(或格斗):伤害与技能经验相同。灵能术与该能力等级不增加威力。固定消耗" + sc + "点魔力。");
            }
            catch (Exception e) { Plugin.Report(e); }
        }
    }
}
