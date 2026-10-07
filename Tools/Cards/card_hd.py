"""
Gera as faces das cartas de tarô em ALTA RESOLUÇÃO (D-059, muda D-041): o mesmo desenho e a mesma
composição da pixel art de `card_pixel.py`, refeitos com bordas limpas e detalhe.

Identidade (D-037 / D-041): traço dourado sobre preto, moldura ornamentada (cantos com arcos e contas,
faixas de título e nome), estrelas, raios atrás do objeto e o objeto da carta no centro. Cristais e luz
arcana em ciano (violeta na amaldiçoada), a assinatura arcana do Pilar 4: máquina e magia na mesma peça.

Como funciona
- As formas são as mesmas do card_pixel.py, nas mesmas coordenadas (grade de 112 x 192 "unidades"),
  mas descritas como campos de distância (SDF) e avaliadas numa grade 12x mais fina (1344 x 2304).
  A cobertura de cada borda sai da distância (anti-aliasing analítico), e no fim a imagem é reduzida
  para 672 x 1152 com LANCZOS.
- Luz contínua: as rampas de dourado / ciano / violeta são interpoladas, sem pontilhado Bayer.
- O contorno tem espessura proporcional (0,7 unidade = ~4 px na saída), mais claro do lado da luz
  (cima à esquerda), e as peças ganham bisel, reflexos e gravação que a resolução nova permite.
- As estrelas ficam exatamente onde estavam: as posições são sorteadas como no card_pixel.py, usando a
  grade antiga só como mapa de ocupação.

Saída (mesmos nomes que a Unity já usa):
    Assets/_Game/Art/Cards/<id>.png          face (RGB, 672 x 1152)
    Assets/_Game/Art/Cards/<id>_brilho.png   máscara do brilho (branco com alfa, só cristal / luz arcana)
    Assets/_Game/Art/Cards/verso.png         verso da carta do chão (D-046) e verso_brilho.png
    Docs/Capturas/resolucao/cartas_hd.png    folha de contato

Uso (Python 3 com Pillow e numpy):
    python Tools/Cards/card_hd.py                  # todas + verso + folha
    python Tools/Cards/card_hd.py pistao_runico    # só algumas (e "verso")
"""

import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import card_pixel as cp  # noqa: E402  (só para o mapa de ocupação das estrelas e a lista de cartas)

ROOT = os.path.abspath(os.path.join(AQUI, "..", ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "_Game", "Art", "Cards")
SHEET = os.path.join(ROOT, "Docs", "Capturas", "resolucao", "cartas_hd.png")

W, H = 112, 192          # grade de desenho (as coordenadas do card_pixel.py)
SAIDA = 6                # px por unidade na saída: 672 x 1152
SS = 2                   # supersampling
K = SAIDA * SS           # px por unidade no desenho
RW, RH = W * K, H * K
CX, CY = 56.0, 96.0
FONTES = "C:/Windows/Fonts"
FONTE_TEXTO = ["COPRGTB.TTF", "GARABD.TTF", "georgiab.ttf"]
ESP = 0.7                # espessura padrão do contorno (unidades)
BISEL = 1.1              # largura do bisel nas bordas das peças

PAL = {k: np.array(v, np.float32) for k, v in cp.PAL.items()}
OURO = ("B0", "B1", "B2", "B3", "B4")
CIANO = ("C0", "C1", "C2", "WH")
VIOLETA = ("V0", "V1", "V2", "WH")
RAIZ2 = math.sqrt(2.0)


def cor(c):
    return PAL[c] if isinstance(c, str) else np.array(c, np.float32)


def mix(a, b, t):
    return cor(a) * (1 - t) + cor(b) * t


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


# posição de cada cor na rampa do ouro: meios-tons um pouco mais claros que a divisão igual, para o metal
# ler tão dourado quanto a pixel art (lá o pontilhado misturava o B3 nas transições)
POS_OURO = (0.0, 0.2, 0.42, 0.68, 1.0)


def rampa(nomes, v):
    """Interpola a rampa de cores (sem quantizar)."""
    v = np.clip(np.asarray(v, np.float32), 0.0, 1.0)
    if tuple(nomes) == OURO:
        v = np.interp(v, POS_OURO, (0.0, 0.25, 0.5, 0.75, 1.0)).astype(np.float32)
    v = v * (len(nomes) - 1)
    i = np.clip(np.floor(v).astype(np.int32), 0, len(nomes) - 2)
    f = (v - i)[..., None]
    tab = np.stack([PAL[n] for n in nomes])
    return tab[i] * (1 - f) + tab[i + 1] * f


def blur(a, sigma):
    """Desfoque gaussiano aproximado (três passadas de caixa) num array 2D float."""
    r = max(1, int(round(sigma - 0.5)))         # três caixas de raio r: sigma ~ sqrt(r (r + 1))
    out = a.astype(np.float32)
    for _ in range(3):
        for eixo in (0, 1):
            p = np.pad(out, [(r + 1, r) if e == eixo else (0, 0) for e in (0, 1)], mode="edge")
            c = np.cumsum(p, axis=eixo, dtype=np.float64)
            if eixo == 0:
                out = ((c[2 * r + 1:] - c[:-2 * r - 1]) / (2 * r + 1)).astype(np.float32)
            else:
                out = ((c[:, 2 * r + 1:] - c[:, :-2 * r - 1]) / (2 * r + 1)).astype(np.float32)
    return out


# ---------- formas (campos de distância em unidades; negativo = dentro) ----------

INF = 1e9
TUDO = (-4.0, -4.0, W + 4.0, H + 4.0)


def _uni(a, b):
    return (min(a[0], b[0]), min(a[1], b[1]), max(a[2], b[2]), max(a[3], b[3]))


def _inter(a, b):
    return (max(a[0], b[0]), max(a[1], b[1]), min(a[2], b[2]), min(a[3], b[3]))


class F:
    def __init__(self, fn, bb):
        self.fn = fn
        self.bb = bb

    def __call__(self, X, Y):
        return self.fn(X, Y)

    def __or__(self, o):
        a, b = self.fn, o.fn
        return F(lambda X, Y: np.minimum(a(X, Y), b(X, Y)), _uni(self.bb, o.bb))

    def __and__(self, o):
        a, b = self.fn, o.fn
        return F(lambda X, Y: np.maximum(a(X, Y), b(X, Y)), _inter(self.bb, o.bb))

    def __sub__(self, o):
        a, b = self.fn, o.fn
        return F(lambda X, Y: np.maximum(a(X, Y), -b(X, Y)), self.bb)

    def suave(self, o, k=2.0):
        """União suave (as bolhas de vapor se fundem numa massa só)."""
        a, b = self.fn, o.fn

        def f(X, Y):
            da, db = a(X, Y), b(X, Y)
            h = np.clip(0.5 + 0.5 * (db - da) / k, 0, 1)
            return db * (1 - h) + da * h - k * h * (1 - h)
        return F(f, _uni(self.bb, o.bb))

    def cresce(self, d):
        a = self.fn
        bb = self.bb
        return F(lambda X, Y: a(X, Y) - d, (bb[0] - max(d, 0), bb[1] - max(d, 0), bb[2] + max(d, 0), bb[3] + max(d, 0)))

    def anel(self, d):
        """Faixa de largura d por dentro da borda."""
        return self - self.cresce(-d)

    def espelho(self, ex=False, ey=False):
        a = self.fn
        x0, y0, x1, y1 = self.bb
        if ex:
            x0, x1 = W - x1, W - x0
        if ey:
            y0, y1 = H - y1, H - y0
        return F(lambda X, Y: a(W - X if ex else X, H - Y if ey else Y), (x0, y0, x1, y1))


def R(x0, y0, x1, y1, r=0.0):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    hx, hy = (x1 - x0) / 2 - r, (y1 - y0) / 2 - r

    def f(X, Y):
        qx = np.abs(X - cx) - hx
        qy = np.abs(Y - cy) - hy
        return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r
    return F(f, (x0, y0, x1, y1))


def E(cx, cy, rx, ry=None):
    ry = rx if ry is None else ry
    if abs(rx - ry) < 1e-6:
        return F(lambda X, Y: np.hypot(X - cx, Y - cy) - rx, (cx - rx, cy - ry, cx + rx, cy + ry))

    def f(X, Y):
        px, py = X - cx, Y - cy
        k0 = np.hypot(px / rx, py / ry)
        k1 = np.hypot(px / (rx * rx), py / (ry * ry))
        return k0 * (k0 - 1.0) / np.maximum(k1, 1e-6)
    return F(f, (cx - rx, cy - ry, cx + rx, cy + ry))


def anel(cx, cy, r0, r1, ry0=None, ry1=None):
    return E(cx, cy, r1, ry1) - E(cx, cy, r0, ry0)


def P(pts):
    """Polígono (distância exata)."""
    v = np.array(pts, np.float64)
    n = len(v)

    def f(X, Y):
        d = (X - v[0, 0]) ** 2 + (Y - v[0, 1]) ** 2
        s = np.ones_like(X)
        j = n - 1
        for i in range(n):
            ex, ey = v[j, 0] - v[i, 0], v[j, 1] - v[i, 1]
            wx, wy = X - v[i, 0], Y - v[i, 1]
            ee = ex * ex + ey * ey
            t = np.clip((wx * ex + wy * ey) / ee, 0, 1) if ee > 0 else 0.0
            bx, by = wx - ex * t, wy - ey * t
            d = np.minimum(d, bx * bx + by * by)
            c1 = Y >= v[i, 1]
            c2 = Y < v[j, 1]
            c3 = ex * wy > ey * wx
            troca = (c1 & c2 & c3) | (~c1 & ~c2 & ~c3)
            s = np.where(troca, -s, s)
            j = i
        return s * np.sqrt(d)
    return F(f, (v[:, 0].min(), v[:, 1].min(), v[:, 0].max(), v[:, 1].max()))


def L(pts, w):
    """Polilinha de largura w (pontas redondas)."""
    r = w / 2.0
    segs = list(zip(pts, pts[1:]))

    def f(X, Y):
        d = None
        for (ax, ay), (bx, by) in segs:
            dx, dy = bx - ax, by - ay
            ll = dx * dx + dy * dy
            if ll == 0:
                dd = np.hypot(X - ax, Y - ay)
            else:
                t = np.clip(((X - ax) * dx + (Y - ay) * dy) / ll, 0, 1)
                dd = np.hypot(X - (ax + t * dx), Y - (ay + t * dy))
            d = dd if d is None else np.minimum(d, dd)
        return d - r
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    return F(f, (min(xs) - r, min(ys) - r, max(xs) + r, max(ys) + r))


def arco(cx, cy, r, a0, a1, w, n=None, ry=None):
    """Arco (graus, sentido da tela) como polilinha."""
    ry = r if ry is None else ry
    n = n or max(6, int(abs(a1 - a0) / 6))
    pts = [(cx + math.cos(math.radians(a0 + (a1 - a0) * i / n)) * r,
            cy + math.sin(math.radians(a0 + (a1 - a0) * i / n)) * ry) for i in range(n + 1)]
    return L(pts, w)


def semiplano(nx, ny, c):
    """Lado onde nx*X + ny*Y < c."""
    return F(lambda X, Y: (nx * X + ny * Y - c) / math.hypot(nx, ny), TUDO)


def girar(pts, cx, cy, ang):
    c, s = math.cos(ang), math.sin(ang)
    return [(cx + x * c - y * s, cy + x * s + y * c) for (x, y) in pts]


def engrenagem(cx, cy, r, dentes, h, fase=0.0, cheio=0.5, ponta=None):
    """Engrenagem com dentes trapezoidais (como no card_pixel.py), como polígono."""
    pts = []
    passo = math.tau / dentes
    hr = cheio / 2 * passo
    ht = (cheio / 2 - 0.1) * passo if ponta is None else ponta * passo
    for i in range(dentes):
        tc = (i + fase + 0.5) * passo
        for k in range(1, 4):                   # vale (arco na raiz)
            a = tc - passo + hr + (passo - 2 * hr) * k / 4.0
            pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
        pts.append((cx + math.cos(tc - hr) * r, cy + math.sin(tc - hr) * r))
        for k in range(3):                      # topo do dente (arco)
            a = tc - ht + ht * k
            pts.append((cx + math.cos(a) * (r + h), cy + math.sin(a) * (r + h)))
        pts.append((cx + math.cos(tc + hr) * r, cy + math.sin(tc + hr) * r))
    return P(pts)


def mover(m, dx, dy):
    a = m.fn
    x0, y0, x1, y1 = m.bb
    return F(lambda X, Y: a(X - dx, Y - dy), (x0 + dx, y0 + dy, x1 + dx, y1 + dy))


def spline(pts, n=10):
    """Catmull-Rom pelos pontos (curvas suaves onde a pixel art tinha quebras)."""
    if len(pts) < 3:
        return list(pts)
    p = [pts[0]] + list(pts) + [pts[-1]]
    out = []
    for i in range(1, len(p) - 2):
        p0, p1, p2, p3 = p[i - 1], p[i], p[i + 1], p[i + 2]
        for k in range(n):
            t = k / n
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2
                                    + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in (0, 1)))
    out.append(tuple(pts[-1]))
    return out


def P5(pts):
    """Pontos de pixel (x, y) para o centro do pixel."""
    return [(x + 0.5, y + 0.5) for x, y in pts]


# ---------- funções de luz (coordenadas contínuas; luz de cima à esquerda) ----------

METAL = cp.METAL


def perfil(t, pts=METAL):
    t = np.clip(t, 0.0, 1.0)
    xs = np.array([p[0] for p in pts])
    vs = np.array([p[1] for p in pts])
    return np.interp(t, xs, vs)


def cil_x(x0, x1, k=1.0, d=0.0):
    return lambda X, Y: perfil((X - x0) / (x1 - x0)) * k + d


def cil_y(y0, y1, k=1.0, d=0.0):
    return lambda X, Y: perfil((Y - y0) / (y1 - y0)) * k + d


