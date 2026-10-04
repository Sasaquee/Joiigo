"""
Gera as faces das cartas de tarô em PIXEL ART (D-041), mantendo a identidade dourada sobre preto
de D-037: moldura ornamentada, faixas de título e nome, estrelas, raios atrás do objeto e o objeto
da carta no centro. Os cristais e a luz arcana são ciano (violeta na amaldiçoada), a assinatura
arcana do Pilar 4: máquina e magia na mesma peça.

Tudo é desenhado pixel a pixel em 112 x 192, sem suavização, com uma paleta fixa pequena:
- contorno de 1 px com contorno seletivo (lado iluminado mais claro, luz vindo de cima à esquerda);
- rampas de dourado (bronze escuro até o brilho) com pontilhado ordenado (Bayer 4x4) nas transições;
- pontilhado ordenado também no halo, nos raios e na vinheta do fundo;
- estrelas em sprites (cintilações de 4 pontas e pontos de 1 px);
- fonte de pixel própria (5 px de altura, com acentos do português) e numerais romanos grandes.

Depois amplia 4x com NEAREST (448 x 768) e salva:
    Assets/_Game/Art/Cards/<id>.png         face da carta
    Assets/_Game/Art/Cards/<id>_brilho.png  máscara do brilho (branco sobre transparente) só com os
                                            pixels de cristal / luz arcana, para a UI animar o cintilar
e a folha de contato em Docs/Capturas/fase5/cartas_pixel.png.

Uso (Python 3 com Pillow):
    python Tools/Cards/card_pixel.py                 # todas
    python Tools/Cards/card_pixel.py pistao_runico   # só algumas
"""

import math
import os
import random
import sys

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "_Game", "Art", "Cards")
SHEET = os.path.join(ROOT, "Docs", "Capturas", "fase5", "cartas_pixel.png")

W, H = 112, 192          # resolução nativa
ESCALA = 4               # 448 x 768 na saída
CX, CY = 56.0, 96.0      # centro da ilustração (entre os pixels 55 e 56)

# ---------- paleta ----------

PAL = {
    "K": (14, 12, 11),       # preto da carta (#0E0C0B)
    "G1": (23, 19, 17),      # cinzas quentes do fundo / pontilhado
    "G2": (33, 27, 23),
    "G3": (47, 38, 31),
    "B0": (60, 37, 19),      # bronze profundo (contorno)
    "B1": (112, 73, 33),
    "B2": (170, 117, 50),
    "B3": (228, 174, 84),    # dourado (#E4AE54)
    "B4": (255, 234, 166),   # brilho
    "C0": (18, 66, 82),      # ciano: contorno escuro do cristal
    "C1": (44, 158, 190),
    "C2": (118, 238, 255),
    "WH": (236, 255, 255),   # reflexo quase branco
    "V0": (52, 22, 68),      # violeta da maldição
    "V1": (122, 56, 172),
    "V2": (206, 138, 250),
}
OURO = ("B0", "B1", "B2", "B3", "B4")
CIANO = ("C0", "C1", "C2", "WH")
VIOLETA = ("V0", "V1", "V2", "WH")
FUNDO = {"K", "G1", "G2", "G3"}
BRILHO = {"C1", "C2", "WH", "V1", "V2"}   # cores que entram na máscara do brilho

BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def bayer(x, y):
    return (BAYER[y & 3][x & 3] + 0.5) / 16.0


def quant(v, x, y, n, forca=0.55):
    """Quantiza v (0..1) em n níveis, com pontilhado ordenado só perto das transições."""
    f = max(0.0, min(1.0, v)) * (n - 1)
    base = int(math.floor(f))
    frac = f - base
    lim = 0.5 + (bayer(x, y) - 0.5) * forca
    return max(0, min(n - 1, base + (1 if frac > lim else 0)))


# ---------- formas (conjuntos de pixels; testes pelo centro do pixel) ----------

def R(x0, y0, x1, y1):
    """Retângulo em coordenadas contínuas."""
    return {(x, y) for y in range(math.ceil(y0 - 0.5), math.floor(y1 - 0.5) + 1)
            for x in range(math.ceil(x0 - 0.5), math.floor(x1 - 0.5) + 1)}


def E(cx, cy, rx, ry=None):
    ry = rx if ry is None else ry
    m = set()
    if rx <= 0 or ry <= 0:
        return m
    for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            if ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1.0:
                m.add((x, y))
    return m


def anel(cx, cy, r0, r1, ry0=None, ry1=None):
    return E(cx, cy, r1, ry1) - E(cx, cy, r0, ry0)


def P(pts):
    """Polígono (par-ímpar)."""
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    m = set()
    n = len(pts)
    for y in range(int(min(ys)) - 1, int(max(ys)) + 2):
        py = y + 0.5
        for x in range(int(min(xs)) - 1, int(max(xs)) + 2):
            px = x + 0.5
            dentro = False
            j = n - 1
            for i in range(n):
                xi, yi = pts[i]
                xj, yj = pts[j]
                if (yi > py) != (yj > py) and px < (xj - xi) * (py - yi) / (yj - yi) + xi:
                    dentro = not dentro
                j = i
            if dentro:
                m.add((x, y))
    return m


def _dist_seg(px, py, a, b):
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    L2 = dx * dx + dy * dy
    t = 0.0 if L2 == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / L2))
    return math.hypot(px - (ax + t * dx), py - (ay + t * dy))


def L(pts, w):
    """Linha grossa (polilinha) de largura w."""
    m = set()
    r = w / 2.0
    for a, b in zip(pts, pts[1:]):
        for y in range(int(min(a[1], b[1]) - r) - 1, int(max(a[1], b[1]) + r) + 2):
            for x in range(int(min(a[0], b[0]) - r) - 1, int(max(a[0], b[0]) + r) + 2):
                if _dist_seg(x + 0.5, y + 0.5, a, b) <= r:
                    m.add((x, y))
    return m


def B(p0, p1):
    """Linha de 1 px (Bresenham) entre pontos inteiros."""
    x0, y0 = int(round(p0[0])), int(round(p0[1]))
    x1, y1 = int(round(p1[0])), int(round(p1[1]))
    pts = []
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err = dx + dy
    while True:
        pts.append((x0, y0))
        if x0 == x1 and y0 == y1:
            break
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy
    return pts


def BL(pts):
    out = []
    for a, b in zip(pts, pts[1:]):
        seg = B(a, b)
        out.extend(seg if not out else seg[1:])
    return out


def viz4(x, y):
    return ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))


def fora(m):
    """Pixels de fora que encostam (4-vizinhança) na forma: onde vai o contorno."""
    return {q for p in m for q in viz4(*p) if q not in m}


def borda(m):
    """Pixels da forma que encostam no lado de fora."""
    return {p for p in m if any(q not in m for q in viz4(*p))}


def encolher(m, n=1):
    for _ in range(n):
        m = m - borda(m)
    return m


def dilatar(m, n=1):
    for _ in range(n):
        m = m | fora(m)
    return m


def girar(pts, cx, cy, ang):
    c, s = math.cos(ang), math.sin(ang)
    return [(cx + x * c - y * s, cy + x * s + y * c) for (x, y) in pts]


# ---------- funções de luz (luz de cima à esquerda) ----------

METAL = [(0.0, 0.42), (0.12, 0.64), (0.24, 0.93), (0.34, 1.0), (0.46, 0.74),
         (0.66, 0.50), (0.86, 0.28), (1.0, 0.38)]


def perfil(t, pts=METAL):
    t = max(0.0, min(1.0, t))
    for (a, va), (b, vb) in zip(pts, pts[1:]):
        if t <= b:
            return va + (vb - va) * (t - a) / (b - a)
    return pts[-1][1]


def cil_x(x0, x1, k=1.0, d=0.0):
    return lambda x, y: perfil((x + 0.5 - x0) / (x1 - x0)) * k + d


def cil_y(y0, y1, k=1.0, d=0.0):
    return lambda x, y: perfil((y + 0.5 - y0) / (y1 - y0)) * k + d


def esfera(cx, cy, r, k=1.0, d=0.0):
    hx, hy = cx - 0.38 * r, cy - 0.42 * r

    def f(x, y):
        px, py = x + 0.5, y + 0.5
        dd = math.hypot(px - hx, py - hy) / (1.5 * r)
        v = 1.06 - dd * 1.12
        e = math.hypot(px - cx, py - cy) / r
        if e > 0.8 and (px - cx) + (py - cy) > r * 0.5:
            v += 0.14                       # luz refletida na borda de baixo
        return v * k + d
    return f


def linear(x0, y0, x1, y1, k=1.0, d=0.0):
    tot = (x1 - x0) + (y1 - y0)

    def f(x, y):
        t = ((x + 0.5 - x0) + (y + 0.5 - y0)) / tot
        return (0.92 - 0.62 * t) * k + d
    return f


def plano(v):
    return lambda x, y: v


def local(fn, cx, cy, ang):
    """Aplica uma função de luz nas coordenadas locais de uma peça girada."""
    c, s = math.cos(-ang), math.sin(-ang)

    def f(x, y):
        px, py = x + 0.5 - cx, y + 0.5 - cy
        lx, ly = px * c - py * s, px * s + py * c
        return fn(lx - 0.5, ly - 0.5)
    return f


# ---------- tela ----------

