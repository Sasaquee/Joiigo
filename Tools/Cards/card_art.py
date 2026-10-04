"""
Gera as faces das cartas de tarô (D-037): traço dourado sobre preto, moldura ornamentada,
estrelas e raios, e no centro o objeto da carta desenhado em linha. Os cristais levam um toque
ciano (violeta na amaldiçoada): a assinatura arcana do mundo dentro do traço mecânico (Pilar 4).

Uso (Python 3 com Pillow):
    python Tools/Cards/card_art.py                 # todas
    python Tools/Cards/card_art.py pistao_runico   # só algumas

Saída: Assets/_Game/Art/Cards/<id>.png (448x768). O CardData.face aponta para estes arquivos.
Desenho em unidades de projeto (1000 x 1714), renderizado em 4x e reduzido para suavizar o traço.
"""

import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "_Game", "Art", "Cards")

OUT_SIZE = (448, 768)
DW, DH = 1000, 1714          # unidades de projeto
SS = 4                       # supersampling
S = OUT_SIZE[0] * SS / DW    # unidades de projeto -> pixels do canvas grande

BG = (14, 12, 11)
GOLD = (228, 174, 84)
GOLD_DIM = (150, 112, 56)
CYAN = (110, 220, 236)
VIOLET = (190, 100, 230)

FONT_DIR = "C:/Windows/Fonts"
FONT_FILES = ["GARABD.TTF", "GARA.TTF", "georgiab.ttf", "georgia.ttf"]

# id, rótulo do topo, nome, moldura (minor / major / cursed)
CARDS = [
    ("pistao_runico", "ÁS DE ESPADAS", "PISTÃO RÚNICO", "minor"),
    ("sopro_caldeira", "III DE ESPADAS", "SOPRO DE CALDEIRA", "minor"),
    ("arco_voltaico", "V DE ESPADAS", "ARCO VOLTAICO", "minor"),
    ("mina_engrenagem", "VII DE ESPADAS", "MINA DE ENGRENAGEM", "minor"),
    ("broquel_cantante", "PAJEM DE ESPADAS", "BROQUEL CANTANTE", "minor"),
    ("tonico_oleo_luz", "II DE COPAS", "TÔNICO DE ÓLEO E LUZ", "minor"),
    ("granada_cristal", "IV DE COPAS", "GRANADA DE CRISTAL", "minor"),
    ("caldeira_interna", "ÁS DE PAUS", "CALDEIRA INTERNA", "minor"),
    ("mola_recuo", "VI DE PAUS", "MOLA DE RECUO", "minor"),
    ("manopla_pistonada", "III DE OUROS", "MANOPLA PISTONADA", "minor"),
    ("lente_prismatica", "RAINHA DE OUROS", "LENTE PRISMÁTICA", "minor"),
    ("lamina_sedenta", "VIII DE ESPADAS", "LÂMINA SEDENTA", "cursed"),
    ("chamine_partida", "XVI", "A CHAMINÉ PARTIDA", "major"),
    ("artifice", "I", "O ARTÍFICE", "major"),
]


# ---------- pincel ----------

