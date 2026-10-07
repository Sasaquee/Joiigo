"""
Fase 10 (D-088), grupo G3: Luva de Cobre, Cristal de Fenda, Engrenagem Mestra, Braçadeira de Latão, A Força,
Coroa de Rebites e Pacto de Cristal, na grade de pixel.

Executado dentro de card_pixel.py (mesmo espaço de nomes). Esta versão serve de mapa de ocupação das estrelas
(as formas ocupam as mesmas coordenadas da versão em alta, hd_ouros_forca.py) e acrescenta as cartas a CARDS.
Auxiliares com o prefixo _of_ para não colidir com os dos outros grupos.
"""


def _of_trap(xl0, xr0, y0, xl1, xr1, y1):
    return P([(xl0, y0), (xr0, y0), (xr1, y1), (xl1, y1)])


def _of_trap_luz(xl0, xr0, y0, xl1, xr1, y1, k=1.0, d=0.0):
    def f(x, y):
        s = max(0.0, min(1.0, (y + 0.5 - y0) / (y1 - y0)))
        xl = xl0 + (xl1 - xl0) * s
        xr = xr0 + (xr1 - xr0) * s
        return perfil((x + 0.5 - xl) / max(xr - xl, 1e-3)) * k + d
    return f


def _of_spline(pts, n=8):
    if len(pts) < 3:
        return list(pts)
    p = [pts[0]] + list(pts) + [pts[-1]]
    out = []
    for i in range(1, len(p) - 2):
        p0, p1, p2, p3 = p[i - 1], p[i], p[i + 1], p[i + 2]
        for k in range(n):
            s = k / n
            s2, s3 = s * s, s * s * s
            out.append(tuple(0.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * s + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * s2
                                    + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * s3) for j in (0, 1)))
    out.append(tuple(pts[-1]))
    return out


def _of_rebite(t, x, y):
    t.rebite(int(round(x)), int(round(y)))


def _of_soquete(t, cx, cy, r, rx=None):
    soq = E(cx, cy, rx or r, r)
    t.fill(soq, "K")
    for x, y in borda(soq):
        t.put(x, y, "B0" if (x + 0.5 - cx) + (y + 0.5 - cy) < 0 else "B3")
    return soq


def _of_engr(t, cx, cy, r, n, h, fase=0.0, furo=0.36, k=1.0):
    e = engrenagem(cx, cy, r, n, h, fase=fase)
    t.pintar(e, linear(cx - r - h, cy - r - h, cx + r + h, cy + r + h, k, 0.08), chanfro=0.2)
    t.fill(E(cx, cy, r * furo), "K")
    return e


def _of_dedo(t, x0, x1, y0, y1, juntas=(0.45,)):
    cortes = [y0] + [y0 + (y1 - y0) * f for f in juntas] + [y1]
    d = R(x0, y0, x1, y1)
    t.pintar(d, cil_x(x0, x1), chanfro=0.1)
    for a in cortes[1:-1]:
        for x in range(int(x0) + 1, int(x1)):
            t.put(x, int(a), "B0")
    return d


def _of_veia(t, pts, dentro):
    for x, y in L(pts, 0.8):
        if (x, y) in dentro:
            t.put(x, y, "V1")


def _of_elipse(cx, cy, rx, ry, ang=0.0, n=48):
    return P(girar([(math.cos(a) * rx, math.sin(a) * ry) for a in (i * math.tau / n for i in range(n))], cx, cy, ang))


# ---------- II DE OUROS · LUVA DE COBRE ----------