class Tela:
    def __init__(self, kind):
        self.px = [["K"] * W for _ in range(H)]
        self.aura = {}              # (x, y) -> alfa extra da máscara de brilho
        self.kind = kind
        self.arc = VIOLETA if kind == "cursed" else CIANO

    def get(self, x, y):
        return self.px[y][x] if 0 <= x < W and 0 <= y < H else None

    def put(self, x, y, c):
        if 0 <= x < W and 0 <= y < H:
            self.px[y][x] = c

    def fill(self, m, c):
        for x, y in m:
            self.put(x, y, c)

    def fundo_em(self, x, y):
        return self.get(x, y) in FUNDO

    def contorno(self, m, claro="B1", escuro="B0", so_fundo=False):
        for x, y in fora(m):
            if so_fundo and not self.fundo_em(x, y):
                continue
            iluminado = (x + 1, y) in m or (x, y + 1) in m
            self.put(x, y, claro if iluminado else escuro)

    def pintar(self, m, fn, rampa=OURO, chanfro=0.0, contorno=True, claro="B1", escuro="B0", forca=0.55):
        """Preenche a forma com a rampa (luz pela função fn) e contorno seletivo de 1 px."""
        if contorno:
            self.contorno(m, claro, escuro)
        n = len(rampa)
        for x, y in m:
            v = fn(x, y)
            if chanfro:
                if (x - 1, y) not in m or (x, y - 1) not in m:
                    v += chanfro
                elif (x + 1, y) not in m or (x, y + 1) not in m:
                    v -= chanfro
            self.put(x, y, rampa[quant(v, x, y, n, forca)])

    def rebite(self, x, y):
        self.put(x, y, "B4")
        self.put(x + 1, y + 1, "B0")

    # ----- arcano -----

    def halo_arcano(self, m, r=2, alfa=110):
        """Pontilhado escuro em volta da luz arcana (e um pouco de alfa na máscara)."""
        anelm = dilatar(m, r) - dilatar(m, 1)
        for x, y in anelm:
            if self.fundo_em(x, y) and (x + y) % 2 == 0:
                self.put(x, y, self.arc[0])
                self.aura[(x, y)] = alfa

    def cristal(self, cx, cy, w, h, pal=None, aura=True):
        """Cristal facetado: topo e corpo, faces da esquerda claras, da direita escuras."""
        pal = pal or self.arc
        if w < 3 or h < 5:
            return self.cristal_pequeno(int(cx), int(cy), pal, aura)
        ys = cy - h * 0.42                 # ombros
        yi = cy + h * 0.45
        pts = [(cx, cy - h), (cx + w, ys), (cx + w, yi), (cx, cy + h), (cx - w, yi), (cx - w, ys)]
        m = P(pts)
        for x, y in fora(m):
            self.put(x, y, pal[0])
        for x, y in m:
            px, py = x + 0.5, y + 0.5
            dx = px - cx
            ombro = ys + (h * 0.2) * (1 - abs(dx) / w)       # linha em V que separa topo e corpo
            desce = (py - ombro) / max(1.0, (cy + h) - ombro)
            if py < ombro:
                v = 0.98 if dx < 0 else 0.7
            elif abs(dx) < 0.6:
                v = 0.86 - 0.3 * desce                       # aresta central
            elif dx < 0:
                v = 0.72 - 0.42 * desce
            else:
                v = 0.42 - 0.3 * desce
            self.put(x, y, pal[quant(v, x, y, 4, 0.7)])
        # reflexos
        gx, gy = int(cx - w * 0.45), int(cy - h * 0.62)
        self.put(gx, gy, pal[3])
        self.put(gx, gy + 1, pal[3])
        if h >= 9:
            self.put(gx + 1, gy - 1, pal[3])
        if aura:
            self.halo_arcano(m)
        return m

    def cristal_pequeno(self, x, y, pal=None, aura=True):
        pal = pal or self.arc
        spr = ["..o..",
               ".o2o.",
               "o2w1o",
               "o221o",
               "o211o",
               ".o1o.",
               "..o.."]
        cores = {"o": pal[0], "1": pal[1], "2": pal[2], "w": pal[3]}
        m = set()
        for j, row in enumerate(spr):
            for i, ch in enumerate(row):
                if ch != ".":
                    self.put(x - 2 + i, y - 3 + j, cores[ch])
                    m.add((x - 2 + i, y - 3 + j))
        if aura:
            self.halo_arcano(m, 2, 90)
        return m

    def raio(self, pts, ponta=True):
        """Descarga arcana: núcleo claro de 1 px com borda escura dos lados."""
        pal = self.arc
        nucleo = BL(pts)
        lado = set()
        for (ax, ay), (bx, by) in zip(pts, pts[1:]):
            horiz = abs(bx - ax) > abs(by - ay)
            for x, y in B((ax, ay), (bx, by)):
                for q in (((x, y - 1), (x, y + 1)) if horiz else ((x - 1, y), (x + 1, y))):
                    lado.add(q)
        for x, y in lado:
            if self.fundo_em(x, y):
                self.put(x, y, pal[1])
        for i, (x, y) in enumerate(nucleo):
            self.put(x, y, pal[3] if i % 3 == 0 else pal[2])
        self.halo_arcano(set(nucleo) | lado, 2, 80)
        if ponta:
            ex, ey = pts[-1]
            self.estrela(int(ex), int(ey), 2, (pal[3], pal[2], pal[1], pal[0]))

    # ----- estrelas -----

    def estrela(self, x, y, tam, pal=("B4", "B3", "B2", "B1")):
        """Cintilação de 4 pontas: tam 0 = ponto, 1 = cruz, 2..4 = pontas longas."""
        if tam == 0:
            self.put(x, y, pal[2])
            return
        self.put(x, y, pal[0])
        braços = {1: [pal[2]], 2: [pal[1], pal[3]], 3: [pal[1], pal[2], pal[3]],
                  4: [pal[0], pal[1], pal[2], pal[3]]}[tam]
        for i, c in enumerate(braços, 1):
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                self.put(x + dx * i, y + dy * i, c)
        if tam >= 3:
            for dx, dy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
                self.put(x + dx, y + dy, pal[3])

    # ----- vapor -----

    def nuvem(self, cx, cy, s, flip=False):
        """Nuvem de vapor em volutas (gravura): bolhas com contorno dourado e fundo escuro."""
        puffs = [(-0.9, 0.15, 0.42), (-0.45, -0.18, 0.55), (0.1, -0.3, 0.62), (0.62, -0.08, 0.5), (1.0, 0.18, 0.38)]
        if flip:
            puffs = [(-dx, dy, rr) for dx, dy, rr in puffs][::-1]
        base = cy + s * 0.2
        tudo = set()
        for dx, dy, rr in puffs:
            d = E(cx + dx * s, cy + dy * s, rr * s)
            d = {p for p in d if p[1] + 0.5 <= base}
            tudo |= d
            for x, y in d:
                self.put(x, y, "G1" if bayer(x, y) < 0.25 else "K")
            for x, y in borda(d):
                topo = (x, y - 1) not in d or (x - 1, y) not in d
                self.put(x, y, "B3" if topo and y + 0.5 < cy + dy * s else "B2")
        for x, y in tudo:                       # linha da base
            if (x, y + 1) not in tudo:
                self.put(x, y, "B2")
        for dx, dy, rr in puffs[1:4]:           # volutas internas
            r = rr * s * 0.5
            ccx, ccy = cx + dx * s, cy + dy * s + 1
            for a in range(200, 331, 8):
                t = math.radians(a)
                self.put(int(ccx + math.cos(t) * r), int(ccy + math.sin(t) * r), "B1")
        return tudo

    def vapor(self, bolhas):
        """Baforada pequena: bolhas sobrepostas com contorno dourado (as de trás primeiro)."""
        for (x, y, r) in bolhas:
            d = E(x, y, r)
            for px, py in d:
                self.put(px, py, "G2" if bayer(px, py) < 0.2 else "K")
            for px, py in borda(d):
                self.put(px, py, "B3" if (px + 0.5 - x) + (py + 0.5 - y) < -r * 0.3 else "B2")
            if r >= 3:
                for a in range(200, 291, 30):
                    t = math.radians(a)
                    self.put(int(x + math.cos(t) * (r - 1.6)), int(y + math.sin(t) * (r - 1.6)), "B1")

    def massa_vapor(self, bolhas):
        """Nuvem de vapor em gravura: só o contorno externo da união das bolhas, com volutas dentro."""
        u = set()
        for (x, y, r) in bolhas:
            u |= E(x, y, r)
        for px, py in u:
            self.put(px, py, "G2" if bayer(px, py) < 0.12 else ("G1" if bayer(px, py) < 0.3 else "K"))
        bd = borda(u)
        for px, py in bd:
            topo = (px, py - 1) not in u or (px - 1, py) not in u
            self.put(px, py, "B3" if topo else "B2")
        for (x, y, r) in bolhas:                    # volutas: borda de cima das bolhas da frente
            atras = [(bx, by, br) for (bx, by, br) in bolhas if by < y - 0.5]
            for a in range(195, 346, 4):
                t = math.radians(a)
                q = (int(x + math.cos(t) * (r - 0.4)), int(y + math.sin(t) * (r - 0.4)))
                if q in u and q not in bd and any(math.hypot(q[0] + 0.5 - bx, q[1] + 0.5 - by) < br - 0.8
                                                  for (bx, by, br) in atras):
                    self.put(*q, "B2" if a < 270 else "B1")
        return u

    def runa(self, x, y, k):
        """Runa arcana de 3 x 5 (desenho próprio), com aura."""
        runas = [["#.#", "###", ".#.", ".#.", ".#."],
                 [".#.", "#.#", "###", "#.#", ".#."],
                 ["#..", ".#.", "..#", ".#.", "#.."],
                 ["###", "#.#", ".#.", "#.#", "###"]]
        m = set()
        for j, row in enumerate(runas[k % len(runas)]):
            for i, ch in enumerate(row):
                if ch == "#":
                    m.add((x + i, y + j))
        for p in fora(m):
            if self.fundo_em(*p):
                self.put(*p, self.arc[0])
        for i, p in enumerate(sorted(m)):
            self.put(*p, self.arc[3] if i == 0 else self.arc[2])
        return m

    def chama(self, cx, base, h, w, incl=0.0):
        """Chama em gota: borda bronze, miolo claro."""
        m = E(cx, base - w, w, w) | P([(cx - w, base - w), (cx + incl, base - h), (cx + w, base - w)])
        self.fill(m, "B2")
        self.fill(encolher(m, 1), "B3")
        self.fill(encolher(m, 2), "B4")
        return m


# ---------- fundo, halo e estrelas ----------

def fundo(t):
    """Vinheta: o centro levemente mais claro em pontilhado, as bordas no preto."""
    for y in range(H):
        for x in range(W):
            d = math.hypot((x + 0.5 - CX) / 58.0, (y + 0.5 - CY) / 86.0)
            v = max(0.0, 1.0 - d) * 0.95
            t.px[y][x] = ("K", "G1", "G2")[quant(v, x, y, 3, 1.0)]
    rnd = random.Random(7)
    for _ in range(90):                       # grão do papel
        x, y = rnd.randrange(8, W - 8), rnd.randrange(28, H - 28)
        if t.px[y][x] == "K":
            t.px[y][x] = "G1"


