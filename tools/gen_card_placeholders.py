# -*- coding: utf-8 -*-
"""为缺少卡图的卡牌生成占位图片（250x190 白底 + 中文名/占位文字）。"""
import json
import os

from PIL import Image, ImageDraw, ImageFont

CARDS_DIR = r"D:\plumamod2\pluma\pluma\images\cards"
JSON_PATH = r"D:\plumamod2\pluma\pluma\localization\zhs\cards.json"
W, H = 250, 190


def find_font():
    candidates = [
        r"C:/Windows/Fonts/msyh.ttc",    # 微软雅黑
        r"C:/Windows/Fonts/simhei.ttf",  # 黑体
        r"C:/Windows/Fonts/msyh.ttf",
        r"C:/Windows/Fonts/simsun.ttc",  # 宋体
        r"C:/Windows/Fonts/Deng.ttf",    # 等线
        r"C:/Windows/Fonts/simkai.ttf",  # 楷体
    ]
    for c in candidates:
        if os.path.exists(c):
            return c
    # 兜底：在字体目录里找任意一个 ttf/otf/ttc
    font_dir = r"C:/Windows/Fonts"
    for name in os.listdir(font_dir):
        if name.lower().endswith((".ttc", ".ttf", ".otf")):
            return os.path.join(font_dir, name)
    return None


def class_name_from_key(key):
    # "PLUMA_CARD_GIN_TONIC.title" -> "GinTonic"
    mid = key[len("PLUMA_CARD_"):-len(".title")]
    return "".join(part.capitalize() for part in mid.split("_"))


def main():
    font_path = find_font()
    if font_path is None:
        print("ERROR: no chinese font found")
        return 1
    print("font:", font_path)

    with open(JSON_PATH, "r", encoding="utf-8-sig") as f:
        data = json.load(f)

    entries = []
    for key, value in data.items():
        if key.startswith("PLUMA_CARD_") and key.endswith(".title"):
            entries.append((class_name_from_key(key), value))

    if not os.path.isdir(CARDS_DIR):
        os.makedirs(CARDS_DIR, exist_ok=True)

    existing = {name for name in os.listdir(CARDS_DIR) if name.lower().endswith(".png")}

    font_big = ImageFont.truetype(font_path, 28)
    font_small = ImageFont.truetype(font_path, 18)

    anchor_x = W * 2 // 3   # 水平约 2/3 处
    anchor_y = H // 3       # 垂直约 1/3 处
    line1_y = anchor_y - 20
    line2_y = anchor_y + 18

    missing, generated, skipped, failed = 0, 0, 0, 0
    for class_name, cn_name in entries:
        png_name = class_name + ".png"
        png_path = os.path.join(CARDS_DIR, png_name)
        if png_name in existing:
            skipped += 1
            continue
        missing += 1
        try:
            img = Image.new("RGB", (W, H), "white")
            draw = ImageDraw.Draw(img)
            draw.text((anchor_x, line1_y), cn_name, fill="black", font=font_big, anchor="mm")
            draw.text((anchor_x, line2_y), "（占位图片）", fill="black", font=font_small, anchor="mm")
            img.save(png_path)
            generated += 1
        except Exception as e:
            failed += 1
            print("FAILED:", png_name, repr(e))

    print("title_keys:", len(entries))
    print("missing:", missing)
    print("generated:", generated)
    print("skipped_existing:", skipped)
    print("failed:", failed)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
