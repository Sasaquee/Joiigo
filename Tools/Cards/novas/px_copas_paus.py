"""
Fase 10 (D-088), grupo copas/paus: versão em pixel de oito cartas novas.

Executado dentro de card_pixel.py (mesmo espaço de nomes). Estes desenhos seguem as mesmas formas e
coordenadas das ilustrações em alta (novas/hd_copas_paus.py), mais simples: servem de mapa de ocupação
para as estrelas e de prévia em pixel. Ajudantes com prefixo _cpx_ para não colidir com outros grupos.
"""


def _cpx_vidro(t, m):
    t.contorno(m, "B2", "B1")
    for x, y in m:
        t.put(x, y, "G1" if bayer(x, y) < 0.3 else "K")
    for x, y in borda(m):
        t.put(x, y, "B3" if (x - 1, y) not in m or (x, y - 1) not in m else "B2")


def _cpx_gota(t, cx, cy, r):
    g = E(cx, cy, r) | P([(cx - r * 0.8, cy - r * 0.5), (cx, cy - r * 2.6), (cx + r * 0.8, cy - r * 0.5)])
    t.pintar(g, esfera(cx, cy, r * 1.2), CIANO, claro="C0", escuro="C0")
    return g


def _cpx_abaixo(m, y):
    return {p for p in m if p[1] + 0.5 > y}


def _cpx_acima(m, y):
    return {p for p in m if p[1] + 0.5 < y}


def il_vapor_condensado(t):
    ex = 46.0
    t.vapor([(60.0, 51.0, 2.6), (55.0, 57.0, 3.5), (50.0, 63.5, 2.4)])
    t.pintar(R(26, 144, 66, 148), linear(26, 144, 66, 148), chanfro=0.18)
    for a, b in (((33, 133), (30, 144)), ((59, 133), (62, 144))):
        t.pintar(L([a, b], 2.4), linear(26, 128, 66, 148))
    t.cristal_pequeno(46, 139)
    t.pintar(E(ex, 120.0, 16.0, 13.0), esfera(ex, 118.0, 16.0))
    t.pintar(R(28, 130, 64, 134), linear(28, 130, 64, 134), chanfro=0.15)
    t.pintar(R(41, 98, 51, 108), cil_x(41, 51))
    t.pintar(R(37, 105, 55, 109), cil_x(37, 55), chanfro=0.12)
    domo = _cpx_acima(E(ex, 84.0, 14.0, 13.0), 97.5)
    _cpx_vidro(t, domo)
    for a in (-55, -28, 0, 24):
        aa = math.radians(a)
        t.put(int(ex + math.cos(aa) * 11.2), int(84 + math.sin(aa) * 10.2), "C2")
    t.pintar(R(36, 96, 56, 100), cil_x(36, 56), chanfro=0.12)
    t.pintar(R(43, 68, 49, 73), cil_x(43, 49))
    for p0, p1 in (((58, 81), (71, 86)), ((71, 86), (86, 101))):
        t.pintar(L([p0, p1], 4.0), linear(56, 78, 88, 104))
    t.pintar(L([(74, 89), (82, 97)], 7.0), linear(70, 85, 86, 101), chanfro=0.12)
    t.pintar(R(84, 100, 88, 108), cil_x(84, 88))
    t.pintar(R(83, 106, 89, 109), cil_x(83, 89))
    _cpx_gota(t, 86.0, 112.0, 1.1)
    _cpx_gota(t, 86.0, 118.5, 1.4)
    copo = R(79, 127, 93, 146)
    _cpx_vidro(t, copo)
    for x, y in _cpx_abaixo(encolher(copo), 136.0):
        t.put(x, y, CIANO[quant(0.9 - (y - 136) / 12.0, x, y, 4)])
    t.pintar(R(78, 125, 94, 128), cil_x(78, 94))


