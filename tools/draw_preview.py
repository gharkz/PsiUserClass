"""ORIGINAL Workshop preview (1024x1024): drawn at 128px and scaled 8x nearest. No game assets used."""
from PIL import Image, ImageDraw, ImageFont
import math, os, random
OUT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
W = 128
im = Image.new("RGBA", (W, W), (16, 10, 28, 255)); d = ImageDraw.Draw(im)
random.seed(7)
for y in range(W):                                                    # dusk gradient
    t = y / W; d.line([(0, y), (W, y)], fill=(int(16 + 30 * t), int(10 + 8 * t), int(28 + 34 * t), 255))
for _ in range(60):                                                   # stars / motes
    x, y = random.randrange(W), random.randrange(70); d.point((x, y), fill=(200, 180, 255, random.randrange(80, 220)))
cx, cy = 64, 54
for r in range(46, 14, -8):                                           # psionic rings
    a = int(30 + (46 - r) * 4); d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=(150, 90, 255, a))
for i in range(5):                                                    # orbiting shards (telekinesis)
    ang = i * 2 * math.pi / 5 + 0.4; x, y = cx + 36 * math.cos(ang), cy + 30 * math.sin(ang)
    d.polygon([(x, y - 4), (x + 3, y), (x, y + 4), (x - 3, y)], fill=(110, 240, 255, 255), outline=(40, 110, 140, 255))
# figure: hooded mystic silhouette with staff
d.polygon([(64, 30), (76, 44), (80, 96), (48, 96), (52, 44)], fill=(34, 22, 52, 255), outline=(90, 60, 140, 255))
d.ellipse((56, 30, 72, 46), fill=(28, 18, 44, 255), outline=(90, 60, 140, 255))
d.ellipse((60, 37, 68, 41), fill=(110, 240, 255, 255)); d.point((63, 38), fill=(255, 255, 255, 255))   # glowing eyes
d.line([(82, 40), (86, 100)], fill=(120, 84, 50, 255), width=2)                                        # staff
d.polygon([(84, 32), (88, 38), (84, 44), (80, 38)], fill=(178, 120, 255, 255))                         # staff crystal
d.rectangle((0, 100, W, W), fill=(20, 14, 32, 255)); d.line([(0, 100), (W, 100)], fill=(90, 60, 140, 255))
big = im.resize((1024, 1024), Image.NEAREST).convert("RGB")
dd = ImageDraw.Draw(big)
try: f = ImageFont.truetype("C:/Windows/Fonts/georgiab.ttf", 120); f2 = ImageFont.truetype("C:/Windows/Fonts/georgia.ttf", 44)
except OSError: f = f2 = ImageFont.load_default()
for txt, font, y, col in (("PSION", f, 830, (230, 210, 255)), ("a new class  ·  beta", f2, 960, (150, 220, 235))):
    w = dd.textlength(txt, font=font); dd.text(((1024 - w) / 2 + 4, y + 4), txt, font=font, fill=(10, 6, 18)); dd.text(((1024 - w) / 2, y), txt, font=font, fill=col)
big.save(os.path.join(OUT, "preview.jpg"), quality=92)
print("ok")