class Pen:
    """Desenha em unidades de projeto sobre o canvas grande."""

    def __init__(self, img, accent):
        self.d = ImageDraw.Draw(img)
        self.accent = accent

    @staticmethod
    def p(x, y):
        return (x * S, y * S)

    @staticmethod
    def w(width):
        return max(1, int(round(width * S)))

    def line(self, pts, width=4, color=GOLD):
        self.d.line([self.p(*q) for q in pts], fill=color, width=self.w(width), joint="curve")
        r = width / 2
        for q in (pts[0], pts[-1]):  # pontas arredondadas
            self.d.ellipse(self.box(q[0], q[1], r * 0.98), fill=color)

    def poly(self, pts, width=4, color=GOLD, fill=None):
        if fill is not None:
            self.d.polygon([self.p(*q) for q in pts], fill=fill)
        self.line(list(pts) + [pts[0]], width, color)

    @staticmethod
    def box(cx, cy, r, ry=None):
        ry = r if ry is None else ry
        return [(cx - r) * S, (cy - ry) * S, (cx + r) * S, (cy + ry) * S]

    def circle(self, cx, cy, r, width=4, color=GOLD, fill=None, ry=None):
        self.d.ellipse(self.box(cx, cy, r, ry), outline=color, width=self.w(width), fill=fill)

    def dot(self, cx, cy, r, color=GOLD):
        self.d.ellipse(self.box(cx, cy, r), fill=color)

    def arc(self, cx, cy, r, start, end, width=4, color=GOLD, ry=None):
        self.d.arc(self.box(cx, cy, r, ry), start, end, fill=color, width=self.w(width))

    def rect(self, x0, y0, x1, y1, width=4, color=GOLD, fill=BG):
        self.d.rectangle([x0 * S, y0 * S, x1 * S, y1 * S], outline=color, width=self.w(width), fill=fill)

    def rrect(self, x0, y0, x1, y1, radius, width=4, color=GOLD, fill=BG):
        self.d.rounded_rectangle([x0 * S, y0 * S, x1 * S, y1 * S], radius=radius * S,
                                 outline=color, width=self.w(width), fill=fill)

    def star4(self, cx, cy, r, color=GOLD):
        k = r * 0.22
        pts = [(cx, cy - r), (cx + k, cy - k), (cx + r, cy), (cx + k, cy + k),
               (cx, cy + r), (cx - k, cy + k), (cx - r, cy), (cx - k, cy - k)]
        self.d.polygon([self.p(*q) for q in pts], fill=color)

    def rays(self, cx, cy, r0, r1, n, width=2, color=GOLD_DIM, phase=0.0, alternate=True):
        for i in range(n):
            a = phase + i * math.tau / n
            rr = r1 if (not alternate or i % 2 == 0) else r0 + (r1 - r0) * 0.6
            self.line([(cx + math.cos(a) * r0, cy + math.sin(a) * r0),
                       (cx + math.cos(a) * rr, cy + math.sin(a) * rr)], width, color)

    def gear(self, cx, cy, r, teeth, width=4, color=GOLD, hole=0.35):
        tooth = r * 0.16
        pts = []
        for i in range(teeth * 4):
            a = i * math.tau / (teeth * 4)
            rr = r + tooth if (i % 4) in (1, 2) else r
            pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
        self.poly(pts, width, color, fill=BG)
        self.circle(cx, cy, r * 0.72, width * 0.6, color)
        if hole:
            self.circle(cx, cy, r * hole, width * 0.8, color, fill=BG)

    def crystal(self, cx, cy, w, h, width=4, color=None):
        """Cristal facetado (hexágono alongado com facetas)."""
        color = color or self.accent
        top, bot = (cx, cy - h), (cx, cy + h)
        l, r = cx - w, cx + w
        pts = [top, (r, cy - h * 0.35), (r, cy + h * 0.35), bot, (l, cy + h * 0.35), (l, cy - h * 0.35)]
        self.poly(pts, width, color, fill=BG)
        self.line([top, (cx, cy + h * 0.35)], width * 0.5, color)
        self.line([(l, cy - h * 0.35), (cx, cy + h * 0.35), (r, cy - h * 0.35)], width * 0.5, color)

    def cloud(self, cx, cy, size, width=3.5, color=GOLD):
        """Nuvem/vapor em volutas, como gravura."""
        puffs = [(-0.9, 0.15, 0.42), (-0.45, -0.18, 0.55), (0.1, -0.3, 0.62), (0.62, -0.08, 0.5), (1.0, 0.18, 0.38)]
        for dx, dy, rr in puffs:
            self.circle(cx + dx * size, cy + dy * size, rr * size, width, color, fill=BG)
        self.rect(cx - size * 1.45, cy + size * 0.18, cx + size * 1.45, cy + size * 0.62, 0, BG, fill=BG)
        self.line([(cx - size * 1.32, cy + size * 0.2), (cx + size * 1.38, cy + size * 0.2)], width, color)
        for dx, dy, rr in puffs[1:4]:  # volutas internas
            self.arc(cx + dx * size, cy + dy * size, rr * size * 0.55, 200, 330, width * 0.6, color)


