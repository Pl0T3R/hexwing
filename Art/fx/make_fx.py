"""
Effect textures for Hex Wing, made with numpy (no Blender needed):
- FX_Explosion.png (1024 x 1024, 8 x 8 frames of 128 px): a fireball that blooms white-yellow, burns
  orange and red, then cools into dark grey smoke that thins out. RGB is the light it adds (premultiplied),
  alpha how much it hides what is behind (the smoke). Read by Shaders/Sprite.shader.
- FX_Glow.png (256 x 256): a soft round glow with a faint four-point star, for impacts, muzzle flashes,
  engines and lights (tinted by the material).
- HUD_Glyphs.png (1024 x 512, 4 x 2 cells of 256 px): the holographic combat markers, white with a soft
  glow (tinted by the material): 0 crosshair, 1 enemy diamond, 2 ally chevron, 3 target brackets,
  4 lead pip, 5 lock ring, 6 warning triangle, 7 missile marker. Each glyph's main shape fits a circle of
  77% of the cell's half width; Cockpit/TargetingHud.cs draws them on 1.3-unit quads.
- UI_Panel.png (1024 x 880, the menu panel's 1.0 x 0.86 m): dark blue glass with faint scan lines, a
  thin glowing frame with cut corners, a title band and corner marks. Premultiplied (RGB = light,
  alpha = how dark the glass is). Read by Shaders/Panel.shader.
- UI_Highlight.png (512 x 64): the glowing bar under the selected menu row.
Run: python Art/fx/make_fx.py  (numpy, pillow)
"""
import os

import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "HexWing", "Resources", "Textures")
RNG = np.random.default_rng(9)
LATTICE = RNG.random((64, 64, 64)).astype(np.float32)


def value_noise(x, y, z):
    """Smooth 3D value noise in 0..1 on a 64^3 repeating lattice."""
    xi, yi, zi = np.floor(x).astype(int), np.floor(y).astype(int), np.floor(z).astype(int)
    xf, yf, zf = x - xi, y - yi, z - zi
    u, v, w = [t * t * (3 - 2 * t) for t in (xf, yf, zf)]
    def L(dx, dy, dz):
        return LATTICE[(xi + dx) % 64, (yi + dy) % 64, (zi + dz) % 64]
    x00 = L(0, 0, 0) * (1 - u) + L(1, 0, 0) * u
    x10 = L(0, 1, 0) * (1 - u) + L(1, 1, 0) * u
    x01 = L(0, 0, 1) * (1 - u) + L(1, 0, 1) * u
    x11 = L(0, 1, 1) * (1 - u) + L(1, 1, 1) * u
    return (x00 * (1 - v) + x10 * v) * (1 - w) + (x01 * (1 - v) + x11 * v) * w


def fbm(x, y, z, octaves=5):
    total, amp, norm = 0.0, 1.0, 0.0
    for _ in range(octaves):
        total = total + value_noise(x, y, z) * amp
        norm += amp
        x, y, z, amp = x * 2.03, y * 2.03, z * 2.03, amp * 0.5
    return total / norm


def ramp(t, stops):
    """Piecewise-linear colour ramp over t (any shape) -> (..., 3)."""
    t = np.clip(t, 0.0, 1.0)
    pos = np.array([s[0] for s in stops])
    cols = np.array([s[1] for s in stops], dtype=np.float32)
    out = np.empty(t.shape + (3,), dtype=np.float32)
    for c in range(3):
        out[..., c] = np.interp(t, pos, cols[:, c])
    return out


