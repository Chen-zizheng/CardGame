# -*- coding: utf-8 -*-
"""bake_art.py — 把 Cards/Characters/Enemies 统一成冷紫薄荷风格（duotone + 归一化）。
用法：
  python bake_art.py --dry      # 只往 Temp/preview 输出样张，不改工程
  python bake_art.py            # 原地替换（原图请先 git 备份；可 git checkout 回滚）
"""
import os, sys, collections, argparse
import numpy as np
from PIL import Image, ImageFilter, ImageOps, ImageChops

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SPR = os.path.join(ROOT, 'Assets', 'Sprites')
PREVIEW = os.path.join(ROOT, 'Temp', 'preview')

SHADOW = (18, 16, 28)      # 暗端
LIGHT  = (168, 232, 220)   # 亮端（薄荷）
CARD_SHADOW = (16, 14, 26)
CARD_LIGHT  = (176, 226, 226)

def duotone(im, shadow, light, contrast=1.12, keep=0.0):
    rgba = im.convert('RGBA')
    a = rgba.split()[3]
    L = ImageOps.grayscale(rgba)
    L = L.point(lambda v: min(255, max(0, int((v - 128) * contrast + 128))))
    out = ImageOps.colorize(L, black=shadow, white=light).convert('RGBA')
    if keep > 0:
        out = Image.blend(out, rgba, keep)
    out.putalpha(a)
    return out

def largest_component(alpha, grid=180):
    small = alpha.resize((grid, grid), Image.BILINEAR)
    arr = np.asarray(small) > 40
    seen = np.zeros_like(arr, dtype=bool); best = []
    for y0 in range(grid):
        for x0 in range(grid):
            if not arr[y0, x0] or seen[y0, x0]:
                continue
            q = collections.deque([(y0, x0)]); seen[y0, x0] = True; comp = []
            while q:
                y, x = q.popleft(); comp.append((y, x))
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < grid and 0 <= nx < grid and arr[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True; q.append((ny, nx))
            if len(comp) > len(best):
                best = comp
    m = Image.new('L', (grid, grid), 0); pm = m.load()
    for y, x in best:
        pm[x, y] = 255
    return m.resize(alpha.size, Image.BILINEAR)

def normalise(im, canvas, margin=0.06):
    """按 alpha 质心居中，等比缩放塞进 canvas（留 margin），返回 canvas 尺寸的图。"""
    a = im.split()[3]
    ys, xs = np.nonzero(np.asarray(a) > 24)
    if len(xs) == 0:
        return im.resize(canvas, Image.LANCZOS)
    cx, cy = xs.mean(), ys.mean()
    w, h = im.size
    scale = min(canvas[0] * (1 - 2 * margin) / max(w, 1), canvas[1] * (1 - 2 * margin) / max(h, 1))
    im2 = im.resize((max(1, int(w * scale)), max(1, int(h * scale))), Image.LANCZOS)
    c2 = Image.new('RGBA', canvas, (0, 0, 0, 0))
    c2.alpha_composite(im2, (int(canvas[0] / 2 - cx * scale), int(canvas[1] / 2 - cy * scale)))
    return c2

def card_vignette(im):
    w, h = im.size
    yy, xx = np.mgrid[0:h, 0:w]
    r = np.sqrt(((xx - w / 2) / (w / 2)) ** 2 + ((yy - h / 2) / (h / 2)) ** 2) / 1.41421
    k = np.clip(1.0 - 0.35 * np.clip(r - 0.55, 0, 1) ** 1.5, 0, 1)
    arr = np.asarray(im.convert('RGBA')).astype(np.float32)
    arr[:, :, :3] *= k[:, :, None]
    return Image.fromarray(arr.clip(0, 255).astype(np.uint8), 'RGBA')

TASKS = []
for f in sorted(os.listdir(os.path.join(SPR, 'Card'))):
    if f.lower().endswith('.png'):
        TASKS.append(('Card', f, 'card'))

# 角色/敌人：带 alpha 的走归一化，不带的只调色
for grp in ('Character', 'Enemy'):
    d = os.path.join(SPR, grp)
    if not os.path.isdir(d):
        continue
    for f in sorted(os.listdir(d)):
        if not f.lower().endswith('.png'):
            continue
        im = Image.open(os.path.join(d, f)).convert('RGBA')
        kind = 'sprite' if im.split()[3].getextrema()[0] < 255 else 'blob'
        TASKS.append((grp, f, kind))

def trim(im, l=0.0, r=0.0, t=0.0, b=0.0):
    """裁掉画内边框与水印所在的边缘，再拉回原尺寸。"""
    if not any((l, r, t, b)):
        return im
    w, h = im.size
    box = (int(w * l), int(h * t), int(w * (1 - r)), int(h * (1 - b)))
    return im.crop(box).resize((w, h), Image.LANCZOS)

def bake(grp, name, kind):
    src = Image.open(os.path.join(SPR, grp, name)).convert('RGBA')
    has_alpha = src.split()[3].getextrema()[0] < 255
    if kind == 'card':
        src = trim(src, 0.055, 0.055, 0.035, 0.085)
        out = card_vignette(duotone(src, CARD_SHADOW, CARD_LIGHT, 1.18, keep=0.04))
    elif has_alpha:
        a = src.split()[3]
        a = ImageChops.multiply(a, largest_component(a.filter(ImageFilter.GaussianBlur(3)).point(lambda v: 255 if v > 110 else 0)))
        src.putalpha(a)
        out = normalise(duotone(src, (10, 26, 32), (150, 226, 214), 1.22, keep=0.04), (1024, 1024), 0.07)
    else:
        src = trim(src, 0.0, 0.085, 0.0, 0.10)
        w, h = src.size
        k = min(1024 / w, 1024 / h, 1.0)
        out = duotone(src, (12, 20, 30), (168, 226, 216), 1.20, keep=0.04).resize((int(w * k), int(h * k)), Image.LANCZOS)
    return out

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--dry', action='store_true')
    ap.add_argument('--only', default='')
    a = ap.parse_args()
    if a.dry:
        os.makedirs(PREVIEW, exist_ok=True)
    n = 0
    for grp, name, kind in TASKS:
        if a.only and a.only not in name:
            continue
        try:
            out = bake(grp, name, kind)
        except Exception as e:
            print('FAIL', grp, name, e)
            continue
        dst = os.path.join(PREVIEW, grp + '__' + name) if a.dry else os.path.join(SPR, grp, name)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        out.save(dst, optimize=True)
        n += 1
        print('%-6s %-16s -> %s' % (grp, name, os.path.basename(dst)))
    print('done', n)

main()