# ---------- moldura e fundo ----------

def font(size):
    for f in FONT_FILES:
        path = os.path.join(FONT_DIR, f)
        if os.path.exists(path):
            return ImageFont.truetype(path, int(size * S))
    return ImageFont.load_default()


def spaced_text(pen, text, cx, cy, size, color=GOLD, spacing=0.12):
    f = font(size)
    letters = list(text)
    widths = [pen.d.textlength(ch, font=f) for ch in letters]
    gap = size * S * spacing
    total = sum(widths) + gap * (len(letters) - 1)
    x = cx * S - total / 2
    for ch, wch in zip(letters, widths):
        pen.d.text((x, cy * S), ch, font=f, fill=color, anchor="lm")
        x += wch + gap


def corner(pen, x, y, sx, sy, kind):
    """Ornamento de canto: quadrado com losango, arcos concêntricos, volutas e pontos."""
    c = GOLD
    pen.rect(*_ordered(x, y, x + sx * 40, y + sy * 40), width=2.5, color=c, fill=BG)
    pen.poly([(x + sx * 20, y + sy * 6), (x + sx * 34, y + sy * 20), (x + sx * 20, y + sy * 34), (x + sx * 6, y + sy * 20)],
             2, c, fill=BG)
    q = _quarter(sx, sy)
    for r, wd in ((96, 3), (80, 1.6)):
        pen.arc(x, y, r, *q, width=wd, color=c)
    for t in (0.2, 0.5, 0.8):  # contas no arco
        a = math.radians(q[0] + (q[1] - q[0]) * t)
        pen.dot(x + math.cos(a) * 88, y + math.sin(a) * 88, 4.2, c)
    for (ex, ey) in ((x + sx * 96, y), (x, y + sy * 96)):  # volutas nas pontas
        pen.circle(ex + (sx * 12 if ey == y else 0), ey + (sy * 12 if ex == x else 0), 9, 2, c)
        pen.dot(ex + (sx * 12 if ey == y else 0), ey + (sy * 12 if ex == x else 0), 3, c)
    pen.line([(x + sx * 118, y + sy * 14), (x + sx * 190, y + sy * 14)], 1.6, c)
    pen.line([(x + sx * 14, y + sy * 118), (x + sx * 14, y + sy * 190)], 1.6, c)
    if kind == "major":
        pen.crystal(x + sx * 58, y + sy * 58, 11, 17, 2.5)
    elif kind == "cursed":
        for i in range(4):
            pen.line([(x + sx * (120 + i * 16), y + sy * 2), (x + sx * (127 + i * 16), y + sy * 13)], 2, VIOLET)
    else:
        pen.star4(x + sx * 58, y + sy * 58, 12, c)


def _ordered(x0, y0, x1, y1):
    return min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)


def edge_ornament(pen, cx, y, sy):
    """Ornamento no meio da borda de cima/baixo: arco com estrela e pontos."""
    pen.arc(cx, y, 46, *((0, 180) if sy > 0 else (180, 360)), width=2.5, color=GOLD)
    pen.rect(cx - 70, min(y, y - sy * 2), cx + 70, max(y, y - sy * 2) + 1, 0, BG, fill=BG)
    pen.star4(cx, y + sy * 22, 14)
    for dx in (-58, 58):
        pen.dot(cx + dx, y + sy * 6, 4)


def _quarter(sx, sy):
    # ângulos do PIL: 0 = direita, sentido horário (y para baixo)
    if sx > 0 and sy > 0:
        return (180, 270)
    if sx < 0 < sy:
        return (270, 360)
    if sx > 0 > sy:
        return (90, 180)
    return (0, 90)