def explosion(frames=8, size=128):
    sheet = np.zeros((frames * size, frames * size, 4), dtype=np.float32)
    ys, xs = np.mgrid[0:size, 0:size].astype(np.float32)
    px, py = (xs + 0.5) / size * 2 - 1, (ys + 0.5) / size * 2 - 1
    r = np.sqrt(px * px + py * py)
    ang = np.arctan2(py, px)
    n = frames * frames
    for i in range(n):
        t = i / (n - 1)
        radius = 0.36 + 0.56 * (1 - (1 - t) ** 2.2)
        # billowing edge: the radius wobbles with noise that drifts outward over time
        edge = fbm(np.cos(ang) * 3.5 + 7, np.sin(ang) * 3.5 + 3, t * 3.0 + r * 2.5, 5)
        dens_n = fbm(px * 3.2 + 11, py * 3.2 + 5, t * 4.0, 5)
        boundary = radius * (0.7 + 0.6 * edge)
        body = np.clip((boundary - r) / (0.25 * boundary + 1e-3), 0, 1)
        density = body * (0.55 + 0.6 * dens_n)
        # temperature: hottest in the middle early on, cooling outward and over time
        heat = np.clip((1 - r / (boundary + 1e-3)) * 1.4, 0, 1) * (0.45 + 0.8 * dens_n) * (1 - t) ** 1.1
        fire = ramp(heat, [(0.0, (0.0, 0.0, 0.0)), (0.12, (0.3, 0.02, 0.0)), (0.3, (0.9, 0.12, 0.01)),
                           (0.5, (1.0, 0.38, 0.04)), (0.72, (1.0, 0.7, 0.2)), (0.9, (1.0, 0.95, 0.7)), (1.0, (1.0, 1.0, 1.0))])
        fire *= np.clip(heat[..., None] * 2.2, 0, 1.3)
        # smoke: grows as the fire cools, dark grey, thinning out at the end
        smoke_amt = np.clip(t * 1.6 - 0.15, 0, 1) * (1 - np.clip((t - 0.75) / 0.25, 0, 1) ** 1.5)
        alpha = np.clip(density * 1.3 * (0.35 + 0.65 * smoke_amt), 0, 1)
        smoke_rgb = np.array((0.022, 0.02, 0.019), dtype=np.float32) * alpha[..., None]
        rgb = np.clip(fire * density[..., None] + smoke_rgb, 0, 1)
        fx, fy = i % frames, i // frames
        sheet[fy * size:(fy + 1) * size, fx * size:(fx + 1) * size, :3] = rgb
        sheet[fy * size:(fy + 1) * size, fx * size:(fx + 1) * size, 3] = np.clip(alpha * (0.15 + 0.85 * smoke_amt), 0, 1)
    return sheet


def glow(size=256):
    ys, xs = np.mgrid[0:size, 0:size].astype(np.float32)
    px, py = (xs + 0.5) / size * 2 - 1, (ys + 0.5) / size * 2 - 1
    r = np.sqrt(px * px + py * py)
    core = np.exp(-(r / 0.12) ** 2)
    halo = np.exp(-(r / 0.42) ** 2) * 0.45
    star = (np.exp(-(np.abs(px) / 0.025) ** 2) + np.exp(-(np.abs(py) / 0.025) ** 2)) * np.exp(-(r / 0.7) ** 2) * 0.25
    v = np.clip(core + halo + star, 0, 1) * np.clip((1 - r) * 4, 0, 1)
    out = np.zeros((size, size, 4), dtype=np.float32)
    out[..., :3] = v[..., None]
    out[..., 3] = 0.0   # pure light: adds, hides nothing
    return out


def _segment(px, py, ax, ay, bx, by):
    """Distance from points to a line segment."""
    abx, aby = bx - ax, by - ay
    t = np.clip(((px - ax) * abx + (py - ay) * aby) / (abx * abx + aby * aby + 1e-9), 0, 1)
    dx, dy = px - (ax + abx * t), py - (ay + aby * t)
    return np.sqrt(dx * dx + dy * dy)


def _poly_lines(px, py, pts, closed=True):
    d = np.full(px.shape, 9.0, dtype=np.float32)
    n = len(pts)
    for i in range(n if closed else n - 1):
        a, b = pts[i], pts[(i + 1) % n]
        d = np.minimum(d, _segment(px, py, a[0], a[1], b[0], b[1]))
    return d


def _ngon(r, sides, start_deg):
    a0 = np.radians(start_deg)
    return [(r * np.cos(a0 + 2 * np.pi * i / sides), r * np.sin(a0 + 2 * np.pi * i / sides)) for i in range(sides)]