def il_luva_cobre(t):
    t.pintar(_of_trap(44, 68, 114, 38, 74, 138), _of_trap_luz(44, 68, 114, 38, 74, 138))
    t.pintar(R(36, 135.5, 76, 141), cil_x(36, 76), chanfro=0.15)
    t.pintar(R(42.5, 112.5, 69.5, 117.5), cil_x(42.5, 69.5), chanfro=0.12)
    for x in (39, 44, 67, 72):
        t.rebite(x, 132)
    t.pintar(engrenagem(56, 126.0, 5.6, 10, 2.0, fase=0.5), linear(48, 118, 64, 134, 1.0, 0.1), chanfro=0.18)
    _of_soquete(t, 56, 126.0, 4.2)
    t.cristal(56, 126.0, 2.5, 3.9)
    for y0, x0, x1 in ((106.0, 45.0, 67.0), (100.5, 45.8, 66.2)):
        t.pintar(R(x0, y0, x1, y0 + 7.5), cil_x(x0, x1, 0.95), chanfro=0.12)
    t.pintar(R(36, 81, 78, 106), linear(36, 81, 78, 106, 0.95, 0.04), chanfro=0.18)
    for x0, x1, y0 in ((37.0, 47.0, 66.0), (47.6, 57.8, 62.0), (58.4, 68.2, 63.0), (68.8, 77.4, 67.5)):
        _of_dedo(t, x0, x1, y0, 87.5, (0.5,))
    t.pintar(E(33.5, 95.0, 7.5, 10.0), esfera(33.5, 93.0, 9.5), chanfro=0.15)
    t.pintar(L([(34.0, 88.5), (59.0, 87.0)], 8.4), lambda x, y: perfil((y + 0.5 - 83.0) / 9.6) * 0.95 + 0.06,
             chanfro=0.15)
    t.pintar(R(51.5, 84.6, 61.0, 90.0), plano(0.8), chanfro=0.15)
    t.estrela(56, 49, 3)
    t.estrela(32, 56, 1)
    t.estrela(81, 57, 1)


# ---------- IX DE OUROS · CRISTAL DE FENDA ----------

def il_cristal_fenda(t):
    cx, cy = 56.0, 86.0
    m = t.cristal(cx, cy, 13.0, 30.0)
    racha = [(57.4, 57.5), (54.4, 66.0), (58.6, 74.5), (53.8, 83.0), (58.2, 91.5), (53.6, 100.0), (56.4, 109.0)]
    for x, y in L(racha, 1.5):
        if (x, y) in m:
            t.put(x, y, "WH")
    for (ya, sx, a) in ((66, -1, -26), (74, 1, -14), (82, -1, 0), (90, 1, 12), (98, -1, 22),
                        (70, 1, -32), (88, -1, -10), (96, 1, 26)):
        ang = math.radians(a)
        p0 = (cx + sx * 10, ya)
        p1 = (cx + sx * (10 + 30 * math.cos(ang)), ya + 30 * math.sin(ang) * 0.9)
        for i, (x, y) in enumerate(B(p0, p1)):
            if t.fundo_em(x, y) and i < 20:
                t.put(x, y, "C1" if i < 10 else "C0")
    for (x, y) in B((57, 56), (58, 33)):
        if t.fundo_em(x, y):
            t.put(x, y, "C2")
    for (sx_, sy_, a, s) in ((71.0, 60.0, 25, 1.0), (40.0, 66.0, -35, 0.85), (75.0, 78.0, 60, 0.7),
                             (37.0, 80.0, 10, 0.6), (67.0, 50.0, -15, 0.6)):
        cac = P(girar([(-1.4 * s, 0.6 * s), (0.2 * s, -2.6 * s), (1.6 * s, 0.2 * s), (0.0, 2.0 * s)],
                      sx_, sy_, math.radians(a)))
        t.fill(cac | {(int(sx_), int(sy_))}, "C2")
    t.estrela(57, 53, 3, ("WH", "C2", "C1", "C0"))
    for sx in (-1, 1):
        for pts, tip in (([(cx + sx * 12.6, 89.0), (cx + sx * 13.1, 82.0), (cx + sx * 11.6, 76.0)], (cx + sx * 10.2, 74.4)),
                         ([(cx + sx * 12.6, 95.0), (cx + sx * 12.5, 101.0), (cx + sx * 9.0, 107.5)], (cx + sx * 7.8, 108.8))):
            t.pintar(L(_of_spline(pts + [tip]), 2.3), linear(40, 72, 72, 110), chanfro=0.15)
            t.pintar(E(tip[0], tip[1], 1.45), esfera(tip[0], tip[1], 1.45))
    cinta = (R(41.0, 88.0, 71.0, 94.5) | E(56, 94.5, 15.0, 2.4)) - E(56, 88.0, 15.0, 2.2)
    t.pintar(cinta, cil_x(41, 71), chanfro=0.18)
    t.pintar(E(56, 92.6, 2.6, 2.9), esfera(56, 92.6, 2.9), chanfro=0.12)
    for x, y in B((56, 124), (56, 117)):
        if t.fundo_em(x, y):
            t.put(x, y, "C2")
    t.pintar(R(43.0, 124.5, 69.0, 132.5), cil_x(43, 69), chanfro=0.12)
    t.pintar(E(56, 124.5, 13.0, 3.2), plano(0.75), chanfro=0.15)
    t.fill(E(56, 124.4, 5.4, 1.4), "C2")
    t.pintar(R(35.5, 132.0, 76.5, 137.0), linear(35.5, 132, 76.5, 137), chanfro=0.18)