def frame(pen, kind, top_label, name):
    c = GOLD
    pen.rrect(14, 14, DW - 14, DH - 14, 36, width=9, color=c, fill=None)
    pen.rect(44, 44, DW - 44, DH - 44, width=3, color=c, fill=None)
    pen.rect(58, 58, DW - 58, DH - 58, width=1.6, color=GOLD_DIM, fill=None)
    for (x, y, sx, sy) in ((44, 44, 1, 1), (DW - 44, 44, -1, 1), (44, DH - 44, 1, -1), (DW - 44, DH - 44, -1, -1)):
        corner(pen, x, y, sx, sy, kind)

    edge_ornament(pen, DW / 2, 44, 1)
    edge_ornament(pen, DW / 2, DH - 44, -1)

    # Faixas do título e do nome
    for y0, y1 in ((96, 196), (DH - 196, DH - 96)):
        pen.line([(150, y0), (DW - 150, y0)], 2, c)
        pen.line([(150, y1), (DW - 150, y1)], 2, c)
        for x in (150, DW - 150):
            pen.poly([(x, (y0 + y1) / 2 - 14), (x + 14, (y0 + y1) / 2), (x, (y0 + y1) / 2 + 14), (x - 14, (y0 + y1) / 2)], 2, c, fill=BG)
    top_size = 62 if len(top_label) <= 4 else 40
    spaced_text(pen, top_label, DW / 2, 148, top_size, c, spacing=0.18 if len(top_label) <= 4 else 0.1)
    name_size = 40 if len(name) <= 16 else 33
    spaced_text(pen, name, DW / 2, DH - 146, name_size, c, spacing=0.08)

    # Ornamentos no meio das bordas laterais
    for x, sx in ((44, 1), (DW - 44, -1)):
        cy = DH / 2
        pen.poly([(x, cy - 30), (x + sx * 16, cy), (x, cy + 30), (x - sx * 4, cy)], 2, c, fill=BG)


def starfield(pen, rnd, center, keep_out):
    """Estrelas, pontos e uma constelação, longe do centro da ilustração."""
    cx, cy = center
    for _ in range(70):
        x, y = rnd.uniform(90, DW - 90), rnd.uniform(230, DH - 230)
        if math.hypot(x - cx, y - cy) < keep_out:
            continue
        if rnd.random() < 0.18:
            pen.star4(x, y, rnd.uniform(9, 17))
        else:
            pen.dot(x, y, rnd.uniform(1.6, 3.4), GOLD if rnd.random() < 0.6 else GOLD_DIM)
    # constelação num canto
    ox, oy = rnd.choice([(170, 300), (DW - 230, 320), (180, DH - 380), (DW - 240, DH - 360)])
    pts = [(ox + rnd.uniform(-40, 70), oy + rnd.uniform(-40, 80)) for _ in range(5)]
    pen.line(pts, 1.4, GOLD_DIM)
    for x, y in pts:
        pen.dot(x, y, 4.5)


def halo(pen, cx, cy, kind):
    """Sol de raios atrás da ilustração, com anéis."""
    pen.rays(cx, cy, 250, 380, 48, 1.6, GOLD_DIM, phase=0.03)
    pen.circle(cx, cy, 245, 2, GOLD_DIM)
    pen.circle(cx, cy, 228, 1.2, GOLD_DIM)
    if kind == "major":
        for i in range(12):
            a = i * math.tau / 12
            pen.star4(cx + math.cos(a) * 300, cy + math.sin(a) * 300, 10)


# ---------- ilustrações (centro em CX, CY) ----------

CX, CY = DW / 2, 860


def il_pistao_runico(p):
    # Pistão vertical; o anel rúnico no corpo é o que dá o impulso.
    p.rect(CX - 26, CY + 60, CX + 26, CY + 330, 4)                     # haste
    p.rrect(CX - 90, CY + 320, CX + 90, CY + 370, 10, 4)               # pé
    p.rrect(CX - 110, CY - 240, CX + 110, CY + 80, 16, 5)               # cilindro
    for y in (CY - 200, CY + 40):
        p.line([(CX - 110, y), (CX + 110, y)], 3)
    for y in (CY - 220, CY + 60):
        for x in (CX - 90, CX - 45, CX, CX + 45, CX + 90):
            p.dot(x, y, 4.5)
    p.circle(CX, CY - 80, 150, 4, p.accent, ry=38)                      # anel rúnico
    for i in range(10):
        a = i * math.tau / 10
        x, y = CX + math.cos(a) * 150, CY - 80 + math.sin(a) * 38
        p.line([(x, y - 9), (x, y + 9)], 3, p.accent)
    p.rrect(CX - 70, CY - 300, CX + 70, CY - 240, 10, 4)               # tampa
    for i, dx in enumerate((-60, -20, 20, 60)):                          # rastro de impulso
        p.line([(CX + dx, CY - 330), (CX + dx, CY - 420 - (i % 2) * 40)], 3)
    p.star4(CX, CY - 470, 26)