def halo(t, cx, cy, r, kind):
    """Sol de raios em pontilhado, anéis e um brilho interno escuro."""
    tinta = ("K", "V0") if kind == "cursed" else ("K", "G2")
    for y in range(int(cy - r) - 1, int(cy + r) + 2):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            d = math.hypot(x + 0.5 - cx, y + 0.5 - cy) / r
            if d < 1.0:
                v = (1.0 - d) * 0.75
                t.put(x, y, tinta[quant(v, x, y, 2, 1.0)] if v > 0.08 else "K")
    for x, y in borda(E(cx, cy, r)):
        t.put(x, y, "B1")
    for x, y in borda(E(cx, cy, r - 2)):
        if (x + y) % 2 == 0:
            t.put(x, y, "B0")
    n = 44
    for i in range(n):
        a = i * math.tau / n + 0.04
        longo = i % 2 == 0
        r1 = r + (12 if longo else 7)
        p0 = (cx - 0.5 + math.cos(a) * (r + 2), cy - 0.5 + math.sin(a) * (r + 2))
        p1 = (cx - 0.5 + math.cos(a) * r1, cy - 0.5 + math.sin(a) * r1)
        pts = B(p0, p1)
        for k, (x, y) in enumerate(pts):
            f = k / max(1, len(pts) - 1)
            if f < 0.35:
                c = "B1" if longo else "B0"
            elif f < 0.7:
                c = "B0" if longo or k % 2 == 0 else None
            else:
                c = "B0" if (k % 2 == 0) else None
            if c and t.fundo_em(x, y):
                t.put(x, y, c)


def livre(t, x, y, folga):
    for yy in range(y - folga, y + folga + 1):
        for xx in range(x - folga, x + folga + 1):
            c = t.get(xx, yy)
            if c is None or c not in FUNDO:
                return False
    return True


def estrelas(t, rnd, raio_halo):
    """Estrelas e uma constelação nos espaços livres, longe do halo e do objeto."""
    def ok(x, y, folga, r_extra=13):
        if math.hypot(x + 0.5 - CX, y + 0.5 - CY) < raio_halo + r_extra:
            return False
        return 10 <= x <= W - 11 and 31 <= y <= H - 32 and livre(t, x, y, folga)

    postas = []

    def longe(x, y, d):
        return all(math.hypot(x - a, y - b) >= d for a, b in postas)

    # constelação num canto
    for _ in range(60):
        ox, oy = rnd.choice([(16, 34), (W - 34, 36), (16, H - 52), (W - 36, H - 50)])
        pts = [(ox + rnd.randint(0, 18), oy + rnd.randint(0, 14)) for _ in range(4)]
        pts.sort()
        if all(ok(x, y, 2, 10) for x, y in pts) and \
                all(math.hypot(a[0] - b[0], a[1] - b[1]) >= 5 for a, b in zip(pts, pts[1:])):
            for a, b in zip(pts, pts[1:]):
                for k, (x, y) in enumerate(B(a, b)):
                    if k % 2 == 1 and t.fundo_em(x, y):
                        t.put(x, y, "B0")
            for x, y in pts:
                t.put(x, y, "B3")
                postas.append((x, y))
            break

    plano_estrelas = [(4, 1), (3, 2), (2, 3), (1, 5), (0, 16)]
    for tam, qtd in plano_estrelas:
        feitos = 0
        for _ in range(400):
            if feitos >= qtd:
                break
            x, y = rnd.randrange(10, W - 10), rnd.randrange(31, H - 31)
            if ok(x, y, tam + 1) and longe(x, y, 7 + tam * 2):
                t.estrela(x, y, tam)
                if tam == 0 and rnd.random() < 0.4:
                    t.put(x, y, "B3")
                postas.append((x, y))
                feitos += 1


# ---------- fonte de pixel ----------

FONTE = {
    "A": [".##.", "#..#", "####", "#..#", "#..#"],
    "B": ["###.", "#..#", "###.", "#..#", "###."],
    "C": [".###", "#...", "#...", "#...", ".###"],
    "D": ["###.", "#..#", "#..#", "#..#", "###."],
    "E": ["###", "#..", "##.", "#..", "###"],
    "F": ["###", "#..", "##.", "#..", "#.."],
    "G": [".###", "#...", "#.##", "#..#", ".###"],
    "H": ["#..#", "#..#", "####", "#..#", "#..#"],
    "I": ["###", ".#.", ".#.", ".#.", "###"],
    "J": ["..##", "...#", "...#", "#..#", ".##."],
    "K": ["#..#", "#.#.", "##..", "#.#.", "#..#"],
    "L": ["#..", "#..", "#..", "#..", "###"],
    "M": ["#...#", "##.##", "#.#.#", "#...#", "#...#"],
    "N": ["#..#", "##.#", "#.##", "#..#", "#..#"],
    "O": [".##.", "#..#", "#..#", "#..#", ".##."],
    "P": ["###.", "#..#", "###.", "#...", "#..."],
    "Q": [".##.", "#..#", "#..#", "#.#.", ".#.#"],
    "R": ["###.", "#..#", "###.", "#.#.", "#..#"],
    "S": [".###", "#...", ".##.", "...#", "###."],
    "T": ["###", ".#.", ".#.", ".#.", ".#."],
    "U": ["#..#", "#..#", "#..#", "#..#", ".##."],
    "V": ["#...#", "#...#", ".#.#.", ".#.#.", "..#.."],
    "W": ["#...#", "#...#", "#.#.#", "##.##", "#...#"],
    "X": ["#...#", ".#.#.", "..#..", ".#.#.", "#...#"],
    "Y": ["#...#", ".#.#.", "..#..", "..#..", "..#.."],
    "Z": ["####", "...#", ".##.", "#...", "####"],
}
ACENTOS = {
    "agudo": {3: ["..#", ".#."], 4: ["..#.", ".#.."]},
    "circ": {3: [".#.", "#.#"], 4: [".##.", "#..#"]},
    "til": {3: ["#.#", ".#."], 4: [".#.#", "#.#."]},
}
COMPOSTAS = {"Á": ("A", "agudo"), "Ã": ("A", "til"), "Â": ("A", "circ"), "É": ("E", "agudo"),
             "Ê": ("E", "circ"), "Í": ("I", "agudo"), "Ó": ("O", "agudo"), "Ô": ("O", "circ"),
             "Õ": ("O", "til"), "Ú": ("U", "agudo"), "Ç": ("C", "cedilha")}
ESPACO = 2

# numerais romanos grandes (9 px) para os arcanos maiores
GRANDE = {
    "I": ["######", "..##..", "..##..", "..##..", "..##..", "..##..", "..##..", "..##..", "######"],
    "V": ["###..###", ".##..##.", ".##..##.", ".##..##.", "..#..#..", "..####..", "..####..",
          "...##...", "...##..."],
    "X": ["###..###", ".##..##.", "..#..#..", "..####..", "...##...", "..####..", "..#..#..",
          ".##..##.", "###..###"],
}


def largura_texto(txt):
    w = 0
    for i, ch in enumerate(txt):
        if ch == " ":
            w += ESPACO
        else:
            base = COMPOSTAS.get(ch, (ch, None))[0]
            w += len(FONTE[base][0])
        if i < len(txt) - 1:
            w += 1
    return w


def texto(t, txt, cy_caps, sombra=True):
    """Escreve centrado em CX; cy_caps = linha de cima das maiúsculas."""
    x = int(round(CX - largura_texto(txt) / 2.0))
    cores = ["B4", "B3", "B3", "B2", "B2"]
    marcas = []
    for ch in txt:
        if ch == " ":
            x += ESPACO + 1
            continue
        base, acento = COMPOSTAS.get(ch, (ch, None))
        g = FONTE[base]
        gw = len(g[0])
        for j, row in enumerate(g):
            for i, c in enumerate(row):
                if c == "#":
                    marcas.append((x + i, cy_caps + j, cores[j]))
        if acento == "cedilha":
            for j, row in enumerate(["..#.", ".##."]):
                for i, c in enumerate(row):
                    if c == "#":
                        marcas.append((x + i, cy_caps + 5 + j, "B2"))
        elif acento:
            for j, row in enumerate(ACENTOS[acento][gw]):
                for i, c in enumerate(row):
                    if c == "#":
                        marcas.append((x + i, cy_caps - 3 + j, "B3"))
        x += gw + 1
    pos = {(a, b) for a, b, _ in marcas}
    if sombra:
        for a, b, _ in marcas:
            if (a + 1, b + 1) not in pos:
                t.put(a + 1, b + 1, "B0")
    for a, b, c in marcas:
        t.put(a, b, c)


def numeral(t, txt, y0):
    larg = sum(len(GRANDE[c][0]) for c in txt) + 2 * (len(txt) - 1)
    x = int(round(CX - larg / 2.0))
    m = set()
    for ch in txt:
        g = GRANDE[ch]
        for j, row in enumerate(g):
            for i, c in enumerate(row):
                if c == "#":
                    m.add((x + i, y0 + j))
        x += len(g[0]) + 2
    t.pintar(m, lambda px, py: 1.0 - (py - y0) / 8.0 * 0.62, chanfro=0.12, claro="B0", escuro="B0", forca=0.4)


# ---------- moldura ----------

