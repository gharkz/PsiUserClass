"""ORIGINAL 32x32 icons (drawn from scratch; no game assets used): Psi-Shield, Psi-Teleport."""
from PIL import Image, ImageDraw
import os, math
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "assets")
VIO, VIO_D, VIO_L, CYAN, INK = (150, 96, 240, 255), (70, 40, 120, 255), (206, 170, 255, 255), (110, 240, 255, 255), (24, 18, 40, 255)

def glow(d, cx, cy):
    for r, a in ((15, 36), (12, 60)): d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(150, 90, 255, a))

def shield():
    im = Image.new("RGBA", (32, 32), (0, 0, 0, 0)); d = ImageDraw.Draw(im); glow(d, 16, 16)
    pts = [(16, 3), (27, 7), (26, 18), (16, 29), (6, 18), (5, 7)]          # kite shield
    d.polygon(pts, fill=VIO, outline=VIO_D)
    d.polygon([(16, 6), (24, 9), (23, 17), (16, 25)], fill=VIO_L)            # bevel
    for r in (7, 4):                                                          # concentric mind ripples
        d.ellipse((16 - r, 15 - r, 16 + r, 15 + r), outline=CYAN)
    d.ellipse((14, 13, 18, 17), fill=INK); d.point((15, 14), fill=(255, 255, 255, 255))
    return im

def teleport():
    im = Image.new("RGBA", (32, 32), (0, 0, 0, 0)); d = ImageDraw.Draw(im); glow(d, 16, 16)
    d.ellipse((5, 5, 27, 27), outline=VIO_D, width=2)                         # portal ring
    for i in range(3):                                                        # spiral arms
        a0 = i * 2 * math.pi / 3
        pts = [(16 + (2 + t * 0.9) * math.cos(a0 + t * 0.35), 16 + (2 + t * 0.9) * math.sin(a0 + t * 0.35)) for t in range(12)]
        d.line(pts, fill=VIO_L if i else CYAN, width=2)
    d.ellipse((13, 13, 19, 19), fill=INK); d.ellipse((15, 15, 17, 17), fill=CYAN)
    d.polygon([(24, 3), (29, 3), (29, 8)], fill=CYAN)                         # destination marker
    return im

for name, f in (("psishield.png", shield), ("psiteleport.png", teleport)):
    im = f(); im.save(os.path.join(OUT, name)); im.resize((128, 128), Image.NEAREST).save(os.path.join(OUT, name.replace(".png", "-preview-4x.png")))
print("ok")
