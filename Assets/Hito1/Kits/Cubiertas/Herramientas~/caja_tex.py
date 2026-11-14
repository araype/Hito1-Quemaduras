# Etiqueta propia de la caja de film sobre el atlas del modelo (texto volteado
# verticalmente, como el original).
import sys, os
from PIL import Image, ImageDraw, ImageFont
src, out, preview = sys.argv[1], sys.argv[2], sys.argv[3]
im = Image.open(src).convert('RGB')
W, H = im.size  # 2048x1024
F = 'C:/Windows/Fonts/'
def font(name, size):
    for n in (name, 'arialbd.ttf'):
        if os.path.exists(F + n):
            return ImageFont.truetype(F + n, size)
bold = lambda s: font('segoeuib.ttf', s)
semi = lambda s: font('seguisb.ttf', s)
reg = lambda s: font('segoeui.ttf', s)

TEAL = (8, 112, 104)
TEAL_D = (5, 84, 78)
TEAL_L = (156, 214, 204)
BEIGE = im.getpixel((300, 470))
WHITE = (250, 250, 246)
d = ImageDraw.Draw(im)
# Fondos: cara trasera beige, franja, frente, extremos.
d.rectangle([0, 0, 1535, 497], fill=BEIGE)
d.rectangle([0, 498, 1535, 511], fill=TEAL_D)
d.rectangle([384, 512, 1535, 1023], fill=TEAL)
d.rectangle([0, 512, 371, 1023], fill=TEAL_L)
d.rectangle([372, 512, 383, 1023], fill=WHITE)
d.rectangle([1536, 420, 2047, 1023], fill=TEAL)
# Brillo suave del carton plastificado en el frente.
for i in range(60):
    a = int(14 * (1 - abs(i - 30) / 30))
    d.line([(384, 880 + i), (1535, 880 + i)], fill=tuple(min(255, c + a) for c in TEAL))

def text(cx, cy, s, f, color, anchor='mm'):
    # Se dibuja derecho en una capa y se voltea arriba-abajo.
    box = d.textbbox((0, 0), s, font=f, anchor='lt')
    w, h = box[2] + 8, box[3] + 8
    layer = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(layer).text((4, 4), s, font=f, fill=color + (255,), anchor='lt')
    layer = layer.transpose(Image.FLIP_TOP_BOTTOM)
    x = int(cx - w / 2) if anchor == 'mm' else int(cx)
    im.paste(layer, (x, int(cy - h / 2)), layer)

# Frente (en el modelo: tapa arriba, frente grande).
text(960, 566, '25 cm DE ANCHO  ·  30 m  ·  PARA ALIMENTOS  ·  CORTE CON EL BORDE DENTADO', semi(30), WHITE)
text(960, 800, 'FILM TRANSPARENTE', bold(104), WHITE)
text(960, 690, 'Film plástico de cocina', semi(52), TEAL_L)
# Extremo izquierdo (claro).
text(186, 860, 'FILM', bold(64), TEAL_D)
text(186, 790, 'transparente', semi(36), TEAL_D)
text(186, 650, '30 m', bold(96), TEAL_D)
# Paneles armados derechos y transformados segun como los mapea el modelo
# (visto en Unity): extremos = girado 90 grados; trasera = espejo horizontal.
def panel(size, bg, items):
    p = Image.new('RGB', size, bg)
    pd = ImageDraw.Draw(p)
    for (x, y, s, f, color, anchor) in items:
        pd.text((x, y), s, font=f, fill=color, anchor=anchor)
    return p

end = panel((496, 496), TEAL, [
    (248, 170, 'FILM', bold(110), WHITE, 'mm'),
    (248, 270, 'transparente', semi(46), TEAL_L, 'mm'),
    (248, 360, '25 cm × 30 m', semi(40), WHITE, 'mm')])
im.paste(end.transpose(Image.ROTATE_270).transpose(Image.FLIP_TOP_BOTTOM), (1540, 524))

DARK = (60, 60, 56)
back = panel((1536, 498), BEIGE, [
    (80, 150, 'Film plástico transparente para alimentos.', semi(38), DARK, 'lm'),
    (80, 212, 'Tire del borde y corte con la sierra de la caja.', reg(34), DARK, 'lm'),
    (80, 274, 'No usar en el horno.', reg(34), DARK, 'lm'),
    (1200, 200, '25 cm × 30 m', bold(60), TEAL_D, 'mm')])
im.paste(back.transpose(Image.FLIP_LEFT_RIGHT), (0, 0))

im.save(out)
im.resize((1024, 512), Image.LANCZOS).transpose(Image.FLIP_TOP_BOTTOM).save(preview)