def hud_glyphs(cell=256):
    """White glyphs on black with a soft glow; coordinates in -1..1 per cell, main shapes inside r = 0.77."""
    ys, xs = np.mgrid[0:cell, 0:cell].astype(np.float32)
    px, py = (xs + 0.5) / cell * 2 - 1, -((ys + 0.5) / cell * 2 - 1)
    r = np.sqrt(px * px + py * py)
    ang = np.arctan2(py, px)
    w = 0.028   # line half width
    R = 0.77
    glyphs = []
    # 0 crosshair: hexagon, three inner ticks, centre dot
    d = _poly_lines(px, py, _ngon(R, 6, 30))
    for k in range(3):
        a = np.radians(90 + k * 120)
        d = np.minimum(d, _segment(px, py, np.cos(a) * 0.3, np.sin(a) * 0.3, np.cos(a) * 0.55, np.sin(a) * 0.55))
    d = np.minimum(d, np.maximum(r - 0.05, 0))
    glyphs.append(d)
    # 1 enemy: the four corners of a diamond (each side open in the middle)
    corners = _ngon(R, 4, 0)
    d = np.full(px.shape, 9.0, dtype=np.float32)
    for i in range(4):
        c, a, b = np.array(corners[i]), np.array(corners[i - 1]), np.array(corners[(i + 1) % 4])
        for o in (a, b):
            e = c + (o - c) * 0.38
            d = np.minimum(d, _segment(px, py, c[0], c[1], e[0], e[1]))
    glyphs.append(d)
    # 2 ally: a chevron pointing up
    d = _poly_lines(px, py, [(-R, -0.35), (0.0, R * 0.7), (R, -0.35)], closed=False)
    d = np.minimum(d, _poly_lines(px, py, [(-0.45, -0.55), (0.0, 0.0), (0.45, -0.55)], closed=False))
    glyphs.append(d)
    # 3 brackets: four corners of a square
    d = np.full(px.shape, 9.0, dtype=np.float32)
    for sx in (-1, 1):
        for sy in (-1, 1):
            c = (sx * R * 0.82, sy * R * 0.82)
            d = np.minimum(d, _segment(px, py, c[0], c[1], c[0] - sx * 0.35, c[1]))
            d = np.minimum(d, _segment(px, py, c[0], c[1], c[0], c[1] - sy * 0.35))
    glyphs.append(d)
    # 4 lead pip: a ring with a dot
    d = np.minimum(np.abs(r - R * 0.75), np.maximum(r - 0.16, 0))
    glyphs.append(d)
    # 5 lock ring: eight arc segments and four inward ticks
    step, fill = np.pi / 4, 0.7
    phase = np.mod(ang + np.pi / 8, step)
    seg = phase < step * fill
    # outside a segment: distance to the nearest segment end
    k = np.floor((ang + np.pi / 8) / step)
    a_end = k * step - np.pi / 8 + step * fill
    a_next = (k + 1) * step - np.pi / 8
    d_end = np.minimum(np.hypot(px - R * np.cos(a_end), py - R * np.sin(a_end)),
                       np.hypot(px - R * np.cos(a_next), py - R * np.sin(a_next)))
    d = np.where(seg, np.abs(r - R), d_end)
    for k in range(4):
        a = np.radians(45 + k * 90)
        d = np.minimum(d, _segment(px, py, np.cos(a) * R, np.sin(a) * R, np.cos(a) * 0.5, np.sin(a) * 0.5))
    glyphs.append(d)
    # 6 warning: a triangle with an exclamation bar
    d = _poly_lines(px, py, _ngon(R, 3, 90))
    d = np.minimum(d, _segment(px, py, 0, 0.35, 0, -0.05))
    d = np.minimum(d, np.maximum(np.sqrt(px * px + (py + 0.22) ** 2) - 0.05, 0))
    glyphs.append(d)
    # 7 missile: a small arrowhead in a circle
    d = np.minimum(np.abs(r - R), _poly_lines(px, py, [(-0.3, -0.3), (0.0, 0.4), (0.3, -0.3), (0.0, -0.1)]))
    glyphs.append(d)

    atlas = np.zeros((cell * 2, cell * 4, 4), dtype=np.float32)
    for i, d in enumerate(glyphs):
        core = np.clip((w - d) / 0.012 + 0.5, 0, 1)
        glow_ = np.exp(-(np.maximum(d - w, 0) / 0.07) ** 2) * 0.35
        v = np.clip(core + glow_, 0, 1) * np.clip((1 - r) * 8, 0, 1)
        cx, cy = i % 4, i // 4
        atlas[cy * cell:(cy + 1) * cell, cx * cell:(cx + 1) * cell, :3] = v[..., None]
        atlas[cy * cell:(cy + 1) * cell, cx * cell:(cx + 1) * cell, 3] = 1.0
    return atlas


