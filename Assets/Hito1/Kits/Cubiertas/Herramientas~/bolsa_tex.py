# Bolsa transparente: cuerpo casi incoloro con alfa bajo, cierre azul suave,
# sin el logo de reciclaje (color y relieve).
import sys
from PIL import Image, ImageFilter, ImageDraw
import numpy as np
src_c, src_n, out_c, out_n, prev = sys.argv[1:6]
N = 1024
c = np.asarray(Image.open(src_c).convert('RGB').resize((N, N), Image.LANCZOS)).astype(np.float32)
lum = c.mean(axis=2)
base = np.median(lum[100:500, 50:450])
rgba = np.zeros((N, N, 4), np.uint8)
# Cuerpo: blanco azulado, con las arrugas oscuras del original como algo mas opaco.
dark = np.clip((base - lum) / 60.0, 0, 1)
# El logo es azul: no cuenta como arruga.
dark[(c[..., 2] - c[..., 0]) > 25] = 0
rgba[..., 0] = (236 - 40 * dark).astype(np.uint8)
rgba[..., 1] = (242 - 36 * dark).astype(np.uint8)
rgba[..., 2] = (248 - 26 * dark).astype(np.uint8)
rgba[..., 3] = (52 + 90 * dark).astype(np.uint8)
# Cierre (franja inferior de cada cara en el UV) y la parte de abajo (corredera).
y0, y1 = int(0.582 * N), int(0.648 * N)
rgba[y0:y1, :, 0:3] = (120, 160, 215)
rgba[y0:y1, :, 3] = 215
rgba[int(0.648 * N):, :, 0:3] = (170, 195, 230)
rgba[int(0.648 * N):, :, 3] = 235
Image.fromarray(rgba, 'RGBA').save(out_c)
# Relieve: borrar el logo (zona del simbolo) con un desenfoque fuerte, que deja las arrugas grandes.
n = Image.open(src_n).convert('RGB').resize((N, N), Image.LANCZOS)
box = (int(0.575 * N), int(0.14 * N), int(0.9 * N), int(0.47 * N))
region = n.crop(box)
blur = region.filter(ImageFilter.GaussianBlur(10))
mask = Image.new('L', region.size, 0)
ImageDraw.Draw(mask).rounded_rectangle([12, 12, region.size[0] - 12, region.size[1] - 12], 40, fill=255)
mask = mask.filter(ImageFilter.GaussianBlur(10))
region.paste(blur, (0, 0), mask)
n.paste(region, box[:2])
n.save(out_n)
pv = Image.new('RGB', (N * 2, N), (40, 40, 40))
bg = Image.new('RGBA', (N, N), (40, 40, 40, 255))
pv.paste(Image.alpha_composite(bg, Image.open(out_c)).convert('RGB'), (0, 0))
pv.paste(n, (N, 0))
pv.resize((N, N // 2)).save(prev)