def il_frasco_faisca(t):
    cx, cy = CX, 107.0
    corpo = E(cx, cy, 18.0, 23.0)
    vidro = corpo | P([(41, 96), (50.5, 78), (61.5, 78), (71, 96)]) | R(50.5, 67, 61.5, 80)
    _cpx_vidro(t, vidro)
    for x, y in encolher(vidro, 2):
        if math.hypot(x + 0.5 - cx, y + 0.5 - cy) < 9 and bayer(x, y) < 0.5:
            t.put(x, y, "C0")
    for pts in ([(55, 106), (50, 101), (47, 95), (44, 90)], [(57, 106), (62, 101), (64, 95), (67, 90)],
                [(55, 108), (48, 111), (45, 117), (42, 120)], [(57, 108), (64, 112), (66, 118), (70, 120)],
                [(56, 109), (54, 116), (57, 122)]):
        for i, (x, y) in enumerate(BL(pts)):
            t.put(x, y, "WH" if i % 3 == 0 else "C2")
    t.estrela(56, 107, 4, ("WH", "C2", "C2", "C1"))
    t.pintar(_cpx_abaixo(corpo, 122.5), cil_x(38, 74), chanfro=0.2)
    for a0, a1 in ((-76, 76), (104, 256)):
        for a in range(a0, a1 + 1, 3):
            ra = math.radians(a)
            x, y = int(cx + math.cos(ra) * 10.5), int(cy + math.sin(ra) * 23.5)
            if y + 0.5 < 123:
                t.put(x, y, "B2")
    t.pintar(R(48.5, 78, 63.5, 82), cil_x(48.5, 63.5), chanfro=0.12)
    t.pintar(P([(49.5, 55), (62.5, 55), (61.2, 68), (50.8, 68)]), cil_x(49.5, 62.5, 0.55))
    t.pintar(L([(61.8, 72.0), (65.5, 72.5), (67.0, 75.5)], 1.0), plano(0.6))
    t.pintar(anel(67.2, 77.4, 1.0, 1.9), plano(0.6))


def il_calice_cheio(t):
    for i in range(18):
        a = i * math.tau / 18 - math.pi / 2
        r1 = 25 if i % 2 == 0 else 20
        for k, (x, y) in enumerate(B((CX - 0.5 + math.cos(a) * 15, 50 + math.sin(a) * 15),
                                     (CX - 0.5 + math.cos(a) * r1, 50 + math.sin(a) * r1))):
            if t.fundo_em(x, y) and (k < 3 or k % 2 == 0):
                t.put(x, y, "C1" if k < 2 else "C0")
    t.pintar(R(31, 134, 81, 139), linear(31, 134, 81, 139), chanfro=0.18)
    t.pintar(P([(49, 121), (63, 121), (75, 134.5), (37, 134.5)]), cil_x(37, 75), chanfro=0.12)
    t.pintar(R(52, 100, 60, 122), cil_x(52, 60))
    for y0 in (99.5, 118.5):
        t.pintar(R(48.5, y0, 63.5, y0 + 3.5), cil_x(48.5, 63.5), chanfro=0.12)
    t.pintar(engrenagem(CX, 110.5, 5.6, 10, 1.8, fase=0.5), linear(48, 103, 64, 119), chanfro=0.15)
    t.pintar(E(CX, 110.5, 2.2), esfera(CX, 110.5, 2.2), CIANO, contorno=False)
    perfil_taca = [(29.5, 66), (31.5, 79), (37.5, 89.5), (46.5, 97), (56, 99.5), (65.5, 97), (74.5, 89.5),
                   (80.5, 79), (82.5, 66)]
    t.pintar(P(perfil_taca), cil_x(29, 83), chanfro=0.2)
    for gx, gy, rr in ((CX, 87.0, 3.0), (42.5, 84.0, 1.7), (69.5, 84.0, 1.7)):
        t.pintar(E(gx, gy, rr), esfera(gx, gy, rr), CIANO, claro="B0", escuro="B0")
    t.pintar(E(CX, 66.0, 27.0, 5.5) - E(CX, 66.0, 24.0, 3.6), cil_x(29, 83), chanfro=0.2)
    liquido = E(CX, 66.0, 24.0, 3.6) | _cpx_acima(E(CX, 65.0, 23.0, 4.6), 66.0)
    t.pintar(liquido, lambda x, y: 0.95 - 0.35 * abs(x + 0.5 - CX) / 24, CIANO, contorno=False)
    for pts in ([(47.5, 69.5), (47.0, 74.0), (47.6, 79.5)], [(62.0, 70.0), (62.5, 73.0), (62.0, 76.0)],
                [(30.0, 66.5), (27.5, 71.0), (26.8, 80.0)], [(82.0, 66.5), (84.5, 71.0), (85.2, 80.0)]):
        t.pintar(L(pts, 1.7), plano(0.85), CIANO, claro="C0", escuro="C0")
    for gx, gy, rr in ((47.6, 82.5, 1.2), (62.0, 78.5, 1.0), (26.8, 82.5, 1.25), (26.8, 92.0, 1.05),
                       (26.8, 100.0, 0.85), (85.2, 82.5, 1.25), (85.2, 91.0, 1.05)):
        _cpx_gota(t, gx, gy, rr)
    t.cristal(45.5, 56.0, 3.4, 6.5)
    t.cristal(66.5, 56.0, 3.4, 6.5)
    t.cristal(CX, 48.5, 6.0, 11.5)