def moldura(t, kind, rotulo, nome):
    # borda externa chanfrada de 3 px, cantos arredondados
    RC = 6.0
    for y in range(H):
        for x in range(W):
            dx = min(x + 0.5, W - x - 0.5)
            dy = min(y + 0.5, H - y - 0.5)
            if dx < RC and dy < RC:
                ins = RC - math.hypot(RC - dx, RC - dy)
            else:
                ins = min(dx, dy)
            if ins < 0:
                t.px[y][x] = "K"
            elif ins < 1:
                t.px[y][x] = "B1"
            elif ins < 2:
                cima_esq = (x + 0.5 < W - x - 0.5 and dx <= dy) or (y + 0.5 < H - y - 0.5 and dy <= dx)
                t.px[y][x] = "B3" if cima_esq else "B2"
            elif ins < 3:
                t.px[y][x] = "B0"
            elif ins < 4:
                t.px[y][x] = "K"
    # filete interno + pontilhado
    for x in range(5, W - 5):
        for y in (5, H - 6):
            t.put(x, y, "B2")
        for y in (7, H - 8):
            if x % 2 == 0 and 7 <= x <= W - 8:
                t.put(x, y, "B0")
    for y in range(5, H - 5):
        for x in (5, W - 6):
            t.put(x, y, "B2")
        for x in (7, W - 8):
            if y % 2 == 0 and 7 <= y <= H - 8:
                t.put(x, y, "B0")

    # ornamentos de um quarto da carta (cima, esquerda), espelhados nos quatro cantos
    canto = {}

    def cp(x, y, c):
        canto[(x, y)] = c
    caixa = R(3, 3, 12, 12)
    for p in caixa:
        cp(*p, "K")
    for (x, y) in borda(caixa):
        cp(x, y, "B3" if (x == 3 or y == 3) else "B2")
    for (x, y) in borda(R(5, 5, 10, 10)):
        cp(x, y, "B0")
    # filete com contas entre a caixa e o brasão
    for x in range(14, 46):
        cp(x, 10, "B1")
    for bx in (22, 34):
        for dx, dy, c in ((0, 0, "B4"), (1, 0, "B2"), (-1, 0, "B3"), (0, 1, "B2"), (0, -1, "B3")):
            cp(bx + dx, 10 + dy, c)
    cp(46, 10, "B3")
    # arco com contas no canto da área da ilustração (abaixo da faixa do título)
    acx, acy, ar = 6.0, 28.0, 13.0
    for (x, y) in R(6, 28, 21, 43):
        d = math.hypot(x + 0.5 - acx, y + 0.5 - acy)
        if ar - 0.5 <= d < ar + 0.5:
            cp(x, y, "B2")
        elif ar - 3.5 <= d < ar - 2.5 and (x + y) % 2 == 0:
            cp(x, y, "B0")
    for ang in (22, 45, 68):
        a = math.radians(ang)
        bx, by = int(acx + math.cos(a) * ar), int(acy + math.sin(a) * ar)
        for dx, dy, c in ((0, 0, "B4"), (1, 0, "B2"), (-1, 0, "B3"), (0, 1, "B2"), (0, -1, "B3")):
            cp(bx + dx, by + dy, c)
    for (x, y, c) in ((20, 30, "B3"), (8, 42, "B3"), (21, 31, "B1"), (9, 43, "B1")):
        cp(x, y, c)
    for (x, y), c in canto.items():
        for mx, my in ((x, y), (W - 1 - x, y), (x, H - 1 - y), (W - 1 - x, H - 1 - y)):
            t.put(mx, my, c)
    # miolo das caixas dos cantos
    for (ox, oy) in ((7, 7), (W - 8, 7), (7, H - 8), (W - 8, H - 8)):
        if kind == "major":
            t.cristal_pequeno(ox, oy, aura=False)
        elif kind == "cursed":
            for (x, y) in P([(ox + 0.5, oy - 2.5), (ox + 3.5, oy + 0.5), (ox + 0.5, oy + 3.5), (ox - 2.5, oy + 0.5)]):
                t.put(x, y, "V1")
            t.put(ox, oy, "V2")
            t.put(ox, oy - 1, "WH")
        else:
            t.estrela(ox, oy, 2)
    if kind == "cursed":                            # espinhos violeta na borda de cima e de baixo
        for i in range(4):
            bx = 16 + i * 6
            for (y0, dy) in ((0, 1), (H - 1, -1)):
                for x in (bx, W - 1 - bx):
                    t.put(x, y0, "V1")
                    t.put(x, y0 + dy, "V2")
                    t.put(x + 1, y0, "V1")

    # brasão no meio da borda de cima e de baixo
    for (cy, sy) in ((2.0, 1), (H - 2.0, -1)):
        m = {p for p in E(CX, cy, 7.0) if (p[1] + 0.5 - cy) * sy >= 0}
        t.fill(m, "K")
        for x, y in borda(m):
            if (y + 0.5 - cy) * sy > 0.6:
                t.put(x, y, "B3")
        lo = P([(CX, cy + sy * 0.5), (CX + 2.5, cy + sy * 3.0), (CX, cy + sy * 5.5), (CX - 2.5, cy + sy * 3.0)])
        t.fill(lo, "B3")
        t.fill(encolher(lo), "B4")
    # losangos no meio das laterais
    for x0 in (5.5, W - 5.5):
        lo = P([(x0, CY - 5), (x0 + 3, CY), (x0, CY + 5), (x0 - 3, CY)])
        t.fill(lo, "K")
        for x, y in borda(lo):
            t.put(x, y, "B3")
        t.put(int(x0), int(CY) - 1, "B4")
        t.put(int(x0), int(CY), "B2")

    # faixas do título e do nome
    for y0, txt in ((14, rotulo), (H - 28, nome)):
        x0, x1 = 12, W - 13
        for x in range(x0, x1 + 1):
            for y in range(y0 + 1, y0 + 13):
                t.put(x, y, "G1" if (x + y) % 2 == 0 else "K")
            t.put(x, y0, "B3")
            t.put(x, y0 + 13, "B3")
            if (x + y0) % 2 == 0:
                t.put(x, y0 + 1, "B0")
                t.put(x, y0 + 12, "B0")
        for sx, xb in ((-1, x0), (1, x1)):          # pontas em chevron
            for k in range(1, 5):
                for yy in range(y0 + 2 * k - 1, y0 + 14 - 2 * k + 1):
                    t.put(xb + sx * k, yy, "K")
                for yy in (y0 + 2 * k - 1, y0 + 2 * k, y0 + 13 - 2 * k + 1, y0 + 13 - 2 * k):
                    t.put(xb + sx * k, yy, "B3")
            for yy in (y0 + 6, y0 + 7):
                t.put(xb + sx * 4, yy, "B3")
                t.put(xb + sx * 5, yy, "B4" if yy == y0 + 6 else "B2")
            t.put(xb + sx * 1, y0 + 6, "B2")
            t.put(xb + sx * 1, y0 + 7, "B1")
        if all(c in "IVX" for c in txt):
            numeral(t, txt, y0 + 3)
            largura = sum(len(GRANDE[c][0]) for c in txt) + 2 * (len(txt) - 1)
        else:
            texto(t, txt, y0 + 5)
            largura = largura_texto(txt)
        if largura <= 62:                             # losangos ao lado do texto curto
            for sx in (-1, 1):
                xd = CX + sx * (largura / 2.0 + 7)
                lo = P([(xd, y0 + 3.5), (xd + 3, y0 + 7), (xd, y0 + 10.5), (xd - 3, y0 + 7)])
                for x, y in borda(lo):
                    t.put(x, y, "B2")
                t.put(int(xd), y0 + 6, "B4")


# ---------- ilustrações ----------