# ---------- REI DE OUROS · ENGRENAGEM MESTRA ----------

def il_engrenagem_mestra(t):
    gx, gy, gr = 56.0, 103.0, 17.0
    for (cx, cy, r, n, h, fase) in ((28.5, 108.0, 6.5, 9, 2.8, 0.25), (83.5, 108.0, 6.5, 9, 2.8, 0.75),
                                    (43.0, 125.5, 4.6, 8, 2.3, 0.1), (69.0, 125.5, 4.6, 8, 2.3, 0.6)):
        _of_engr(t, cx, cy, r, n, h, fase, k=0.92)
    for (cx, cy, r, n, h, fase) in ((19.5, 90.0, 3.4, 7, 1.9, 0.2), (92.5, 90.0, 3.4, 7, 1.9, 0.7)):
        _of_engr(t, cx, cy, r, n, h, fase, furo=0.42, k=0.85)
    roda = engrenagem(gx, gy, gr, 14, 4.5, fase=0.0)
    janelas = anel(gx, gy, 6.9, 11.6)
    for i in range(6):
        a = math.radians(i * 60 - 90)
        janelas = janelas - L([(gx + math.cos(a) * 5, gy + math.sin(a) * 5),
                               (gx + math.cos(a) * 12.8, gy + math.sin(a) * 12.8)], 3.6)
    t.pintar(roda - janelas, linear(gx - 22, gy - 22, gx + 22, gy + 22, 1.05, 0.06), chanfro=0.22)
    t.fill(janelas, "K")
    t.pintar(E(gx, gy, 7.2), esfera(gx, gy, 7.2), chanfro=0.15)
    _of_soquete(t, gx, gy, 5.0)
    t.cristal(gx, gy, 3.2, 5.0, aura=False)
    coroa_pts = [(43.0, 78.0), (43.0, 64.0), (47.6, 71.0), (50.5, 59.5), (53.6, 69.0), (56.0, 54.5), (58.4, 69.0),
                 (61.5, 59.5), (64.4, 71.0), (69.0, 64.0), (69.0, 78.0)]
    t.pintar(P(coroa_pts), linear(43, 54, 69, 78, 1.0, 0.1), chanfro=0.22)
    for (x, y) in ((43.0, 64.0), (50.5, 59.5), (56.0, 54.5), (61.5, 59.5), (69.0, 64.0)):
        t.pintar(E(x, y - 0.6, 1.8), esfera(x, y - 0.6, 1.8))
    t.pintar(R(41.5, 75.0, 70.5, 81.5), cil_y(73.5, 81.5, 1.0, 0.04), chanfro=0.15)
    t.cristal(56, 77.6, 2.2, 3.6)
    t.estrela(56, 46, 2)


