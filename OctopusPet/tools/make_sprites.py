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
    '03_身体2.png': (141, 126, 1296, 1329),
    '04_身体3.png': (135, 88, 1284, 1354),
    '05_抓鼠标1.png': (110, 48, 1271, 1332),
    '06_抓鼠标2.png': (121, 79, 1260, 1311),
    '07_抓鼠标3.png': (118, 69, 1249, 1344),
    '08_抓鼠标常态过渡.png': (111, 57, 1268, 1373),
    '09_眼睛_睁.png': (485, 423, 681, 1004),
    '10_眼睛_闭.png': (609, 328, 703, 1074),
    '11_眼睛_激动.png': (500, 357, 677, 1071),
    '12_舌头1.png': (720, 669, 796, 812),
    '13_舌头2.png': (721, 684, 820, 785),
    '16_睡觉身体1.png': (364, 0, 1251, 1493),
    '17_睡觉身体2.png': (308, 0, 1231, 1419),
    '18_睡觉身体3.png': (301, 53, 1252, 1469),
    '19_睡觉眼睛.png': (736, 318, 806, 1107),
    '20_刚醒眼睛.png': (702, 339, 803, 1080),
    '21_z1.png': (272, 790, 487, 1019),
    '22_z2.png': (112, 1017, 296, 1227),
    '23_z3.png': (0, 1261, 222, 1458),
    '26_过渡身体.png': (227, 28, 1285, 1431),
    '27_过渡眼睛.png': (657, 321, 740, 1104),
    # ---- 唱歌组（5 个方向：正面/半右/右/左/半左）----
    '31_唱歌眼睛.png': (541, 325, 724, 1069),
    '32_唱歌嘴巴1.png': (762, 648, 853, 742),
    '33_唱歌嘴巴2.png': (762, 648, 853, 763),
    '34_唱歌嘴巴3.png': (752, 666, 851, 731),
    '37_唱歌眼睛.png': (553, 620, 720, 1310),
    '38_唱歌嘴巴1.png': (762, 944, 853, 1022),
    '39_唱歌嘴巴2.png': (734, 945, 853, 1040),
    '40_唱歌嘴巴3.png': (752, 959, 851, 1013),
    '43_唱歌眼睛.png': (575, 1064, 720, 1296),
    '44_唱歌嘴巴1.png': (762, 1271, 849, 1316),
    '45_唱歌嘴巴2.png': (734, 1272, 847, 1322),
    '46_唱歌嘴巴3.png': (752, 1285, 841, 1320),
    '49_唱歌眼睛.png': (575, -40, 720, 388),
    '50_唱歌嘴巴1.png': (762, 136, 849, 181),
    '51_唱歌嘴巴2.png': (734, 130, 847, 180),
    '52_唱歌嘴巴3.png': (752, 132, 841, 167),
    '55_唱歌眼睛.png': (541, 167, 708, 857),
    '56_唱歌嘴巴1.png': (750, 455, 841, 533),
    '57_唱歌嘴巴2.png': (722, 437, 841, 532),
    '58_唱歌嘴巴3.png': (752, 464, 839, 518),
}

# 唱歌组方向定义：方向索引 → (眼睛图层, 嘴巴1/2/3 图层)
SING_DIRS = [
    ('front', '31_唱歌眼睛.png', ['32_唱歌嘴巴1.png', '33_唱歌嘴巴2.png', '34_唱歌嘴巴3.png']),
    ('hr',    '37_唱歌眼睛.png', ['38_唱歌嘴巴1.png', '39_唱歌嘴巴2.png', '40_唱歌嘴巴3.png']),
    ('r',     '43_唱歌眼睛.png', ['44_唱歌嘴巴1.png', '45_唱歌嘴巴2.png', '46_唱歌嘴巴3.png']),
    ('l',     '49_唱歌眼睛.png', ['50_唱歌嘴巴1.png', '51_唱歌嘴巴2.png', '52_唱歌嘴巴3.png']),
    ('hl',    '55_唱歌眼睛.png', ['56_唱歌嘴巴1.png', '57_唱歌嘴巴2.png', '58_唱歌嘴巴3.png']),
]
BODIES = ['02_身体1.png', '03_身体2.png', '04_身体3.png']
GRAB_BODIES = ['05_抓鼠标1.png', '06_抓鼠标2.png', '07_抓鼠标3.png']

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
    emit(f'n_open{b}', composite([body, '09_眼睛_睁.png']))
    emit(f'n_closed{b}', composite([body, '10_眼睛_闭.png']))
    emit(f'n_excited{b}', composite([body, '11_眼睛_激动.png']))
    emit(f'n_t1_{b}', composite([body, '10_眼睛_闭.png', '12_舌头1.png']))
    emit(f'n_t2_{b}', composite([body, '10_眼睛_闭.png', '13_舌头2.png']))

# ---- 抓鼠标 ----
for b in (1, 2, 3):
    body = GRAB_BODIES[b - 1]
    emit(f'n_grab{b}', composite([body, '11_眼睛_激动.png']))
# 抓鼠标过渡：过渡身体 + 激动眼睛
emit('n_grab_trans', composite(['08_抓鼠标常态过渡.png', '11_眼睛_激动.png']))

# ---- 唱歌组 ----
# 无脸身体（旋转到背面 = 后脑勺）
for b in (1, 2, 3):
    emit(f'none{b}', composite([BODIES[b - 1]]))
# 各方向：眼睛（+ 嘴巴1/2/3）
for dir_tag, eye, mouths in SING_DIRS:
    for b in (1, 2, 3):
        emit(f's_{dir_tag}_{b}', composite([BODIES[b - 1], eye]))
        for mi, mouth in enumerate(mouths, 1):
            emit(f's_{dir_tag}_{b}_m{mi}', composite([BODIES[b - 1], eye, mouth]))

# ---- 过渡 ----
emit('trans', composite(['26_过渡身体.png', '27_过渡眼睛.png']))

# ---- 睡觉 ----
for b in (1, 2, 3):
    emit(f'sleep{b}', composite([f'{b + 15}_睡觉身体{b}.png', '19_睡觉眼睛.png']))
    emit(f'woke{b}', composite([f'{b + 15}_睡觉身体{b}.png', '20_刚醒眼睛.png']))

# ---- zzz 覆盖图（独立，不受翻转影响）----
scale = DISPLAY_H / CANVAS_H
for src, name in [('21_z1.png', 'z1'), ('22_z2.png', 'z2'), ('23_z3.png', 'z3')]:
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