def il_pistao_runico(t):
    # rastro do impulso acima da tampa
    for i, (x, topo) in enumerate(((47, 44), (51, 39), (60, 39), (64, 44))):
        for y in range(topo, 54):
            f = (y - topo) / (54 - topo)
            c = "B3" if f > 0.66 else ("B2" if f > 0.33 else ("B1" if y % 2 == 0 else None))
            if c:
                t.put(x, y, c)
    t.estrela(55, 33, 4)
    t.estrela(44, 39, 1)
    t.estrela(67, 39, 1)

    cy_anel = 86.0
    anelm = E(CX, cy_anel, 27, 7.5) - E(CX, cy_anel, 24, 5.0)
    tras = {p for p in anelm if p[1] + 0.5 < cy_anel}
    t.fill(tras, "C0")
    for x, y in tras:
        if (x + y) % 3 == 0:
            t.put(x, y, "C1")

    # baforadas de vapor dos escapes de baixo
    # haste e pé
    t.pintar(R(52, 108, 60, 132), cil_x(52, 60))
    t.pintar(R(49, 115, 63, 119), cil_x(49, 63), chanfro=0.1)
    pe = R(36, 131, 76, 137)
    t.pintar(pe, linear(36, 131, 76, 137, 0.9, 0.12), chanfro=0.18)
    t.pintar(R(42, 128, 70, 131), cil_x(42, 70, 0.9), chanfro=0.1)
    for x in (39, 72):
        t.rebite(x, 133)
    # corpo
    corpo = R(43, 68, 69, 106)
    t.pintar(corpo, cil_x(43, 69))
    for y in (74, 101):                              # sulcos
        for x in range(44, 68):
            t.put(x, y, "B1")
    # tirantes laterais
    for x0 in (39.0, 71.0):
        t.pintar(R(x0, 68, x0 + 2, 106), cil_x(x0, x0 + 2, 0.9))
    # visor de cristal no corpo
    visor = R(51, 93, 61, 100)
    t.fill(visor, "C0")
    for x, y in visor:
        if y in (95, 96, 97) and x not in (51, 60):
            t.put(x, y, "C1")
    for x in range(53, 59):
        t.put(x, 96, "C2")
    t.put(54, 96, "WH")
    for x, y in fora(visor):
        t.put(x, y, "B0" if y > 96 else "B1")
    # flanges, tampa, botão
    for y0 in (64, 105):
        t.pintar(R(37, y0, 75, y0 + 4), cil_x(37, 75), chanfro=0.15)
        for x in (41, 50, 61, 70):
            t.rebite(x, y0 + 1)
    t.pintar(R(46, 58, 66, 64), cil_x(46, 66), chanfro=0.12)
    t.pintar(R(52, 54, 60, 58), cil_x(52, 60), chanfro=0.12)
    for x in range(48, 65, 3):                       # runas gravadas na tampa
        t.put(x, 61, "C1")
    t.put(55, 61, "C2")
    # anel rúnico: metade da frente
    frente = {p for p in anelm if p[1] + 0.5 >= cy_anel}
    for x, y in fora(frente):
        if (x, y) not in tras and t.get(x, y) not in BRILHO:
            t.put(x, y, "C0")
    for x, y in frente:
        topo = (x, y - 1) not in frente
        t.put(x, y, "C2" if topo or (x, y - 2) not in frente else "C1")
    for i, x in enumerate(range(33, 80, 4)):       # runas no anel
        col = [yy for (xx, yy) in frente if xx == x]
        if not col:
            continue
        y = max(col)
        t.put(x, y - 1, "WH")
        t.put(x + (1 if i % 2 else -1), y - 2, "WH")
        t.put(x, y, "C0")
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
    t.pintar(R(32, 137, 66, 142), linear(32, 137, 66, 142), chanfro=0.18)
    caixa = R(35, 121, 63, 137)
    t.pintar(caixa, linear(35, 121, 63, 137, 0.8, 0.0), chanfro=0.15)
    porta = R(39, 125, 59, 135)
    t.fill(porta, "K")
    for x, y in fora(porta):
        if (x, y) in caixa:
            t.put(x, y, "B0" if y < 125 or x < 39 else "B3")
    for cx, h, w, inc in ((44, 8, 2.2, -1), (49, 10, 2.6, 1), (54, 8, 2.2, 1)):
        t.chama(cx, 135, h, w, inc)
    for x in range(40, 59, 3):                     # grade
        for y in range(125, 135):
            if y % 2 == 0 or y > 131:
                t.put(x, y, "B1")
    for x in (37, 61):
        t.rebite(x, 123)
        t.rebite(x, 134)
    # bico (cano inclinado)
    ang = math.atan2(76 - 102, 84 - 64)
    cano = L([(64, 102), (84, 76)], 7.0)
    t.pintar(cano, local(cil_x(-3.5, 3.5), 74, 89, ang + math.pi / 2))
    boca = L([(81, 72), (90, 79)], 3.6)
    t.pintar(boca, linear(80, 70, 92, 80), chanfro=0.15)
    # caldeira
    cx, cy, r = 49.0, 104.0, 19.0
    corpo = E(cx, cy, r)
    t.pintar(corpo, esfera(cx, cy, r))
    cinta = {p for p in corpo if 101 <= p[1] <= 105}
    t.pintar(cinta, lambda x, y: esfera(cx, cy, r)(x, y) * 0.8 + (0.12 if y == 101 else -0.05), contorno=False)
    for x, y in corpo:
        if y == 106:
            t.put(x, y, "B0")
    for i in range(-3, 4):
        x = int(cx + i * 5.2)
        if (x, 103) in corpo:
            t.rebite(x, 102)
    for a in range(200, 341, 28):                 # rebites da cúpula
        ra = math.radians(a)
        t.rebite(int(cx + math.cos(ra) * 14), int(cy - 2 + math.sin(ra) * 12))
    # tampa
    t.pintar(R(41, 83, 57, 87), cil_x(41, 57), chanfro=0.12)
    t.pintar(R(46, 79, 52, 83), cil_x(46, 52), chanfro=0.1)
    t.put(48, 77, "B3")
    t.put(49, 77, "B2")
    # manômetro na caldeira
    m = E(cx - 8, cy + 9, 4.6)
    t.pintar(m, esfera(cx - 8, cy + 9, 4.6, 1.1))
    t.fill(E(cx - 8, cy + 9, 3.0), "K")
    for x, y in B((40, 113), (43, 111)):
        t.put(x, y, "B4")
    t.put(43, 110, "C2")


def il_arco_voltaico(t):
    # base
    t.pintar(R(30, 134, 82, 140), linear(30, 134, 82, 140), chanfro=0.18)
    t.pintar(R(40, 126, 72, 134), cil_x(40, 72), chanfro=0.12)
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
        costas = {p for p in espira if p[1] + 0.5 < cy and not (47 <= p[0] < 65)}
        frente = {p for p in espira if p[1] + 0.5 >= cy - 0.5}
        t.fill(costas, "B1")
        t.pintar(frente, cil_x(38.5, 73.5), contorno=False)
        for x, y in frente:
            if (x, y + 1) not in frente and (x, y + 1) not in espira:
                t.put(x, y + 1, "B0") if t.get(x, y + 1) in FUNDO or (47 <= x < 65) else None
    # colar e esfera
    t.pintar(R(50, 74, 62, 80), cil_x(50, 62), chanfro=0.1)
    esf = E(CX, 64.0, 12.0)
    t.pintar(esf, esfera(CX, 64.0, 12.0))
    for x, y in borda(E(CX, 64.0, 8.0)):
        if y > 67:
            t.put(x, y, "B1")
    t.cristal(CX, 51.0, 4.5, 8.5)
    # descargas
    t.raio([(47, 57), (40, 52), (37, 45), (30, 42), (25, 35)])
    t.raio([(65, 58), (72, 55), (75, 48), (82, 46), (86, 39)])
    t.raio([(58, 44), (61, 40), (57, 36), (59, 33)], ponta=False)
    t.estrela(59, 33, 2, ("WH", "C2", "C1", "C0"))
    t.raio([(44, 70), (36, 74), (33, 81)], ponta=False)
    t.raio([(68, 69), (75, 72), (78, 79)], ponta=False)


def engrenagem(cx, cy, r, dentes, h, fase=0.0, cheio=0.5):
    """Máscara de engrenagem com dentes trapezoidais (mais finos na ponta)."""
    m = set()
    for y in range(int(cy - r - h) - 1, int(cy + r + h) + 2):
        for x in range(int(cx - r - h) - 1, int(cx + r + h) + 2):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            if d <= r:
                m.add((x, y))
            elif d <= r + h:
                a = (math.atan2(dy, dx) / math.tau * dentes - fase) % 1.0
                meia = cheio / 2 - (d - r) / h * 0.1
                if abs(a - 0.5) < meia:
                    m.add((x, y))
    return m


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
    for x, y in borda(E(CX, CY, 17.0)):
        t.put(x, y, "B0")
    for x, y in borda(E(CX, CY, 18.0)):
        if x + y < CX + CY - 2:
            t.put(x, y, "B3")
    for i in range(6):                               # furos de alívio
        a = math.radians(i * 60)
        hx, hy = CX + math.cos(a) * 19.5, CY + math.sin(a) * 19.5
        furo = E(hx, hy, 1.5)
        t.fill(furo, "K")
        for x, y in fora(furo):
            if (x, y) in eng and y > hy:
                t.put(x, y, "B3")
    # anel interno com parafusos
    inn = E(CX, CY, 15.0)
    t.pintar(inn, linear(CX - 15, CY - 15, CX + 15, CY + 15, 0.85, 0.0), chanfro=0.2, contorno=False)
    for i in range(10):
        a = i * math.tau / 10
        t.rebite(int(CX - 0.5 + math.cos(a) * 12.3), int(CY - 0.5 + math.sin(a) * 12.3))
    # soquete do cristal
    soq = E(CX, CY, 9.0)
    t.fill(soq, "K")
    for x, y in borda(soq):
        t.put(x, y, "B0" if x + y < CX + CY else "B3")
    for x, y in soq - borda(soq):
        if bayer(x, y) < 0.3:
            t.put(x, y, t.arc[0])
    t.cristal(CX, CY, 5.0, 8.5, aura=False)
    # pontos de luz arcana nos vãos (a mina está armada)
    for i in range(6):
        a = math.radians(i * 60)
        t.put(int(CX - 0.5 + math.cos(a) * 24.5), int(CY - 0.5 + math.sin(a) * 24.5), "C2")


def il_broquel_cantante(t):
    # ondas sonoras dos dois lados
    for k, r in enumerate((32.0, 36.5, 41.0)):
        for y in range(int(CY - r) - 1, int(CY + r) + 2):
            for x in range(int(CX - r) - 1, int(CX + r) + 2):
                dx, dy = x + 0.5 - CX, y + 0.5 - CY
                d = math.hypot(dx, dy)
                if r - 0.6 <= d < r + 0.6:
                    ang = abs(math.degrees(math.atan2(dy, abs(dx))))
                    lim = 28 - k * 4
                    if ang < lim and (ang < lim - 7 or (x + y) % 2 == 0):
                        t.put(x, y, "C2" if ang < lim * 0.5 else "C1")
    # escudo
    R0 = 27.0
    aro = E(CX, CY, R0)
    t.pintar(aro, esfera(CX, CY, R0, 1.0, 0.05))
    face = E(CX, CY, 22.5)
    for x, y in borda(face):
        t.put(x, y, "B0")
    face2 = encolher(face)
    # face em gomos alternados, como um sol gravado
    for x, y in face2:
        dx, dy = x + 0.5 - CX, y + 0.5 - CY
        a = (math.atan2(dy, dx) / math.tau * 16) % 1.0
        base = esfera(CX, CY, 22.5, 0.8, -0.05)(x, y)
        v = base + (0.12 if a < 0.5 else -0.12)
        t.put(x, y, OURO[quant(v, x, y, 5)])
    for i in range(16):                            # tachas no aro
        a = i * math.tau / 16 + math.tau / 32
        t.rebite(int(CX - 0.5 + math.cos(a) * 24.8), int(CY - 0.5 + math.sin(a) * 24.8))
    # anel e cubo central
    an = anel(CX, CY, 10.5, 13.0)
    t.pintar(an, esfera(CX, CY, 13.0, 1.0, 0.05), contorno=False)
    for x, y in fora(an):
        if math.hypot(x + 0.5 - CX, y + 0.5 - CY) > 12:
            t.put(x, y, "B0")
    cubo = E(CX, CY, 10.5)
    t.fill(cubo, "K")
    for x, y in borda(cubo):
        t.put(x, y, "B0")
    t.cristal(CX, CY, 5.5, 8.5, aura=False)
    for x, y in cubo - borda(cubo):
        if t.get(x, y) == "K" and bayer(x, y) < 0.35:
            t.put(x, y, t.arc[0])


