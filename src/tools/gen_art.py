# -*- coding: utf-8 -*-
"""生成 ICEY Tuner 的背景图与图标。

配色取自用户提供的参考图（青黑 / 亮青 / 银白），但不使用该图片本身，
全部为程序化绘制的原创图形。
"""
import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

OUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets")
W, H = 1600, 1000

# 从参考图提取的关键色
BG_DARK = (8, 13, 14)        # #080D0E
BG_MID = (18, 38, 42)        # #12262A
TEAL_DEEP = (20, 78, 84)     # #144E54
TEAL = (26, 112, 121)        # #1A7079
CYAN = (47, 230, 238)        # #2FE6EE
CYAN_SOFT = (101, 182, 188)  # #65B6BC
SILVER = (213, 218, 224)     # #D5DAE0
MAGENTA = (176, 58, 106)     # 参考图左侧那抹品红


def find_font(names, size):
    for n in names:
        p = os.path.join(r"C:\Windows\Fonts", n)
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, size)
            except Exception:
                pass
    return ImageFont.load_default()


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def make_background():
    base = Image.new("RGB", (W, H), BG_DARK)
    d = ImageDraw.Draw(base)

    for y in range(H):
        d.line([(0, y), (W, y)], fill=lerp(BG_DARK, BG_MID, (y / H) ** 0.8))

    # 右侧青色光带
    glow = Image.new("RGB", (W, H), (0, 0, 0))
    gd = ImageDraw.Draw(glow)
    for cx, width, color in ((1150, 320, (18, 110, 124)), (1400, 180, (30, 170, 185)), (900, 70, (12, 70, 80))):
        gd.polygon(
            [(cx, -120), (cx + width, -120), (cx + width - 520, H + 120), (cx - 520, H + 120)],
            fill=color,
        )
    glow = glow.filter(ImageFilter.GaussianBlur(70))
    base = Image.blend(base, glow, 0.55)
    d = ImageDraw.Draw(base)

    # 网格
    grid = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    gd = ImageDraw.Draw(grid)
    for x in range(0, W, 44):
        gd.line([(x, 0), (x, H)], fill=(120, 220, 230, 11))
    for y in range(0, H, 44):
        gd.line([(0, y), (W, y)], fill=(120, 220, 230, 9))
    base = Image.alpha_composite(base.convert("RGBA"), grid).convert("RGB")

    # 巨大 ICEY 字样
    txt = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    td = ImageDraw.Draw(txt)
    f = find_font(["impact.ttf", "arialbd.ttf", "seguibl.ttf"], 450)
    word = "ICEY"
    bbox = td.textbbox((0, 0), word, font=f)
    tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
    x, y = W - tw - 80, H - th - 200
    for off in range(12, 0, -3):
        td.text((x + off, y + off), word, font=f, fill=(14, 120, 132, 9))
    td.text((x, y), word, font=f, fill=(47, 230, 238, 26))
    base = Image.alpha_composite(base.convert("RGBA"), txt).convert("RGB")

    # 装饰斜切条：青色为主，左侧一抹品红呼应参考图
    deco = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    dd = ImageDraw.Draw(deco)
    dd.polygon([(0, 0), (W, 0), (W, 5), (0, 5)], fill=(47, 230, 238, 150))
    dd.polygon([(0, 36), (330, 36), (276, 43), (0, 43)], fill=(47, 230, 238, 80))
    dd.polygon([(0, 862), (300, 862), (238, 870), (0, 870)], fill=(47, 230, 238, 70))
    dd.polygon([(0, 52), (150, 52), (104, 58), (0, 58)], fill=(176, 58, 106, 110))
    base = Image.alpha_composite(base.convert("RGBA"), deco).convert("RGB")

    # 扫描线
    scan = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sd = ImageDraw.Draw(scan)
    for yy in range(0, H, 3):
        sd.line([(0, yy), (W, yy)], fill=(0, 0, 0, 30))
    base = Image.alpha_composite(base.convert("RGBA"), scan).convert("RGB")

    # 暗角
    vig = Image.new("L", (W, H), 0)
    vd = ImageDraw.Draw(vig)
    vd.ellipse([-W * 0.25, -H * 0.35, W * 1.25, H * 1.35], fill=255)
    vig = vig.filter(ImageFilter.GaussianBlur(180))
    base = Image.composite(base, Image.new("RGB", (W, H), (0, 0, 0)), vig)

    return base


def make_icon():
    size = 256
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([8, 8, size - 8, size - 8], radius=52, fill=(10, 20, 23, 255))
    d.rounded_rectangle([8, 8, size - 8, size - 8], radius=52, outline=CYAN + (255,), width=8)
    f = find_font(["impact.ttf", "arialbd.ttf"], 150)
    bbox = d.textbbox((0, 0), "I", font=f)
    d.text(((size - (bbox[2] - bbox[0])) / 2 - bbox[0], (size - (bbox[3] - bbox[1])) / 2 - bbox[1] - 8),
           "I", font=f, fill=CYAN + (255,))
    return img


if __name__ == "__main__":
    os.makedirs(OUT_DIR, exist_ok=True)
    bg_path = os.path.join(OUT_DIR, "bg.png")
    make_background().save(bg_path, optimize=True)
    print("bg:", bg_path, os.path.getsize(bg_path))
    ico_path = os.path.join(OUT_DIR, "app.ico")
    make_icon().save(ico_path, sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)])
    print("ico:", ico_path, os.path.getsize(ico_path))