# ---------- V DE OUROS · BRAÇADEIRA DE LATÃO ----------

_OF_BR = (56.0, 104.0, math.radians(22))


def _of_br_g(pts):
    ox, oy, ang = _OF_BR
    return girar(pts, ox, oy, ang)


def _of_br_hw(ly):
    return 13.0 + 4.5 * (ly + 40.0) / 78.0


def _of_br_faixa(ly0, ly1, folga=0.8, ry=3.6, n=20):
    cima, baixo = [], []
    for i in range(n + 1):
        f = -1 + 2 * i / n
        hw0, hw1 = _of_br_hw(ly0) + folga, _of_br_hw(ly1) + folga
        cima.append((f * hw0, ly0 + ry * math.sqrt(max(0.0, 1 - f * f))))
        baixo.append((f * hw1, ly1 + ry * math.sqrt(max(0.0, 1 - f * f))))
    return P(_of_br_g(cima + baixo[::-1]))


def il_bracadeira_latao(t):
    ox, oy, ang = _OF_BR
    g = _of_br_g

    def luz_corpo(lx, ly):
        hw = _of_br_hw(ly)
        return perfil((lx + 0.5 + hw) / (2 * hw))
    corpo = P(g([(-13.0, -40.0), (13.0, -40.0), (17.5, 38.0), (-17.5, 38.0)])) | _of_elipse(*g([(0, 38.0)])[0], 17.5, 5.0, ang)
    t.pintar(corpo, local(luz_corpo, ox, oy, ang))
    bx_, by_ = g([(0, -40.0)])[0]
    boca = _of_elipse(bx_, by_, 13.0, 4.4, ang)
    t.fill(boca, "K")
    for ly0, ly1 in ((-31.0, -25.5), (23.5, 29.0)):
        t.pintar(_of_br_faixa(ly0, ly1), plano(0.55), chanfro=0.12)
        hw = _of_br_hw((ly0 + ly1) / 2) + 1.0
        lm = (ly0 + ly1) / 2 + 0.6
        t.pintar(P(g([(hw - 2.6, lm - 4.4), (hw + 3.8, lm - 4.4), (hw + 3.8, lm + 4.4), (hw - 2.6, lm + 4.4)])),
                 plano(0.7), chanfro=0.15)
        t.pintar(L(g([(hw + 3.6, lm), (hw + 7.2, lm)]), 3.0), plano(0.6))
    t.pintar(_of_br_faixa(34.0, 38.5, 0.9, 5.0), plano(0.6), chanfro=0.12)
    t.pintar(_of_elipse(bx_, by_, 14.4, 5.6, ang) - _of_elipse(bx_, by_, 12.6, 3.9, ang), plano(0.7), chanfro=0.2)
    for k in range(5):
        ly = -21.0 + k * 8.4
        a, b = g([(-_of_br_hw(ly) - 0.8, ly), (-_of_br_hw(ly + 6.0) - 0.8, ly + 6.0)])
        t.pintar(L([a, b], 2.8), plano(0.6), chanfro=0.1)
    for tb in ([(10.5, -9.5), (15.0, -9.5)], [(10.5, -1.5), (15.0, -1.5)],
               [(3.6, 13.0), (9.0, 13.0), (13.0, 10.0), (17.0, 7.0)],
               [(-5.2, 13.0), (-9.0, 15.0), (-11.6, 20.0)], [(0.0, -17.5), (0.0, -22.0)]):
        t.pintar(L(g(tb), 1.5), plano(0.6))
    cap = L(g([(17.6, -13.0), (17.6, 4.0)]), 5.6)
    t.contorno(cap, "B2", "B1")
    t.fill(cap, "K")
    t.fill(cap & P(g([(10, -6.0), (25, -6.0), (25, 10), (10, 10)])), "C1")
    t.fill(L(g([(17.2, -5.0), (17.2, 3.0)]), 1.2), "C2")
    t.halo_arcano(cap, 2, 80)
    for ly in (-13.6, 5.0):
        t.pintar(L(g([(14.4, ly), (20.8, ly)]), 3.2), plano(0.7), chanfro=0.12)
    mx, my = g([(0.0, -6.0)])[0]
    t.pintar(E(mx, my, 11.0), esfera(mx, my, 11.0, 1.0, 0.04), chanfro=0.15)
    t.fill(E(mx, my, 8.6), "K")
    for a in range(-50, 31, 6):
        ra = math.radians(a) + ang
        t.put(int(mx + math.cos(ra) * 7.0), int(my + math.sin(ra) * 7.0), "C2")
    a = math.radians(-20) + ang
    for (x, y) in B((mx, my), (mx + math.cos(a) * 7.0, my + math.sin(a) * 7.0)):
        t.put(x, y, "B4")
    sx_, sy_ = g([(-1.0, 13.0)])[0]
    t.pintar(E(sx_, sy_, 5.4), esfera(sx_, sy_, 5.4), chanfro=0.12)
    t.fill(E(sx_, sy_, 4.0), "K")
    t.put(int(sx_), int(sy_), "B4")