def il_tonico_oleo_luz(t):
    cx, cy, r = CX, 111.0, 21.0
    # tampa de latão, gargalo e boca
    t.pintar(R(48, 61, 64, 68), cil_x(48, 64), chanfro=0.12)
    t.pintar(R(52, 56, 60, 61), cil_x(52, 60), chanfro=0.1)
    t.cristal_pequeno(55, 52)
    for x in (50, 61):
        t.rebite(x, 63)
    t.pintar(R(45, 68, 67, 72), cil_x(45, 67), chanfro=0.15)
    # vidro: bulbo + gargalo
    garg = R(49, 72, 63, 92)
    bulbo = E(cx, cy, r)
    vidro = garg | bulbo
    t.contorno(vidro, "B2", "B1")
    for x, y in vidro:
        t.put(x, y, "G1" if bayer(x, y) < 0.3 else "K")
    for x, y in borda(vidro):
        t.put(x, y, "B3" if (x - 1, y) not in vidro or (x, y - 1) not in vidro else "B2")
    # óleo luminoso
    oleo = {p for p in encolher(bulbo) if p[1] + 0.5 > 107 + math.sin((p[0]) * 0.55) * 1.2}
    for x, y in oleo:
        dd = math.hypot(x + 0.5 - cx, y + 0.5 - 117) / 16.0
        v = 0.88 - dd * 0.7
        t.put(x, y, OURO[quant(v, x, y, 5, 0.8)])
    for x, y in oleo:
        if (x, y - 1) not in oleo:
            t.put(x, y, "B4")
    # cristal suspenso
    t.cristal(cx, 118.0, 4.5, 7.5, aura=False)
    for x, y in (dilatar(E(cx, 118, 7), 2) - dilatar(E(cx, 118, 7), 1)):
        if (x, y) in oleo and (x + y) % 2 == 0:
            t.put(x, y, "C1")
    # bolhas
    for bx, by, grande in ((45, 113, 1), (66, 116, 1), (61, 109, 0), (49, 125, 0), (63, 125, 1), (52, 110, 0)):
        if grande:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                t.put(bx + dx, by + dy, "B4")
            t.put(bx, by, "C1")
        else:
            t.put(bx, by, "B4")
    # reflexo no vidro
    for a in range(196, 250, 6):
        ra = math.radians(a)
        t.put(int(cx + math.cos(ra) * 16), int(cy + math.sin(ra) * 16), "B4")
    for y in range(75, 88):
        t.put(51, y, "B3" if y % 4 else "B4")
    # etiqueta de latão no gargalo
    t.pintar(R(49, 83, 63, 86), cil_x(49, 63, 0.9), chanfro=0.1, contorno=False)


def il_granada_cristal(t):
    cx, cy, r = CX, 106.0, 22.0
    # pavio e faísca
    pav = [(57, 78), (54, 72), (57, 67), (61, 63)]
    for x, y in BL(pav):
        t.put(x, y, "B2")
        t.put(x + 1, y, "B1")
    t.estrela(62, 58, 4)
    for x, y in ((67, 54), (68, 61), (57, 54), (70, 57)):
        t.put(x, y, "B3")
    # argola
    arg = anel(73.0, 80.0, 3.0, 5.0)
    t.pintar(arg, esfera(73, 80, 5))
    corpo = E(cx, cy, r)
    t.pintar(corpo, esfera(cx, cy, r))
    # cinta equatorial
    cinta = {p for p in corpo if abs(p[1] + 0.5 - (cy + 3 + (p[0] + 0.5 - cx) ** 2 / (r * r) * -3)) < 2.1}
    t.pintar(cinta, lambda x, y: esfera(cx, cy, r)(x, y) * 0.75 + 0.1, contorno=False)
    for x, y in cinta:
        if (x, y + 1) not in cinta and (x, y + 1) in corpo:
            t.put(x, y + 1, "B0")
    for i in range(-3, 4):
        x = int(cx + i * 5.5)
        ys = [yy for (xx, yy) in cinta if xx == x]
        if ys:
            t.rebite(x, min(ys) + 1)
    # tampa
    t.pintar(R(47, 78, 65, 86), cil_x(47, 65), chanfro=0.12)
    t.pintar(R(44, 84, 68, 87), cil_x(44, 68), chanfro=0.12)
    # rachaduras arcanas
    rach = [
        [(46, 96), (51, 101), (48, 108), (54, 114), (52, 120)],
        [(67, 94), (62, 101), (67, 108), (64, 117), (68, 122)],
        [(51, 101), (56, 104), (62, 101)],
        [(48, 108), (40, 110)],
    ]
    for c in rach:
        nuc = BL(c)
        for x, y in nuc:
            for q in viz4(x, y):
                if q in corpo and t.get(*q) not in BRILHO:
                    t.put(*q, "B0")
        for i, (x, y) in enumerate(nuc):
            t.put(x, y, "C2" if i % 4 else "WH")
    t.cristal(56.0, 109.0, 4.0, 6.5, aura=False)


def il_caldeira_interna(t):
    # cajado
    t.pintar(R(53, 56, 59, 156), cil_x(53, 59))
    t.pintar(P([(53, 155), (59, 155), (56, 162)]), cil_x(53, 59))
    for y0 in (66, 136, 143):
        t.pintar(R(50, y0, 62, y0 + 4), cil_x(50, 62), chanfro=0.12)
    # garras segurando o cristal
    for sx in (-1, 1):
        garra = L([(CX + sx * 2, 58), (CX + sx * 7, 50), (CX + sx * 6, 40), (CX + sx * 3, 35)], 2.2)
        t.pintar(garra, linear(40, 30, 72, 62), chanfro=0.12)
    # raios do cristal
    for i in range(16):
        a = i * math.tau / 16
        r0, r1 = 13, (21 if i % 2 == 0 else 17)
        for k, (x, y) in enumerate(B((CX - 0.5 + math.cos(a) * r0, 45 + math.sin(a) * r0),
                                     (CX - 0.5 + math.cos(a) * r1, 45 + math.sin(a) * r1))):
            if t.fundo_em(x, y) and (k < 3 or k % 2 == 0):
                t.put(x, y, "C1" if k < 2 else "C0")
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
        cano = R(min(x0, x0 + sx * 8), 100, max(x0, x0 + sx * 8), 104)
        t.pintar(cano, cil_y(100, 104))
        t.pintar(R(min(x0 + sx * 8, x0 + sx * 11), 97, max(x0 + sx * 8, x0 + sx * 11), 107), cil_x(0, 1, 0, 0.6), chanfro=0.2)
        vx = int(CX + sx * 28.5 - 0.5)
        for k in range(12):                          # fio de vapor subindo
            x = vx + int(round(math.sin(k * 0.7) * 1.2)) * sx
            y = 95 - k
            if k < 6 or k % 2 == 0:
                t.put(x, y, "B2" if k < 4 else "B1")
    # manômetro
    aro = E(cx, cy, 11.0)
    t.pintar(aro, esfera(cx, cy, 11.0, 1.0, 0.15), contorno=True, claro="B0", escuro="B0")
    mostrador = E(cx, cy, 8.5)
    t.fill(mostrador, "K")
    for x, y in mostrador:
        if bayer(x, y) < 0.2:
            t.put(x, y, "G2")
    for i in range(9):
        a = math.radians(150 + i * 30)
        x, y = int(cx - 0.5 + math.cos(a) * 7.0 + 0.5), int(cy - 0.5 + math.sin(a) * 7.0 + 0.5)
        t.put(x, y, "B3" if i % 2 == 0 else "B2")
    for i in range(5, 9):                          # zona arcana do mostrador
        a = math.radians(150 + i * 30)
        t.put(int(cx - 0.5 + math.cos(a) * 7.0 + 0.5), int(cy - 0.5 + math.sin(a) * 7.0 + 0.5), "C2")
    for x, y in B((55, 103), (60, 98)):
        t.put(x, y, "B4")
    t.put(55, 103, "B3")
    t.put(56, 103, "B2")
    # bocais embaixo da caldeira, com o calor do golpe virando energia
    for x0 in (47.0, 63.0):
        t.pintar(R(x0 - 2, 121, x0 + 1, 127), cil_x(x0 - 2, x0 + 1))
        for k in range(4):
            t.put(int(x0) - 1 + (k % 2), 129 + k * 2, "C1" if k < 2 else "C0")


def il_mola_recuo(t):
    # setas laterais de recuo
    for x in (23, 88):
        for y in range(76, 124):
            if y % 2 == 0 or y < 92:
                t.put(x, y, "B2" if y < 100 else "B1")
        for k in range(4):
            for dx in range(-k, k + 1):
                t.put(x + dx, 74 + k, "B3" if abs(dx) < k else "B2")
        t.put(x, 73, "B4")
    # placa de baixo
    t.pintar(R(30, 130, 82, 136), linear(30, 130, 82, 136), chanfro=0.18)
    t.pintar(R(36, 136, 42, 140), cil_x(36, 42))
    t.pintar(R(70, 136, 76, 140), cil_x(70, 76))
    # mola em hélice: costas escuras primeiro, depois a frente iluminada
    voltas, y0, y1, rx = 5.5, 128.0, 70.0, 19.0
    passos = 900
    for frente in (False, True):
        for i in range(passos + 1):
            u = i / passos
            a = u * voltas * math.tau
            x = CX + math.cos(a) * rx
            y = y0 + (y1 - y0) * u + math.sin(a) * 3.0
            ef = math.sin(a) > 0
            if ef != frente:
                continue
            for (px, py) in E(x, y, 1.7):
                if frente:
                    v = 0.95 - abs((px + 0.5 - CX) / rx + 0.35) * 0.55 + (0.1 if py + 0.5 < y else -0.12)
                    t.put(px, py, OURO[quant(v, px, py, 5, 0.4)])
                else:
                    t.put(px, py, "B1" if (px + py) % 3 else "B0")
    # placa de cima e suporte do cristal
    t.pintar(R(34, 64, 78, 70), linear(34, 64, 78, 70), chanfro=0.18)
    for x in (37, 74):
        t.rebite(x, 66)
        t.rebite(x, 132)
    t.pintar(P([(48, 64), (51, 57), (61, 57), (64, 64)]), cil_x(48, 64), chanfro=0.1)
    t.cristal(CX, 47.0, 5.0, 9.5)