def il_tonico_fraco(t):
    cx = CX
    t.pintar(L([(61.0, 83.5), (66.0, 85.0), (73.5, 90.0)], 0.8), plano(0.45), contorno=False)
    t.pintar(P(girar([(-3.8, -5.2), (3.8, -5.2), (3.8, 5.2), (-3.8, 5.2)], 76.0, 96.5, 0.22)), cil_x(71, 81, 0.85),
             chanfro=0.18)
    corpo = R(42, 94, 70, 128)
    vidro = corpo | P([(43.0, 97), (50.5, 87.5), (61.5, 87.5), (69.0, 97)]) | R(51, 80, 61, 89)
    _cpx_vidro(t, vidro)
    oleo = _cpx_abaixo(encolher(corpo), 116.5)
    for x, y in oleo:
        t.put(x, y, OURO[quant(0.66 - math.hypot(x + 0.5 - cx, y + 0.5 - 122) / 14.0 * 0.5, x, y, 5, 0.8)])
    t.cristal_pequeno(56, 121, aura=False)
    t.pintar(R(47, 100, 65, 109), cil_x(47, 65, 0.8), chanfro=0.18)
    for x in (55, 57):
        for y in range(102, 107):
            t.put(x, y, "B1")
    t.pintar(R(49, 77, 63, 81), cil_x(49, 63), chanfro=0.12)
    t.pintar(R(51, 70, 61, 77.5), cil_x(51, 61), chanfro=0.12)
    t.pintar(E(cx, 68.2, 2.4), esfera(cx, 68.2, 2.4))


_CPX_PASSO = (47.0, 140.5, 0.18, -4.0, 0.0)          # o mesmo giro da versão em alta


def _cpx_girada(m, ox, oy, ang, dx=0.0, dy=0.0):
    """Gira um conjunto de pixels (mapeando o centro de cada pixel de volta para a forma em pé)."""
    if not m:
        return set()
    c, s = math.cos(ang), math.sin(ang)
    xs = [p[0] for p in m]
    ys = [p[1] for p in m]
    cantos = [(min(xs), min(ys)), (max(xs) + 1, min(ys)), (min(xs), max(ys) + 1), (max(xs) + 1, max(ys) + 1)]
    rc = [(ox + (x - ox) * c - (y - oy) * s + dx, oy + (x - ox) * s + (y - oy) * c + dy) for x, y in cantos]
    out = set()
    for y in range(int(min(p[1] for p in rc)) - 1, int(max(p[1] for p in rc)) + 2):
        for x in range(int(min(p[0] for p in rc)) - 1, int(max(p[0] for p in rc)) + 2):
            px, py = x + 0.5 - dx - ox, y + 0.5 - dy - oy
            qx, qy = ox + px * c + py * s, oy - px * s + py * c
            if (math.floor(qx), math.floor(qy)) in m:
                out.add((x, y))
    return out