def il_sopro_caldeira(p):
    p.circle(CX - 60, CY + 120, 170, 5, fill=BG)
    p.arc(CX - 60, CY + 120, 120, 200, 340, 3)
    p.line([(CX - 230, CY + 120), (CX + 110, CY + 120)], 3)
    for a in range(0, 360, 30):
        r = math.radians(a)
        p.dot(CX - 60 + math.cos(r) * 150, CY + 120 + math.sin(r) * 150, 4)
    p.poly([(CX + 95, CY + 40), (CX + 240, CY - 110), (CX + 270, CY - 80), (CX + 125, CY + 80)], 4, fill=BG)  # bico
    p.rrect(CX - 110, CY - 70, CX - 10, CY - 40, 6, 4)                   # tampa
    for dx in (-130, -60, 10):                                            # chamas embaixo
        p.line([(CX + dx, CY + 330), (CX + dx + 20, CY + 290), (CX + dx + 5, CY + 260)], 3)
    p.cloud(CX + 170, CY - 230, 90)
    p.cloud(CX + 40, CY - 380, 70)
    for (x, y) in ((CX + 120, CY - 280), (CX + 240, CY - 250), (CX + 60, CY - 410)):
        p.crystal(x, y, 8, 13, 2.5)


def il_arco_voltaico(p):
    p.rrect(CX - 130, CY + 260, CX + 130, CY + 320, 10, 4)
    p.rect(CX - 40, CY + 200, CX + 40, CY + 260, 4)
    for i in range(9):                                                    # bobina
        y = CY + 180 - i * 42
        p.circle(CX, y, 85, 4, ry=20, fill=BG)
    p.circle(CX, CY - 250, 70, 5, fill=BG)                               # esfera
    p.arc(CX, CY - 250, 45, 200, 300, 3)
    bolts = [
        [(CX - 60, CY - 290), (CX - 140, CY - 360), (CX - 120, CY - 400), (CX - 220, CY - 470)],
        [(CX + 50, CY - 300), (CX + 140, CY - 330), (CX + 120, CY - 380), (CX + 230, CY - 430)],
        [(CX + 10, CY - 320), (CX - 20, CY - 400), (CX + 30, CY - 430), (CX, CY - 520)],
    ]
    for b in bolts:
        p.line(b, 4.5, p.accent)
    for b in bolts:
        p.star4(*b[-1], 20, p.accent)


def il_mina_engrenagem(p):
    for i in range(8):                                                    # espinhos
        a = i * math.tau / 8 + math.pi / 8
        x0, y0 = CX + math.cos(a) * 205, CY + math.sin(a) * 205
        x1, y1 = CX + math.cos(a) * 290, CY + math.sin(a) * 290
        p.line([(x0, y0), (x1, y1)], 5)
        p.dot(x1, y1, 9)
    p.gear(CX, CY, 190, 14, 5, hole=0)
    p.circle(CX, CY, 110, 4, fill=BG)
    for i in range(12):
        a = i * math.tau / 12
        p.dot(CX + math.cos(a) * 92, CY + math.sin(a) * 92, 4)
    p.crystal(CX, CY, 38, 62, 4.5)


def il_broquel_cantante(p):
    for side in (-1, 1):                                                  # ondas de som
        for k in range(3):
            r = 280 + k * 50
            p.arc(CX, CY, r, (150 if side < 0 else -30), (210 if side < 0 else 30), 3, p.accent)
    p.circle(CX, CY, 250, 6, fill=BG)
    p.circle(CX, CY, 215, 3)
    p.circle(CX, CY, 140, 3)
    for i in range(16):
        a = i * math.tau / 16
        p.dot(CX + math.cos(a) * 232, CY + math.sin(a) * 232, 5)
        p.line([(CX + math.cos(a) * 145, CY + math.sin(a) * 145), (CX + math.cos(a) * 210, CY + math.sin(a) * 210)], 2)
    p.circle(CX, CY, 70, 4, fill=BG)
    p.crystal(CX, CY, 34, 52, 4)