def il_manopla_pistonada(t):
    # antebraço
    t.pintar(R(41, 121, 71, 153), cil_x(41, 71))
    for y0 in (127, 146):
        t.pintar(R(40, y0, 72, y0 + 3), cil_x(40, 72), chanfro=0.12)
    for x in range(44, 70, 5):
        t.rebite(x, 137)
    # punho
    t.pintar(R(43, 112, 69, 122), cil_x(43, 69, 0.75), chanfro=0.1)
    for y in (115, 118):
        for x in range(44, 68):
            t.put(x, y, "B1")
    # pistões nas laterais do braço, ligados à mão
    for x0 in (36.0, 76.0):
        t.pintar(R(x0 - 1.5, 106, x0 + 1.5, 130), cil_x(x0 - 1.5, x0 + 1.5, 1.05))
        t.pintar(R(x0 - 3.5, 129, x0 + 3.5, 149), cil_x(x0 - 3.5, x0 + 3.5))
        for y0 in (129, 146):
            t.pintar(R(x0 - 4.5, y0, x0 + 4.5, y0 + 3), cil_x(x0 - 4.5, x0 + 4.5), chanfro=0.12)
        t.pintar(E(x0, 150.5, 2.0), esfera(x0, 150.5, 2.0))
    # polegar
    pol = L([(31, 108), (28, 99), (31, 90)], 7.0)
    t.pintar(pol, linear(22, 85, 36, 112), chanfro=0.15)
    for x in range(26, 33):
        t.put(x, 99, "B0")
    t.rebite(28, 101)
    # dorso da mão
    mao = R(35, 82, 77, 112) - {(35, 82), (76, 82), (35, 111), (76, 111)}
    t.pintar(mao, linear(35, 82, 77, 112, 1.0, 0.06), chanfro=0.2)
    for x in range(36, 76):                          # placa das juntas
        t.put(x, 89, "B0")
    for x in (36, 76):
        t.pintar(E(x, 107.5, 2.6), esfera(x, 107.5, 2.6))
    # dedos
    dedos = set()
    for i in range(4):
        x0 = 37.0 + i * 10
        d = R(x0, 59, x0 + 8, 84) - {(int(x0), 59), (int(x0) + 7, 59)}
        dedos |= d
        t.pintar(d, cil_x(x0, x0 + 8), chanfro=0.08)
        for y in (67, 75):
            for x in range(int(x0) + 1, int(x0) + 7):
                t.put(x, y, "B0")
            t.put(int(x0) + 2, y + 1, "B4")
        kn = E(x0 + 4, 86.0, 3.0)                    # juntas
        t.pintar(kn, esfera(x0 + 4, 86.0, 3.0), contorno=False)
    for x in range(36, 77):
        for y in range(60, 83):
            if (x, y) not in dedos and t.get(x, y) in ("B0", "B1"):
                t.put(x, y, "K" if (x - 1, y) in dedos and (x + 2, y) in dedos else t.get(x, y))
    # engrenagem no dorso com cristal
    eng = engrenagem(CX, 100.0, 7.5, 10, 3.0, fase=0.5)
    t.pintar(eng, linear(44, 88, 68, 112, 1.0, 0.1), chanfro=0.2)
    soq = E(CX, 100.0, 5.0)
    t.fill(soq, "K")
    for x, y in borda(soq):
        t.put(x, y, "B0" if x + y < 156 else "B3")
    t.cristal(CX, 100.0, 3.0, 5.0)
    for x, y in ((39, 93), (72, 93)):
        t.rebite(x, y)