# ---------- XI · A FORÇA ----------

def il_forca(t):
    m = set()
    for i in range(240):
        a = i * math.tau / 240
        k = 1 + math.sin(a) ** 2
        m |= E(CX + 11 * math.cos(a) / k, 47 + 11 * math.sin(a) * math.cos(a) / k, 1.15)
    t.pintar(m, linear(44, 42, 68, 53, 1.0, 0.12), chanfro=0.15)
    for s in (-1, 1):
        t.massa_vapor([(CX + s * 30.0, 121.0, 3.4), (CX + s * 33.5, 126.5, 4.2), (CX + s * 30.5, 133.0, 4.6),
                       (CX + s * 25.5, 138.5, 3.6), (CX + s * 34.5, 135.0, 3.2)])
    barra_pts = _of_spline([(14.5, 100.0), (23.0, 87.5), (37.0, 78.5), (56.0, 74.5), (75.0, 78.5), (89.0, 87.5),
                            (97.5, 100.0)], 10)
    t.pintar(L(barra_pts, 7.2), plano(0.4), chanfro=0.3)
    t.estrela(24, 78, 2)
    t.estrela(87, 78, 2)
    t.estrela(18, 89, 1)
    t.estrela(93, 89, 1)
    for x0, xj in ((35.0, 44.5), (77.0, 67.5)):
        t.pintar(L([(x0, 101.0), (xj, 101.5)], 2.2), plano(0.6))
        t.pintar(R(x0 - 1.4, 100, x0 + 1.4, 114), cil_x(x0 - 1.4, x0 + 1.4, 1.05))
        t.pintar(R(x0 - 3.6, 112, x0 + 3.6, 140), cil_x(x0 - 3.6, x0 + 3.6))
        for ya in (111.5, 137.0):
            t.pintar(R(x0 - 4.6, ya, x0 + 4.6, ya + 3.2), cil_x(x0 - 4.6, x0 + 4.6), chanfro=0.12)
        t.pintar(E(x0, 143.0, 2.2), esfera(x0, 143.0, 2.2))
        t.pintar(E(x0, 101.0, 2.5), esfera(x0, 101.0, 2.5))
    t.pintar(_of_trap(44, 68, 100, 41, 71, 150), _of_trap_luz(44, 68, 100, 41, 71, 150))
    t.fill(R(50.5, 108.0, 61.5, 140.0), "K")
    for i, y in enumerate((111, 121, 131)):
        t.runa(55, y, (0, 1, 3)[i])
    for (ya, x0, x1) in ((101.5, 42.7, 69.3), (140.5, 40.9, 71.1), (146.5, 40.3, 71.7)):
        t.pintar(R(x0, ya, x1, ya + 3.6), cil_x(x0, x1), chanfro=0.12)
    t.pintar(R(44.5, 94.5, 67.5, 102.0), cil_x(44.5, 67.5, 0.95), chanfro=0.12)
    t.pintar(R(39.0, 76.0, 73.5, 98.0), linear(39, 76, 73.5, 98, 0.95, 0.04), chanfro=0.18)
    for x0, x1, y0 in ((40.5, 48.4, 69.5), (48.9, 56.8, 66.5), (57.3, 65.2, 67.5), (65.7, 72.8, 71.0)):
        _of_dedo(t, x0, x1, y0, 85.0, (0.5,))
    t.pintar(E(74.5, 90.0, 6.2, 8.4), esfera(73.5, 88.0, 8.0), chanfro=0.15)
    t.pintar(L([(73.0, 86.0), (51.5, 84.6)], 7.2), lambda x, y: perfil((y + 0.5 - 81.2) / 8.4) * 0.95 + 0.06,
             chanfro=0.15)
    t.pintar(R(48.4, 82.0, 56.0, 87.4), plano(0.8), chanfro=0.15)