def esfera(cx, cy, r, k=1.0, d=0.0):
    hx, hy = cx - 0.38 * r, cy - 0.42 * r

    def f(X, Y):
        dd = np.hypot(X - hx, Y - hy) / (1.5 * r)
        v = 1.06 - dd * 1.12
        e = np.hypot(X - cx, Y - cy) / r
        borda = smooth(0.72, 0.9, e) * smooth(0.3 * r, 0.7 * r, (X - cx) + (Y - cy))
        v = v + 0.14 * borda                       # luz refletida na borda de baixo
        brilho = np.exp(-((X - (cx - 0.42 * r)) ** 2 + (Y - (cy - 0.46 * r)) ** 2) / (0.03 * r * r + 0.5))
        return (v + 0.18 * brilho) * k + d
    return f


def linear(x0, y0, x1, y1, k=1.0, d=0.0):
    tot = (x1 - x0) + (y1 - y0)
    return lambda X, Y: (0.92 - 0.62 * ((X - x0) + (Y - y0)) / tot) * k + d


def plano(v):
    return lambda X, Y: np.full_like(X, v)


def local(fn, cx, cy, ang):
    c, s = math.cos(-ang), math.sin(-ang)

    def f(X, Y):
        px, py = X - cx, Y - cy
        return fn(px * c - py * s, px * s + py * c)
    return f


# ---------- tela ----------

class Tela:
    def __init__(self, kind):
        self.kind = kind
        self.arc = VIOLETA if kind == "cursed" else CIANO
        self.rgb = np.zeros((RH, RW, 3), np.float32)
        self.rgb[:] = PAL["K"]
        self.glow = np.zeros((RH, RW), np.float32)
        self.fundo = np.ones((RH, RW), np.float32)

    # ----- base -----

    def regiao(self, bb, margem=1.5):
        x0, y0, x1, y1 = bb
        i0 = max(0, int(math.floor((x0 - margem) * K)))
        i1 = min(RW, int(math.ceil((x1 + margem) * K)))
        j0 = max(0, int(math.floor((y0 - margem) * K)))
        j1 = min(RH, int(math.ceil((y1 + margem) * K)))
        if i1 - i0 < 3 or j1 - j0 < 3:
            return None
        xs = (np.arange(i0, i1, dtype=np.float32) + 0.5) / K
        ys = (np.arange(j0, j1, dtype=np.float32) + 0.5) / K
        X, Y = np.meshgrid(xs, ys)
        return (slice(j0, j1), slice(i0, i1)), X, Y

    @staticmethod
    def cob(d):
        return np.clip(0.5 - d * K, 0.0, 1.0)

    @staticmethod
    def normal(d):
        gy, gx = np.gradient(d)
        n = np.hypot(gx, gy) + 1e-9
        return gx / n, gy / n

    @staticmethod
    def luz(nx, ny):
        """1 onde a normal aponta para a luz (cima à esquerda), -1 do outro lado."""
        return np.clip(-(nx + ny) / RAIZ2 * 1.4, -1, 1)

    def compor(self, sl, cob, c, glow=0.0, ocupa=True, so_fundo=False):
        if so_fundo:
            cob = cob * self.fundo[sl]
        a = cob[..., None]
        c = np.asarray(c, np.float32)
        self.rgb[sl] = self.rgb[sl] * (1 - a) + c * a
        self.glow[sl] = self.glow[sl] * (1 - cob) + glow * cob
        if ocupa:
            self.fundo[sl] *= (1 - cob)

    def fill(self, m, c, glow=0.0, ocupa=True, so_fundo=False, alfa=1.0):
        reg = self.regiao(m.bb)
        if reg is None:
            return
        sl, X, Y = reg
        cc = c(X, Y) if callable(c) else cor(c)
        self.compor(sl, self.cob(m(X, Y)) * alfa, cc, glow, ocupa, so_fundo)

    def clarear(self, m, c="B4", t=0.4, glow=None):
        """Mistura parcial (reflexos e sombras suaves por cima do que já existe)."""
        reg = self.regiao(m.bb)
        if reg is None:
            return
        sl, X, Y = reg
        a = (self.cob(m(X, Y)) * t)
        self.rgb[sl] = self.rgb[sl] * (1 - a[..., None]) + cor(c) * a[..., None]
        if glow is not None:
            self.glow[sl] = np.maximum(self.glow[sl], glow * a / max(t, 1e-6))

    def contorno(self, m, claro="B1", escuro="B0", esp=ESP, so_fundo=False, glow=0.0):
        reg = self.regiao(m.bb, esp + 1.5)
        if reg is None:
            return
        sl, X, Y = reg
        d = m(X, Y)
        nx, ny = self.normal(d)
        t = (self.luz(nx, ny) * 0.5 + 0.5)[..., None]
        c = cor(escuro) * (1 - t) + cor(claro) * t
        cob = np.clip(self.cob(d - esp) - self.cob(d), 0, 1)
        self.compor(sl, cob, c, glow, True, so_fundo)

    def pintar(self, m, fn, rp=OURO, chanfro=0.0, contorno=True, claro="B1", escuro="B0", esp=ESP,
               glow=None, ocupa=True):
        """Preenche a forma com a rampa (luz pela função fn), bisel nas bordas e contorno seletivo."""
        reg = self.regiao(m.bb, esp + 1.5)
        if reg is None:
            return
        sl, X, Y = reg
        d = m(X, Y)
        nx, ny = self.normal(d)
        lz = self.luz(nx, ny)
        if contorno:
            t = (lz * 0.5 + 0.5)[..., None]
            c = cor(escuro) * (1 - t) + cor(claro) * t
            self.compor(sl, self.cob(d - esp), c, 0.0, ocupa)
        v = fn(X, Y)
        if chanfro:
            faixa = 1.0 - smooth(0.0, BISEL, -d)
            v = v + chanfro * 1.25 * faixa * lz
        c = rampa(rp, v)
        g = 0.0
        if glow is not None:
            g = glow if not callable(glow) else glow(v)
        elif rp in (CIANO, VIOLETA):
            g = np.clip((np.clip(v, 0, 1) * (len(rp) - 1) - 0.4) / 0.8, 0, 1)
        self.compor(sl, self.cob(d), c, g, ocupa)

    def rebite(self, x, y, r=0.85):
        """Rebite (antigo: B4 em (x, y) e B0 em (x+1, y+1))."""
        cx, cy = x + 0.85, y + 0.85
        self.fill(E(cx + 0.3, cy + 0.35, r * 1.05), "B0", alfa=0.85)
        self.pintar(E(cx, cy, r), esfera(cx, cy, r, 0.95, 0.1), contorno=False)

    def sulco_h(self, x0, x1, y, c="B1", luz=0.3):
        """Sulco gravado (antes uma linha de 1 px): faixa escura e um fio de luz embaixo."""
        self.fill(R(x0, y + 0.05, x1, y + 0.75), c)
        if luz:
            self.clarear(R(x0, y + 0.75, x1, y + 1.05), "B4", luz)

    def sulco_v(self, x, y0, y1, c="B1", luz=0.3):
        self.fill(R(x + 0.05, y0, x + 0.75, y1), c)
        if luz:
            self.clarear(R(x + 0.75, y0, x + 1.05, y1), "B4", luz)

    # ----- arcano -----

    def halo_arcano(self, m, r=2, alfa=110):
        """Brilho suave em volta da luz arcana, só no fundo (e um pouco de alfa na máscara)."""
        reg = self.regiao(m.bb, r + 2.5)
        if reg is None:
            return
        sl, X, Y = reg
        d = m(X, Y)
        w = (1.0 - smooth(0.0, r + 1.2, d)) * smooth(-0.1, 0.5, d)
        w = w * self.fundo[sl]
        c = mix(self.arc[0], self.arc[1], 0.25)
        a = (w * 0.85)[..., None]
        self.rgb[sl] = self.rgb[sl] * (1 - a) + c * a
        self.glow[sl] = np.maximum(self.glow[sl], w * alfa / 255.0)

    def cristal(self, cx, cy, w, h, pal=None, aura=True, esp=0.6):
        """Cristal facetado: topo em V, aresta central, faces da esquerda claras, da direita escuras."""
        pal = pal or self.arc
        ys = cy - h * 0.42
        yi = cy + h * 0.45
        topo, base = (cx, cy - h), (cx, cy + h)
        ombro_c = ys + h * 0.2
        pts = [topo, (cx + w, ys), (cx + w, yi), base, (cx - w, yi), (cx - w, ys)]
        m = P(pts)
        self.fill(m.cresce(esp), pal[0])
        rp = (pal[0], pal[1], pal[2], pal[3])
        desce = lambda Y: (Y - ombro_c) / max(1.0, (cy + h) - ombro_c)  # noqa: E731
        faces = [
            (P([topo, (cx - w, ys), (cx, ombro_c)]), plano(0.98)),
            (P([topo, (cx + w, ys), (cx, ombro_c)]), lambda X, Y: 0.70 + 0.06 * (Y - topo[1]) / h),
            (P([(cx - w, ys), (cx, ombro_c), base, (cx - w, yi)]), lambda X, Y: 0.74 - 0.42 * desce(Y)),
            (P([(cx + w, ys), (cx, ombro_c), base, (cx + w, yi)]), lambda X, Y: 0.44 - 0.30 * desce(Y)),
        ]
        for f, fn in faces:
            self.pintar(f & m, fn, rp, contorno=False)
        # faceta interna e aresta central
        if w >= 3:
            self.pintar(P([(cx - w * 0.55, ys + h * 0.12), (cx - w * 0.12, ombro_c + h * 0.05),
                           (cx - w * 0.12, cy + h * 0.55), (cx - w * 0.55, yi - h * 0.05)]),
                        lambda X, Y: 0.9 - 0.4 * desce(Y), rp, contorno=False)
        self.pintar(L([(cx, ombro_c), (cx, cy + h * 0.92)], max(0.45, w * 0.12)),
                    lambda X, Y: 0.92 - 0.3 * desce(Y), rp, contorno=False)
        self.pintar(L([(cx - w + 0.3, ys + 0.2), (cx, ombro_c), (cx + w - 0.3, ys + 0.2)], max(0.35, w * 0.07)),
                    plano(0.95), rp, contorno=False)
        # reflexos
        gx, gy = cx - w * 0.42, cy - h * 0.6
        self.fill(L([(gx, gy + h * 0.16), (gx + w * 0.16, gy - h * 0.06)], max(0.5, w * 0.16)), pal[3], glow=1.0)
        self.estrela_c(cx - w * 0.38, cy - h * 0.7, max(1.2, h * 0.28), (pal[3], pal[3], pal[2], pal[1]), glow=1.0)
        if aura:
            self.halo_arcano(m)
        return m

    def cristal_pequeno(self, cx, cy, pal=None, aura=True):
        """Antigo sprite 5 x 7 centrado em (cx, cy) contínuo."""
        return self.cristal(cx, cy, 2.4, 3.5, pal, aura, esp=0.5)

    def raio(self, pts, ponta=True, seed=0):
        """Descarga arcana: núcleo claro, borda colorida e brilho no fundo."""
        pal = self.arc
        rnd = random.Random(seed or hash(tuple(pts)) & 0xFFFF)
        fino = [pts[0]]
        for (ax, ay), (bx, by) in zip(pts, pts[1:]):
            dx, dy = bx - ax, by - ay
            ll = math.hypot(dx, dy) or 1
            nx, ny = -dy / ll, dx / ll
            for t in (0.35, 0.7):
                j = rnd.uniform(-0.55, 0.55)
                fino.append((ax + dx * t + nx * j, ay + dy * t + ny * j))
            fino.append((bx, by))
        lado = L(fino, 2.0)
        self.halo_arcano(lado, 2.5, 80)
        self.fill(lado, pal[1], glow=1.0, so_fundo=True)
        self.fill(L(fino, 1.05), pal[2], glow=1.0)
        self.fill(L(fino, 0.45), pal[3], glow=1.0)
        if ponta:
            ex, ey = pts[-1]
            self.estrela(int(ex), int(ey), 2, (pal[3], pal[2], pal[1], pal[0]))

    # ----- estrelas -----

    def estrela_c(self, cx, cy, comp, pal=("B4", "B3", "B2", "B1"), diag=0.0, glow=None, brilho=0.22):
        """Cintilação de 4 pontas centrada em (cx, cy) contínuo, braços de comprimento comp."""
        arc = glow if glow is not None else (1.0 if pal[0] in ("WH", "C2", "V2") else 0.0)
        reg = self.regiao((cx - comp, cy - comp, cx + comp, cy + comp), 1.5)
        if reg is None:
            return
        sl, X, Y = reg
        dx, dy = X - cx, Y - cy
        r = np.hypot(dx, dy)
        if brilho:                                  # brilho suave em volta
            g = np.exp(-(r / (comp * 0.45 + 0.4)) ** 2) * brilho
            a = (g * self.fundo[sl])[..., None]
            self.rgb[sl] = self.rgb[sl] * (1 - a) + cor(pal[1]) * a
            if arc:
                self.glow[sl] = np.maximum(self.glow[sl], g * 2 * arc)
        larg = max(0.32, comp * 0.11)
        if diag:                                    # braços curtos nas diagonais, por baixo
            u, w = np.abs(dx + dy) / RAIZ2, np.abs(dx - dy) / RAIZ2
            ld = larg * 0.8
            f = np.minimum(u / diag + w / ld, w / diag + u / ld)
            cob = np.clip((1.0 - f) * ld * K * 0.9, 0, 1)
            self.compor(sl, cob, rampa((pal[1], pal[2], pal[3]), np.clip(r / diag, 0, 1)), arc)
        # braços: losangos finos nos eixos (forma côncava de estrela)
        ax_, ay_ = np.abs(dx), np.abs(dy)
        f1 = ax_ / comp + ay_ / larg
        f2 = ay_ / comp + ax_ / larg
        f = np.minimum(f1, f2)
        cob = np.clip((1.0 - f) * larg * K * 0.9, 0, 1)
        nucleo = np.clip((1.0 - r / (larg * 1.6)) * larg * K, 0, 1)
        cob = np.maximum(cob, nucleo)
        self.compor(sl, cob, rampa(pal, np.clip(r / comp, 0, 1)), arc)

    def estrela(self, x, y, tam, pal=("B4", "B3", "B2", "B1")):
        """Mesma chamada do card_pixel.py (pixel x, y; tam 0 a 4)."""
        cx, cy = x + 0.5, y + 0.5
        if tam == 0:
            self.estrela_c(cx, cy, 0.9, (pal[1], pal[2], pal[2], pal[3]), brilho=0.12)
            return
        comp = {1: 1.8, 2: 2.9, 3: 3.9, 4: 5.0}[tam]
        self.estrela_c(cx, cy, comp, pal, diag=(1.7 if tam >= 3 else 0.0), brilho=0.18 + 0.04 * tam)

    # ----- vapor -----

    def nuvem(self, cx, cy, s, flip=False):
        """Nuvem de vapor em volutas (gravura): bolhas com contorno dourado e miolo escuro."""
        puffs = [(-0.9, 0.15, 0.42), (-0.45, -0.18, 0.55), (0.1, -0.3, 0.62), (0.62, -0.08, 0.5), (1.0, 0.18, 0.38)]
        if flip:
            puffs = [(-dx, dy, rr) for dx, dy, rr in puffs][::-1]
        base = cy + s * 0.2
        u = None
        for dx, dy, rr in puffs:
            e = E(cx + dx * s, cy + dy * s, rr * s)
            u = e if u is None else (u | e)
        u = u & semiplano(0, 1, base)
        self.fill(u, mix("K", "G1", 0.35))
        self.pintar(u.anel(0.75), lambda X, Y: 0.82 - 0.2 * np.clip((Y - (cy - s * 0.4)) / s, 0, 1),
                    contorno=False, chanfro=0.12)
        for dx, dy, rr in puffs[1:4]:                 # volutas internas
            r = rr * s * 0.5
            self.fill(arco(cx + dx * s, cy + dy * s + 1, r, 200, 330, 0.55) & u.cresce(-0.9), "B1")
        return u

    def massa_vapor(self, bolhas):
        """Nuvem de vapor: contorno externo da união das bolhas, com volutas das bolhas da frente."""
        u = None
        for (x, y, r) in bolhas:
            e = E(x, y, r)
            u = e if u is None else u.suave(e, 1.8)
        y0, y1 = u.bb[1], u.bb[3]
        self.fill(u, lambda X, Y: rampa(("K", "G1", "G2", "G3"), 0.62 - 0.5 * np.clip((Y - y0) / (y1 - y0), 0, 1)))
        reg = self.regiao(u.bb)
        if reg:                                         # volume: miolo um pouco mais escuro que a borda
            sl, X, Y = reg
            d = u(X, Y)
            a = (self.cob(d) * smooth(0.5, 4.0, -d) * 0.4)[..., None]
            self.rgb[sl] = self.rgb[sl] * (1 - a) + mix("K", "G1", 0.5) * a
        reg = self.regiao(u.bb, 1.0)                    # contorno só por fora (erosão da máscara, como a
        if reg:                                         # borda() da pixel art; o campo da união não serve)
            sl, X, Y = reg
            c = self.cob(u(X, Y))
            dentro = smooth(0.955, 0.99, blur(c, 0.8 * K / 2))
            aro = np.clip(c - dentro, 0, 1)
            gy, gx = np.gradient(blur(c, 0.4 * K))
            lz = np.clip((gx + gy) / (np.hypot(gx, gy) + 1e-6) / RAIZ2 * 1.4, -1, 1)
            v = 0.82 - 0.22 * np.clip((Y - 40) / 25.0, 0, 1) + 0.12 * lz
            self.compor(sl, aro, rampa(OURO, v))
        for (x, y, r) in bolhas:                        # volutas nas bolhas da frente
            if not any(by < y - 0.5 for (_, by, _) in bolhas):
                continue
            for a0, a1, c in ((200, 270, "B3"), (270, 340, "B2")):
                self.fill(arco(x, y + 0.6, r * 0.62, a0, a1, 0.65) & u.cresce(-0.9), c, alfa=0.8)
        return u

    def vapor(self, bolhas):
        """Baforada pequena: bolhas sobrepostas com contorno dourado (as de trás primeiro)."""
        for (x, y, r) in bolhas:
            d = E(x, y, r)
            self.fill(d, mix("K", "G2", 0.25))
            self.pintar(d.anel(0.7), lambda X, Y: 0.72, contorno=False, chanfro=0.2)
            if r >= 3:
                self.fill(arco(x, y, r - 1.6, 200, 290, 0.5), "B1")

    def runa(self, x, y, k):
        """Runa arcana (desenho próprio) numa caixa 3 x 5 a partir do pixel (x, y)."""
        tr = {
            0: [[(0.5, 0.5), (0.5, 1.5), (2.5, 1.5), (2.5, 0.5)], [(1.5, 1.5), (1.5, 4.5)]],
            1: [[(1.5, 0.5), (0.5, 1.6), (0.5, 3.4), (1.5, 4.5), (2.5, 3.4), (2.5, 1.6), (1.5, 0.5)],
                [(0.5, 2.5), (2.5, 2.5)]],
            2: [[(0.5, 0.5), (2.5, 2.5), (0.5, 4.5)]],
            3: [[(0.5, 0.5), (2.5, 0.5)], [(0.5, 0.5), (2.5, 4.5)], [(2.5, 0.5), (0.5, 4.5)], [(0.5, 4.5), (2.5, 4.5)]],
        }[k % 4]
        m = None
        for pl in tr:
            s = L([(x + a, y + b) for a, b in pl], 0.75)
            m = s if m is None else (m | s)
        self.halo_arcano(m, 2.0, 90)
        self.contorno(m, self.arc[0], self.arc[0], 0.5, so_fundo=True)
        self.fill(m, self.arc[2], glow=1.0)
        self.fill(m.cresce(-0.22), self.arc[3], glow=1.0, alfa=0.7)
        return m

    def luz_radial(self, cx, cy, r, c, forca=0.5, dentro=None, glow=0.0, so_fundo=False):
        """Brilho suave (gaussiano) em volta de (cx, cy), opcionalmente só dentro de uma forma."""
        reg = self.regiao((cx - 2 * r, cy - 2 * r, cx + 2 * r, cy + 2 * r), 0.5)
        if reg is None:
            return
        sl, X, Y = reg
        a = np.exp(-((X - cx) ** 2 + (Y - cy) ** 2) / (r * r)) * forca
        if dentro is not None:
            a = a * self.cob(dentro(X, Y))
        if so_fundo:
            a = a * self.fundo[sl]
        self.rgb[sl] = self.rgb[sl] * (1 - a[..., None]) + cor(c) * a[..., None]
        if glow:
            self.glow[sl] = np.maximum(self.glow[sl], a / max(forca, 1e-6) * glow)

    def raio_luz(self, p0, p1, c0, c1, larg=0.45, some=0.25, glow=0.0, so_fundo=True, ocupa=False):
        """Traço que afina e some ao longo do comprimento (raios de luz, rastros)."""
        reg = self.regiao((min(p0[0], p1[0]), min(p0[1], p1[1]), max(p0[0], p1[0]), max(p0[1], p1[1])))
        if reg is None:
            return
        sl, X, Y = reg
        dx, dy = p1[0] - p0[0], p1[1] - p0[1]
        ll = dx * dx + dy * dy or 1.0
        tt = np.clip(((X - p0[0]) * dx + (Y - p0[1]) * dy) / ll, 0, 1)
        dist = np.hypot(X - (p0[0] + tt * dx), Y - (p0[1] + tt * dy))
        lg = larg * (1 - 0.55 * tt)
        cob = np.clip(0.5 - (dist - lg) * K, 0, 1) * (1 - smooth(some, 1.0, tt) * 0.9)
        c = cor(c0)[None, None, :] * (1 - tt[..., None]) + cor(c1)[None, None, :] * tt[..., None]
        self.compor(sl, cob, c, glow, ocupa, so_fundo)

    def chama(self, cx, base, h, w, incl=0.0):
        """Chama em gota: borda bronze, miolo claro (gradiente pela profundidade)."""
        m = E(cx, base - w, w, w) | P([(cx - w, base - w), (cx + incl, base - h), (cx + w, base - w)])
        reg = self.regiao(m.bb)
        if reg is None:
            return m
        sl, X, Y = reg
        d = m(X, Y)
        v = 0.5 + np.clip(-d / 1.6, 0, 1) * 0.5
        self.compor(sl, self.cob(d), rampa(OURO, v))
        return m