def il_passo_pistao(t):
    G = lambda m: _cpx_girada(m, *_CPX_PASSO)          # noqa: E731
    for y, x0, comp in ((54, 45, 17), (64, 42, 21), (75, 39, 17), (87, 36, 19), (100, 33, 14)):
        for k in range(comp):
            if k < comp * 0.5 or k % 2 == 0:
                t.put(x0 - k, y, "B3" if k < 4 else ("B2" if k < 10 else "B1"))
    t.massa_vapor([(13.5, 130.0, 3.0), (18.5, 134.0, 4.0), (25.5, 137.0, 4.6), (33.0, 140.0, 4.0)])
    t.vapor([(70.5, 133.0, 2.2), (65.0, 137.0, 3.0), (59.0, 140.0, 3.4)])
    t.pintar(G(R(36.5, 138, 57.5, 143)), linear(32, 136, 56, 146), chanfro=0.18)
    t.pintar(G(R(43.5, 128, 50.5, 139)), plano(0.8))
    t.pintar(G(R(38, 120, 56, 129)), plano(0.7), chanfro=0.12)
    t.pintar(G(R(32.5, 74, 37.5, 118)), plano(0.7))
    cano = P([(39, 52), (63, 52), (64.5, 98), (38, 101)])
    pe = P([(38, 99), (62, 97), (71, 100), (79, 104), (84, 108.5), (86.5, 113), (86.5, 118), (38, 118)]) \
        | E(80.0, 112.5, 6.5, 5.8)
    t.pintar(G(cano | pe), linear(40, 50, 90, 125, 0.6), chanfro=0.15)
    t.pintar(G(pe & E(84.5, 112.5, 8.5, 8.5)), plano(0.85), contorno=False, chanfro=0.2)
    t.pintar(G(R(36, 117, 89, 121.5)), plano(0.45), chanfro=0.18)
    for y0 in (63.0, 79.0):
        t.pintar(G(R(36, y0, 66, y0 + 4.5) & dilatar(cano)), plano(0.3))
        t.pintar(G(R(53.5, y0 - 1.6, 61.5, y0 + 6.1)), plano(0.8), chanfro=0.12)
    t.pintar(G(R(36, 46.5, 66, 54.5)), plano(0.75), chanfro=0.15)
    ex, ey = _CPX_PASSO[0] + (51 - _CPX_PASSO[0]) * math.cos(0.18) - (99.5 - _CPX_PASSO[1]) * math.sin(0.18) - 4, \
        _CPX_PASSO[1] + (51 - _CPX_PASSO[0]) * math.sin(0.18) + (99.5 - _CPX_PASSO[1]) * math.cos(0.18)
    t.pintar(engrenagem(ex, ey, 6.5, 10, 2.2, fase=0.5), linear(ex - 9, ey - 9, ex + 9, ey + 9), chanfro=0.2)
    t.cristal_pequeno(int(ex), int(ey), aura=False)