def panel(width=1024, height=880):
    ys, xs = np.mgrid[0:height, 0:width].astype(np.float32)
    u, v = (xs + 0.5) / width, (ys + 0.5) / height          # v = 0 at the top
    aspect = width / height
    cut = 0.045
    # distance (in panel widths) to the frame: a rectangle with 45-degree cut corners, inset a little
    m = 0.012
    dx, dy = np.minimum(u - m, 1 - m - u), np.minimum(v - m, 1 - m - v) / aspect
    corner = (np.minimum(u - m, 1 - m - u) + np.minimum(v - m, 1 - m - v) / aspect - cut) / np.sqrt(2)
    d_frame = np.minimum(np.minimum(dx, dy), corner)
    inside = d_frame > 0
    line = np.exp(-(np.abs(d_frame) / 0.0022) ** 2)
    glow_ = np.exp(-(np.maximum(d_frame, 0) / 0.02) ** 2) * 0.25
    # title band and a divider under it
    title = (v > 0.03) & (v < 0.135)
    divider = np.exp(-((v - 0.14) / 0.0025) ** 2) * ((u > 0.06) & (u < 0.94))
    # corner marks: short bright ticks near each cut corner
    ticks = np.zeros_like(u)
    for cu in (0.03, 0.97):
        for cv in (0.035, 0.965):
            ticks += np.exp(-((u - cu) / 0.004) ** 2) * (np.abs(v - cv) < 0.05) * 0.0
    scan = 0.5 + 0.5 * np.sin(v * height * np.pi / 2.0)
    base_alpha = np.where(inside, 0.82, 0.0) * (1 - 0.06 * scan)
    glass = np.stack([0.006 + 0.01 * (1 - v), 0.012 + 0.016 * (1 - v), 0.022 + 0.02 * (1 - v)], -1) * base_alpha[..., None]
    cyan = np.array((0.35, 0.82, 1.0), dtype=np.float32)
    amber = np.array((1.0, 0.66, 0.25), dtype=np.float32)
    light = (line + glow_ * inside)[..., None] * cyan * 0.9 + divider[..., None] * cyan * 0.6
    accent = (title & (u > 0.035) & (u < 0.042))
    light += accent[..., None] * amber * 0.9
    glass = np.where((title & inside)[..., None], glass * 0.55, glass)
    rgb = np.clip(glass + light, 0, 1)
    alpha = np.clip(base_alpha + line * 0.5, 0, 1)
    return np.concatenate([rgb, alpha[..., None]], -1).astype(np.float32)


def highlight(width=512, height=64):
    ys, xs = np.mgrid[0:height, 0:width].astype(np.float32)
    u, v = (xs + 0.5) / width, (ys + 0.5) / height
    fade = np.clip(np.minimum(u, 1 - u) / 0.08, 0, 1)
    body = np.exp(-((v - 0.5) / 0.32) ** 2) * 0.35
    edge = (np.exp(-((v - 0.12) / 0.03) ** 2) + np.exp(-((v - 0.88) / 0.03) ** 2)) * 0.6
    left = np.exp(-((u - 0.03) / 0.006) ** 2) * (np.abs(v - 0.5) < 0.38)
    val = np.clip((body + edge) * fade + left, 0, 1)
    out = np.zeros((height, width, 4), dtype=np.float32)
    out[..., :3] = val[..., None]
    out[..., 3] = 0.0
    return out


def save(arr, name):
    os.makedirs(OUT, exist_ok=True)
    srgb = np.where(arr[..., :3] <= 0.0031308, arr[..., :3] * 12.92, 1.055 * np.power(np.clip(arr[..., :3], 0, 1), 1 / 2.4) - 0.055)
    img = np.concatenate([srgb, arr[..., 3:4]], axis=-1)
    Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(OUT, name), optimize=True)
    print("wrote", name)


if __name__ == "__main__":
    save(explosion(), "FX_Explosion.png")
    save(glow(), "FX_Glow.png")
    save(hud_glyphs(), "HUD_Glyphs.png")
    save(panel(), "UI_Panel.png")
    save(highlight(), "UI_Highlight.png")
