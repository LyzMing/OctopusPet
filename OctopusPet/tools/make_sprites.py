# -*- coding: utf-8 -*-
"""从 assets/layers/ 生成桌宠全部精灵图（画布对齐合成后缩放），并同步到程序目录 sprites/。

路径自动定位：本脚本在 OctopusPet/tools/ 下，素材在 ../assets/。
"""
from PIL import Image
import os
import shutil

# 自动定位目录：tools/ 的上级 = OctopusPet
OCTOPUS_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS_DIR = os.path.join(OCTOPUS_DIR, 'assets')
LAYERS_DIR = os.path.join(ASSETS_DIR, 'layers')
SPRITES_FULL_DIR = os.path.join(ASSETS_DIR, 'sprites_full')   # 原尺寸精灵（存档）
SPRITES_APP_DIR = os.path.join(ASSETS_DIR, 'sprites_app')     # 程序尺寸精灵
APP_SPRITES_DIR = os.path.join(OCTOPUS_DIR, 'sprites')        # 程序内嵌目录

# 画布与显示尺寸
CANVAS_W, CANVAS_H = 1493, 1306
DISPLAY_W, DISPLAY_H = 146, 128

rects = {
    '02_身体1.png': (177, 98, 1279, 1304),
    '03_身体2.png': (141, 126, 1306, 1329),
    '04_身体3.png': (135, 88, 1284, 1354),
    '05_眼睛_睁.png': (485, 423, 681, 1004),
    '06_眼睛_闭.png': (609, 328, 703, 1074),
    '07_眼睛_激动.png': (500, 357, 677, 1071),
    '08_舌头1.png': (728, 669, 868, 902),
    '09_舌头2.png': (730, 672, 919, 853),
    '12_睡觉身体1.png': (364, 0, 1251, 1493),
    '13_睡觉身体2.png': (308, 0, 1231, 1419),
    '14_睡觉身体3.png': (301, 53, 1252, 1469),
    '15_睡觉眼睛.png': (736, 318, 806, 1107),
    '16_刚醒眼睛.png': (702, 339, 803, 1080),
    '17_z1.png': (272, 790, 487, 1019),
    '18_z2.png': (112, 1017, 296, 1227),
    '19_z3.png': (0, 1261, 222, 1458),
    '22_过渡身体.png': (227, 28, 1285, 1431),
    '23_过渡眼睛.png': (657, 321, 740, 1104),
}

def load(name):
    return Image.open(os.path.join(LAYERS_DIR, name)).convert('RGBA'), rects[name]

def composite(names):
    c = Image.new('RGBA', (CANVAS_W, CANVAS_H), (0, 0, 0, 0))
    for n in names:
        im, (t, l, b, r) = load(n)
        c.alpha_composite(im, (l, t))
    return c

os.makedirs(SPRITES_FULL_DIR, exist_ok=True)
os.makedirs(SPRITES_APP_DIR, exist_ok=True)
os.makedirs(APP_SPRITES_DIR, exist_ok=True)

def emit(name, canvas):
    canvas.save(os.path.join(SPRITES_FULL_DIR, name + '.png'))
    small = canvas.resize((DISPLAY_W, DISPLAY_H), Image.LANCZOS)
    small.save(os.path.join(SPRITES_APP_DIR, name + '.png'))
    print('sprite', name)

# ---- 常态 ----
for b in (1, 2, 3):
    body = f'0{b + 1}_身体{b}.png'
    emit(f'n_open{b}', composite([body, '05_眼睛_睁.png']))
    emit(f'n_closed{b}', composite([body, '06_眼睛_闭.png']))
    emit(f'n_excited{b}', composite([body, '07_眼睛_激动.png']))
    emit(f'n_t1_{b}', composite([body, '06_眼睛_闭.png', '08_舌头1.png']))
    emit(f'n_t2_{b}', composite([body, '06_眼睛_闭.png', '09_舌头2.png']))

# ---- 过渡 ----
emit('trans', composite(['22_过渡身体.png', '23_过渡眼睛.png']))

# ---- 睡觉 ----
for b in (1, 2, 3):
    emit(f'sleep{b}', composite([f'1{b + 1}_睡觉身体{b}.png', '15_睡觉眼睛.png']))
    emit(f'woke{b}', composite([f'1{b + 1}_睡觉身体{b}.png', '16_刚醒眼睛.png']))

# ---- zzz 覆盖图（独立，不受翻转影响）----
scale = DISPLAY_H / CANVAS_H
for src, name in [('17_z1.png', 'z1'), ('18_z2.png', 'z2'), ('19_z3.png', 'z3')]:
    im, (t, l, b, r) = load(src)
    w = max(1, round((r - l) * scale))
    h = max(1, round((b - t) * scale))
    z = im.resize((w, h), Image.LANCZOS)
    z.save(os.path.join(SPRITES_APP_DIR, name + '.png'))
    print(f'zzz {name}: size {w}x{h}  margin left={round(l*scale)} top={round(t*scale)}')

# ---- 同步到程序内嵌目录 ----
for f in os.listdir(SPRITES_APP_DIR):
    if f.endswith('.png'):
        shutil.copy2(os.path.join(SPRITES_APP_DIR, f), os.path.join(APP_SPRITES_DIR, f))
print(f'synced {len(os.listdir(APP_SPRITES_DIR))} sprites -> {APP_SPRITES_DIR}')
print('done')