def il_coracao_caldeira(t):
    cx, cy = CX, 100.0
    for k, r in enumerate((33.0, 37.5, 42.0)):
        lim = 26 - k * 5
        for a0 in (0.0, 180.0):
            for a in range(int(a0 - lim), int(a0 + lim) + 1, 2):
                ra = math.radians(a)
                t.put(int(cx + math.cos(ra) * r), int(cy + math.sin(ra) * r), "C2" if abs(a - a0) < lim * 0.6 else "C1")
    t.vapor([(18.0, 51.0, 2.4), (21.5, 56.5, 3.2)])
    t.vapor([(94.0, 51.0, 2.4), (90.5, 56.5, 3.2)])
    for sx in (-1, 1):
        pts = [(CX + sx * 12, 82.0), (CX + sx * 15, 71.0), (CX + sx * 22, 64.5), (CX + sx * 29, 63.5)]
        t.pintar(L(pts, 3.8), linear(22, 58, 90, 86), chanfro=0.1)
        fx = CX + sx * 30.5
        t.pintar(R(fx - 1.6, 59.5, fx + 1.6, 67.5), cil_x(fx - 1.6, fx + 1.6), chanfro=0.15)
    t.pintar(R(54, 68, 58, 84), cil_x(54, 58))
    cor_ = E(44.5, 91.0, 12.8) | E(67.5, 91.0, 12.8) | P([(32.4, 95.0), (79.6, 95.0), (56.0, 129.0)])
    t.pintar(cor_, esfera(cx, 97.0, 30.0), chanfro=0.15)
    t.pintar(anel(cx, 103.0, 6.6, 9.0), esfera(cx, 103.0, 9.0), chanfro=0.15)
    t.fill(E(cx, 103.0, 6.6), "C0")
    t.cristal(cx, 103.0, 3.4, 5.6, aura=False)
    t.pintar(R(51, 72, 61, 75), cil_x(51, 61), chanfro=0.1)
    t.pintar(E(CX, 63.0, 7.0), esfera(CX, 63.0, 7.0), claro="B0", escuro="B0")
    t.fill(E(CX, 63.0, 5.2), "K")
    t.pintar(R(54.2, 126, 57.8, 132), cil_x(54.2, 57.8))
    t.pintar(R(51, 131, 61, 133.5), cil_x(51, 61), chanfro=0.1)


def il_pavio_curto(t):
    bx = 42.0
    t.pintar(R(22, 135, 90, 140), linear(22, 135, 90, 140), chanfro=0.18)
    t.pintar(R(25, 128, 59, 135), cil_x(25, 59), chanfro=0.15)
    for i in range(10):
        ey = 124.8 - i * 4.8
        t.pintar(E(bx, ey, 14.6, 3.0), cil_x(27.4, 56.6, 0.92), chanfro=0.12)
    t.pintar(R(25, 73, 59, 80), cil_x(25, 59), chanfro=0.15)
    t.pintar(R(37.5, 67, 46.5, 73.5), cil_x(37.5, 46.5), chanfro=0.12)
    t.cristal(bx, 59.5, 3.8, 7.2)
    t.pintar(R(64, 100, 88, 135.5), cil_x(64, 88))
    t.fill(R(70.8, 111.8, 81.2, 122.2), "C0")
    t.cristal_pequeno(76, 117, aura=False)
    t.pintar(R(66.5, 95, 85.5, 101), cil_x(66.5, 85.5), chanfro=0.12)
    t.pintar(R(73.5, 91.5, 78.5, 95.5), cil_x(73.5, 78.5))
    t.pintar(L([(58.5, 77.0), (63.0, 80.0), (64.0, 90.0), (66.5, 97.5)], 1.3), plano(0.55))
    t.pintar(L([(76.0, 92.0), (76.6, 89.4), (78.6, 87.0)], 2.3), plano(0.48))
    t.raio([(46, 54), (53, 50), (60, 53), (67, 52), (73, 58), (77, 66), (79, 75), (79, 81)], ponta=False)
    for i in range(14):
        a = math.radians(-170 + i * (160 / 13))
        r1 = 7.5 + 3.5 * ((i * 7) % 3)
        for k, (x, y) in enumerate(B((80 + math.cos(a) * 2.5, 85.5 + math.sin(a) * 2.5),
                                     (80 + math.cos(a) * r1, 85.5 + math.sin(a) * r1))):
            if t.fundo_em(x, y) and k % 2 == 0:
                t.put(x, y, "B3" if k < 3 else "B2")
    t.estrela(80, 85, 4)