# ---------- VIII DE OUROS (amaldiçoada) · COROA DE REBITES ----------

def il_coroa_rebites(t):
    cx, yt, yb, rx, ry = 56.0, 103.0, 121.0, 28.0, 7.0

    def frente(x, y0):
        return y0 + ry * math.sqrt(max(0.0, 1 - ((x - cx) / rx) ** 2))

    def tras(x, y0):
        return y0 - ry * math.sqrt(max(0.0, 1 - ((x - cx) / rx) ** 2))

    def espinho(bx, by, w, h, k, ponta):
        tip = (bx, by - h)
        m = P([(bx - w, by + 2.0), tip, (bx + w, by + 2.0)])
        t.pintar(m, lambda x, y: (0.74 if x + 0.5 < bx else 0.36) * k, chanfro=0.12)
        if ponta:
            t.fill({p for p in m if p[1] + 0.5 < tip[1] + h * 0.2}, "V2")
        return m

    for x, h in ((37.0, 15.0), (47.0, 21.0), (65.0, 21.0), (75.0, 15.0)):
        espinho(x, tras(x, yt) + 1.0, 3.2, h, 0.6, False)
    boca = E(cx, yt, rx, ry)
    t.fill(boca, "V0")
    t.fill(boca - E(cx, yt + 4.2, rx - 0.8, ry), "B1")
    for x, y in borda(boca):
        t.put(x, y, "B3")
    banda = (E(cx, yb, rx, ry) | R(cx - rx, yt, cx + rx, yb)) - E(cx, yt, rx, ry)
    t.pintar(banda, cil_x(cx - rx, cx + rx, 1.0, 0.02), chanfro=0.15)
    for x in range(30, 83, 5):
        for y0, dy in ((yt, 2.4), (yb, -2.6)):
            _of_rebite(t, x, frente(x + 0.85, y0) + dy - 0.85)
    for gx_ in (41.5, 70.5):
        gy_ = (frente(gx_, yt) + frente(gx_, yb)) / 2
        _of_engr(t, gx_, gy_, 2.9, 8, 1.6, 0.0, furo=0.4, k=0.9)
    for x, h, w in ((31.0, 21.0, 3.4), (42.5, 30.0, 4.0), (56.0, 42.0, 4.8), (69.5, 30.0, 4.0), (81.0, 21.0, 3.4)):
        espinho(x, frente(x, yt) - 0.6, w, h, 0.85, True)
    for v in ([(49.5, 115.0), (46.0, 113.4), (44.6, 116.6), (39.0, 114.8), (35.5, 118.0), (31.0, 116.2)],
              [(62.5, 115.5), (65.8, 118.6), (68.6, 115.2), (73.8, 117.6), (77.2, 114.4)],
              [(52.0, 124.5), (49.0, 127.6), (45.5, 126.4)], [(60.0, 124.5), (63.4, 127.2), (66.2, 125.6)]):
        _of_veia(t, v, banda)
    t.pintar(E(cx, 116.0, 7.4, 10.4), esfera(cx, 116.0, 10.4, 1.0, 0.05), chanfro=0.18)
    _of_soquete(t, cx, 116.0, 8.0, 5.1)
    t.cristal(cx, 116.0, 4.0, 7.4, aura=False)
    for (x, y, tam) in ((24, 84, 1), (88, 86, 1), (56, 58, 2)):
        t.estrela(x, y, tam, ("WH", "V2", "V1", "V0"))


