# -*- coding: utf-8 -*-
"""从参考图里抠出白色字标，生成透明底 PNG。

做法：亮度阈值 + 饱和度筛选（白/近白的笔画），
输出 alpha 由亮度线性映射，颜色统一为近白，去掉背景人物与青色辉光。
"""
import os

import numpy as np
from PIL import Image, ImageFilter

SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)), "ref_glyph.png")
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets", "glyph.png")

img = Image.open(SRC).convert("RGB")
w, h = img.size
print("source:", img.size)

a = np.asarray(img).astype(np.float32)
r, g, b = a[..., 0], a[..., 1], a[..., 2]
lum = 0.299 * r + 0.587 * g + 0.114 * b
mx = a.max(axis=2)
mn = a.min(axis=2)
sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1), 0)

# 白色笔画：足够亮且饱和度低
mask = (lum > 165) & (sat < 0.25)
ys, xs = np.nonzero(mask)
print("white-ish pixels:", len(xs))
if len(xs) == 0:
    raise SystemExit("没有找到白色笔画")

x0, x1 = xs.min(), xs.max()
y0, y1 = ys.min(), ys.max()
print("bbox:", x0, y0, x1, y1)

pad = 2
x0, y0 = max(0, x0 - pad), max(0, y0 - pad)
x1, y1 = min(w - 1, x1 + pad), min(h - 1, y1 + pad)

crop = a[y0:y1 + 1, x0:x1 + 1]
cl, cs = lum[y0:y1 + 1, x0:x1 + 1], sat[y0:y1 + 1, x0:x1 + 1]

# alpha：亮度 150→0，235→255；并压掉彩色像素（背景人物皮肤/头发）
alpha = np.clip((cl - 150) / 85.0, 0, 1)
alpha *= np.clip((0.30 - cs) / 0.18, 0, 1)
alpha = (alpha * 255).astype(np.uint8)

# 轻微提亮，让笔画接近纯白
rgb = np.clip(cl[..., None].repeat(3, axis=2) * 1.06 + 30, 0, 255).astype(np.uint8)

out = np.dstack([rgb, alpha])
im = Image.fromarray(out, "RGBA")

# 去掉孤立噪点
im = im.filter(ImageFilter.MedianFilter(3))
im = im.crop(im.getbbox() or (0, 0, im.width, im.height))

os.makedirs(os.path.dirname(OUT), exist_ok=True)
im.save(OUT)
print("saved:", OUT, im.size, os.path.getsize(OUT), "bytes")
