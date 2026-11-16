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
        internal const int NecropathyEnchant = 79403, PsiShield = 79404, PsiTeleport = 79405, PsiEdge = 79406;
        internal static readonly int[] KineticIds = { KineticArrow, KineticBolt, KineticBall, KineticFunnel };
        internal const int Telekinesis = 8215;
        internal const int PsiArrow = 50504, PsiBolt = 50304, PsiBall = 50104, PsiFunnel = 50604; // vanilla *_Mind rows
        internal static readonly int[] OwnElementIds = { Psicraft, FeatPsion, FeatNecropathy, KineticArrow, KineticBolt, KineticBall, KineticFunnel, PsiShield, NecropathyEnchant, PsiTeleport, PsiEdge };

        // Psicraft (base) level -> power. Irregular on purpose; Necropathy gating lives in the feat's req.
        internal static readonly (int lv, int id)[] Unlocks =
        {
            (1, Telekinesis), (5, PsiArrow), (10, PsiBolt), (15, KineticArrow), (20, KineticBolt), (25, PsiShield),
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
            }
            if (!EClass.sources.tactics.map.ContainsKey(JobId)) AddTactics();
            if (!EClass.sources.jobs.map.ContainsKey(JobId)) AddJob();
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
            SetText(r, "textExtra", "Unlocks psionic powers as Psicraft grows,Telekinesis: collision dice sides x2 and mana cost x0.5 (adjustable in Psion settings),Mind damage you deal to the undead lashes back at you",
                "サイクラフトの成長でサイ能力を得る,テレキネシス: 衝突ダイスの面数2倍、マナ消費0.5倍(サイオン設定で変更可),アンデッドに与えた精神ダメージは自分に跳ね返る(精神反動)",
                "随灵能术成长解锁灵能,念动术: 碰撞骰面数x2,魔力消耗x0.5(可在灵能者设置中调整),对不死者造成的精神伤害会反噬自身(精神反噬)");
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
            SetText(r, "textExtra", "Rank 1: no whiplash from the undead,Rank 2: mind attacks affect the undead at half strength,Rank 3: mind attacks affect the undead fully",
                "ランク1: アンデッドからの精神反動を受けない,ランク2: 精神攻撃がアンデッドに半分の効果,ランク3: 精神攻撃がアンデッドに完全な効果",
                "等级1: 不再受到不死者的精神反噬,等级2: 精神攻击对不死者造成一半效果,等级3: 精神攻击对不死者完全有效");
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
    }
}
