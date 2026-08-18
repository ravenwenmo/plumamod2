// 与基酒/鸡尾酒体系相关的牌：在悬浮提示（AdditionalHoverTips）或关键词中展示"基酒"或"鸡尾酒"。
// 用于自动召唤检测：卡组中存在实现该接口的牌时，进入战斗自动召唤龙舌兰（Brother）。
// 注意：部分牌（如调酒师的小壶）仅在悬浮提示中展示基酒关键词，自身 Keywords 不含该关键词，
// 因此不能依赖 card.Keywords 判断，统一使用本标记接口。
public interface IBaseSpiritRelatedCard { }
