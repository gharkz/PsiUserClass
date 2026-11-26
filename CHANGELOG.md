# Changelog

## 0.2.6

PSI User Class 0.2.6
Fixed Psionic Edge sometimes giving new characters negative crafting and lockpicking skills (e.g. -2 crafting, -4 lockpicking) instead of its bonus. A free Telekinesis (mana cost set to 0) now still earns spell and Psicraft experience.

日本語
サイオニック・エッジが新しいキャラクターの製作・解錠スキルにボーナスではなくマイナス(製作-2、解錠-4など)を与えることがある不具合を修正。テレキネシスのマナ消費を0にしていても、魔法とサイクラフトの経験値を得られるようになりました。

简体中文
修复灵能锋刃有时让新角色的制作与开锁技能变为负值(如制作-2、开锁-4)而非加成的问题。念动术魔力消耗设为0时，现在也能获得法术与灵能术经验。


## 0.2.5

PSI User Class 0.2.5
Added System menu > Mod > Bug reports > PSI User Class: a window that copies a bug report (versions, enabled mods, log) to post at https://github.com/gharkz/elin-mods-support/issues

日本語
システムメニュー > Mod > Bug reports > PSI User Class を追加。不具合報告(バージョン、有効なMod、ログ)をコピーして https://github.com/gharkz/elin-mods-support/issues に投稿できます。

简体中文
新增 系统菜单 > Mod > Bug reports > PSI User Class:可复制问题报告(版本、已启用模组、日志)并发布到 https://github.com/gharkz/elin-mods-support/issues 。


## 0.2.4
Psi-Hurl's ability card is now bullet points like Telekinesis's, with its flight distance as one of them; the long paragraph is gone (the damage formula, cost options and resistance are in the Psion primer). Fixed a bullet that split in two at a comma.

## 0.2.3
Fix: "Pull ranged foes into melee" now actually pulls them. AutoCombat used to aim Telekinesis only at its current target (usually the enemy beside you), so ranged foes were rarely pulled, and Psi-Hurl then threw them away instead. Telekinesis now aims at a visible ranged foe (your target if it is one, else the farthest it can reach), and while the option is on Psi-Hurl no longer throws ranged foes that are still at range, except toward you.

Telekinesis and Psi-Hurl casts now name their target, and Telekinesis says which way it goes: "You cast telekinesis on a yeek archer (pull).", "... (hurl).", "You cast psi-hurl on yourself."