def il_lente_prismatica(t):
    cy = 90.0
    # feixe que entra
    for x in range(10, 34):
        for y in (89, 90):
            if x > 16 or (x + y) % 2 == 0:
                t.put(x, y, "B3" if y == 89 else "B2")
    # feixes que saem (espectro)
    for k, ang in enumerate((-0.42, -0.21, 0.0, 0.21, 0.42)):
        p0 = (79, cy - 0.5)
        p1 = (101, cy - 0.5 + math.tan(ang) * 22)
        cor = ("C2", "B3", "WH", "B3", "C2")[k]
        for i, (x, y) in enumerate(B(p0, p1)):
            if i < 14 or i % 2 == 0:
                t.put(x, y, cor)
    # cabo
    t.pintar(R(52, 112, 60, 150), cil_x(52, 60))
    for y in range(119, 147, 4):
        for x in range(52, 60):
            t.put(x, y + (x - 52) // 4, "B0")
    t.pintar(R(49, 111, 63, 117), cil_x(49, 63), chanfro=0.12)
    t.pintar(E(CX, 152.0, 4.5), esfera(CX, 152.0, 4.5))
    # aro da lente
    aro = anel(CX, cy, 18.5, 24.0)
    t.pintar(aro, esfera(CX, cy, 24.0, 1.0, 0.08))
    for x, y in borda(E(CX, cy, 18.5)) | fora(E(CX, cy, 18.5)):
        if (x, y) in aro:
            t.put(x, y, "B0" if x + y < CX + cy else "B2")
    for i in range(8):
        a = i * math.tau / 8 + math.tau / 16
        t.rebite(int(CX - 0.5 + math.cos(a) * 21.3), int(cy - 0.5 + math.sin(a) * 21.3))
    # vidro e cristal grande
    vidro = E(CX, cy, 17.5)
    for x, y in vidro:
        t.put(x, y, "C0" if bayer(x, y) < 0.18 else ("G1" if bayer(x, y) < 0.45 else "K"))
    t.cristal(CX, cy, 9.0, 14.0, aura=False)
    for a in range(200, 246, 5):
        ra = math.radians(a)
        x, y = int(CX + math.cos(ra) * 15), int(cy + math.sin(ra) * 15)
        if t.get(x, y) not in BRILHO:
            t.put(x, y, "B4")


def il_lamina_sedenta(t):
    ang = math.radians(20)
    ox, oy = 56.0, 100.0

    def g(pts):
        return girar(pts, ox, oy, ang)

    # espinhos violeta em volta
    for i in range(9):
        a = math.radians(200 + i * 17.5)
        p0 = (CX + math.cos(a) * 41, CY + math.sin(a) * 41)
        p1 = (CX + math.cos(a) * 46, CY + math.sin(a) * 46)
        for k, (x, y) in enumerate(B(p0, p1)):
            t.put(x, y, "V2" if k == 0 else "V1")
    # lâmina
    lam = P(g([(-6.5, 16), (-6.5, -38), (0, -64), (6.5, -38), (6.5, 16)]))
    serr = set()
    for yy in (-28, -16, -4, 8):
        serr |= P(g([(6, yy), (10.5, yy - 5), (6, yy - 7)]))
    lam_tudo = lam | serr

    def luz_lamina(x, y):
        lx = x + 0.5
        if lx > 6.2:
            return 0.45                              # dentes da serra
        return 0.9 if lx < -0.6 else (1.0 if lx < 0.6 else 0.6)
    t.pintar(lam_tudo, local(luz_lamina, ox, oy, ang), chanfro=0.12)
    # veias violeta (a lâmina tem sede)
    veia = [(1, -50), (3, -42), (1, -33), (4, -24), (2, -14), (4, -5), (2, 4), (3, 12)]
    for i, (x, y) in enumerate(BL([tuple(int(round(v)) for v in g([p])[0]) for p in veia])):
        t.put(x, y, "V2" if i % 5 == 0 else "V1")
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
        tira = BL([tuple(int(round(v)) for v in g([(-3, 24 + k * 4)])[0]),
                   tuple(int(round(v)) for v in g([(3, 22 + k * 4)])[0])])
        for x, y in tira:
            if (x, y) in cabo:
                t.put(x, y, "B0")
    px, py = g([(0, 45)])[0]
    t.pintar(E(px, py, 4.2), esfera(px, py, 4.2))
    t.put(int(px), int(py), "V2")
    gx, gy = g([(0, 18)])[0]
    t.cristal(gx, gy, 3.2, 5.5, aura=False)
    # gotas
    gota_g = ["..1..", "..1..", ".121.", "12221", "1w221", "12221", ".111."]
    gota_p = [".1.", ".1.", "121", "1w1", ".1."]
    cores = {"1": "V1", "2": "V2", "w": "WH"}
    for (x, y, spr) in ((80, 110, gota_g), (85, 123, gota_p), (79, 133, gota_p), (32, 66, gota_p)):
        for j, row in enumerate(spr):
            for i, ch in enumerate(row):
                if ch != ".":
                    t.put(x + i, y + j, cores[ch])
    t.halo_arcano(lam_tudo, 2, 60)


def il_chamine_partida(t):
    base, topo = 152.0, 86.0
    # nuvens e raio
    t.nuvem(31, 56, 9)
    t.nuvem(83, 46, 9, flip=True)
    # torre
    torre = P([(36, base), (76, base), (71, topo), (41, topo)])
    quebra = P([(41, topo - 1), (48, topo + 4), (54, topo - 1), (60, topo + 6), (66, topo + 2), (72, topo - 1),
                (72, topo - 8), (40, topo - 8)])
    torre = torre - quebra
    lin = 5
    for x, y in torre:
        fila = int((base - (y + 0.5)) // lin)
        off = 4 if fila % 2 else 0
        junta_h = int(base - (y + 0.5)) % lin == 0
        junta_v = (x + off) % 8 == 0
        globo = perfil((x + 0.5 - 36) / 40.0)
        if junta_h or junta_v:
            c = "B0"
        else:
            topo_tijolo = int(base - (y + 0.5)) % lin == lin - 1
            esq = (x + off) % 8 == 1
            v = globo * 0.85 + (0.18 if (topo_tijolo or esq) else 0.0) + ((x * 7 + y * 3) % 5 - 2) * 0.02
            c = OURO[quant(v, x, y, 5, 0.5)]
        t.put(x, y, c)
    t.contorno(torre, "B1", "B0")
    # janelas: uma de fornalha e uma arcana
    for (wx, wy, cor) in ((56.0, 108.0, "C"), (56.0, 126.0, "F")):
        jan = R(wx - 3, wy - 4, wx + 3, wy + 4) | E(wx, wy - 4, 3.0)
        t.contorno(jan, "B3", "B2")
        if cor == "C":
            t.fill(jan, "C1")
            t.fill(R(wx - 1, wy - 5, wx + 1, wy + 3), "C2")
            t.put(int(wx) - 1, int(wy) - 4, "WH")
        else:
            t.fill(jan, "B4")
            t.fill(R(wx - 3, wy + 1, wx + 3, wy + 4), "B3")
    # boca da fornalha
    boca = R(49, 140, 63, 152) | E(56.0, 140.0, 7.0)
    boca = {p for p in boca if p[1] < 152}
    t.contorno(boca, "B3", "B2")
    t.fill(boca, "K")
    for cx2, h, w in ((52, 9, 2.3), (56, 11, 2.6), (60, 8, 2.2)):
        t.chama(cx2, 152, h, w)
    # chão
    t.pintar(R(28, 152, 84, 156), linear(28, 152, 84, 156), chanfro=0.18)
    # topo partido voando
    peca_pts = [(-17, -4), (17, -4), (19, 1), (-19, 1), (-17, 4), (17, 4)]
    peca = P(girar([(-18, -4), (18, -4), (16, 4), (-16, 4)], 62, 70, math.radians(-22)))
    t.pintar(peca, local(lambda x, y: 0.9 if y < -1 else 0.55, 62, 70, math.radians(-22)), chanfro=0.15)
    for k in (-9, 0, 9):
        a, b = girar([(k, -3), (k, 3)], 62, 70, math.radians(-22))
        for x, y in B(a, b):
            if (x, y) in peca:
                t.put(x, y, "B0")
    # raio arcano atingindo a torre
    t.raio([(66, 31), (60, 42), (66, 49), (57, 60), (61, 66), (55, 80)], ponta=False)
    t.estrela(55, 82, 3, ("WH", "C2", "C1", "C0"))
    # tijolos caindo
    for (bx, by, a) in ((26, 98, 20), (88, 92, -25), (24, 124, -10), (88, 118, 30), (33, 82, 40)):
        tij = P(girar([(-2.5, -1.5), (2.5, -1.5), (2.5, 1.5), (-2.5, 1.5)], bx, by, math.radians(a)))
        t.pintar(tij, linear(bx - 3, by - 2, bx + 3, by + 2), chanfro=0.1)
        for k in range(3):
            if t.fundo_em(int(bx), int(by) - 4 - k * 2):
                t.put(int(bx), int(by) - 4 - k * 2, "B1" if k == 0 else "B0")


def il_artifice(t):
    # lemniscata (o Mago do tarô)
    lem = []
    for i in range(240):
        a = i * math.tau / 240
        k = 1 + math.sin(a) ** 2
        lem.append((CX + 15 * math.cos(a) / k, 46 + 15 * math.sin(a) * math.cos(a) / k))
    m = set()
    for x, y in lem:
        m |= E(x, y, 1.5)
    t.pintar(m, linear(40, 38, 72, 54, 1.0, 0.12), chanfro=0.15)
    # raios do cristal
    for i in range(20):
        a = i * math.tau / 20 + 0.08
        r0, r1 = 15, (25 if i % 2 == 0 else 20)
        for k, (x, y) in enumerate(B((CX - 0.5 + math.cos(a) * r0, 84 + math.sin(a) * r0),
                                     (CX - 0.5 + math.cos(a) * r1, 84 + math.sin(a) * r1))):
            if t.fundo_em(x, y) and (k < 4 or k % 2 == 0):
                t.put(x, y, "C1" if k < 3 else "C0")
    # bancada
    t.pintar(R(22, 137, 26, 158), cil_x(22, 26))
    t.pintar(R(86, 137, 90, 158), cil_x(86, 90))
    t.pintar(R(25, 148, 87, 151), cil_y(148, 151, 0.8))
    t.pintar(R(16, 130, 96, 137), linear(16, 130, 96, 137), chanfro=0.2)
    for x in range(18, 95, 9):
        t.put(x, 134, "B1")
    # engrenagens sobre a bancada
    def engr(cx, cy, r, n):
        e = set()
        for y in range(int(cy - r - 3), int(cy + r + 4)):
            for x in range(int(cx - r - 3), int(cx + r + 4)):
                dx, dy = x + 0.5 - cx, y + 0.5 - cy
                d = math.hypot(dx, dy)
                a = (math.atan2(dy, dx) / math.tau * n) % 1.0
                if d <= r or (d <= r + 2.5 and a < 0.48):
                    e.add((x, y))
        e = {p for p in e if p[1] < 130}
        t.pintar(e, linear(cx - r, cy - r, cx + r, cy + r, 1.0, 0.08), chanfro=0.2)
        furo = E(cx, cy, r * 0.35)
        t.fill(furo, "K")
        for x, y in borda(E(cx, cy, r * 0.7)):
            if (x, y) in e and (x, y) not in furo:
                t.put(x, y, "B1")
    engr(29.0, 120.0, 9.0, 10)
    engr(42.0, 125.5, 5.0, 8)
    # compasso
    for p0, p1 in (((80, 106), (73, 130)), ((80, 106), (88, 130))):
        t.pintar(L([p0, p1], 2.2), linear(70, 100, 90, 130), chanfro=0.1)
    t.pintar(E(80.0, 105.5, 2.8), esfera(80.0, 105.5, 2.8))
    # pinças segurando o cristal
    for p0, p1 in (((47, 130), (61, 96)), ((65, 130), (51, 96))):
        t.pintar(L([p0, p1], 2.6), linear(44, 94, 68, 130), chanfro=0.12)
    for x0, sx in ((60.5, 1), (51.5, -1)):
        t.pintar(L([(x0, 96), (x0 + sx * 0.5, 92)], 2.0), linear(44, 88, 68, 98), chanfro=0.1)
    t.pintar(E(CX, 113.5, 2.3), esfera(CX, 113.5, 2.3))
    t.cristal(CX, 82.0, 6.5, 12.0)


ILUSTRACOES = {k[3:]: v for k, v in globals().items() if k.startswith("il_")}

# id, rótulo do topo, nome, moldura (minor / major / cursed), raio do halo
CARDS = [
    ("pistao_runico", "ÁS DE ESPADAS", "PISTÃO RÚNICO", "minor", 34),
    ("sopro_caldeira", "III DE ESPADAS", "SOPRO DE CALDEIRA", "minor", 34),
    ("arco_voltaico", "V DE ESPADAS", "ARCO VOLTAICO", "minor", 34),
    ("mina_engrenagem", "VII DE ESPADAS", "MINA DE ENGRENAGEM", "minor", 34),
    ("broquel_cantante", "PAJEM DE ESPADAS", "BROQUEL CANTANTE", "minor", 31),
    ("tonico_oleo_luz", "II DE COPAS", "TÔNICO DE ÓLEO E LUZ", "minor", 34),
    ("granada_cristal", "IV DE COPAS", "GRANADA DE CRISTAL", "minor", 34),
    ("caldeira_interna", "ÁS DE PAUS", "CALDEIRA INTERNA", "minor", 34),
    ("mola_recuo", "VI DE PAUS", "MOLA DE RECUO", "minor", 34),
    ("manopla_pistonada", "III DE OUROS", "MANOPLA PISTONADA", "minor", 34),
    ("lente_prismatica", "RAINHA DE OUROS", "LENTE PRISMÁTICA", "minor", 34),
    ("lamina_sedenta", "VIII DE ESPADAS", "LÂMINA SEDENTA", "cursed", 34),
    ("chamine_partida", "XVI", "A CHAMINÉ PARTIDA", "major", 34),
    ("artifice", "I", "O ARTÍFICE", "major", 34),
]


def desenhar(card):
    cid, rotulo, nome, kind, rh = card
    t = Tela(kind)
    fundo(t)
    halo(t, CX, CY, rh, kind)
    ILUSTRACOES[cid](t)
    if kind == "major":                     # coroa de estrelas dos arcanos maiores
        for i in range(12):
            a = i * math.tau / 12 + math.tau / 24
            x, y = int(CX - 0.5 + math.cos(a) * (rh + 16)), int(CY - 0.5 + math.sin(a) * (rh + 16))
            if livre(t, x, y, 1) or all(t.get(x + dx, y + dy) in FUNDO or t.get(x + dx, y + dy) in ("B0", "B1")
                                       for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                t.estrela(x, y, 1)
    estrelas(t, random.Random(cid), rh)
    moldura(t, kind, rotulo, nome)
    return t


def exportar(t, cid):
    face = Image.new("RGB", (W, H))
    face.putdata([PAL[c] for row in t.px for c in row])
    mask = Image.new("RGBA", (W, H), (255, 255, 255, 0))
    dados = []
    for y in range(H):
        for x in range(W):
            c = t.px[y][x]
            a = 255 if c in BRILHO else (t.aura.get((x, y), 0) if c == t.arc[0] else 0)
            dados.append((255, 255, 255, a))
    mask.putdata(dados)
    os.makedirs(OUT_DIR, exist_ok=True)
    grande = face.resize((W * ESCALA, H * ESCALA), Image.NEAREST)
    grande.save(os.path.join(OUT_DIR, f"{cid}.png"), optimize=True)
    mask.resize((W * ESCALA, H * ESCALA), Image.NEAREST).save(os.path.join(OUT_DIR, f"{cid}_brilho.png"), optimize=True)
    return face


def folha(faces, escala=2, por_linha=7):
    """Folha de contato com todas as cartas (ampliadas sem suavizar)."""
    pad = 8
    n = len(faces)
    linhas = (n + por_linha - 1) // por_linha
    cw, ch = W * escala, H * escala
    img = Image.new("RGB", (pad + por_linha * (cw + pad), pad + linhas * (ch + pad)), (30, 28, 27))
    for i, f in enumerate(faces):
        x = pad + (i % por_linha) * (cw + pad)
        y = pad + (i // por_linha) * (ch + pad)
        img.paste(f.resize((cw, ch), Image.NEAREST), (x, y))
    os.makedirs(os.path.dirname(SHEET), exist_ok=True)
    img.save(SHEET, optimize=True)


if __name__ == "__main__":
    only = sys.argv[1:]
    faces = []
    for c in CARDS:
        if only and c[0] not in only:
            continue
        t = desenhar(c)
        faces.append(exportar(t, c[0]))
        print(f"[card_pixel] {c[0]}")
    if not only:
        folha(faces)
        print(f"[card_pixel] folha: {SHEET}")
    print("[card_pixel] concluído")
