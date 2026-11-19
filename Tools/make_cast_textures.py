#!/usr/bin/env python3
"""Makes the Indian (Hindu) cast's head textures from the Rocketbox originals. The originals are never changed:
every output is a new  <id>_head_<variant>_color.jpg  next to them in Assets/Resources/Characters/RB.

  python3 Tools/make_cast_textures.py          (needs Pillow:  python3 -m pip install --user pillow)

What it does (texture coordinates were read off gridded crops of each texture):
  * Male_Adult_15 (m204) wore a skullcap, modelled as two separate texture islands: the embroidered band and the
    embroidered crown. They are repainted with the model's own hair ("hair" variant: Vikram, crowd men) or with its
    own forehead skin plus a vermilion-and-sandal tilak ("shaved" variant: Pandit Harishankar).
  * Faces that read pale are warmed to a wheatish / brown complexion (hair darkens with them). The eye, teeth and
    tongue islands in the lower-left corner are left alone so the whites of the eyes stay white.
  * Women get a bindi between the brows.
The game picks these up through CharacterNPC (AvatarLook table). Run again after changing anything here.
"""
import os, random
from PIL import Image, ImageDraw, ImageFilter

RB = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources", "Characters", "RB")

# id: (warm multiplier or None, bindi?)      -- the multiplier is applied to skin and hair, not to eyes/teeth
FACES = {
    "f009": ((0.72, 0.60, 0.50), True),    # Kamala Kapoor (Female_Adult_09): wheatish, hair darkened
    "f003": ((0.84, 0.72, 0.60), True),    # Priya Kapoor (Female_Adult_03)
    "f016": ((0.78, 0.66, 0.55), True),    # Councilor Deshpande (Business_Female_03)
    "f014": (None, True),                  # Inspector Reddy (Business_Female_01): complexion already right
    "m004": ((0.76, 0.64, 0.53), False),   # Male_Adult_03: the judge, the cafe owner, crowd
    "m009": ((0.76, 0.64, 0.53), False),   # Male_Adult_05: Bhaskar, crowd
    "m020": ((0.78, 0.66, 0.55), False),   # Business_Male_03: the lawyer, Deshpande's men
    "m110": ((0.82, 0.70, 0.58), False),   # Wood_Male_01: crowd
    "m171": ((0.76, 0.64, 0.53), False),   # Security_Male_01: Sharma-ji
}

BINDI_UV = (0.500, 0.237)      # between the brows on every Rocketbox female head
BINDI_R = 0.0085               # ~0.8 cm across


def keep_mask(im):
    """White where the texture may be recoloured. Black only for the whites of the eyes and the teeth: bright,
    nearly colourless pixels in the lower-left corner where Rocketbox keeps the eye, teeth and tongue islands."""
    w, h = im.size
    hsv = im.convert("HSV")
    _, sat, val = hsv.split()
    white = Image.merge("RGB", (sat, val, val)).point(lambda v: v)   # placeholder to keep sizes aligned
    sp, vp = sat.load(), val.load()
    m = Image.new("L", (w, h), 255); mp = m.load()
    for y in range(int(h * 0.60), h):
        for x in range(0, int(w * 0.40)):
            if sp[x, y] < 46 and vp[x, y] > 140: mp[x, y] = 0
    return m.filter(ImageFilter.GaussianBlur(1.2))


def warm(im, mul):
    r, g, b = im.split()
    r = r.point(lambda v: int(v * mul[0])); g = g.point(lambda v: int(v * mul[1])); b = b.point(lambda v: int(v * mul[2]))
    w = Image.merge("RGB", (r, g, b))
    return Image.composite(w, im, keep_mask(im))


def brown_irises(im):
    """Grey, green or blue irises become dark brown (the pupil is found as the darkest blob in the eye island)."""
    w, h = im.size
    x0, x1, y0, y1 = int(0.17 * w), int(0.34 * w), int(0.85 * h), int(0.995 * h)
    px = im.load()
    # centre of the eye = centre of the white of the eye (bright, nearly colourless; it rings the iris evenly)
    xs = ys = n = 0
    for y in range(y0, y1):
        for x in range(x0, x1):
            r, g, b = px[x, y]
            if min(r, g, b) > 150 and max(r, g, b) - min(r, g, b) < 40: xs += x; ys += y; n += 1
    if n < 10: return im
    cx, cy = xs / n, ys / n
    # refine: the pupil is the darkest spot within a small radius of that centre
    best = None
    for y in range(int(cy - 0.02 * w), int(cy + 0.02 * w)):
        for x in range(int(cx - 0.02 * w), int(cx + 0.02 * w)):
            r, g, b = px[x, y]
            if best is None or r + g + b < best[0]: best = (r + g + b, x, y)
    if best is not None and best[0] < 90: cx, cy = best[1], best[2]
    rad = 0.021 * w
    sr = sg = sb = cnt = 0
    for y in range(int(cy - rad), int(cy + rad)):
        for x in range(int(cx - rad), int(cx + rad)):
            if (x - cx) ** 2 + (y - cy) ** 2 < rad * rad:
                r, g, b = px[x, y]; sr += r; sg += g; sb += b; cnt += 1
    # (every cast iris is recoloured: after skin warming a grey iris can already look "brownish" without being brown)
    out = im.copy(); po = out.load()
    for y in range(int(cy - rad), int(cy + rad) + 1):
        for x in range(int(cx - rad), int(cx + rad) + 1):
            d = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
            if d > rad: continue
            r, g, b = px[x, y]
            lum = 0.3 * r + 0.59 * g + 0.11 * b
            if lum > 175: continue                         # the white of the eye
            k = min(1.0, (rad - d) / (0.15 * rad))         # soft edge
            nr, ng, nb = min(255, lum * 0.78 + 8), lum * 0.48, lum * 0.26
            po[x, y] = (int(r + (nr - r) * k), int(g + (ng - g) * k), int(b + (nb - b) * k))
    return out