## 0.2.2
Auto-combat Telekinesis: pull ranged enemies into melee (default on). Anything with a ranged weapon or that keeps its distance (archers, casters, performers) is pulled in beside you, overriding the Telekinesis settings above it and Psi-Hurl (Psi-Hurl pulls instead only when Telekinesis is set to Don't use). Optional "keep them close": once beside you, neither power hurls them away.

## 0.2.1
Psion settings: a note explaining the powers table's AutoCombat column (unchecked means AutoCombat never uses that power).

## 0.2.0
- Psi-Hurl (Psicraft 5): hurl any object or creature you can see, yourself included, along a line you aim from the target's tile, like a bolt cast from there. It flies 4 tiles, +1 per 3 Telekinesis levels (max 20), or always 20 with a Psion settings option. Its description spells out the distance multiplier on collision damage. It shares Telekinesis's level and XP and has its own icon. Costs 1.5x Telekinesis. Distance, weight and higher-level creatures make it harder.
- AutoCombat aims Psi-Hurl itself: loose objects or hostile creatures are thrown into enemies (yes, enemies into each other), never through allies, neutrals or you.
- Psi-Strike (Psicraft 15): your normal melee attack on a target up to 8 tiles away, like a thrown blade called back. Same weapons or martial arts, same damage and skill experience; Psicraft and ability level add nothing. Fixed 12 MP (adjustable). AutoCombat uses it only on targets out of reach.
- Telekinetic Control feat (2 ranks, 1 point each): self-hurl collision damage -50% / -100% (passing a creature of at least 2x your level still lets 20% through; optional, and level can be swapped for weight, with an adjustable multiplier), and -10% / -20% when someone else hurls you. By default it also protects allies you throw (option). Optionally it also shields you and allies in your projectile's path. These ally protections apply to Telekinesis hurls as well as Psi-Hurl. The path shield covers anything you throw, creatures included.
- Psi-Hurl settings: mana cost as a multiplier of the current or natural Telekinesis cost, or a fixed cost (default 1.5x current). AutoCombat direction when hurling an enemy: random (default), away from you or toward you, optionally preferred over throwing into other enemies. By default AutoCombat leaves enemies beside you to the cheaper Telekinesis, unless Psi-Hurl would hit more enemies with them.
- Lost the primer? Psion settings give you a new copy, once per in-game day.
- Psion settings: an AutoCombat checkbox per power. The Telekinesis sound effect can be turned off entirely, or set per power (Telekinesis, Psi-Hurl, Psi-Strike) to default, muted, or another sound: throw, slash, swat, splash, push, whip or failed cast. Optional: AutoCombat may throw allies with Psi-Hurl (off by default), by default only while Telekinetic Control is rank 2 and protects them (rank 1 selectable).

## 0.1.3
Fix: Necropathy showed the Looter feat's texts. It now has its own description and feat-list line, and its hover card lists what each rank gives, like vanilla feats (current rank and the next).

## 0.1.2 (beta)

Primer: Psionic Edge now spells out what it boosts (harvesting tools' effective hardness, and the level of learned crafts and fine skills when used), with two examples and a note that it does not count toward learning recipes by dismantling.

日本語
入門書: サイオニック・エッジの効果(採取道具の実質的な硬さと、使用時の製作・技術のレベル)を明記し、例を2つと、分解でのレシピ習得には含まれない旨の注意を追加。

简体中文
入门书: 明确说明灵能锋刃的效果(采集工具的有效硬度,以及使用时制作与技巧的等级),并加入两个例子和“不计入通过分解学习配方”的注意事项。

## 0.1.1 (beta)

Kinetic (Impact) powers now count as physical: armor (PV) and physical damage reduction (PDR) apply, elemental damage reduction (EDR) no longer does. The primer explains this and adds a short page on Psi-Shield and Psi-Teleport. Description: the settings let you balance the class your way and rename it.

日本語
念動(衝撃)の能力が物理扱いになりました: 鎧(PV)と物理ダメージ軽減(PDR)が適用され、属性ダメージ軽減(EDR)は効かなくなりました。入門書にその説明と、サイシールド・サイテレポートの短いページを追加。説明文: 設定でクラスのバランス調整や名前の変更ができることを追記。

简体中文
念动(冲击)能力现视为物理: 适用护甲(PV)与物理伤害减免(PDR),不再受元素伤害减免(EDR)影响。入门书加入相关说明,并新增灵能护盾与灵能传送的简短一页。说明文字: 补充了可在设置中按自己喜好平衡职业及改名。

## 0.1.0 (beta)

Psion 0.1.0
First public beta. The Psion class: Psicraft and power unlocks, innate stock-free powers, stronger and cheaper Telekinesis with smart AutoCombat use, undead whiplash and the Necropathy feat, Psionic Edge, Psi-Shield, Psi-Teleport, Telekinesis dummy practice, a live-updating Psion's Primer, and a Psion settings window (per-power on/off, unlock levels, AutoCombat bonuses, Telekinesis cost and collision rules).

日本語
初の公開ベータ版。サイオンクラス: サイクラフトと能力の覚醒、ストック不要の生得能力、自動戦闘で賢く使える強化テレキネシス、アンデッドへの精神反動と特技ネクロパシー、サイオニック・エッジ、サイシールド、サイテレポート、テレキネシス訓練、設定が反映されるサイオン入門、サイオン設定画面(能力のオン/オフ、解放レベル、自動戦闘ボーナス、テレキネシスの消費と衝突ルール)。

简体中文
首个公开测试版。灵能者职业: 灵能术与能力觉醒、无需储备的天生能力、在自动战斗中智能使用的强化念动术、对不死者的精神反噬与专长死灵感应、灵能锋刃、灵能护盾、灵能传送、念动术训练、随设置更新的《灵能者入门》,以及灵能者设置界面(能力开关、解锁等级、自动战斗加成、念动术消耗与碰撞规则)。
