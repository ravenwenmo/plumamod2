using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;             // 提供 CardModel (如果上述 Entities.Cards 不行，试试这个)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 鸡尾酒：2费稀有技能，本能，消耗。获得3个随机增益。
[RegisterCard(typeof(PlumaCardPool))]
public class Cocktail : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        //MyKeywords.MuscleMemory,
        CardKeyword.Exhaust
    };
// 悬浮提示：列出所有可能获得的增益（自定义 + 原版常见正面能力）
protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
{
    // 自定义能力
    HoverTipFactory.FromPower<FlowState>(),
    HoverTipFactory.FromPower<ExcellentMobilityPower>(),
    HoverTipFactory.FromPower<LiberiPower>(),
    HoverTipFactory.FromPower<TheBeatPower>(),
    HoverTipFactory.FromPower<ConstantFlowPower>(),
    HoverTipFactory.FromPower<TransparentWorldPower>(),
    HoverTipFactory.FromPower<LikeABirdPower>(),
    HoverTipFactory.FromPower<FlawSensePower>(),
    HoverTipFactory.FromPower<WindFeatherPower>(),
    HoverTipFactory.FromPower<SlashingDrawPower>(),
    // 原版常见正面能力
    /*
    HoverTipFactory.FromPower<StrengthPower>(),             // 力量
    HoverTipFactory.FromPower<DexterityPower>(),            // 敏捷
    HoverTipFactory.FromPower<VigorPower>(),                // 活力
    HoverTipFactory.FromPower<FocusPower>(),                // 集中
    HoverTipFactory.FromPower<IntangiblePower>(),           // 无实体
    HoverTipFactory.FromPower<BufferPower>(),               // 缓冲
    HoverTipFactory.FromPower<BurstPower>(),                // 爆发
    HoverTipFactory.FromPower<DuplicationPower>(),          // 复制
    HoverTipFactory.FromPower<DoubleDamagePower>(),         // 双倍伤害
    HoverTipFactory.FromPower<RitualPower>(),               // 仪式
    HoverTipFactory.FromPower<DemonFormPower>(),            // 恶魔形态
    HoverTipFactory.FromPower<EchoFormPower>(),             // 回响形态
    HoverTipFactory.FromPower<CreativeAiPower>(),           // 创造性AI
    HoverTipFactory.FromPower<MachineLearningPower>(),      // 机器学习
    HoverTipFactory.FromPower<HelloWorldPower>(),           // 你好世界
    HoverTipFactory.FromPower<EnvenomPower>(),              // 涂毒
    HoverTipFactory.FromPower<JuggernautPower>(),           // 势不可当
    HoverTipFactory.FromPower<FlameBarrierPower>(),         // 火焰屏障
    HoverTipFactory.FromPower<FeelNoPainPower>(),           // 无惧疼痛
    HoverTipFactory.FromPower<DarkEmbracePower>(),          // 黑暗之拥
    HoverTipFactory.FromPower<CorruptionPower>(),           // 腐化
    HoverTipFactory.FromPower<BarricadePower>(),            // 壁垒
    HoverTipFactory.FromPower<BlurPower>(),                 // 残影
    HoverTipFactory.FromPower<PlatingPower>(),              // 覆甲
    HoverTipFactory.FromPower<RegenPower>(),                // 再生
    HoverTipFactory.FromPower<WellLaidPlansPower>(),        // 计划妥当
    HoverTipFactory.FromPower<ToolsOfTheTradePower>(),      // 必备工具
    HoverTipFactory.FromPower<AfterimagePower>(),           // 余像
    HoverTipFactory.FromPower<InfiniteBladesPower>(),       // 无尽刀刃
    HoverTipFactory.FromPower<NoxiousFumesPower>(),         // 毒雾
    HoverTipFactory.FromPower<ThornsPower>(),               // 荆棘
    HoverTipFactory.FromPower<SelfFormingClayPower>(),     // 自成型黏土
    //HoverTipFactory.FromPower<LoopPower>(),                 // 循环
    HoverTipFactory.FromPower<StormPower>(),                // 雷暴
    */
};


    public Cocktail() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 手动维护的增益施加器列表，每个施加 1 层
    private static readonly List<Action<PlayerChoiceContext, Creature, Creature, CardModel>> BuffAppliers = new()
    {
        // 渐入佳境：叠层抽取攻击牌或本能牌，提升攻击力
        (ctx, owner, _, card) => PowerCmd.Apply<FlowState>(ctx, owner, 1, owner, card),
        // 优良机动：获得渐入佳境时额外获得格挡
        (ctx, owner, _, card) => PowerCmd.Apply<ExcellentMobilityPower>(ctx, owner, 1, owner, card),
        // 黎博利：造成伤害时附加创伤
        (ctx, owner, _, card) => PowerCmd.Apply<LiberiPower>(ctx, owner, 1, owner, card),
        // 节奏感：打出本能牌时获得能量
        (ctx, owner, _, card) => PowerCmd.Apply<TheBeatPower>(ctx, owner, 1, owner, card),
        // 源源不断：每回合开始时获得渐入佳境
        (ctx, owner, _, card) => PowerCmd.Apply<ConstantFlowPower>(ctx, owner, 1, owner, card),
        // 通透世界：渐入佳境抽到的本能牌自动打出
        (ctx, owner, _, card) => PowerCmd.Apply<TransparentWorldPower>(ctx, owner, 1, owner, card),
        // 如鸟一般：受到攻击时获得渐入佳境
        (ctx, owner, _, card) => PowerCmd.Apply<LikeABirdPower>(ctx, owner, 1, owner, card),
        // 破绽感知：拥有创伤的敌人造成的伤害减半
        (ctx, owner, _, card) => PowerCmd.Apply<FlawSensePower>(ctx, owner, 1, owner, card),
        // 风中之羽：打出攻击牌时获得渐入佳境，但渐入佳境无法抽牌
        (ctx, owner, _, card) => PowerCmd.Apply<WindFeatherPower>(ctx, owner, 1, owner, card),
        // 切割连携：打出切割时抽牌
        (ctx, owner, _, card) => PowerCmd.Apply<SlashingDrawPower>(ctx, owner, 1, owner, card),
        // 精准复击
        (ctx, owner, _, card) => PowerCmd.Apply<PrecisionRepetitionPower>(ctx, owner, 1, owner, card),
        // 利刃形态
        (ctx, owner, _, card) => PowerCmd.Apply<BladeFormPower>(ctx, owner, 1, owner, card),
        // 利刃形态+
        (ctx, owner, _, card) => PowerCmd.Apply<BladeFormUpgradedPower>(ctx, owner, 1, owner, card),

        
        // ---------- 原版常见正面能力 ----------
        (ctx, owner, _, card) => PowerCmd.Apply<StrengthPower>(ctx, owner, 1, owner, card),
        // 力量
        (ctx, owner, _, card) => PowerCmd.Apply<DexterityPower>(ctx, owner, 1, owner, card), 
        // 敏捷
        (ctx, owner, _, card) => PowerCmd.Apply<VigorPower>(ctx, owner, 1, owner, card),    
        // 活力
        (ctx, owner, _, card) => PowerCmd.Apply<FocusPower>(ctx, owner, 1, owner, card),    
        // 集中
        (ctx, owner, _, card) => PowerCmd.Apply<IntangiblePower>(ctx, owner, 1, owner, card), 
        // 无实体
        (ctx, owner, _, card) => PowerCmd.Apply<BufferPower>(ctx, owner, 1, owner, card),        
        // 缓冲
        (ctx, owner, _, card) => PowerCmd.Apply<BurstPower>(ctx, owner, 1, owner, card),         
        // 爆发
        (ctx, owner, _, card) => PowerCmd.Apply<DuplicationPower>(ctx, owner, 1, owner, card),   
        // 复制
        (ctx, owner, _, card) => PowerCmd.Apply<DoubleDamagePower>(ctx, owner, 1, owner, card),    
        // 双倍伤害
        (ctx, owner, _, card) => PowerCmd.Apply<RitualPower>(ctx, owner, 1, owner, card),         
        // 仪式
        (ctx, owner, _, card) => PowerCmd.Apply<DemonFormPower>(ctx, owner, 1, owner, card),       
        // 恶魔形态
        (ctx, owner, _, card) => PowerCmd.Apply<EchoFormPower>(ctx, owner, 1, owner, card),       
        // 回响形态
        (ctx, owner, _, card) => PowerCmd.Apply<CreativeAiPower>(ctx, owner, 1, owner, card),     
        // 创造性AI
         (ctx, owner, _, card) => PowerCmd.Apply<MachineLearningPower>(ctx, owner, 1, owner, card),  
        // 机器学习
        (ctx, owner, _, card) => PowerCmd.Apply<HelloWorldPower>(ctx, owner, 1, owner, card),   
        // 你好世界
        (ctx, owner, _, card) => PowerCmd.Apply<EnvenomPower>(ctx, owner, 1, owner, card),      
        // 涂毒
        (ctx, owner, _, card) => PowerCmd.Apply<JuggernautPower>(ctx, owner, 1, owner, card),     
        // 势不可当
        (ctx, owner, _, card) => PowerCmd.Apply<FlameBarrierPower>(ctx, owner, 1, owner, card),   
        // 火焰屏障
        (ctx, owner, _, card) => PowerCmd.Apply<FeelNoPainPower>(ctx, owner, 1, owner, card),    
        // 无惧疼痛
        (ctx, owner, _, card) => PowerCmd.Apply<DarkEmbracePower>(ctx, owner, 1, owner, card),   
        // 黑暗之拥
        (ctx, owner, _, card) => PowerCmd.Apply<CorruptionPower>(ctx, owner, 1, owner, card),   
        // 腐化
        (ctx, owner, _, card) => PowerCmd.Apply<BarricadePower>(ctx, owner, 1, owner, card),   
        // 壁垒
        (ctx, owner, _, card) => PowerCmd.Apply<BlurPower>(ctx, owner, 1, owner, card),      
        // 残影
        (ctx, owner, _, card) => PowerCmd.Apply<PlatingPower>(ctx, owner, 1, owner, card),     
        // 覆甲
        (ctx, owner, _, card) => PowerCmd.Apply<RegenPower>(ctx, owner, 1, owner, card),      
        // 再生
        (ctx, owner, _, card) => PowerCmd.Apply<WellLaidPlansPower>(ctx, owner, 1, owner, card),  
        // 计划妥当
        (ctx, owner, _, card) => PowerCmd.Apply<ToolsOfTheTradePower>(ctx, owner, 1, owner, card),    
        // 必备工具
        (ctx, owner, _, card) => PowerCmd.Apply<AfterimagePower>(ctx, owner, 1, owner, card),        
        // 余像
        (ctx, owner, _, card) => PowerCmd.Apply<InfiniteBladesPower>(ctx, owner, 1, owner, card),  
        // 无尽刀刃
        (ctx, owner, _, card) => PowerCmd.Apply<NoxiousFumesPower>(ctx, owner, 1, owner, card),   
        // 毒雾
        (ctx, owner, _, card) => PowerCmd.Apply<ThornsPower>(ctx, owner, 1, owner, card),       
        // 荆棘
        (ctx, owner, _, card) => PowerCmd.Apply<SelfFormingClayPower>(ctx, owner, 1, owner, card),   
        // 自成型黏土
        //(ctx, owner, _, card) => PowerCmd.Apply<LoopPower>(ctx, owner, 1, owner, card),   
        // 循环
        (ctx, owner, _, card) => PowerCmd.Apply<StormPower>(ctx, owner, 1, owner, card),   
        // 雷暴
            // ---------- 第二次扩充的正面能力 ----------
        (ctx, owner, _, card) => PowerCmd.Apply<RupturePower>(ctx, owner, 1, owner, card),   
        // 撕裂：自身回合失去生命时获得力量
        (ctx, owner, _, card) => PowerCmd.Apply<RagePower>(ctx, owner, 1, owner, card),        
        // 狂怒：打出攻击牌时获得格挡
        (ctx, owner, _, card) => PowerCmd.Apply<ReboundPower>(ctx, owner, 1, owner, card),    
        // 弹回：打出的下一张牌放置到抽牌堆顶
        (ctx, owner, _, card) => PowerCmd.Apply<ArtifactPower>(ctx, owner, 1, owner, card),    
        // 人工制品：免疫负面效果
        (ctx, owner, _, card) => PowerCmd.Apply<AccuracyPower>(ctx, owner, 1, owner, card),   
        // 精准：小刀造成额外伤害
        (ctx, owner, _, card) => PowerCmd.Apply<ArsenalPower>(ctx, owner, 1, owner, card),    
        // 武器库：生成牌时获得力量
        (ctx, owner, _, card) => PowerCmd.Apply<CalamityPower>(ctx, owner, 1, owner, card),    
        // 劫难：打出攻击牌时添加随机攻击牌
        (ctx, owner, _, card) => PowerCmd.Apply<CuriousPower>(ctx, owner, 1, owner, card),     
        // 好奇：能力牌耗能减少
        (ctx, owner, _, card) => PowerCmd.Apply<EnragePower>(ctx, owner, 1, owner, card),   
        // 激怒：打出技能牌时获得力量
        (ctx, owner, _, card) => PowerCmd.Apply<EntropyPower>(ctx, owner, 1, owner, card),      
        // 熵：回合开始时变化手牌
        (ctx, owner, _, card) => PowerCmd.Apply<FanOfKnivesPower>(ctx, owner, 1, owner, card),  
        // 刀扇：小刀命中所有敌人
        (ctx, owner, _, card) => PowerCmd.Apply<FeralPower>(ctx, owner, 1, owner, card),        
        // 野性：首次打出0费攻击牌时放回手牌
        (ctx, owner, _, card) => PowerCmd.Apply<GravityPower>(ctx, owner, 1, owner, card),     
        // 引力：打出牌时对所有敌人造成伤害
        (ctx, owner, _, card) => PowerCmd.Apply<JugglingPower>(ctx, owner, 1, owner, card),    
        // 杂耍：第三张攻击牌复制加入手牌
        (ctx, owner, _, card) => PowerCmd.Apply<LethalityPower>(ctx, owner, 1, owner, card),  
        // 致死性：第一张攻击牌额外伤害
        (ctx, owner, _, card) => PowerCmd.Apply<MayhemPower>(ctx, owner, 1, owner, card),     
        // 乱战：回合开始时打出抽牌堆顶的牌
        (ctx, owner, _, card) => PowerCmd.Apply<NostalgiaPower>(ctx, owner, 1, owner, card),   
        // 怀旧：第一张攻击或技能牌放回抽牌堆顶
        //(ctx, owner, _, card) => PowerCmd.Apply<NightmarePower>(ctx, owner, 1, owner, card),  
        // 夜魇：下回合复制牌加入手牌
        (ctx, owner, _, card) => PowerCmd.Apply<PanachePower>(ctx, owner, 1, owner, card),     
        // 神气制胜：打出5张牌后造成伤害
        (ctx, owner, _, card) => PowerCmd.Apply<PhantomBladesPower>(ctx, owner, 1, owner, card), 
        // 幻影之刃：小刀获得保留，首张小刀伤害增加
        (ctx, owner, _, card) => PowerCmd.Apply<RadiancePower>(ctx, owner, 1, owner, card),   
        // 明耀：额外获得能量
        (ctx, owner, _, card) => PowerCmd.Apply<ReflectPower>(ctx, owner, 1, owner, card),   
        // 倒映：被格挡的伤害反弹
        (ctx, owner, _, card) => PowerCmd.Apply<SerpentFormPower>(ctx, owner, 1, owner, card),   
        // 群蛇形态：每打出牌对随机敌人造成伤害
        (ctx, owner, _, card) => PowerCmd.Apply<ShadowmeldPower>(ctx, owner, 1, owner, card),  
        // 融入暗影：本回合格挡值翻倍
        (ctx, owner, _, card) => PowerCmd.Apply<ShroudPower>(ctx, owner, 1, owner, card),     
        // 厄运之衣：给予灾厄时获得格挡
        (ctx, owner, _, card) => PowerCmd.Apply<SpectrumShiftPower>(ctx, owner, 1, owner, card),   
        // 光谱偏移：回合开始时添加无色牌
        (ctx, owner, _, card) => PowerCmd.Apply<SpeedsterPower>(ctx, owner, 1, owner, card),       
        // 速行者：抽牌时对所有人造成伤害
        (ctx, owner, _, card) => PowerCmd.Apply<SpiritOfAshPower>(ctx, owner, 1, owner, card),  
        // 灰烬之灵：打出虚无牌时获得格挡
        (ctx, owner, _, card) => PowerCmd.Apply<StampedePower>(ctx, owner, 1, owner, card),    
        // 惊逃：回合结束时随机打出攻击牌
        (ctx, owner, _, card) => PowerCmd.Apply<StratagemPower>(ctx, owner, 1, owner, card),   
        // 计策：洗牌时选择一张牌入手
        (ctx, owner, _, card) => PowerCmd.Apply<UnmovablePower>(ctx, owner, 1, owner, card), 
        // 不动：首次获得格挡时数值翻倍
        (ctx, owner, _, card) => PowerCmd.Apply<VeilpiercerPower>(ctx, owner, 1, owner, card),   
        // 刺破帷幕：下张虚无牌耗能为0
        (ctx, owner, _, card) => PowerCmd.Apply<ViciousPower>(ctx, owner, 1, owner, card),   
        // 凶恶：给予易伤时抽牌
        (ctx, owner, _, card) => PowerCmd.Apply<VoidFormPower>(ctx, owner, 1, owner, card),   
        // 虚空形态：每回合前2张牌免费打出
        //(ctx, owner, _, card) => PowerCmd.Apply<WitheringPresencePower>(ctx, owner, 1, owner, card), 
        // 凋萎存在：打出6张牌后添加凋萎
        (ctx, owner, _, card) => PowerCmd.Apply<SummonNextTurnPower>(ctx, owner, 1, owner, card),   
        // 下回合召唤2

        //3
        
        (ctx, owner, _, card) => PowerCmd.Apply<FriendshipPower>(ctx, owner, 1, owner, card),  
        // 友谊：每回合开始时获得特殊资源
        (ctx, owner, _, card) => PowerCmd.Apply<GenesisPower>(ctx, owner, 1, owner, card),   
        // 创世纪：回合开始时获得能量
        (ctx, owner, _, card) => PowerCmd.Apply<FurnacePower>(ctx, owner, 1, owner, card),       
        // 熔炉：回合开始时铸造
        (ctx, owner, _, card) => PowerCmd.Apply<LeadershipPower>(ctx, owner, 1, owner, card),      
        // 领袖气质：盟友造成额外伤害
        (ctx, owner, _, card) => PowerCmd.Apply<MasterPlannerPower>(ctx, owner, 1, owner, card),  
        // 谋划专家：打出技能牌时获得奇巧
        (ctx, owner, _, card) => PowerCmd.Apply<MonologuePower>(ctx, owner, 1, owner, card),     
        // 独白：打出卡牌时获得力量
        (ctx, owner, _, card) => PowerCmd.Apply<ReaperFormPower>(ctx, owner, 1, owner, card),    
        // 死神形态：攻击伤害时给予灾厄
        (ctx, owner, _, card) => PowerCmd.Apply<RoyaltiesPower>(ctx, owner, 1, owner, card),     
        // 王国资产：战斗结束时获得金币
        (ctx, owner, _, card) => PowerCmd.Apply<SignalBoostPower>(ctx, owner, 1, owner, card),    
        // 信号增强：下一张能力牌多打出一次
        (ctx, owner, _, card) => PowerCmd.Apply<SubroutinePower>(ctx, owner, 1, owner, card),    
        // 子程序：打出能力牌时获得能量
        (ctx, owner, _, card) => PowerCmd.Apply<ThunderPower>(ctx, owner, 1, owner, card),        
        // 雷霆：激发闪电充能球时额外伤害
        (ctx, owner, _, card) => PowerCmd.Apply<TyrannyPower>(ctx, owner, 1, owner, card),      
        // 暴政：回合开始时抽牌并消耗手牌
        (ctx, owner, _, card) => PowerCmd.Apply<AnticipatePower>(ctx, owner, 1, owner, card),    
        // 预判：本回合结束前获得敏捷
        (ctx, owner, _, card) => PowerCmd.Apply<AutomationPower>(ctx, owner, 1, owner, card),    
        // 自动化：每抽10张牌获得能量
        (ctx, owner, _, card) => PowerCmd.Apply<BlockNextTurnPower>(ctx, owner, 1, owner, card),    
        // 下回合格挡：下回合开始时获得格挡
        (ctx, owner, _, card) => PowerCmd.Apply<ChildOfTheStarsPower>(ctx, owner, 1, owner, card),  
        // 群星之子：花费能量时获得格挡
        (ctx, owner, _, card) => PowerCmd.Apply<ClarityPower>(ctx, owner, 1, owner, card),         
        // 明晰：下回合额外抽牌
        (ctx, owner, _, card) => PowerCmd.Apply<CorrosiveWavePower>(ctx, owner, 1, owner, card),    
        // 腐蚀波：抽牌时给予所有敌人中毒
        (ctx, owner, _, card) => PowerCmd.Apply<CountdownPower>(ctx, owner, 1, owner, card),  
        // 倒数计时：回合开始时给予随机敌人灾厄
        (ctx, owner, _, card) => PowerCmd.Apply<DanseMacabrePower>(ctx, owner, 1, owner, card),  
        // 死亡之舞：打出高费牌时获得格挡
        (ctx, owner, _, card) => PowerCmd.Apply<DemesnePower>(ctx, owner, 1, owner, card),     
        // 领域：回合开始时获得能量并抽牌
        (ctx, owner, _, card) => PowerCmd.Apply<EnergyNextTurnPower>(ctx, owner, 1, owner, card),   
        // 下回合能量：下回合额外获得能量
        (ctx, owner, _, card) => PowerCmd.Apply<FastenPower>(ctx, owner, 1, owner, card),    
        // 勒紧：防御牌额外获得格挡
        (ctx, owner, _, card) => PowerCmd.Apply<FeedingFrenzyPower>(ctx, owner, 1, owner, card),
        // 疯狂进食：本回合结束前获得力量
        (ctx, owner, _, card) => PowerCmd.Apply<ForegoneConclusionPower>(ctx, owner, 1, owner, card), 
        // 既定事项：下回合将抽牌堆的牌放入手牌
        (ctx, owner, _, card) => PowerCmd.Apply<FreeAttackPower>(ctx, owner, 1, owner, card),  
        // 免费攻击：下一张攻击牌耗能为0
        (ctx, owner, _, card) => PowerCmd.Apply<FreePowerPower>(ctx, owner, 1, owner, card),  
        // 免费能力：下一张能力牌耗能为0
        (ctx, owner, _, card) => PowerCmd.Apply<FreeSkillPower>(ctx, owner, 1, owner, card),   
        // 免费技能：下一张技能牌耗能为0
        (ctx, owner, _, card) => PowerCmd.Apply<GigantificationPower>(ctx, owner, 1, owner, card), 
        // 超巨化：下一张攻击牌三倍伤害
        (ctx, owner, _, card) => PowerCmd.Apply<HammerTimePower>(ctx, owner, 1, owner, card), 
        // 锤子时间：铸造时盟友也铸造
        (ctx, owner, _, card) => PowerCmd.Apply<InfernoPower>(ctx, owner, 1, owner, card),  
        // 狱火：回合内失去生命时对所有敌人造成伤害
        (ctx, owner, _, card) => PowerCmd.Apply<IterationPower>(ctx, owner, 1, owner, card),    
        // 迭代：抽到状态牌时抽更多牌
        (ctx, owner, _, card) => PowerCmd.Apply<OrbitPower>(ctx, owner, 1, owner, card),      
        // 环绕轨道：花费能量时获得额外能量
        (ctx, owner, _, card) => PowerCmd.Apply<PagestormPower>(ctx, owner, 1, owner, card),  
        // 书页风暴：抽到虚无牌时抽牌
        (ctx, owner, _, card) => PowerCmd.Apply<PaleBlueDotPower>(ctx, owner, 1, owner, card), 
        // 暗淡蓝点：打出5张牌后下回合额外抽牌
        (ctx, owner, _, card) => PowerCmd.Apply<PillarOfCreationPower>(ctx, owner, 1, owner, card), 
        // 创世之柱：创造牌时获得格挡
        (ctx, owner, _, card) => PowerCmd.Apply<PrepTimePower>(ctx, owner, 1, owner, card),  
        // 准备时间：回合开始时获得活力
        (ctx, owner, _, card) => PowerCmd.Apply<RetainHandPower>(ctx, owner, 1, owner, card),   
        // 保留手牌：数回合内保留手牌
        (ctx, owner, _, card) => PowerCmd.Apply<SneakyPower>(ctx, owner, 1, owner, card),   
        // 鬼祟：其他玩家攻击敌人时获得格挡
        (ctx, owner, _, card) => PowerCmd.Apply<SpinnerPower>(ctx, owner, 1, owner, card),   
        // 旋转工艺：回合开始时生成玻璃充能球
        (ctx, owner, _, card) => PowerCmd.Apply<TrackingPower>(ctx, owner, 1, owner, card),  
        // 跟踪：虚弱敌人受到攻击牌双倍伤害
        (ctx, owner, _, card) => PowerCmd.Apply<TrashToTreasurePower>(ctx, owner, 1, owner, card), 
        // 化废为宝：生成状态牌时随机充能球

    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var owner = base.Owner.Creature;
        var rng = new Random();

        // 随机选择 3 个不同的增益并施加
        var selected = BuffAppliers.OrderBy(_ => rng.Next()).Take(3).ToList();
        foreach (var apply in selected)
        {
            apply(choiceContext, owner, owner, this);
            // 每施加一个增益后等待 0.5 秒，使动画依次播放
            await Cmd.Wait(0.5f);
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}