def bindi(im, color=(150, 18, 28)):
    w, h = im.size
    cx, cy, r = BINDI_UV[0] * w, BINDI_UV[1] * h, BINDI_R * w
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=color + (235,))
    d.ellipse([cx - r * 0.45, cy - r * 0.55, cx - r * 0.05, cy - r * 0.15], fill=(230, 120, 120, 70))   # a soft highlight
    layer = layer.filter(ImageFilter.GaussianBlur(0.8))
    return Image.alpha_composite(im.convert("RGBA"), layer).convert("RGB")


def tilak(im):
    """A sandal-paste U with a vermilion line up the middle, from the brows towards the hairline."""
    w, h = im.size
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    x = 0.5 * w
    top, bottom = 0.165 * h, 0.236 * h
    d.rounded_rectangle([x - 0.012 * w, top, x + 0.012 * w, bottom], radius=int(0.008 * w), fill=(236, 206, 150, 200))
    d.rounded_rectangle([x - 0.0035 * w, top + 0.004 * h, x + 0.0035 * w, bottom - 0.002 * h], radius=3, fill=(196, 34, 22, 235))
    layer = layer.filter(ImageFilter.GaussianBlur(1.0))
    return Image.alpha_composite(im.convert("RGBA"), layer).convert("RGB")


def cap_mask(im):
    """The skullcap's two islands on m204: the band (a long strip) and the crown (a disc)."""
    w, h = im.size
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    d.rectangle([int(0.10 * w), int(0.632 * h), int(0.88 * w), int(0.778 * h)], fill=255)
    cx, cy, r = 0.846 * w, 0.853 * h, 0.135 * w
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=255)
    # only where the original has paint (the islands), not the black background between them
    lum = im.convert("L").point(lambda v: 255 if v > 18 else 0)
    from PIL import ImageChops
    return ImageChops.multiply(m, lum).filter(ImageFilter.GaussianBlur(1.5))


def tiled(src, box, size, seed):
    """Fill an image of `size` with random tiles cut from region `box` of `src` (so the fill has real texture)."""
    rnd = random.Random(seed)
    patch = src.crop(box)
    pw, ph = patch.size
    out = Image.new("RGB", size)
    for y in range(0, size[1], ph // 2):
        for x in range(0, size[0], pw // 2):
            p = patch.rotate(rnd.choice([0, 180]), expand=False)
            out.paste(p, (x - rnd.randint(0, pw // 4), y - rnd.randint(0, ph // 4)))
    return out


def make_m204():
    src = Image.open(os.path.join(RB, "m204_head_color.jpg")).convert("RGB")
    w, h = src.size
    mask = cap_mask(src)
    # hair: the dark hair at the upper left of the head
    hair = tiled(src, (int(0.04 * w), int(0.20 * h), int(0.16 * w), int(0.34 * h)), src.size, 7)
    Image.composite(hair, src, mask).save(os.path.join(RB, "m204_head_hair_color.jpg"), quality=92)
    # shaved: the forehead's average skin tone, a touch darker (the shadow of stubble), with fine grain - not tiled
    fore = src.crop((int(0.44 * w), int(0.150 * h), int(0.56 * w), int(0.205 * h)))
    avg = fore.resize((1, 1), Image.BILINEAR).getpixel((0, 0))
    base = tuple(int(c * 0.88) for c in avg)
    rnd = random.Random(9)
    skin = Image.new("RGB", src.size, base)
    grain = Image.effect_noise(src.size, 14).convert("L")
    dark = Image.new("RGB", src.size, tuple(int(c * 0.8) for c in base))
    skin = Image.composite(dark, skin, grain.point(lambda v: max(0, v - 120))).filter(ImageFilter.GaussianBlur(0.7))
    shaved = Image.composite(skin, src, mask)
    tilak(shaved).save(os.path.join(RB, "m204_head_shaved_color.jpg"), quality=92)
    print("m204: hair, shaved")


def main():
    make_m204()
    for fid, (mul, has_bindi) in FACES.items():
        im = Image.open(os.path.join(RB, fid + "_head_color.jpg")).convert("RGB")
        if mul: im = warm(im, mul)
        im = brown_irises(im)
        if has_bindi: im = bindi(im)
        im.save(os.path.join(RB, fid + "_head_in_color.jpg"), quality=92)
        print(fid, "warm" if mul else "", "bindi" if has_bindi else "")


if __name__ == "__main__":
    main()
