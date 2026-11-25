# -*- coding: utf-8 -*-
"""从参考图提取关键色（中位切分量化 + 饱和度加权）。"""
import os
import sys

from PIL import Image

SRC = r"C:\Users\Dac'ta\.dsh\attachments\v1\objects\d8\d815bb82ab20f6a003ac76406e74f418f30fe99a2d5531b61b413378909b5009"

img = Image.open(SRC).convert("RGB")
w, h = img.size
print("size:", img.size)

small = img.resize((160, int(160 * h / w)))

# 量化到 12 色
q = small.quantize(colors=12, method=Image.MEDIANCUT)
pal = q.getpalette()
counts = sorted(q.getcolors(), reverse=True)

print("\n量化调色板（按面积占比）:")
total = sum(c for c, _ in counts)
for cnt, idx in counts:
    r, g, b = pal[idx * 3: idx * 3 + 3]
    print(f"  #{r:02X}{g:02X}{b:02X}  {cnt / total * 100:5.1f}%   rgb({r},{g},{b})")

# 额外：找出最亮的青/蓝色像素（高光）与最暗的背景色
px = list(small.getdata())
teal = [p for p in px if p[1] > p[0] + 20 and p[2] > p[0] + 20]
if teal:
    teal.sort(key=lambda p: -(p[1] + p[2]))
    print("\n最亮的青色调样本:")
    for p in teal[:3]:
        print(f"  #{p[0]:02X}{p[1]:02X}{p[2]:02X}")
    avg = tuple(sum(c[i] for c in teal) // len(teal) for i in range(3))
    print(f"  青色均值 #{avg[0]:02X}{avg[1]:02X}{avg[2]:02X}  ({len(teal)} px)")

dark = sorted(px, key=lambda p: sum(p))[:max(1, len(px) // 20)]
avgd = tuple(sum(c[i] for c in dark) // len(dark) for i in range(3))
print(f"\n最暗 5% 均值 #{avgd[0]:02X}{avgd[1]:02X}{avgd[2]:02X}")

bright = sorted(px, key=lambda p: -sum(p))[:max(1, len(px) // 40)]
avgb = tuple(sum(c[i] for c in bright) // len(bright) for i in range(3))
print(f"最亮 2.5% 均值 #{avgb[0]:02X}{avgb[1]:02X}{avgb[2]:02X}")