# ---------- fundo, halo e estrelas ----------

def fundo(t):
    """Vinheta contínua: o centro levemente mais claro, as bordas no preto, e um grão de papel bem leve."""
    ys, xs = np.mgrid[0:RH, 0:RW].astype(np.float32)
    X, Y = (xs + 0.5) / K, (ys + 0.5) / K
    d = np.hypot((X - CX) / 58.0, (Y - CY) / 86.0)
    v = np.clip(1.0 - d, 0, 1) * 0.95
    t.rgb[:] = rampa(("K", "G1", "G2"), v * 0.92)
    rnd = np.random.default_rng(7)
    grao = blur(rnd.standard_normal((RH // 4, RW // 4)).astype(np.float32), 1.0)
    grao = np.asarray(Image.fromarray(grao).resize((RW, RH), Image.BILINEAR))
    t.rgb[:] += (grao * 2.2)[..., None]
    rnd2 = random.Random(7)
    for _ in range(90):                       # pontinhos do papel, como antes
        x, y = rnd2.randrange(8, W - 8), rnd2.randrange(28, H - 28)
        t.fill(E(x + 0.5, y + 0.5, 0.35), "G2", ocupa=False, alfa=0.6)


def halo(t, cx, cy, r, kind):
    """Sol de raios: disco escuro com brilho, aro, anel pontilhado e raios que somem."""
    tinta = "V0" if kind == "cursed" else "G2"
    reg = t.regiao((cx - r, cy - r, cx + r, cy + r))
    sl, X, Y = reg
    dd = np.hypot(X - cx, Y - cy)
    v = np.clip(1.0 - dd / r, 0, 1) * 0.75
    v = np.where(v > 0.08, v, 0.0)
    disco = rampa(("K", tinta), v * 0.62 + 0.0)
    a = t.cob(dd - r)[..., None]
    t.rgb[sl] = t.rgb[sl] * (1 - a) + disco * a
    # aro e anel pontilhado
    t.pintar(anel(cx, cy, r - 0.9, r), lambda X, Y: 0.27, contorno=False, ocupa=False, chanfro=0.12)
    for i in range(int(2 * math.pi * (r - 2) / 2)):
        a_ = i * math.tau / int(2 * math.pi * (r - 2) / 2)
        t.fill(E(cx + math.cos(a_) * (r - 2.3), cy + math.sin(a_) * (r - 2.3), 0.33), "B0", ocupa=False)
    # raios
    n = 44
    for i in range(n):
        a_ = i * math.tau / n + 0.04
        longo = i % 2 == 0
        r1 = r + (12 if longo else 7)
        p0 = (cx + math.cos(a_) * (r + 1.6), cy + math.sin(a_) * (r + 1.6))
        p1 = (cx + math.cos(a_) * r1, cy + math.sin(a_) * r1)
        reg = t.regiao((min(p0[0], p1[0]), min(p0[1], p1[1]), max(p0[0], p1[0]), max(p0[1], p1[1])))
        if reg is None:
            continue
        sl, X, Y = reg
        dx, dy = p1[0] - p0[0], p1[1] - p0[1]
        ll = dx * dx + dy * dy
        tt = np.clip(((X - p0[0]) * dx + (Y - p0[1]) * dy) / ll, 0, 1)
        dist = np.hypot(X - (p0[0] + tt * dx), Y - (p0[1] + tt * dy))
        larg = (0.42 if longo else 0.32) * (1 - 0.6 * tt)
        cob = np.clip(0.5 - (dist - larg) * K, 0, 1) * (1 - smooth(0.25, 1.0, tt) * 0.85)
        c = rampa(("B0", "B1"), (1 - tt) * (0.8 if longo else 0.45))
        t.compor(sl, cob, c, 0.0, False, True)


def livre(t0, x, y, folga):
    for yy in range(y - folga, y + folga + 1):
        for xx in range(x - folga, x + folga + 1):
            c = t0.get(xx, yy)
            if c is None or c not in cp.FUNDO:
                return False
    return True


def estrelas(t, t0, rnd, raio_halo):
    """Mesmo sorteio do card_pixel.py (as estrelas caem nos mesmos lugares), desenho em alta."""
    def ok(x, y, folga, r_extra=13):
        if math.hypot(x + 0.5 - CX, y + 0.5 - CY) < raio_halo + r_extra:
            return False
        return 10 <= x <= W - 11 and 31 <= y <= H - 32 and livre(t0, x, y, folga)

    postas = []

    def longe(x, y, d):
        return all(math.hypot(x - a, y - b) >= d for a, b in postas)

    for _ in range(60):
        ox, oy = rnd.choice([(16, 34), (W - 34, 36), (16, H - 52), (W - 36, H - 50)])
        pts = [(ox + rnd.randint(0, 18), oy + rnd.randint(0, 14)) for _ in range(4)]
        pts.sort()
        if all(ok(x, y, 2, 10) for x, y in pts) and \
                all(math.hypot(a[0] - b[0], a[1] - b[1]) >= 5 for a, b in zip(pts, pts[1:])):
            for a, b in zip(pts, pts[1:]):              # linha tracejada da constelação
                ax, ay, bx, by = a[0] + 0.5, a[1] + 0.5, b[0] + 0.5, b[1] + 0.5
                ll = math.hypot(bx - ax, by - ay)
                n = max(1, int(ll / 1.6))
                for k in range(1, n):
                    if k % 2:
                        f0, f1 = (k - 0.35) / n, (k + 0.35) / n
                        t.fill(L([(ax + (bx - ax) * f0, ay + (by - ay) * f0),
                                  (ax + (bx - ax) * f1, ay + (by - ay) * f1)], 0.38), "B1", so_fundo=True)
                for x, y in cp.B(a, b):
                    if t0.fundo_em(x, y):
                        t0.put(x, y, "B0")
            for x, y in pts:
                t.fill(E(x + 0.5, y + 0.5, 0.75), "B3")
                t.estrela_c(x + 0.5, y + 0.5, 1.5, brilho=0.2)
                t0.put(x, y, "B3")
                postas.append((x, y))
            break

    for tam, qtd in [(4, 1), (3, 2), (2, 3), (1, 5), (0, 16)]:
        feitos = 0
        for _ in range(400):
            if feitos >= qtd:
                break
            x, y = rnd.randrange(10, W - 10), rnd.randrange(31, H - 31)
            if ok(x, y, tam + 1) and longe(x, y, 7 + tam * 2):
                if tam == 0:
                    claro = rnd.random() < 0.4
                    t.estrela(x, y, 0, ("B4", "B3", "B3", "B2") if claro else ("B3", "B2", "B2", "B1"))
                else:
                    t.estrela(x, y, tam)
                cp.Tela.estrela(t0, x, y, tam)
                postas.append((x, y))
                feitos += 1


# ---------- texto ----------

_fontes = {}


def fonte(px):
    if px not in _fontes:
        for nome in FONTE_TEXTO:
            caminho = os.path.join(FONTES, nome)
            if os.path.exists(caminho):
                _fontes[px] = ImageFont.truetype(caminho, px)
                break
        else:
            _fontes[px] = ImageFont.load_default()
    return _fontes[px]


def mascara_texto(txt, cap_u, larg_max_u):
    """Máscara (float 0..1) do texto com altura de maiúscula cap_u, e a largura em unidades."""
    px = int(cap_u * K * 1.42)
    while True:
        f = fonte(px)
        hb = f.getbbox("H", anchor="ls")
        cap_px = -hb[1]
        bb = f.getbbox(txt, anchor="ls")
        if bb[2] - bb[0] <= larg_max_u * K or px < 10:
            break
        px -= 1
    esc = cap_u * K / cap_px
    if abs(esc - 1) > 0.02 and px == int(cap_u * K * 1.42):
        px = int(px * esc)
        f = fonte(px)
        hb = f.getbbox("H", anchor="ls")
        cap_px = -hb[1]
        bb = f.getbbox(txt, anchor="ls")
    pad = 8
    w, h = bb[2] - bb[0] + 2 * pad, bb[3] - bb[1] + 2 * pad
    img = Image.new("L", (w, h), 0)
    ImageDraw.Draw(img).text((pad - bb[0], pad - bb[1]), txt, font=f, fill=255, anchor="ls")
    # deslocamento da linha de base dentro da imagem
    return np.asarray(img, np.float32) / 255.0, pad - bb[1], (bb[2] - bb[0]) / K, cap_px / K


def carimbar(t, m, x_esq_px, base_px, base_in_img, cap_u, sombra=True, contorno=0):
    """Cola a máscara de texto com degradê dourado (claro em cima), sombra e contorno opcional."""
    h, w = m.shape
    j0 = int(round(base_px - base_in_img))
    i0 = int(round(x_esq_px))
    sl = (slice(j0, j0 + h), slice(i0, i0 + w))
    ys = (np.arange(j0, j0 + h, dtype=np.float32) + 0.5) / K
    topo_u = base_px / K - cap_u
    v = 1.0 - np.clip((ys - topo_u) / cap_u, 0, 1) * 0.62
    c = rampa(OURO, np.repeat(v[:, None], w, axis=1))
    if contorno:
        o = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(contorno)),
                       np.float32) / 255.0
        t.compor(sl, o, PAL["B0"])
    if sombra:
        dsh = int(round(0.55 * K))
        ms = np.zeros_like(m)
        ms[dsh:, dsh:] = m[:-dsh, :-dsh]
        t.compor(sl, ms * 0.9, PAL["B0"])
    t.compor(sl, m, c)


def texto(t, txt, cy_caps, cap=5.0, larg_max=80.0):
    m, base_in, larg, _ = mascara_texto(txt, cap, larg_max)
    x_esq = CX * K - m.shape[1] / 2.0
    carimbar(t, m, x_esq, (cy_caps + cap) * K, base_in, cap)
    return larg


def numeral(t, txt, y0):
    m, base_in, larg, _ = mascara_texto(txt, 8.6, 60)
    x_esq = CX * K - m.shape[1] / 2.0
    carimbar(t, m, x_esq, (y0 + 8.8) * K, base_in, 8.6, sombra=True, contorno=7)
    return larg


# ---------- moldura ----------

def moldura(t, kind, rotulo, nome):
    carta = R(0, 0, W, H, 6.0)
    # borda externa: aro de metal chanfrado
    reg = t.regiao(TUDO, 0)
    sl, X, Y = reg
    d = carta(X, Y)
    ins = -d
    nx, ny = t.normal(d)
    lz = t.luz(nx, ny)
    fora = np.clip(0.5 + d * K, 0, 1)                   # cantos de fora: preto
    v = perfil(np.clip((ins - 0.15) / 2.3, 0, 1)) * 0.62 + 0.06 + lz * 0.16
    metal = rampa(OURO, v)
    cob_aro = np.clip(0.5 - (ins - 2.55) * K, 0, 1) * (1 - fora)
    t.compor(sl, cob_aro, metal)
    linha = np.clip(0.5 - np.abs(ins - 2.75) * K + 0.2 * K, 0, 1) * (1 - fora)
    t.compor(sl, linha, PAL["B0"])
    t.compor(sl, np.clip(0.5 - np.abs(ins - 0.18) * K + 0.1 * K, 0, 1), PAL["B1"])
    t.compor(sl, (ins > 2.9) * np.clip(0.5 - (ins - 4.0) * K, 0, 1) * 1.0, PAL["K"])
    t.compor(sl, fora, PAL["K"])
    # filete interno e pontilhado
    t.pintar(R(5, 5, W - 5, H - 5).anel(0.9), plano(0.5), contorno=False, chanfro=0.18)
    for x in range(7, W - 7):
        if x % 2 == 0:
            for y in (7.5, H - 7.5):
                t.fill(E(x + 0.5, y, 0.32), "B0")
    for y in range(7, H - 7):
        if y % 2 == 0:
            for x in (7.5, W - 7.5):
                t.fill(E(x, y + 0.5, 0.32), "B0")

    # ornamentos de um quarto da carta (cima, esquerda), espelhados nos quatro cantos
    def cantos(f):
        for ex, ey in ((False, False), (True, False), (False, True), (True, True)):
            f(lambda m, ex=ex, ey=ey: m.espelho(ex, ey), ex, ey)

    def orn(e, ex, ey):
        def px(x):
            return W - x if ex else x

        def py(y):
            return H - y if ey else y
        caixa = e(R(3, 3, 12, 12))
        t.fill(caixa, "K")
        t.pintar(caixa.anel(1.0), plano(0.6), contorno=False, chanfro=0.3)
        t.pintar(e(R(5, 5, 10, 10)).anel(0.75), plano(0.12), contorno=False)
        # filete com contas
        t.pintar(e(R(14, 10.1, 46, 10.9)), plano(0.3), contorno=False)
        t.fill(e(R(14, 10.9, 46, 11.2)), "B0", alfa=0.6)
        for bx in (22.5, 34.5):
            cx_, cy_ = px(bx), py(10.5)
            t.pintar(P([(cx_, cy_ - 1.6), (cx_ + 1.6, cy_), (cx_, cy_ + 1.6), (cx_ - 1.6, cy_)]),
                     esfera(cx_, cy_, 1.6, 1.0, 0.1), esp=0.45, chanfro=0.2)
        t.pintar(E(px(46.5), py(10.5), 0.75), esfera(px(46.5), py(10.5), 0.75), contorno=False)
        # arco com contas no canto da área da ilustração
        acx, acy, ar = 6.0, 28.0, 13.0
        t.pintar(e(arco(acx, acy, ar, -2, 92, 0.95, 30)), lambda X, Y: 0.5, contorno=False, chanfro=0.2,
                 esp=0.4)
        for k in range(16):
            a = math.radians(4 + k * 5.5)
            t.fill(E(px(acx + math.cos(a) * (ar - 3)), py(acy + math.sin(a) * (ar - 3)), 0.32), "B0")
        for ang in (22, 45, 68):
            a = math.radians(ang)
            bx, by = px(acx + math.cos(a) * ar), py(acy + math.sin(a) * ar)
            t.pintar(E(bx, by, 1.15), esfera(bx, by, 1.15, 1.0, 0.1), esp=0.45)
        for (x, y, r) in ((19.6, 30.6, 0.75), (8.6, 41.6, 0.75)):
            t.pintar(E(px(x), py(y), r), esfera(px(x), py(y), r), contorno=False)
    cantos(orn)

    # miolo das caixas dos cantos
    for (ox, oy) in ((7.5, 7.5), (W - 7.5, 7.5), (7.5, H - 7.5), (W - 7.5, H - 7.5)):
        if kind == "major":
            t.cristal(ox, oy, 2.0, 3.0, aura=False, esp=0.45)
        elif kind == "cursed":
            lo = P([(ox, oy - 3), (ox + 3, oy), (ox, oy + 3), (ox - 3, oy)])
            t.fill(lo.cresce(0.4), "V0")
            t.pintar(lo, lambda X, Y: 0.75 - 0.35 * (X - ox + Y - oy) / 6, VIOLETA, contorno=False, chanfro=0.15)
            t.fill(L([(ox - 0.6, oy - 1.4), (ox + 0.2, oy - 2.0)], 0.45), "WH", glow=1.0)
        else:
            t.estrela(int(ox), int(oy), 2)
    if kind == "cursed":                                # espinhos violeta na borda de cima e de baixo
        for i in range(4):
            bx = 16 + i * 6
            for x in (bx + 1.0, W - 1 - bx):
                for (y0, dy) in ((0.0, 1), (H, -1)):
                    esp_ = P([(x - 1.2, y0 + dy * 2.6), (x, y0 + dy * 0.05), (x + 1.2, y0 + dy * 2.6)])
                    t.pintar(esp_, lambda X, Y: 0.7, VIOLETA, contorno=False, chanfro=0.25)

    # brasão no meio da borda de cima e de baixo
    for (cy, sy) in ((2.0, 1), (H - 2.0, -1)):
        meia = E(CX, cy, 7.0) & semiplano(0, -sy, -sy * cy)
        t.fill(meia, "K")
        aro_ = (E(CX, cy, 7.0) - E(CX, cy, 6.0)) & semiplano(0, -sy, -sy * (cy + sy * 0.6))
        t.pintar(aro_, plano(0.72), contorno=False, chanfro=0.2)
        lo = P([(CX, cy + sy * 0.5), (CX + 2.6, cy + sy * 3.0), (CX, cy + sy * 5.6), (CX - 2.6, cy + sy * 3.0)])
        t.pintar(lo, esfera(CX, cy + sy * 3.0, 2.6, 1.0, 0.12), contorno=False, chanfro=0.2)
    # losangos no meio das laterais
    for x0 in (5.5, W - 5.5):
        lo = P([(x0, CY - 5), (x0 + 3, CY), (x0, CY + 5), (x0 - 3, CY)])
        t.fill(lo, "K")
        t.pintar(lo.anel(0.8), plano(0.72), contorno=False, chanfro=0.2)
        t.pintar(E(x0, CY - 0.3, 1.0), esfera(x0, CY - 0.3, 1.0, 1.0, 0.1), contorno=False)

    # faixas do título e do nome
    for y0, txt in ((14, rotulo), (H - 28, nome)):
        x0, x1 = 12.0, W - 12.0
        faixa = P([(x0, y0), (x1, y0), (x1 + 4.6, y0 + 7), (x1, y0 + 14), (x0, y0 + 14), (x0 - 4.6, y0 + 7)])
        t.pintar(faixa, lambda X, Y: 0.74 - 0.3 * np.clip((Y - y0) / 14.0, 0, 1), contorno=True,
                 claro="B0", escuro="B0", esp=0.5, chanfro=0.2)
        dentro = faixa.cresce(-0.9)
        t.fill(dentro, lambda X, Y: rampa(("K", "G1", "G2"), 0.55 - 0.4 * np.abs((Y - (y0 + 7)) / 7.0)))
        for x in range(int(x0) + 1, int(x1) - 1):
            if x % 2 == 0:
                for yy in (y0 + 1.75, y0 + 12.25):
                    t.fill(E(x + 0.5, yy, 0.3), "B0")
        for sx, xb in ((-1, x0), (1, x1)):
            tx = xb + sx * 5.5
            t.pintar(P([(tx, y0 + 5.6), (tx + 1.4, y0 + 7), (tx, y0 + 8.4), (tx - 1.4, y0 + 7)]),
                     esfera(tx, y0 + 7, 1.4, 1.0, 0.1), esp=0.4)
            t.pintar(E(xb + sx * 1.6, y0 + 7, 0.6), esfera(xb + sx * 1.6, y0 + 7, 0.6), contorno=False)
        # os losangos ao lado do texto curto saem nas mesmas cartas que antes (largura da fonte de pixel)
        if not txt:
            largura, curto = 0, True
        elif all(c in "IVX" for c in txt):
            largura = numeral(t, txt, y0 + 3)
            curto = True
        else:
            curto = cp.largura_texto(txt) <= 62
            largura = texto(t, txt, y0 + 4.6, 5.0, 60.0 if curto else 78.0)
        if curto:                             # losangos ao lado do texto curto
            for sx in (-1, 1):
                xd = CX + sx * (largura / 2.0 + 7)
                lo = P([(xd, y0 + 3.5), (xd + 3, y0 + 7), (xd, y0 + 10.5), (xd - 3, y0 + 7)])
                t.pintar(lo.anel(0.7), plano(0.55), contorno=False, chanfro=0.2)
                t.pintar(E(xd, y0 + 7, 0.75), esfera(xd, y0 + 7, 0.75), contorno=False)


# ---------- ilustrações (mesmas formas e coordenadas do card_pixel.py) ----------

def il_pistao_runico(t):
    # rastro do impulso acima da tampa
    for x, topo in ((47, 44), (51, 39), (60, 39), (64, 44)):
        rastro = L([(x + 0.5, topo + 0.6), (x + 0.5, 54.0)], 0.85)
        t.fill(rastro, lambda X, Y, topo=topo: rampa(OURO, 0.3 + 0.5 * np.clip((Y - topo) / (54 - topo), 0, 1)),
               alfa=1.0)
        t.clarear(L([(x + 0.5, topo), (x + 0.5, topo + (54 - topo) * 0.45)], 1.0), "K", 0.55)
    t.estrela(55, 33, 4)
    t.estrela(44, 39, 1)
    t.estrela(67, 39, 1)

    cy_anel = 86.0
    anelm = E(CX, cy_anel, 27, 7.5) - E(CX, cy_anel, 24, 5.0)
    tras = anelm & semiplano(0, 1, cy_anel)
    t.fill(tras, lambda X, Y: rampa(CIANO, 0.12 + 0.12 * np.clip((cy_anel - Y) / 7, 0, 1)), glow=0.25)

    # haste e pé
    t.pintar(R(52, 108, 60, 132), cil_x(52, 60))
    t.pintar(R(49, 115, 63, 119), cil_x(49, 63), chanfro=0.1)
    t.pintar(R(36, 131, 76, 137, 0.6), linear(36, 131, 76, 137, 0.9, 0.12), chanfro=0.18)
    t.pintar(R(42, 128, 70, 131, 0.3), cil_x(42, 70, 0.9), chanfro=0.1)
    for x in (39, 72):
        t.rebite(x, 133)
    # corpo
    t.pintar(R(43, 68, 69, 106), cil_x(43, 69))
    for y in (74, 101):
        t.sulco_h(43.3, 68.7, y)
    for y in range(78, 98, 4):                      # gravação fina entre os sulcos
        t.clarear(R(44.5, y + 0.3, 49.5, y + 0.6), "B1", 0.5)
        t.clarear(R(62.5, y + 0.3, 67.5, y + 0.6), "B1", 0.5)
    # tirantes laterais
    for x0 in (39.0, 71.0):
        t.pintar(R(x0, 68, x0 + 2, 106, 0.4), cil_x(x0, x0 + 2, 0.9))
    # visor de cristal no corpo
    visor = R(51, 93, 61, 100, 0.8)
    t.contorno(visor, "B1", "B0", 0.8)
    t.pintar(visor, lambda X, Y: 0.15 + 0.62 * np.exp(-((Y - 96.6) / 1.7) ** 2), CIANO, contorno=False)
    t.fill(L([(53.2, 96.5), (58.8, 96.5)], 0.6), "C2", glow=1.0)
    t.fill(E(54.6, 96.4, 0.55), "WH", glow=1.0)
    t.clarear(R(51.5, 93.4, 60.5, 94.2, 0.3), "C2", 0.25)
    # flanges, tampa, botão
    for y0 in (64, 105):
        t.pintar(R(37, y0, 75, y0 + 4, 0.5), cil_x(37, 75), chanfro=0.15)
        for x in (41, 50, 61, 70):
            t.rebite(x, y0 + 1)
    t.pintar(R(46, 58, 66, 64, 0.4), cil_x(46, 66), chanfro=0.12)
    t.pintar(R(52, 54, 60, 58, 0.4), cil_x(52, 60), chanfro=0.12)
    for x in range(48, 65, 3):                       # runas gravadas na tampa
        g = L([(x + 0.5, 60.6), (x + 0.5, 62.0)], 0.5)
        t.fill(g, "C2" if x == 54 else "C1", glow=1.0)
    t.fill(E(55.5, 61.3, 0.45), "WH", glow=1.0)
    # anel rúnico: metade da frente
    frente = anelm & semiplano(0, -1, -cy_anel)
    t.pintar(frente, plano(0.5), CIANO, chanfro=0.35,
             claro="C0", escuro="C0", esp=0.5)
    t.fill(arco(CX, cy_anel, 25.6, 8, 172, 0.35, 60, 6.3), "C2", glow=1.0, alfa=0.6)
    for i, x in enumerate(range(33, 80, 4)):          # runas no anel
        xr = x + 0.5
        dxn = (xr - CX) / 27.0
        if abs(dxn) >= 1:
            continue
        yb = cy_anel + 7.5 * math.sqrt(1 - dxn * dxn)
        s = 1 if i % 2 else -1
        t.fill(L([(xr, yb - 0.9), (xr, yb - 2.2), (xr + s * 0.9, yb - 2.9)], 0.42), "WH", glow=1.0)
    t.halo_arcano(frente | tras, 2, 90)


def il_sopro_caldeira(t):
    # cone de vapor saindo do bico, com runas
    bolhas = [(92.0, 71.0, 2.4), (93.0, 65.0, 3.0),
              (88.0, 57.0, 5.0), (81.0, 49.0, 6.5), (71.0, 43.0, 7.5), (59.0, 44.0, 6.5), (49.0, 49.0, 5.0),
              (40.0, 54.0, 3.6), (57.0, 54.0, 5.0), (69.0, 54.0, 6.0), (80.0, 59.0, 4.5)]
    t.vapor(bolhas[:2])
    t.massa_vapor(bolhas[2:])
    for i, (x, y) in enumerate(((57, 45), (70, 40), (81, 49))):
        t.runa(x, y, i)
    # fornalha: caixa com grade e chamas
    t.pintar(R(32, 137, 66, 142, 0.5), linear(32, 137, 66, 142), chanfro=0.18)
    caixa = R(35, 121, 63, 137, 0.4)
    t.pintar(caixa, linear(35, 121, 63, 137, 0.8, 0.0), chanfro=0.15)
    porta = R(39, 125, 59, 135, 0.3)
    t.contorno(porta, "B0", "B3", 0.8)
    t.fill(porta, lambda X, Y: rampa(("K", "B0", "B1"), np.clip((Y - 126) / 9.0, 0, 1) * 0.9))
    t.luz_radial(49, 134, 6.5, "B2", 0.45, dentro=porta)
    for cx, h, w, inc in ((44, 8, 2.2, -1), (49, 10, 2.6, 1), (54, 8, 2.2, 1)):
        t.chama(cx, 135, h, w, inc)
    for x in range(40, 59, 3):                     # grade
        t.pintar(R(x + 0.15, 125.3, x + 0.85, 135), cil_x(x + 0.15, x + 0.85, 0.55), contorno=False)
    t.pintar(R(39.3, 131.2, 58.7, 132.0), cil_y(131.2, 132.0, 0.55), contorno=False)
    for x in (37, 61):
        t.rebite(x, 123)
        t.rebite(x, 134)
    # bico (cano inclinado)
    ang = math.atan2(76 - 102, 84 - 64)
    cano = L([(64, 102), (84, 76)], 7.0)
    t.pintar(cano, local(cil_x(-3.5, 3.5), 74, 89, ang + math.pi / 2))
    nx, ny = math.cos(ang + math.pi / 2), math.sin(ang + math.pi / 2)
    for f in (0.3, 0.62):                          # anéis do cano
        px, py = 64 + 20 * f, 102 - 26 * f
        anelc = L([(px - nx * 3.7, py - ny * 3.7), (px + nx * 3.7, py + ny * 3.7)], 1.3)
        t.pintar(anelc, local(cil_x(-3.7, 3.7), px, py, ang + math.pi / 2), chanfro=0.1, esp=0.4)
    boca = L([(81, 72), (90, 79)], 3.6)
    t.pintar(boca, linear(80, 70, 92, 80), chanfro=0.15)
    # caldeira
    cx, cy, r = 49.0, 104.0, 19.0
    corpo = E(cx, cy, r)
    t.pintar(corpo, esfera(cx, cy, r))
    cinta = corpo & R(0, 101, W, 106)
    fe = esfera(cx, cy, r)
    t.pintar(cinta, lambda X, Y: fe(X, Y) * 0.8 + 0.02, contorno=False, chanfro=0.2)
    t.fill(corpo & R(0, 106, W, 106.8), "B0")
    for i in range(-3, 4):
        x = int(cx + i * 5.2)
        if abs(x + 0.5 - cx) < r - 2:
            t.rebite(x, 102)
    for a in range(200, 341, 28):                 # rebites da cúpula
        ra = math.radians(a)
        t.rebite(int(cx + math.cos(ra) * 14), int(cy - 2 + math.sin(ra) * 12))
    # tampa
    t.pintar(R(41, 83, 57, 87, 0.4), cil_x(41, 57), chanfro=0.12)
    t.pintar(R(46, 79, 52, 83, 0.3), cil_x(46, 52), chanfro=0.1)
    t.pintar(E(49, 77.8, 1.3), esfera(49, 77.8, 1.3), esp=0.5)
    # manômetro na caldeira
    mx, my = cx - 8, cy + 9
    t.pintar(E(mx, my, 4.6), esfera(mx, my, 4.6, 1.1))
    t.fill(E(mx, my, 3.1), lambda X, Y: rampa(("K", "G2"), 0.6 - 0.12 * np.hypot(X - mx, Y - my)))
    for k in range(7):
        a = math.radians(150 + k * 40)
        t.fill(L([(mx + math.cos(a) * 2.2, my + math.sin(a) * 2.2), (mx + math.cos(a) * 2.8, my + math.sin(a) * 2.8)],
                 0.3), "B2")
    t.fill(L([(mx, my), (43.4, 111.4)], 0.45), "B4")
    t.fill(E(mx, my, 0.55), "B3")
    t.fill(E(43.5, 110.5, 0.55), "C2", glow=1.0)
    t.luz_radial(43.5, 110.5, 1.4, "C1", 0.35, glow=0.6)


def il_arco_voltaico(t):
    # base
    t.pintar(R(30, 134, 82, 140, 0.5), linear(30, 134, 82, 140), chanfro=0.18)
    t.pintar(R(40, 126, 72, 134, 0.4), cil_x(40, 72), chanfro=0.12)
    for x in (33, 78):
        t.rebite(x, 136)
    for x in (44, 67):
        t.rebite(x, 129)
    # coluna
    t.pintar(R(47, 78, 65, 127), cil_x(47, 65, 0.55, -0.02))
    # bobina: espiras com frente clara e costas escuras
    for i in range(9):
        cy = 122.5 - i * 5.0
        espira = E(CX, cy, 17.5, 3.6) - E(CX, cy - 0.8, 15.2, 2.2)
        costas = espira & semiplano(0, 1, cy) & (R(-10, 0, 47, H) | R(65, 0, W + 10, H))
        frente = espira & semiplano(0, -1, -(cy - 0.5))
        t.fill(costas, lambda X, Y: rampa(OURO, 0.2 + 0.08 * np.clip((X - 38) / 36, 0, 1)))
        t.fill(mover(frente, 0, 0.9) - espira, "B0", alfa=0.9)
        t.pintar(frente, cil_x(38.5, 73.5), contorno=False, chanfro=0.12)
    # colar e esfera
    t.pintar(R(50, 74, 62, 80, 0.4), cil_x(50, 62), chanfro=0.1)
    t.pintar(E(CX, 64.0, 12.0), esfera(CX, 64.0, 12.0))
    esf = E(CX, 64.0, 12.0)                         # costura do equador da esfera (antes um arco de 1 px)
    t.fill(arco(CX, 65.2, 11.6, 0, 180, 0.6, 40, 3.2) & esf.cresce(-0.3), "B1")
    t.clarear(arco(CX, 66.0, 11.4, 10, 170, 0.3, 40, 3.2) & esf.cresce(-0.3), "B4", 0.35)
    t.cristal(CX, 51.0, 4.5, 8.5)
    # descargas
    t.raio(P5([(47, 57), (40, 52), (37, 45), (30, 42), (25, 35)]))
    t.raio(P5([(65, 58), (72, 55), (75, 48), (82, 46), (86, 39)]))
    t.raio(P5([(58, 44), (61, 40), (57, 36), (59, 33)]), ponta=False)
    t.estrela(59, 33, 2, ("WH", "C2", "C1", "C0"))
    t.raio(P5([(44, 70), (36, 74), (33, 81)]), ponta=False)
    t.raio(P5([(68, 69), (75, 72), (78, 79)]), ponta=False)


def il_mina_engrenagem(t):
    # espinhos com bolas, saindo pelos vãos dos dentes
    for i in range(6):
        a = math.radians(30 + i * 60)
        ca, sa = math.cos(a), math.sin(a)
        haste = L([(CX + ca * 20, CY + sa * 20), (CX + ca * 32, CY + sa * 32)], 3.0)
        t.pintar(haste, linear(CX - 34, CY - 34, CX + 34, CY + 34, 1.0, 0.05), chanfro=0.12)
        cx2, cy2 = CX + ca * 28.5, CY + sa * 28.5
        t.pintar(L([(cx2 - sa * 2.5, cy2 + ca * 2.5), (cx2 + sa * 2.5, cy2 - ca * 2.5)], 2.0),
                 linear(CX - 34, CY - 34, CX + 34, CY + 34), chanfro=0.1)
        bx, by = CX + ca * 35.0, CY + sa * 35.0
        t.pintar(E(bx, by, 3.2), esfera(bx, by, 3.2))
    # engrenagem: 12 dentes, vão em cima e embaixo (simétrica)
    eng = engrenagem(CX, CY, 21.0, 12, 4.5, fase=0.5)
    t.pintar(eng, linear(CX - 26, CY - 26, CX + 26, CY + 26, 1.05, 0.05), chanfro=0.2)
    t.fill(anel(CX, CY, 16.3, 17.0), "B0")
    t.fill(anel(CX, CY, 17.2, 17.9) & semiplano(1, 1, CX + CY - 2), "B3", alfa=0.9)
    for i in range(6):                               # furos de alívio
        a = math.radians(i * 60)
        hx, hy = CX + math.cos(a) * 19.5, CY + math.sin(a) * 19.5
        t.fill(E(hx, hy, 1.5), "K")
        t.fill((E(hx, hy, 2.1) - E(hx, hy, 1.5)) & semiplano(0, -1, -hy - 0.3), "B3", alfa=0.9)
    # anel interno com parafusos
    inn = E(CX, CY, 15.0)
    t.pintar(inn, linear(CX - 15, CY - 15, CX + 15, CY + 15, 0.85, 0.0), chanfro=0.2, contorno=False)
    t.clarear(anel(CX, CY, 10.3, 10.7), "B1", 0.6)
    for i in range(10):
        a = i * math.tau / 10
        t.rebite(int(CX - 0.5 + math.cos(a) * 12.3), int(CY - 0.5 + math.sin(a) * 12.3))
    # soquete do cristal
    soq = E(CX, CY, 9.0)
    t.fill(soq, lambda X, Y: rampa(("K", t.arc[0]), 0.55 - 0.05 * np.hypot(X - CX, Y - CY)))
    t.fill(soq.anel(0.85), lambda X, Y: rampa(("B0", "B3"), smooth(-4, 4, (X - CX) + (Y - CY))))
    t.cristal(CX, CY, 5.0, 8.5, aura=False)
    # pontos de luz arcana nos vãos (a mina está armada)
    for i in range(6):
        a = math.radians(i * 60)
        px, py = CX + math.cos(a) * 24.5, CY + math.sin(a) * 24.5
        t.luz_radial(px, py, 1.6, "C1", 0.45, glow=0.7, so_fundo=True)
        t.fill(E(px, py, 0.62), "C2", glow=1.0)
        t.fill(E(px - 0.15, py - 0.15, 0.28), "WH", glow=1.0)


def il_broquel_cantante(t):
    # ondas sonoras dos dois lados
    for k, r in enumerate((32.0, 36.5, 41.0)):
        lim = 28 - k * 4
        reg = t.regiao((CX - r - 1, CY - r - 1, CX + r + 1, CY + r + 1))
        sl, X, Y = reg
        d = np.abs(np.hypot(X - CX, Y - CY) - r) - 0.55
        ang = np.degrees(np.abs(np.arctan2(Y - CY, np.abs(X - CX))))
        solido = np.clip((lim - 7 - ang) * 1.5, 0, 1)
        fase = np.mod(ang - (lim - 7), 2.4)
        pont = ((fase > 0.5) & (fase < 1.7) & (ang >= lim - 7) & (ang < lim)) * 1.0
        cob = t.cob(d) * np.maximum(solido, pont)
        c = rampa(("C1", "C2", "WH"), np.clip(1 - ang / lim, 0, 1) * 0.75)
        t.halo_arcano(anel(CX, CY, r - 0.6, r + 0.6) & (R(CX + 25, CY - 16, W, CY + 16) | R(0, CY - 16, CX - 25, CY + 16)),
                      1.6, 70)
        t.compor(sl, cob, c, 1.0, True)
    # escudo
    R0 = 27.0
    t.pintar(E(CX, CY, R0), esfera(CX, CY, R0, 1.0, 0.05))
    t.fill(anel(CX, CY, 21.8, 22.6), "B0")
    fe = esfera(CX, CY, 22.5, 0.8, -0.05)

    def gomos(X, Y):
        th = np.arctan2(Y - CY, X - CX)
        rr = np.hypot(X - CX, Y - CY)
        s = np.clip(np.sin(th * 8) * rr / 8 * K * 0.5, -1, 1)
        return fe(X, Y) + 0.12 * s
    t.pintar(E(CX, CY, 21.8), gomos, contorno=False)
    for i in range(16):                            # tachas no aro
        a = i * math.tau / 16 + math.tau / 32
        t.rebite(int(CX - 0.5 + math.cos(a) * 24.8), int(CY - 0.5 + math.sin(a) * 24.8))
    # anel e cubo central
    t.fill(anel(CX, CY, 12.8, 13.7), "B0")
    t.pintar(anel(CX, CY, 10.5, 13.0), esfera(CX, CY, 13.0, 1.0, 0.05), contorno=False, chanfro=0.15)
    cubo = E(CX, CY, 10.5)
    t.fill(cubo, lambda X, Y: rampa(("K", t.arc[0]), 0.6 - 0.05 * np.hypot(X - CX, Y - CY)))
    t.fill(cubo.anel(0.7), "B0")
    t.cristal(CX, CY, 5.5, 8.5, aura=False)


def il_tonico_oleo_luz(t):
    cx, cy, r = CX, 111.0, 21.0
    # tampa de latão, gargalo e boca
    t.pintar(R(48, 61, 64, 68, 0.4), cil_x(48, 64), chanfro=0.12)
    t.pintar(R(52, 56, 60, 61, 0.3), cil_x(52, 60), chanfro=0.1)
    t.cristal_pequeno(CX, 52.5)
    for x in (50, 61):
        t.rebite(x, 63)
    t.pintar(R(45, 68, 67, 72, 0.5), cil_x(45, 67), chanfro=0.15)
    # vidro: bulbo + gargalo
    vidro = R(49, 72, 63, 92) | E(cx, cy, r)
    t.contorno(vidro, "B2", "B1", 0.7)
    t.fill(vidro, lambda X, Y: rampa(("K", "G1", "G2"), 0.3 + 0.25 * np.exp(-((X - 46) ** 2 + (Y - 100) ** 2) / 120)))
    t.pintar(vidro.anel(0.9), plano(0.62), contorno=False, chanfro=0.3)
    # óleo luminoso
    sup = F(lambda X, Y: (107 + np.sin(X * 0.55) * 1.2) - Y, TUDO)
    oleo = E(cx, cy, r - 1.0) & sup
    t.fill(oleo, lambda X, Y: rampa(OURO, 0.88 - np.hypot(X - cx, Y - 117) / 16.0 * 0.7))
    t.fill(oleo & F(lambda X, Y: Y - (107 + np.sin(X * 0.55) * 1.2) - 0.8, TUDO), "B4")
    t.luz_radial(cx, 118, 8.0, "B4", 0.35, dentro=oleo)
    # cristal suspenso
    t.luz_radial(cx, 118, 6.5, "C1", 0.45, dentro=oleo, glow=0.5)
    t.cristal(cx, 118.0, 4.5, 7.5, aura=False)
    # bolhas
    for bx, by, grande in ((45, 113, 1), (66, 116, 1), (61, 109, 0), (49, 125, 0), (63, 125, 1), (52, 110, 0)):
        if grande:
            t.fill(anel(bx + 0.5, by + 0.5, 0.75, 1.35), "B4")
            t.fill(E(bx + 0.5, by + 0.5, 0.75), "C1", glow=1.0)
            t.fill(E(bx + 0.2, by + 0.2, 0.3), "WH", glow=1.0)
        else:
            t.fill(E(bx + 0.5, by + 0.5, 0.6), "B4")
    # reflexo no vidro
    t.clarear(arco(cx, cy, 16, 196, 250, 0.8), "B4", 0.85)
    t.clarear(L([(51.5, 75.2), (51.5, 88.5)], 0.75), "B4", 0.8)
    # etiqueta de latão no gargalo
    t.pintar(R(49, 83, 63, 86), cil_x(49, 63, 0.9), chanfro=0.1, contorno=False)
    t.fill(R(49, 86, 63, 86.5), "B0", alfa=0.7)


def il_granada_cristal(t):
    cx, cy, r = CX, 106.0, 22.0
    # pavio e faísca
    pav = spline(P5([(57, 78), (54, 72), (57, 67), (61, 63)]), 8)
    t.pintar(L(pav, 1.5), lambda X, Y: 0.5 + 0.05 * np.sin(Y * 3.0), contorno=True, esp=0.4, chanfro=0.25)
    for k in range(0, len(pav) - 1, 2):            # fio trançado
        x, y = pav[k]
        t.fill(E(x + 0.2, y, 0.3), "B1", alfa=0.8)
    t.estrela(62, 58, 4)
    for x, y in ((67, 54), (68, 61), (57, 54), (70, 57)):
        t.estrela_c(x + 0.5, y + 0.5, 1.1, ("B4", "B3", "B3", "B2"), brilho=0.15)
    # argola
    t.pintar(anel(73.0, 80.0, 3.0, 5.0), esfera(73, 80, 5))
    corpo = E(cx, cy, r)
    t.pintar(corpo, esfera(cx, cy, r))

    def curva(X):
        return cy + 3 - 3 * ((X - cx) / r) ** 2
    cinta = F(lambda X, Y: np.abs(Y - curva(X)) - 2.1, TUDO) & corpo
    fe = esfera(cx, cy, r)
    t.pintar(cinta, lambda X, Y: fe(X, Y) * 0.75 + 0.1, contorno=False, chanfro=0.18)
    t.fill(F(lambda X, Y: np.abs(Y - curva(X) - 2.55) - 0.4, TUDO) & corpo, "B0", alfa=0.9)
    for i in range(-3, 4):
        x = int(cx + i * 5.5)
        if abs(x + 0.5 - cx) < r - 1.5:
            t.rebite(x, int(round(curva(x + 0.5) - 2.1 + 0.4)))
    # tampa
    t.pintar(R(47, 78, 65, 86, 0.4), cil_x(47, 65), chanfro=0.12)
    t.pintar(R(44, 84, 68, 87, 0.4), cil_x(44, 68), chanfro=0.12)
    # rachaduras arcanas
    rach = [
        [(46, 96), (51, 101), (48, 108), (54, 114), (52, 120)],
        [(67, 94), (62, 101), (67, 108), (64, 117), (68, 122)],
        [(51, 101), (56, 104), (62, 101)],
        [(48, 108), (40, 110)],
    ]
    for c in rach:
        t.clarear(L(P5(c), 3.4) & corpo, "C1", 0.18, glow=0.35)
    for c in rach:
        t.fill(L(P5(c), 1.9) & corpo, "B0")
    for c in rach:
        t.fill(L(P5(c), 0.95), "C2", glow=1.0)
        t.fill(L(P5(c), 0.38), "WH", glow=1.0)
    t.cristal(56.0, 109.0, 4.0, 6.5, aura=False)


def il_caldeira_interna(t):
    # cajado
    t.pintar(R(53, 56, 59, 156), cil_x(53, 59))
    t.pintar(P([(53, 155), (59, 155), (56, 162)]), cil_x(53, 59))
    for y0 in (66, 136, 143):
        t.pintar(R(50, y0, 62, y0 + 4, 0.4), cil_x(50, 62), chanfro=0.12)
    # garras segurando o cristal
    for sx in (-1, 1):
        garra = L(spline([(CX + sx * 2, 58), (CX + sx * 7, 50), (CX + sx * 6, 40), (CX + sx * 3, 35)], 8), 2.2)
        t.pintar(garra, linear(40, 30, 72, 62), chanfro=0.12)
        t.pintar(E(CX + sx * 3.1, 35.2, 1.2), esfera(CX + sx * 3.1, 35.2, 1.2), esp=0.4)
    # raios do cristal
    for i in range(16):
        a = i * math.tau / 16
        r0, r1 = 13, (21 if i % 2 == 0 else 17)
        t.raio_luz((CX + math.cos(a) * r0, 45.5 + math.sin(a) * r0), (CX + math.cos(a) * r1, 45.5 + math.sin(a) * r1),
                   "C1", "C0", 0.42, 0.3, glow=0.8)
    t.cristal(CX, 45.0, 5.5, 10.0)
    # caldeira-coração
    cx, cy, r = CX, 104.0, 18.0
    t.pintar(E(cx, cy, r), esfera(cx, cy, r))
    for i in range(14):
        a = i * math.tau / 14
        t.rebite(int(cx - 0.5 + math.cos(a) * 15.5), int(cy - 0.5 + math.sin(a) * 15.5))
    # canos laterais com escape de vapor
    for sx in (-1, 1):
        x0 = cx + sx * 18
        t.pintar(R(min(x0, x0 + sx * 8), 100, max(x0, x0 + sx * 8), 104), cil_y(100, 104))
        fl = cil_y(97, 107, 0.3, 0.45)
        t.pintar(R(min(x0 + sx * 8, x0 + sx * 11), 97, max(x0 + sx * 8, x0 + sx * 11), 107, 0.4), fl, chanfro=0.2)
        vx = int(CX + sx * 28.5 - 0.5) + 0.5
        fio = [(vx + math.sin(k * 0.7) * 1.2 * sx, 95.5 - k) for k in range(13)]
        for k in range(len(fio) - 1):
            a0 = 1.0 if k < 6 else max(0.0, 1 - (k - 6) / 6)
            t.fill(L(fio[k:k + 2], 0.8 - 0.03 * k), "B2" if k < 4 else "B1", alfa=a0, ocupa=False)
    # manômetro
    t.pintar(E(cx, cy, 11.0), esfera(cx, cy, 11.0, 1.0, 0.15), claro="B0", escuro="B0")
    t.fill(E(cx, cy, 8.5), lambda X, Y: rampa(("K", "G2"), 0.55 - 0.05 * np.hypot(X - cx, Y - cy)))
    t.fill(anel(cx, cy, 8.0, 8.5), "B0")
    for i in range(9):
        a = math.radians(150 + i * 30)
        p0 = (cx + math.cos(a) * 6.1, cy + math.sin(a) * 6.1)
        p1 = (cx + math.cos(a) * 7.6, cy + math.sin(a) * 7.6)
        if i >= 5:
            t.fill(L([p0, p1], 0.6), "C2", glow=1.0)
        else:
            t.fill(L([p0, p1], 0.6), "B3" if i % 2 == 0 else "B2")
    t.fill(arco(cx, cy, 5.2, 300, 390, 0.4), "C1", glow=0.8)       # zona arcana do mostrador
    t.fill(L([(55.5, 103.5), (60.6, 98.4)], 0.65), "B4")
    t.fill(E(55.6, 103.4, 1.0), "B3")
    # bocais embaixo da caldeira, com o calor do golpe virando energia
    for x0 in (47.0, 63.0):
        t.pintar(R(x0 - 2, 121, x0 + 1, 127, 0.3), cil_x(x0 - 2, x0 + 1))
        for k in range(4):
            px = int(x0) - 1 + (k % 2) + 0.5
            py = 129 + k * 2 + 0.5
            t.luz_radial(px, py, 1.2, "C1", 0.3, glow=0.5, so_fundo=True)
            t.fill(E(px, py, 0.62 - 0.06 * k), "C1" if k < 2 else "C0", glow=1.0 if k < 2 else 0.5)


def il_mola_recuo(t):
    # setas laterais de recuo
    for x in (23, 88):
        xc = x + 0.5
        t.fill(L([(xc, 77), (xc, 92)], 0.8), lambda X, Y: rampa(OURO, 0.62 - 0.2 * np.clip((Y - 77) / 23, 0, 1)))
        for k in range(8):
            y = 93.5 + k * 4
            if y > 123:
                break
            t.fill(L([(xc, y), (xc, y + 1.6)], 0.7), "B2" if y < 100 else "B1", alfa=1 - k * 0.08)
        t.pintar(P([(xc, 72.6), (xc + 3.6, 78.2), (xc - 3.6, 78.2)]), lambda X, Y: 0.85 - 0.4 * (Y - 72.6) / 6,
                 esp=0.45, chanfro=0.2)
    # placa de baixo
    t.pintar(R(30, 130, 82, 136, 0.5), linear(30, 130, 82, 136), chanfro=0.18)
    t.pintar(R(36, 136, 42, 140, 0.3), cil_x(36, 42))
    t.pintar(R(70, 136, 76, 140, 0.3), cil_x(70, 76))
    # mola em hélice: costas escuras primeiro, depois a frente iluminada
    voltas, y0, y1, rx = 5.5, 128.0, 70.0, 19.0
    passos = 600
    tras, frente = [], []
    atual, lado = [], None
    for i in range(passos + 1):
        u = i / passos
        a = u * voltas * math.tau
        p = (CX + math.cos(a) * rx, y0 + (y1 - y0) * u + math.sin(a) * 3.0)
        ef = math.sin(a) > 0
        if lado is None or ef == lado:
            atual.append(p)
        else:
            atual.append(p)
            (frente if lado else tras).append(atual)
            atual = [p]
        lado = ef
    (frente if lado else tras).append(atual)
    for seg in tras:
        if len(seg) > 1:
            t.pintar(L(seg[::3] + [seg[-1]], 3.4), lambda X, Y: 0.28 - 0.08 * np.clip((X - CX) / rx, -1, 1),
                     contorno=False)
    fr = None
    for seg in frente:
        if len(seg) > 1:
            s_ = L(seg[::3] + [seg[-1]], 3.4)
            fr = s_ if fr is None else (fr | s_)
    t.pintar(fr, lambda X, Y: 0.95 - np.abs((X - CX) / rx + 0.35) * 0.55, chanfro=0.3)
    # placa de cima e suporte do cristal
    t.pintar(R(34, 64, 78, 70, 0.5), linear(34, 64, 78, 70), chanfro=0.18)
    for x in (37, 74):
        t.rebite(x, 66)
        t.rebite(x, 132)
    t.pintar(P([(48, 64), (51, 57), (61, 57), (64, 64)]), cil_x(48, 64), chanfro=0.1)
    t.cristal(CX, 47.0, 5.0, 9.5)


def il_manopla_pistonada(t):
    # antebraço
    t.pintar(R(41, 121, 71, 153, 0.6), cil_x(41, 71))
    for y0 in (127, 146):
        t.pintar(R(40, y0, 72, y0 + 3, 0.4), cil_x(40, 72), chanfro=0.12)
    for x in range(44, 70, 5):
        t.rebite(x, 137)
    # punho
    t.pintar(R(43, 112, 69, 122), cil_x(43, 69, 0.75), chanfro=0.1)
    for y in (115, 118):
        t.sulco_h(44, 68, y)
    # pistões nas laterais do braço, ligados à mão
    for x0 in (36.0, 76.0):
        t.pintar(R(x0 - 1.5, 106, x0 + 1.5, 130), cil_x(x0 - 1.5, x0 + 1.5, 1.05))
        t.pintar(R(x0 - 3.5, 129, x0 + 3.5, 149, 0.3), cil_x(x0 - 3.5, x0 + 3.5))
        for y0 in (129, 146):
            t.pintar(R(x0 - 4.5, y0, x0 + 4.5, y0 + 3, 0.4), cil_x(x0 - 4.5, x0 + 4.5), chanfro=0.12)
        t.pintar(E(x0, 150.5, 2.0), esfera(x0, 150.5, 2.0))
    # polegar
    pol = L(spline([(31, 108), (28, 99), (31, 90)], 8), 7.0)
    t.pintar(pol, linear(22, 85, 36, 112), chanfro=0.15)
    t.sulco_h(25.5, 32.8, 99, "B0")
    t.rebite(28, 101)
    # dorso da mão
    t.pintar(R(35, 82, 77, 112, 1.2), linear(35, 82, 77, 112, 1.0, 0.06), chanfro=0.2)
    t.sulco_h(36, 76, 89, "B0")
    for x in (36, 76):
        t.pintar(E(x, 107.5, 2.6), esfera(x, 107.5, 2.6))
    # dedos
    for i in range(4):
        x0 = 37.0 + i * 10
        d = R(x0, 59, x0 + 8, 84, 1.0) | R(x0, 63, x0 + 8, 84)
        t.pintar(d, cil_x(x0, x0 + 8), chanfro=0.08)
        for y in (67, 75):
            t.sulco_h(x0 + 0.8, x0 + 7.2, y, "B0")
            t.fill(E(x0 + 2.6, y + 1.6, 0.45), "B4")
        t.pintar(E(x0 + 4, 86.0, 3.0), esfera(x0 + 4, 86.0, 3.0), contorno=False)
    # engrenagem no dorso com cristal
    eng = engrenagem(CX, 100.0, 7.5, 10, 3.0, fase=0.5)
    t.pintar(eng, linear(44, 88, 68, 112, 1.0, 0.1), chanfro=0.2)
    soq = E(CX, 100.0, 5.0)
    t.fill(soq, lambda X, Y: rampa(("K", t.arc[0]), np.full_like(X, 0.5)))
    t.fill(soq.anel(0.7), lambda X, Y: rampa(("B0", "B3"), smooth(-3, 3, (X - CX) + (Y - 100))))
    t.cristal(CX, 100.0, 3.0, 5.0)
    for x, y in ((39, 93), (72, 93)):
        t.rebite(x, y)


def il_lente_prismatica(t):
    cy = 90.0
    # feixe que entra
    t.fill(R(17, 89, 34, 90), "B3")
    t.fill(R(17, 90, 34, 91), "B2")
    t.clarear(R(17, 89.2, 34, 89.6), "B4", 0.6)
    for x in range(10, 17):
        if x % 2 == 0:
            t.fill(R(x, 89, x + 1, 91, 0.3), "B2", alfa=0.5 + (x - 10) * 0.07)
    # feixes que saem (espectro)
    for k, ang in enumerate((-0.42, -0.21, 0.0, 0.21, 0.42)):
        p0 = (79.5, cy)
        p1 = (101.5, cy + math.tan(ang) * 22)
        c = ("C2", "B3", "WH", "B3", "C2")[k]
        arc_ = c in ("C2", "WH")
        t.raio_luz(p0, p1, c, c, 0.55, 0.55, glow=1.0 if arc_ else 0.0, so_fundo=False)
    # cabo
    cabo = R(52, 112, 60, 150)
    t.pintar(cabo, cil_x(52, 60))
    for y in range(119, 147, 4):
        t.fill(L([(52, y + 0.5), (60, y + 2.5)], 0.5) & cabo, "B0")
        t.clarear(L([(52, y + 1.1), (60, y + 3.1)], 0.25) & cabo, "B4", 0.35)
    t.pintar(R(49, 111, 63, 117, 0.4), cil_x(49, 63), chanfro=0.12)
    t.pintar(E(CX, 152.0, 4.5), esfera(CX, 152.0, 4.5))
    # aro da lente
    t.pintar(anel(CX, cy, 18.5, 24.0), esfera(CX, cy, 24.0, 1.0, 0.08))
    t.fill(anel(CX, cy, 18.5, 19.4), lambda X, Y: rampa(("B0", "B2"), smooth(-6, 6, (X - CX) + (Y - cy))))
    t.clarear(anel(CX, cy, 21.0, 21.4), "B1", 0.5)
    for i in range(8):
        a = i * math.tau / 8 + math.tau / 16
        t.rebite(int(CX - 0.5 + math.cos(a) * 21.3), int(cy - 0.5 + math.sin(a) * 21.3))
    # vidro e cristal grande
    vidro = E(CX, cy, 18.5)
    t.fill(vidro, lambda X, Y: rampa(("K", "G1", "C0"), 0.35 + 0.35 * np.exp(-((X - CX) ** 2 + (Y - cy) ** 2) / 150)))
    m = t.cristal(CX, cy, 9.0, 14.0, aura=False)
    t.clarear(arco(CX, cy, 15, 200, 245, 0.8) - m.cresce(0.8), "B4", 0.9)
    t.clarear(arco(CX, cy, 15, 30, 60, 0.5) - m.cresce(0.8), "C2", 0.35)


def il_lamina_sedenta(t):
    ang = math.radians(20)
    ox, oy = 56.0, 100.0

    def g(pts):
        return girar(pts, ox, oy, ang)

    # espinhos violeta em volta
    for i in range(9):
        a = math.radians(200 + i * 17.5)
        ca, sa = math.cos(a), math.sin(a)
        b0 = (CX + ca * 40.8 - sa * 0.75, CY + sa * 40.8 + ca * 0.75)
        b1 = (CX + ca * 40.8 + sa * 0.75, CY + sa * 40.8 - ca * 0.75)
        tip = (CX + ca * 46.5, CY + sa * 46.5)
        esp_ = P([b0, tip, b1])
        t.halo_arcano(esp_, 1.5, 70)
        t.fill(esp_, lambda X, Y: rampa(VIOLETA, 0.75 - 0.4 * np.clip((np.hypot(X - CX, Y - CY) - 41) / 5.5, 0, 1)),
               glow=1.0)
    # lâmina
    lam = P(g([(-6.5, 16), (-6.5, -38), (0, -64), (6.5, -38), (6.5, 16)]))
    serr = None
    for yy in (-28, -16, -4, 8):
        d = P(g([(6, yy), (10.5, yy - 5), (6, yy - 7)]))
        serr = d if serr is None else (serr | d)
    lam_tudo = lam | serr

    def luz_lamina(lx, ly):
        aresta = smooth(-0.9, -0.3, lx) * (1 - smooth(0.3, 0.9, lx))
        v = np.where(lx < 0, 0.88, 0.6) + aresta * 0.12
        v = v - smooth(5.8, 6.6, lx) * 0.15
        return v + 0.06 * np.clip(-ly / 64, 0, 1)
    t.pintar(lam_tudo, local(luz_lamina, ox, oy, ang), chanfro=0.12)
    t.fill(L(g([(0, -58), (0, 12)]), 0.35), "B4", alfa=0.5)           # fio central
    # veias violeta (a lâmina tem sede)
    veia = spline(g([(1, -50), (3, -42), (1, -33), (4, -24), (2, -14), (4, -5), (2, 4), (3, 12)]), 6)
    t.fill(L(veia, 1.3) & lam, "V0", alfa=0.8)
    t.fill(L(veia, 0.75), "V1", glow=1.0)
    t.fill(L(veia, 0.3), "V2", glow=1.0)
    for k in (1, 3, 5):                                 # ramos
        a, b = veia[k * 6], g([(4.8 if k % 2 else -2.5, -50 + k * 9 + 4)])[0]
        t.fill(L([a, b], 0.45) & lam, "V1", glow=1.0)
    # guarda
    guarda = P(g([(-17, 15), (17, 15), (14, 21), (-14, 21)]))
    t.pintar(guarda, linear(30, 100, 80, 125), chanfro=0.2)
    for sx in (-1, 1):
        bx, by = g([(sx * 18.5, 17.5)])[0]
        t.pintar(E(bx, by, 2.8), esfera(bx, by, 2.8))
    # cabo com tiras
    cabo = P(g([(-3.2, 21), (3.2, 21), (3.2, 41), (-3.2, 41)]))
    t.pintar(cabo, local(cil_x(-3.2, 3.2), ox, oy, ang))
    for k in range(5):
        tira = L(g([(-3.2, 24.2 + k * 4), (3.2, 22.2 + k * 4)]), 0.7)
        t.fill(tira & cabo, "B0")
        t.clarear(mover(tira, 0.0, 0.6) & cabo, "B4", 0.3)
    px, py = g([(0, 45)])[0]
    t.pintar(E(px, py, 4.2), esfera(px, py, 4.2))
    t.pintar(E(px, py, 1.4), lambda X, Y: 0.7 - 0.3 * ((X - px) + (Y - py)) / 2.8, VIOLETA, contorno=True,
             claro="V0", escuro="V0", esp=0.4)
    gx, gy = g([(0, 18)])[0]
    t.cristal(gx, gy, 3.2, 5.5, aura=False)
    # gotas
    for (x, y, grande) in ((80, 110, True), (85, 123, False), (79, 133, False), (32, 66, False)):
        if grande:
            gcx, rr, topo = x + 2.5, 2.4, y + 0.3
        else:
            gcx, rr, topo = x + 1.5, 1.45, y + 0.3
        base = topo + (7.0 if grande else 5.0) - rr
        gota = E(gcx, base, rr) | P([(gcx - rr * 0.92, base - rr * 0.4), (gcx, topo), (gcx + rr * 0.92, base - rr * 0.4)])
        t.halo_arcano(gota, 1.5, 70)
        t.pintar(gota, lambda X, Y, gcx=gcx, base=base, rr=rr:
                 0.75 - 0.35 * np.clip(((X - gcx) + (Y - base)) / (2 * rr), -1, 1),
                 VIOLETA, contorno=True, claro="V0", escuro="V0", esp=0.4)
        t.fill(E(gcx - rr * 0.38, base - rr * 0.2, rr * 0.28), "WH", glow=1.0)
    t.halo_arcano(lam_tudo, 2, 60)


def il_chamine_partida(t):
    base, topo = 152.0, 86.0
    # nuvens
    t.nuvem(31, 56, 9)
    t.nuvem(83, 46, 9, flip=True)
    # torre de tijolos
    torre = P([(36, base), (76, base), (71, topo), (41, topo)])
    quebra = P([(41, topo - 1), (48, topo + 4), (54, topo - 1), (60, topo + 6), (66, topo + 2), (72, topo - 1),
                (72, topo - 8), (40, topo - 8)])
    torre = torre - quebra
    lin = 5.0

    def tijolos(X, Y):
        h_ = base - Y
        fila = np.floor(h_ / lin)
        off = np.where(fila % 2 == 1, 4.0, 0.0)
        by = h_ - fila * lin                  # 0 embaixo do tijolo .. 5 em cima
        bx = np.mod(X + off, 8.0)             # 0 na junta da esquerda .. 8
        idx = (fila * 13 + np.floor((X + off) / 8.0) * 7) % 5
        globo = perfil((X - 36) / 40.0)
        v = globo * 0.85 + (idx - 2) * 0.025
        v = v + 0.16 * (1 - smooth(0.0, 1.2, (lin - by))) + 0.12 * (1 - smooth(0.9, 2.0, bx))
        v = v - 0.12 * (1 - smooth(0.9, 1.8, by)) - 0.1 * (1 - smooth(0.0, 1.0, 8.0 - bx))
        junta = np.maximum(1 - smooth(0.55, 0.95, by), 1 - smooth(0.55, 0.95, bx))
        c = rampa(OURO, v)
        return c * (1 - junta[..., None]) + PAL["B0"] * junta[..., None]
    t.contorno(torre, "B1", "B0")
    t.fill(torre, tijolos)
    # janelas: uma arcana e uma de fornalha
    for (wx, wy, tipo) in ((56.0, 108.0, "C"), (56.0, 126.0, "F")):
        jan = R(wx - 3, wy - 4, wx + 3, wy + 4) | E(wx, wy - 4, 3.0)
        t.contorno(jan, "B3", "B2", 0.8)
        if tipo == "C":
            t.fill(jan, lambda X, Y: rampa(CIANO, 0.42 + 0.3 * np.exp(-((X - wx) / 1.8) ** 2)), glow=1.0)
            t.fill(R(wx - 1, wy - 5, wx + 1, wy + 3, 0.6), "C2", glow=1.0)
            t.fill(E(wx - 0.6, wy - 4.6, 0.55), "WH", glow=1.0)
            t.fill(R(wx - 3, wy - 0.3, wx + 3, wy + 0.3), "C0", alfa=0.7)
            t.luz_radial(wx, wy, 6, "C1", 0.25, glow=0.3, dentro=torre - jan.cresce(0.8))
        else:
            t.fill(jan, lambda X, Y: rampa(OURO, 1.0 - 0.3 * np.clip((Y - wy) / 4, 0, 1)))
            t.fill(R(wx - 3, wy + 1, wx + 3, wy + 4), "B3")
            t.luz_radial(wx, wy, 5.5, "B3", 0.25, dentro=torre - jan.cresce(0.8))
    # boca da fornalha
    boca = (R(49, 140, 63, 152) | E(56.0, 140.0, 7.0)) & semiplano(0, 1, 152)
    t.contorno(boca, "B3", "B2", 0.8)
    t.fill(boca, lambda X, Y: rampa(("K", "B0"), np.clip((Y - 136) / 16, 0, 1)))
    for cx2, h, w in ((52, 9, 2.3), (56, 11, 2.6), (60, 8, 2.2)):
        t.chama(cx2, 152, h, w)
    # chão
    t.pintar(R(28, 152, 84, 156, 0.4), linear(28, 152, 84, 156), chanfro=0.18)
    # topo partido voando
    ap = math.radians(-22)
    peca = P(girar([(-18, -4), (18, -4), (16, 4), (-16, 4)], 62, 70, ap))
    t.pintar(peca, local(lambda x, y: 0.55 + 0.35 * (1 - smooth(-1.4, -0.6, y)), 62, 70, ap), chanfro=0.15)
    for k in (-9, 0, 9):
        a, b = girar([(k, -0.6), (k, 3.6)], 62, 70, ap)
        t.fill(L([a, b], 0.6) & peca, "B0")
    t.fill(L(girar([(-17.5, -1.0), (17.5, -1.0)], 62, 70, ap), 0.5) & peca, "B1", alfa=0.8)
    # raio arcano atingindo a torre
    t.raio(P5([(66, 31), (60, 42), (66, 49), (57, 60), (61, 66), (55, 80)]), ponta=False)
    t.estrela(55, 82, 3, ("WH", "C2", "C1", "C0"))
    # tijolos caindo
    for (bx, by, a) in ((26, 98, 20), (88, 92, -25), (24, 124, -10), (88, 118, 30), (33, 82, 40)):
        tij = P(girar([(-2.5, -1.5), (2.5, -1.5), (2.5, 1.5), (-2.5, 1.5)], bx, by, math.radians(a)))
        t.pintar(tij, linear(bx - 3, by - 2, bx + 3, by + 2), chanfro=0.15, esp=0.55)
        for k in range(3):
            y = by - 4.2 - k * 2.2
            t.fill(L([(bx, y), (bx, y - 0.9)], 0.45), "B1" if k == 0 else "B0", alfa=1 - k * 0.25, so_fundo=True)


def il_artifice(t):
    # lemniscata (o Mago do tarô)
    lem = []
    for i in range(241):
        a = i * math.tau / 240
        k = 1 + math.sin(a) ** 2
        lem.append((CX + 15 * math.cos(a) / k, 46 + 15 * math.sin(a) * math.cos(a) / k))
    t.pintar(L(lem, 3.0), linear(40, 38, 72, 54, 1.0, 0.12), chanfro=0.2)
    t.fill(L(lem, 0.35), "B4", alfa=0.35)
    # raios do cristal
    for i in range(20):
        a = i * math.tau / 20 + 0.08
        r0, r1 = 15, (25 if i % 2 == 0 else 20)
        t.raio_luz((CX + math.cos(a) * r0, 84.5 + math.sin(a) * r0), (CX + math.cos(a) * r1, 84.5 + math.sin(a) * r1),
                   "C1", "C0", 0.42, 0.35, glow=0.8)
    # bancada
    t.pintar(R(22, 137, 26, 158, 0.3), cil_x(22, 26))
    t.pintar(R(86, 137, 90, 158, 0.3), cil_x(86, 90))
    t.pintar(R(25, 148, 87, 151), cil_y(148, 151, 0.8))
    t.pintar(R(16, 130, 96, 137, 0.5), linear(16, 130, 96, 137), chanfro=0.2)
    for x in range(18, 95, 9):
        t.sulco_v(x, 133.2, 135.8)
    t.sulco_h(16.6, 95.4, 131.6, "B1", 0.2)

    # engrenagens sobre a bancada
    def engr(cx, cy, r, n):
        e = engrenagem(cx, cy, r, n, 2.5, fase=-0.26, cheio=0.48, ponta=0.21) & semiplano(0, 1, 130)
        t.pintar(e, linear(cx - r, cy - r, cx + r, cy + r, 1.0, 0.08), chanfro=0.2)
        t.fill(anel(cx, cy, r * 0.7 - 0.6, r * 0.7) & semiplano(0, 1, 130), "B1")
        t.fill(E(cx, cy, r * 0.35), "K")
        t.fill((E(cx, cy, r * 0.35 + 0.5) - E(cx, cy, r * 0.35)) & semiplano(0, -1, -cy), "B3", alfa=0.8)
    engr(29.0, 120.0, 9.0, 10)
    engr(42.0, 125.5, 5.0, 8)
    # compasso
    for p0, p1 in (((80, 106), (73, 130)), ((80, 106), (88, 130))):
        t.pintar(L([p0, p1], 2.2), linear(70, 100, 90, 130), chanfro=0.1)
    t.pintar(E(80.0, 105.5, 2.8), esfera(80.0, 105.5, 2.8))
    t.fill(E(80.0, 105.5, 0.7), "B0")
    # pinças segurando o cristal
    for p0, p1 in (((47, 130), (61, 96)), ((65, 130), (51, 96))):
        t.pintar(L([p0, p1], 2.6), linear(44, 94, 68, 130), chanfro=0.12)
    for x0, sx in ((60.5, 1), (51.5, -1)):
        t.pintar(L([(x0, 96), (x0 + sx * 0.5, 92)], 2.0), linear(44, 88, 68, 98), chanfro=0.1)
    t.pintar(E(CX, 113.5, 2.3), esfera(CX, 113.5, 2.3))
    t.cristal(CX, 82.0, 6.5, 12.0)


ILUSTRACOES = {}


def _carregar_novas():
    """Fase 10 (D-088): as ilustrações em alta das cartas novas ficam em Tools/Cards/novas/hd_*.py, uma por grupo
    de artista, executadas neste espaço de nomes (veem R, E, P, cil_x, esfera, t.pintar etc.) e definindo `il_<id>(t)`."""
    import glob
    for caminho in sorted(glob.glob(os.path.join(AQUI, "novas", "hd_*.py"))):
        with open(caminho, encoding="utf-8") as f:
            exec(compile(f.read(), caminho, "exec"), globals())


def _registrar():
    _carregar_novas()
    for k, v in list(globals().items()):
        if k.startswith("il_"):
            ILUSTRACOES[k[3:]] = v


# ---------- montagem ----------

def coroa(t, t0, rh):
    """Coroa de estrelas dos arcanos maiores (mesmo teste do card_pixel.py)."""
    for i in range(12):
        a = i * math.tau / 12 + math.tau / 24
        x, y = int(CX - 0.5 + math.cos(a) * (rh + 16)), int(CY - 0.5 + math.sin(a) * (rh + 16))
        if cp.livre(t0, x, y, 1) or all(t0.get(x + dx, y + dy) in cp.FUNDO or t0.get(x + dx, y + dy) in ("B0", "B1")
                                       for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
            t.estrela(x, y, 1)
            cp.Tela.estrela(t0, x, y, 1)


def desenhar(card):
    cid, rotulo, nome, kind, rh = card
    t0 = cp.Tela(kind)                       # grade antiga: só o mapa de ocupação das estrelas
    cp.fundo(t0)
    cp.halo(t0, CX, CY, rh, kind)
    cp.ILUSTRACOES[cid](t0)
    t = Tela(kind)
    fundo(t)
    halo(t, CX, CY, rh, kind)
    ILUSTRACOES[cid](t)
    if kind == "major":
        coroa(t, t0, rh)
    estrelas(t, t0, random.Random(cid), rh)
    moldura(t, kind, rotulo, nome)
    return t


def il_verso(t):
    """Verso da carta do chão (D-046): roda de engrenagem dourada com cristal ciano no miolo."""
    roda = engrenagem(CX, CY, 25.0, 16, 4.5, fase=0.25)
    miolo = E(CX, CY, 17.5)
    t.pintar(roda - miolo, esfera(CX, CY, 29.5, 1.0, 0.05), chanfro=0.15)
    t.clarear(anel(CX, CY, 21.9, 22.4), "B0", 0.55)            # sulco gravado na roda
    t.clarear(anel(CX, CY, 22.4, 22.8), "B4", 0.25)
    t.pintar(anel(CX, CY, 12.5, 15.0), esfera(CX, CY, 15.0, 1.0, 0.1), chanfro=0.1)
    for i in range(8):                       # raios da roda
        a = i * math.tau / 8 + math.tau / 16
        p0 = (CX + math.cos(a) * 15.3, CY + math.sin(a) * 15.3)
        p1 = (CX + math.cos(a) * 18.7, CY + math.sin(a) * 18.7)
        t.pintar(L([p0, p1], 1.8), linear(p0[0] - 2, p0[1] - 2, p1[0] + 2, p1[1] + 2), chanfro=0.1)
    for i in range(4):
        a = i * math.tau / 4
        t.rebite(int(CX - 0.5 + math.cos(a) * 21.0), int(CY - 0.5 + math.sin(a) * 21.0))
    t.cristal(CX, CY, 8.0, 15.0)
    for dy in (-44, 44):
        t.cristal_pequeno(CX, CY + dy)


def desenhar_verso():
    capt = []
    orig = cp.estrelas, cp.moldura
    cp.estrelas = lambda tt, rnd, rh: capt.append(tt)
    cp.moldura = lambda *a: None
    try:
        cp.desenhar_verso()
    finally:
        cp.estrelas, cp.moldura = orig
    t0 = capt[0]
    t = Tela("major")
    fundo(t)
    halo(t, CX, CY, 40, "major")
    il_verso(t)
    estrelas(t, t0, random.Random("verso"), 40)
    moldura(t, "major", "", "")
    return t


def exportar(t, cid):
    tam = (W * SAIDA, H * SAIDA)
    face = Image.fromarray(np.clip(t.rgb + 0.5, 0, 255).astype(np.uint8), "RGB").resize(tam, Image.LANCZOS)
    alfa = Image.fromarray(np.clip(t.glow * 255 + 0.5, 0, 255).astype(np.uint8), "L").resize(tam, Image.LANCZOS)
    mask = Image.merge("RGBA", (Image.new("L", tam, 255),) * 3 + (alfa,))
    os.makedirs(OUT_DIR, exist_ok=True)
    face.save(os.path.join(OUT_DIR, f"{cid}.png"), optimize=True)
    mask.save(os.path.join(OUT_DIR, f"{cid}_brilho.png"), optimize=True)
    return face


def folha(faces, por_linha=5, escala=0.5):
    pad = 12
    cw, ch = int(W * SAIDA * escala), int(H * SAIDA * escala)
    n = len(faces)
    linhas = (n + por_linha - 1) // por_linha
    img = Image.new("RGB", (pad + por_linha * (cw + pad), pad + linhas * (ch + pad)), (30, 28, 27))
    for i, f in enumerate(faces):
        img.paste(f.resize((cw, ch), Image.LANCZOS), (pad + (i % por_linha) * (cw + pad), pad + (i // por_linha) * (ch + pad)))
    os.makedirs(os.path.dirname(SHEET), exist_ok=True)
    img.save(SHEET, optimize=True)


def main(only):
    _registrar()
    faces = []
    for c in cp.CARDS:
        if only and c[0] not in only:
            continue
        if c[0] not in ILUSTRACOES:
            print(f"[card_hd] sem ilustração em alta: {c[0]}")
            continue
        faces.append(exportar(desenhar(c), c[0]))
        print(f"[card_hd] {c[0]}")
    if not only or "verso" in only:
        faces.append(exportar(desenhar_verso(), "verso"))
        print("[card_hd] verso")
    if not only:
        folha(faces)
        print(f"[card_hd] folha: {SHEET}")
    print("[card_hd] concluído")


if __name__ == "__main__":
    main(sys.argv[1:])
