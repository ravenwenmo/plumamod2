using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Pluma.Scripts;

// 由意图（AbstractIntent 子类）实现：在意图数值（如 "12×1"）后面追加一段同款字体的文本。
// 目前用于龙舌兰攻击意图显示"剩余N回合"。
public interface IIntentExtraValueText
{
    // 返回 null 或空串表示不显示。
    // 返回值按 BBCode 解析（和游戏意图数值标签一样），可以自带 [font_size=..] 之类的标签做局部排版。
    string? GetIntentExtraValueText();
}

// Harmony 补丁：把额外文本做成 NIntent 里 %Value 标签的"兄弟节点"，挂在同一个父节点 %IntentHolder 下。
//
// 为什么不用 RitsuLib 的 ExtraCornerAmountLabel 角标：
//   角标挂在 NIntent 根节点上，而意图图标与数值标签在 NIntent._Process 里是靠
//   _intentHolder.Position 上下摆动（sin 曲线）实现浮动的，挂在根节点上的角标不会跟着动。
//   这里直接挂到 %IntentHolder 下，就能和 "12×1" 完全同步地摆动。
//
// 字体/字号/描边/颜色全部从 %Value 标签现拷，保证和"12×1"一模一样；
// 位置按 %Value 的实际文字宽度右移，紧跟在数值后面。
[HarmonyPatch(typeof(NIntent), "UpdateVisuals")]
public static class IntentExtraValueTextPatch
{
    // NIntent 的私有成员
    private static readonly FieldInfo? IntentField = AccessTools.Field(typeof(NIntent), "_intent");
    private static readonly FieldInfo? HolderField = AccessTools.Field(typeof(NIntent), "_intentHolder");
    private static readonly FieldInfo? ValueLabelField = AccessTools.Field(typeof(NIntent), "_valueLabel");

    // 额外文本节点名（挂在 %IntentHolder 下）
    private const string LabelName = "PlumaIntentExtraValue";

    // 与 %Value 文字之间的间隔（像素）
    private const float Gap = 6f;

    [HarmonyPostfix]
    public static void Postfix(NIntent __instance)
    {
        if (IntentField == null || HolderField == null || ValueLabelField == null)
        {
            return;
        }

        if (HolderField.GetValue(__instance) is not Control holder ||
            ValueLabelField.GetValue(__instance) is not MegaRichTextLabel valueLabel)
        {
            return; // 节点还没 _Ready
        }

        string? text = (IntentField.GetValue(__instance) as IIntentExtraValueText)?.GetIntentExtraValueText();

        MegaRichTextLabel? label = holder.GetNodeOrNull<MegaRichTextLabel>(LabelName);

        if (string.IsNullOrEmpty(text))
        {
            if (label != null)
            {
                label.Visible = false;
            }
            return;
        }

        if (label == null || !GodotObject.IsInstanceValid(label))
        {
            label = CreateLabel(valueLabel);
            if (label == null)
            {
                // 拿不到 %Value 的字体就不建节点：MegaRichTextLabel._Ready 里有"必须有字体 override"的断言，
                // 没字体就 add_child 会抛异常，反而把意图节点搞坏。
                return;
            }
            holder.AddChild(label);
        }

        label.Visible = true;
        label.Text = text;
        label.Position = GetAttachedPosition(valueLabel, label);
    }

    // 建一个和 %Value 排版完全一致的标签；拿不到字体时返回 null
    private static MegaRichTextLabel? CreateLabel(MegaRichTextLabel valueLabel)
    {
        // 字体必须是 override（MegaRichTextLabel._Ready 里有断言），
        // 直接沿用 %Value 的字体，字形与 "12×1" 相同
        Font? font = valueLabel.GetThemeFont(ThemeConstants.RichTextLabel.NormalFont, "RichTextLabel");
        if (font == null)
        {
            return null;
        }

        MegaRichTextLabel label = new()
        {
            Name = LabelName,
            // 与 scenes/combat/intent.tscn 里的 %Value 保持一致
            BbcodeEnabled = true,
            AutoSizeEnabled = false,   // 关掉自动缩放，避免它把字号改小
            FitContent = false,
            ScrollActive = false,
            ClipContents = false,
            AutowrapMode = TextServer.AutowrapMode.Off,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        label.AddThemeFontOverride(ThemeConstants.RichTextLabel.NormalFont, font);

        label.AddThemeFontSizeOverride(
            ThemeConstants.RichTextLabel.NormalFontSize,
            valueLabel.GetThemeFontSize(ThemeConstants.RichTextLabel.NormalFontSize, "RichTextLabel")
        );

        // 颜色与描边同样照抄，避免和数值标签有色差
        label.AddThemeColorOverride(
            "default_color",
            valueLabel.GetThemeColor("default_color", "RichTextLabel")
        );
        label.AddThemeColorOverride(
            "font_outline_color",
            valueLabel.GetThemeColor("font_outline_color", "RichTextLabel")
        );
        label.AddThemeConstantOverride(
            "outline_size",
            valueLabel.GetThemeConstant("outline_size", "RichTextLabel")
        );

        return label;
    }

    // 紧跟在 %Value 实际文字宽度之后，纵向与 %Value 对齐（同一个 rect 顶边 + 同字号 = 同基线）
    private static Vector2 GetAttachedPosition(MegaRichTextLabel valueLabel, MegaRichTextLabel label)
    {
        float offsetX = 0f;

        Font? font = valueLabel.GetThemeFont(ThemeConstants.RichTextLabel.NormalFont, "RichTextLabel");
        string parsed = valueLabel.GetParsedText();   // 去掉 BBCode 后的纯文字（"12×1"）
        if (font != null && !string.IsNullOrEmpty(parsed))
        {
            int fontSize = valueLabel.GetThemeFontSize(
                ThemeConstants.RichTextLabel.NormalFontSize,
                "RichTextLabel"
            );
            offsetX = font.GetStringSize(parsed, HorizontalAlignment.Left, -1f, fontSize).X;
        }

        label.Size = new Vector2(220f, valueLabel.Size.Y);
        return new Vector2(valueLabel.Position.X + offsetX + Gap, valueLabel.Position.Y);
    }
}