# ---------- VII DE PAUS (amaldiçoada) · PACTO DE CRISTAL ----------

def il_pacto_cristal(t):
    t.pintar(_of_trap(45, 67, 136, 40, 72, 158), _of_trap_luz(45, 67, 136, 40, 72, 158))
    for (ya, x0, x1) in ((135.0, 43.5, 68.5), (152.5, 38.5, 73.5)):
        t.pintar(R(x0, ya, x1, ya + 3.6), cil_x(x0, x1), chanfro=0.12)
    t.pintar(L(_of_spline([(41.0, 128.0), (33.0, 120.0), (29.5, 111.0), (29.5, 103.0)]), 7.0),
             linear(24, 98, 42, 130, 1.0, 0.05), chanfro=0.15)
    palma = R(38.0, 100.0, 74.0, 138.0)
    t.pintar(palma, linear(38, 100, 74, 138, 0.95, 0.05), chanfro=0.2)
    for x0, x1, y0 in ((38.4, 46.4, 69.0), (47.3, 55.3, 63.0), (56.2, 64.2, 61.0), (65.1, 72.5, 67.0)):
        _of_dedo(t, x0, x1, y0, 104.0, (0.34, 0.67))
    mao = palma | R(38.4, 61, 72.5, 104)
    for v in ([(52.0, 113.0), (49.6, 117.6), (51.0, 121.4), (47.2, 126.0), (48.4, 130.2), (45.6, 134.0)],
              [(60.0, 113.0), (62.8, 117.2), (61.2, 121.6), (65.0, 125.4), (63.8, 129.6)],
              [(52.4, 82.0), (50.6, 77.4), (52.0, 73.2), (50.8, 69.0)],
              [(59.6, 82.0), (61.2, 76.6), (59.8, 71.4), (61.0, 66.0)]):
        _of_veia(t, v, mao)
    t.cristal(56, 96, 9.0, 18.0)
    for p0, p1 in (((28.0, 76.0), (84.0, 128.0)), ((84.0, 76.0), (28.0, 128.0))):
        t.pintar(L([p0, p1], 2.8), plano(0.6), chanfro=0.15)
        for (ax, ay) in (p0, p1):
            t.pintar(anel(ax, ay, 1.6, 3.0), esfera(ax, ay, 3.0))
    t.pintar(E(56, 102, 5.4), esfera(56, 102, 5.4), chanfro=0.2)
    t.fill(E(56, 102, 3.9), "V0")
    t.runa(55, 100, 1)


CARDS.append(("luva_cobre", "II DE OUROS", "LUVA DE COBRE", "minor", 34))
CARDS.append(("cristal_fenda", "IX DE OUROS", "CRISTAL DE FENDA", "minor", 34))
CARDS.append(("engrenagem_mestra", "REI DE OUROS", "ENGRENAGEM MESTRA", "minor", 34))
CARDS.append(("bracadeira_latao", "V DE OUROS", "BRAÇADEIRA DE LATÃO", "minor", 34))
CARDS.append(("forca", "XI", "A FORÇA", "major", 34))
CARDS.append(("coroa_rebites", "VIII DE OUROS", "COROA DE REBITES", "cursed", 34))
CARDS.append(("pacto_cristal", "VII DE PAUS", "PACTO DE CRISTAL", "cursed", 34))