def il_tonico_oleo_luz(p):
    p.rays(CX, CY + 80, 230, 330, 24, 2, GOLD, phase=0.13)
    p.circle(CX, CY + 80, 200, 5, fill=BG)
    p.rect(CX - 50, CY - 260, CX + 50, CY - 110, 5)
    p.rrect(CX - 70, CY - 300, CX + 70, CY - 255, 8, 4)
    p.line([(CX - 175, CY + 60), (CX - 80, CY + 30), (CX, CY + 60), (CX + 90, CY + 30), (CX + 178, CY + 60)], 3)
    for (x, y, r) in ((CX - 60, CY + 150, 14), (CX + 50, CY + 120, 10), (CX + 10, CY + 210, 8), (CX - 100, CY + 100, 7)):
        p.circle(x, y, r, 2.5)
    p.crystal(CX, CY + 130, 16, 26, 3)
    p.arc(CX - 40, CY + 40, 120, 200, 250, 3)                            # brilho no vidro


def il_granada_cristal(p):
    p.circle(CX, CY + 60, 210, 6, fill=BG)
    p.circle(CX, CY + 60, 210, 3, ry=60)
    p.rrect(CX - 50, CY - 200, CX + 50, CY - 140, 8, 4)
    p.circle(CX + 80, CY - 190, 34, 4)                                     # argola
    p.line([(CX, CY - 200), (CX - 30, CY - 260), (CX + 10, CY - 300)], 3)  # pavio
    p.star4(CX + 10, CY - 330, 34)
    cracks = [
        [(CX - 120, CY - 20), (CX - 60, CY + 30), (CX - 90, CY + 100), (CX - 20, CY + 160)],
        [(CX + 60, CY - 40), (CX + 20, CY + 40), (CX + 100, CY + 90), (CX + 70, CY + 180)],
        [(CX - 60, CY + 30), (CX + 20, CY + 40)],
    ]
    for c in cracks:
        p.line(c, 4, p.accent)
    p.crystal(CX - 20, CY + 60, 20, 30, 3)


def il_caldeira_interna(p):
    p.rect(CX - 24, CY - 330, CX + 24, CY + 360, 4)                       # cajado-cano
    for y in (CY - 250, CY + 250):
        p.rrect(CX - 40, y - 14, CX + 40, y + 14, 6, 3)
    p.circle(CX, CY + 20, 170, 6, fill=BG)                                 # caldeira-coração
    p.circle(CX, CY + 20, 100, 4, fill=BG)                                 # manômetro
    for i in range(9):
        a = math.radians(150 + i * 30)
        p.line([(CX + math.cos(a) * 80, CY + 20 + math.sin(a) * 80), (CX + math.cos(a) * 95, CY + 20 + math.sin(a) * 95)], 3)
    p.line([(CX, CY + 20), (CX + 55, CY - 30)], 4)
    p.dot(CX, CY + 20, 10)
    for dx in (-60, 0, 60):
        p.line([(CX + dx, CY + 200), (CX + dx + 18, CY + 230), (CX + dx, CY + 262)], 3)
    p.crystal(CX, CY - 400, 34, 56, 4.5)
    p.rays(CX, CY - 400, 70, 120, 12, 2, p.accent)


def il_mola_recuo(p):
    p.rrect(CX - 160, CY + 280, CX + 160, CY + 330, 10, 5)
    p.rrect(CX - 140, CY - 270, CX + 140, CY - 220, 10, 5)
    pts = []
    for i in range(13):
        y = CY + 280 - i * (550 / 12)
        x = CX + (-120 if i % 2 else 120)
        pts.append((x, y))
    pts[0] = (CX, CY + 280)
    pts[-1] = (CX, CY - 220)
    p.line(pts, 6)
    p.crystal(CX, CY - 330, 30, 48, 4)
    for side in (-1, 1):
        x = CX + side * 220
        p.line([(x, CY + 160), (x, CY - 160)], 3)
        p.line([(x - 20, CY - 130), (x, CY - 170), (x + 20, CY - 130)], 3)