def il_fornalha_faminta(t):
    bx, by, rx, ry = CX, 109.0, 17.0, 10.5
    t.vapor([(67.0, 33.0, 2.2), (62.0, 36.5, 3.0)])
    t.pintar(R(50, 44, 62, 66), cil_x(50, 62))
    t.pintar(R(47, 40, 65, 45.5), cil_x(47, 65), chanfro=0.15)
    for sx in (-1, 1):
        t.pintar(P([(CX + sx * 20, 131), (CX + sx * 14, 131), (CX + sx * 18, 145), (CX + sx * 25.5, 145)]),
                 linear(28, 128, 84, 146), chanfro=0.15)
        t.pintar(E(CX + sx * 21.8, 145.0, 4.2, 1.6), plano(0.5))
    t.pintar(P([(40, 69), (72, 69), (70, 89), (42, 89)]), cil_x(40, 72, 0.95), chanfro=0.15)
    t.pintar(R(35, 64, 77, 70), cil_x(35, 77), chanfro=0.18)
    t.pintar(E(CX, 107.0, 28.0, 25.0), esfera(CX, 104.0, 29.0, 0.95), chanfro=0.15)
    t.pintar(R(31, 128, 81, 134), cil_x(31, 81), chanfro=0.18)
    t.pintar(E(CX, 77.5, 5.2), esfera(CX, 77.5, 5.2), claro="B0", escuro="B0")
    t.fill(E(CX, 77.5, 3.8), "K")
    for sx in (-1, 1):
        t.pintar(engrenagem(bx + sx * (rx + 2.8), by, 4.4, 9, 2.0, fase=0.25), plano(0.7), chanfro=0.2)
    boca = E(bx, by, rx, ry)
    t.pintar(E(bx, by, rx + 3.6, ry + 3.4) - boca, linear(bx - 21, by - 14, bx + 21, by + 14), chanfro=0.2)
    for x, y in boca:
        v = 1.12 - math.hypot((x + 0.5 - bx) / rx, (y + 0.5 - by - 6) / (ry * 1.2)) * 0.85
        t.put(x, y, OURO[quant(v, x, y, 5, 0.8)])
    t.cristal(bx, by - 1.0, 4.4, 8.2, aura=False)
    for xs, lado, comp in (((44.5, 50.3, 56.0, 61.7, 67.5), -1, 4.2), ((47.2, 53.0, 59.0, 64.8), 1, 3.4)):
        for x in xs:
            u = (x - bx) / rx
            yb = by + lado * ry * math.sqrt(max(0.0, 1 - u * u))
            ponta = yb - lado * comp
            t.pintar(P([(x - 2.4, yb + lado * 1.2), (x + 2.4, yb + lado * 1.2), (x + 1.15, ponta), (x - 1.15, ponta)]),
                     plano(0.8), chanfro=0.2)


CARDS.append(("vapor_condensado", "VI DE COPAS", "VAPOR CONDENSADO", "minor", 34))
CARDS.append(("frasco_faisca", "VIII DE COPAS", "FRASCO DE FAÍSCA", "minor", 34))
CARDS.append(("calice_cheio", "X DE COPAS", "CÁLICE CHEIO", "minor", 34))
CARDS.append(("tonico_fraco", "PAJEM DE COPAS", "TÔNICO FRACO", "minor", 34))
CARDS.append(("passo_pistao", "III DE PAUS", "PASSO DE PISTÃO", "minor", 34))
CARDS.append(("coracao_caldeira", "VIII DE PAUS", "CORAÇÃO DE CALDEIRA", "minor", 34))
CARDS.append(("pavio_curto", "II DE PAUS", "PAVIO CURTO", "minor", 34))
CARDS.append(("fornalha_faminta", "V DE PAUS", "FORNALHA FAMINTA", "minor", 34))