def il_manopla_pistonada(p):
    p.rrect(CX - 110, CY + 120, CX + 110, CY + 380, 14, 5)                 # antebraço
    p.rrect(CX - 150, CY - 120, CX + 150, CY + 130, 26, 6)                 # punho
    for i in range(4):                                                      # dedos
        x0 = CX - 140 + i * 72
        p.rrect(x0, CY - 250, x0 + 62, CY - 110, 18, 4)
        p.line([(x0 + 6, CY - 180), (x0 + 56, CY - 180)], 2.5)
    p.rrect(CX - 210, CY - 70, CX - 140, CY + 60, 20, 4)                    # polegar
    for side in (-1, 1):                                                    # pistões
        x = CX + side * 75
        p.rect(x - 14, CY + 150, x + 14, CY + 360, 3)
        p.rrect(x - 26, CY + 130, x + 26, CY + 170, 6, 3)
    p.gear(CX, CY + 10, 62, 10, 4)
    p.crystal(CX, CY + 10, 18, 28, 3)


def il_lente_prismatica(p):
    p.line([(CX - 360, CY - 40), (CX - 200, CY - 40)], 4)                  # feixe que entra
    for k, ang in enumerate((-0.42, -0.21, 0.0, 0.21, 0.42)):               # feixes que saem
        x0, y0 = CX + 190, CY - 40
        x1, y1 = CX + 360, CY - 40 + math.tan(ang) * 170
        p.line([(x0, y0), (x1, y1)], 3, p.accent if k % 2 == 0 else GOLD)
    p.rrect(CX - 26, CY + 160, CX + 26, CY + 420, 12, 4)                   # cabo
    p.circle(CX, CY - 40, 210, 7, fill=BG)
    p.circle(CX, CY - 40, 180, 2.5)
    p.crystal(CX, CY - 40, 90, 140, 4)
    for i in range(6):
        a = i * math.tau / 6
        p.dot(CX + math.cos(a) * 196, CY - 40 + math.sin(a) * 196, 5)


def il_lamina_sedenta(p):
    ang = math.radians(-18)
    def rot(x, y):
        return (CX + x * math.cos(ang) - y * math.sin(ang), CY + x * math.sin(ang) + y * math.cos(ang))
    blade = [rot(-30, 150), rot(-36, -250), rot(0, -420), rot(30, -250), rot(30, 150)]
    p.poly(blade, 5, fill=BG)
    p.line([rot(0, -380), rot(0, 140)], 2)
    for y in (-200, -80, 40):                                               # serrilha
        p.line([rot(30, y), rot(52, y - 20), rot(30, y - 40)], 3)
    p.poly([rot(-130, 150), rot(130, 150), rot(110, 195), rot(-110, 195)], 4, fill=BG)  # guarda
    p.poly([rot(-22, 195), rot(22, 195), rot(22, 350), rot(-22, 350)], 4, fill=BG)  # cabo
    p.circle(*rot(0, 380), 30, 4, fill=BG)
    p.crystal(*rot(0, 172), 18, 28, 3.5)
    for (x, y, r) in ((CX + 190, CY + 120, 16), (CX + 215, CY + 230, 12), (CX + 175, CY + 320, 10)):  # gotas
        p.poly([(x, y - r * 2.2), (x + r, y), (x - r, y)], 3, VIOLET, fill=BG)
        p.circle(x, y, r, 3, VIOLET, fill=BG)
    for i in range(7):                                                      # espinhos em volta
        a = math.radians(200 + i * 20)
        p.line([(CX + math.cos(a) * 330, CY + math.sin(a) * 330), (CX + math.cos(a) * 370, CY + math.sin(a) * 370)], 3, VIOLET)


def il_chamine_partida(p):
    top = CY - 160
    p.poly([(CX - 120, CY + 380), (CX + 120, CY + 380), (CX + 95, top), (CX - 95, top)], 6, fill=BG)
    for i in range(1, 9):                                                   # tijolos
        y = top + i * ((CY + 380 - top) / 9)
        half = 95 + (120 - 95) * (y - top) / (CY + 380 - top)
        p.line([(CX - half, y), (CX + half, y)], 2)
        for k in range(-2, 3):
            x = CX + k * 45 + (22 if i % 2 else 0)
            if abs(x - CX) < half - 10:
                p.line([(x, y), (x, y - 30)], 2)
    p.rrect(CX - 34, CY + 250, CX + 34, CY + 330, 10, 3, color=GOLD, fill=(70, 30, 12))  # boca de fogo
    p.poly([(CX - 110, top - 20), (CX + 40, top - 110), (CX + 90, top - 60), (CX - 60, top + 30)], 5, fill=BG)  # topo partido
    p.line([(CX - 95, top), (CX - 40, top - 30), (CX + 10, top + 10), (CX + 95, top - 10)], 3)
    p.cloud(CX - 170, top - 250, 80)
    p.cloud(CX + 170, top - 300, 70)
    p.line([(CX + 20, CY - 640), (CX - 30, CY - 520), (CX + 40, CY - 470), (CX - 10, top - 120)], 6, p.accent)
    for (x, y) in ((CX - 220, CY + 60), (CX + 220, CY + 0), (CX - 200, CY + 230), (CX + 230, CY + 220)):
        p.line([(x, y), (x + 20, y + 40)], 3)                               # pedaços caindo
        p.rect(x - 14, y - 10, x + 14, y + 10, 3)


def il_artifice(p):
    lem = []                                                                # lemniscata (o Mago do tarô)
    for i in range(121):
        t = i * math.tau / 120
        k = 1 + math.sin(t) ** 2
        lem.append((CX + 130 * math.cos(t) / k, CY - 430 + 130 * math.sin(t) * math.cos(t) / k))
    p.line(lem, 4.5)
    p.rrect(CX - 300, CY + 260, CX + 300, CY + 300, 8, 5)                    # bancada
    for x in (CX - 260, CX + 260):
        p.line([(x, CY + 300), (x, CY + 420)], 5)
    p.gear(CX - 150, CY + 170, 80, 12, 4)
    p.gear(CX - 40, CY + 205, 46, 9, 3.5)
    p.line([(CX + 160, CY + 260), (CX + 110, CY + 60), (CX + 210, CY + 260)], 4)  # compasso
    p.circle(CX + 110, CY + 50, 14, 4, fill=BG)
    p.line([(CX - 80, CY + 30), (CX - 20, CY - 120)], 4)                    # pinças segurando o cristal
    p.line([(CX + 80, CY + 30), (CX + 20, CY - 120)], 4)
    p.crystal(CX, CY - 200, 60, 100, 5)
    p.rays(CX, CY - 200, 130, 190, 16, 2.5, p.accent)


ILLUSTRATIONS = {k[3:]: v for k, v in globals().items() if k.startswith("il_")}


def render(card):
    cid, top, name, kind = card
    rnd = random.Random(cid)
    img = Image.new("RGB", (OUT_SIZE[0] * SS, OUT_SIZE[1] * SS), BG)
    pen = Pen(img, VIOLET if kind == "cursed" else CYAN)
    starfield(pen, rnd, (CX, CY), 380)
    halo(pen, CX, CY, kind)
    pen.circle(CX, CY, 226, 0.1, BG, fill=BG)  # fundo limpo dentro do anel
    ILLUSTRATIONS[cid](pen)
    frame(pen, kind, top, name)
    os.makedirs(OUT_DIR, exist_ok=True)
    out = img.resize(OUT_SIZE, Image.LANCZOS)
    path = os.path.join(OUT_DIR, f"{cid}.png")
    out.save(path, optimize=True)
    print(f"[card_art] {cid}")


if __name__ == "__main__":
    only = sys.argv[1:]
    for c in CARDS:
        if not only or c[0] in only:
            render(c)
    print("[card_art] concluído")
