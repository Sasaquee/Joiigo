"""
Fase 10 (D-088), grupo G3: Luva de Cobre, Cristal de Fenda, Engrenagem Mestra, Braçadeira de Latão, A Força,
Coroa de Rebites e Pacto de Cristal, em alta resolução.

Executado dentro de card_hd.py (mesmo espaço de nomes: R, E, P, L, arco, engrenagem, cil_x, esfera, linear,
t.pintar, t.cristal etc.). As formas ficam nas mesmas coordenadas de px_ouros_forca.py. Os auxiliares levam o
prefixo _of_ para não colidir com os dos outros grupos (todos os hd_*.py dividem este espaço de nomes).
"""


def _of_trap(xl0, xr0, y0, xl1, xr1, y1):
    """Trapézio de lados inclinados (largura xl0..xr0 em y0, xl1..xr1 em y1)."""
    return P([(xl0, y0), (xr0, y0), (xr1, y1), (xl1, y1)])


def _of_trap_luz(xl0, xr0, y0, xl1, xr1, y1, k=1.0, d=0.0):
    """Luz de cilindro para um trapézio (a faixa clara acompanha os lados inclinados)."""
    def f(X, Y):
        s = np.clip((Y - y0) / (y1 - y0), 0, 1)
        xl = xl0 + (xl1 - xl0) * s
        xr = xr0 + (xr1 - xr0) * s
        return perfil((X - xl) / np.maximum(xr - xl, 1e-3)) * k + d
    return f


def _of_soquete(t, cx, cy, r, rx=None):
    """Soquete escuro com aro (o miolo onde o cristal assenta)."""
    soq = E(cx, cy, rx or r, r)
    t.fill(soq, lambda X, Y: rampa(("K", t.arc[0]), np.full_like(X, 0.5)))
    t.fill(soq.anel(0.7), lambda X, Y: rampa(("B0", "B3"), smooth(-3, 3, (X - cx) + (Y - cy))))
    return soq


def _of_engr(t, cx, cy, r, n, h, fase=0.0, furo=0.36, k=1.0):
    """Engrenagem pequena com sulco e furo (como as da bancada do Artífice)."""
    e = engrenagem(cx, cy, r, n, h, fase=fase)
    t.pintar(e, linear(cx - r - h, cy - r - h, cx + r + h, cy + r + h, k, 0.08), chanfro=0.2)
    t.fill(anel(cx, cy, r * 0.72 - 0.55, r * 0.72), "B1", alfa=0.9)
    t.fill(E(cx, cy, r * furo), "K")
    t.fill((E(cx, cy, r * furo + 0.5) - E(cx, cy, r * furo)) & semiplano(0, -1, -cy), "B3", alfa=0.8)
    return e


def _of_dedo(t, x0, x1, y0, y1, juntas=(0.45,), raio=3.0):
    """Dedo de placas articuladas: o segmento de cima sobrepõe o de baixo, rebite em cada junta."""
    cortes = [y0] + [y0 + (y1 - y0) * f for f in juntas] + [y1]
    xc = (x0 + x1) / 2
    segs = list(zip(cortes, cortes[1:]))
    for i, (a, b) in reversed(list(enumerate(segs))):
        enc = 0.35 * i
        seg = R(x0 + enc, a - (0 if i == 0 else 1.0), x1 - enc, b, raio if i == 0 else 1.6)
        t.pintar(seg, lambda X, Y, a=a, b=b, enc=enc: perfil((X - x0 - enc) / (x1 - x0 - 2 * enc))
                 + 0.1 * (1 - np.clip((Y - a) / max(b - a, 1), 0, 1)), chanfro=0.1)
    for a, b in segs[1:]:
        t.rebite(xc - 0.85, a + 0.3)
    t.clarear(E(xc - (x1 - x0) * 0.16, y0 + 2.2, (x1 - x0) * 0.22, 1.2), "B4", 0.45)


def _of_veia(t, pts, dentro, w=1.0):
    """Veia violeta fina e quebrada (a maldição correndo pelo metal)."""
    t.fill(L(pts, w * 1.25) & dentro, "V0", alfa=0.75)
    t.fill(L(pts, w * 0.6) & dentro, "V1", glow=1.0)
    t.fill(L(pts, w * 0.24) & dentro, "V2", glow=1.0)


def _of_elipse(cx, cy, rx, ry, ang=0.0, n=48):
    """Elipse girada (como polígono)."""
    return P(girar([(math.cos(a) * rx, math.sin(a) * ry) for a in np.linspace(0, math.tau, n, endpoint=False)],
                   cx, cy, ang))


# ---------- II DE OUROS · LUVA DE COBRE ----------

def il_luva_cobre(t):
    # sino da manopla com a engrenagem e o cristal
    t.pintar(_of_trap(44, 68, 114, 38, 74, 138), _of_trap_luz(44, 68, 114, 38, 74, 138))
    for sx in (-1, 1):
        t.fill(L([(56 + sx * 7.0, 118), (56 + sx * 10.5, 136)], 0.6), "B1")
        t.clarear(L([(56 + sx * 7.0 + 0.7, 118), (56 + sx * 10.5 + 0.7, 136)], 0.3), "B4", 0.3)
    t.pintar(R(36, 135.5, 76, 141, 0.6), cil_x(36, 76), chanfro=0.15)
    t.pintar(R(42.5, 112.5, 69.5, 117.5, 0.5), cil_x(42.5, 69.5), chanfro=0.12)
    for x in (39, 44, 67, 72):
        t.rebite(x, 131.5)
    t.pintar(engrenagem(56, 126.0, 5.6, 10, 2.0, fase=0.5), linear(48, 118, 64, 134, 1.0, 0.1), chanfro=0.18)
    _of_soquete(t, 56, 126.0, 4.2)
    t.cristal(56, 126.0, 2.5, 3.9, esp=0.5)
    # lâminas do pulso
    for y0, x0, x1 in ((106.0, 45.0, 67.0), (100.5, 45.8, 66.2)):
        t.pintar(R(x0, y0, x1, y0 + 7.5, 0.6), cil_x(x0, x1, 0.95), chanfro=0.12)
    # palma (base da mão)
    palma = R(36, 81, 78, 106, 5.0)
    t.pintar(palma, linear(36, 81, 78, 106, 0.95, 0.04), chanfro=0.18)
    for x, y in ((71, 97), (64, 101)):
        t.rebite(x, y)
    t.sulco_h(62, 75, 93.5, "B0", 0.25)
    # dedos fechados: blocos curtos com o nó redondo em cima
    for x0, x1, y0 in ((37.0, 47.0, 66.0), (47.6, 57.8, 62.0), (58.4, 68.2, 63.0), (68.8, 77.4, 67.5)):
        _of_dedo(t, x0, x1, y0, 87.5, (0.5,), (x1 - x0) / 2 - 0.2)
    for x0, x1 in ((58.4, 68.2), (68.8, 77.4)):             # pontas dos dedos que o polegar não cobre
        t.pintar(R(x0 + 1.4, 83.0, x1 - 1.4, 87.5, 1.6), plano(0.42), chanfro=0.12, esp=0.5)
    # polegar cruzando os dedos
    t.pintar(E(33.5, 95.0, 7.5, 10.0), esfera(33.5, 93.0, 9.5, 1.0, 0.04), chanfro=0.15)
    t.pintar(L([(34.0, 88.5), (59.0, 87.0)], 8.4), lambda X, Y: perfil((Y - 83.0) / 9.6) * 0.95 + 0.06, chanfro=0.15)
    t.sulco_v(45.2, 84.0, 91.5, "B0", 0.3)
    t.pintar(R(51.5, 84.6, 61.0, 90.0, 2.4), lambda X, Y: 0.86 - 0.3 * np.clip((Y - 84.6) / 5.4, 0, 1),
             chanfro=0.15, esp=0.5)
    t.rebite(40.0, 86.4)
    t.rebite(30.0, 98.0)
    # brilho do golpe
    t.estrela(56, 49, 3)
    t.estrela(32, 56, 1)
    t.estrela(81, 57, 1)


# ---------- IX DE OUROS · CRISTAL DE FENDA ----------

def il_cristal_fenda(t):
    cx, cy, w, h = 56.0, 86.0, 13.0, 30.0
    t.luz_radial(cx, 82, 22, "C1", 0.28, glow=0.35, so_fundo=True)
    m = t.cristal(cx, cy, w, h, aura=True)
    # luz que vaza pela fenda (só no fundo, em volta do cristal)
    for (ya, sx, a) in ((66, -1, -26), (74, 1, -14), (82, -1, 0), (90, 1, 12), (98, -1, 22),
                        (70, 1, -32), (88, -1, -10), (96, 1, 26)):
        ang = math.radians(a)
        p0 = (cx + sx * 10, ya)
        p1 = (cx + sx * (10 + 30 * math.cos(ang)), ya + 30 * math.sin(ang) * 0.9)
        t.raio_luz(p0, p1, "C2", "C0", 0.6, 0.35, glow=0.9)
    t.raio_luz((57.4, 56.0), (57.8, 33.0), "WH", "C1", 0.8, 0.3, glow=1.0)
    # a fenda: sulco largo e escuro, luz forte no meio
    racha = [(57.4, 57.5), (54.4, 66.0), (58.6, 74.5), (53.8, 83.0), (58.2, 91.5), (53.6, 100.0), (56.4, 109.0)]
    ramos = [[(58.6, 74.5), (63.0, 71.0), (66.5, 71.8)], [(53.8, 83.0), (48.8, 86.4), (45.2, 85.4)],
             [(58.2, 91.5), (63.4, 95.6)], [(54.4, 66.0), (50.2, 64.0)]]
    t.fill(L(racha, 3.0) & m, mix("C0", "K", 0.5))
    for rm in ramos:
        t.fill(L(rm, 1.2) & m, mix("C0", "K", 0.3))
    t.luz_radial(56, 84, 7.5, "C2", 0.4, dentro=m, glow=0.6)
    t.fill(L(racha, 1.5) & m, "C2", glow=1.0)
    t.fill(L(racha, 0.6) & m, "WH", glow=1.0)
    for rm in ramos:
        t.fill(L(rm, 0.5) & m, "C2", glow=1.0)
    # estilhaços soltos
    for (sx_, sy_, a, s) in ((71.0, 60.0, 25, 1.0), (40.0, 66.0, -35, 0.85), (75.0, 78.0, 60, 0.7),
                             (37.0, 80.0, 10, 0.6), (67.0, 50.0, -15, 0.6)):
        cac = P(girar([(-1.4 * s, 0.6 * s), (0.2 * s, -2.6 * s), (1.6 * s, 0.2 * s), (0.0, 2.0 * s)],
                      sx_, sy_, math.radians(a)))
        t.halo_arcano(cac, 1.3, 70)
        t.pintar(cac, lambda X, Y, sx_=sx_, sy_=sy_: 0.8 - 0.25 * ((X - sx_) + (Y - sy_)) / 3, CIANO,
                 claro="C0", escuro="C0", esp=0.4)
    t.estrela(57, 53, 3, ("WH", "C2", "C1", "C0"))
    # engaste: cinta de latão na cintura do cristal e garras subindo e descendo pelas faces
    for sx in (-1, 1):
        for pts, tip in (([(cx + sx * 12.6, 89.0), (cx + sx * 13.1, 82.0), (cx + sx * 11.6, 76.0)], (cx + sx * 10.2, 74.4)),
                         ([(cx + sx * 12.6, 95.0), (cx + sx * 12.5, 101.0), (cx + sx * 9.0, 107.5)], (cx + sx * 7.8, 108.8))):
            garra = L(spline(pts + [tip], 8), 2.3)
            t.pintar(garra, linear(40, 72, 72, 110), chanfro=0.15, esp=0.55)
            t.pintar(E(tip[0], tip[1], 1.45), esfera(tip[0], tip[1], 1.45), esp=0.45)
    cinta = (R(41.0, 88.0, 71.0, 94.5) | E(56, 94.5, 15.0, 2.4)) - E(56, 88.0, 15.0, 2.2)
    t.pintar(cinta, lambda X, Y: perfil((X - 41.0) / 30.0) * 0.95 + 0.05, chanfro=0.18)
    t.fill(E(56, 91.9, 14.6, 2.2) - E(56, 91.3, 14.6, 2.2), "B0", alfa=0.6)
    for x in (44.0, 50.0, 62.0, 68.0):
        t.rebite(x - 0.85, 88.8 + 2.4 * math.sqrt(max(0.0, 1 - ((x - 56) / 15) ** 2)) + 0.6, 0.75)
    t.pintar(E(56, 92.6, 2.6, 2.9), esfera(56, 92.6, 2.9), chanfro=0.12, esp=0.5)
    t.fill(E(56, 92.6, 1.1), "C2", glow=1.0)
    # a luz que mantém o cristal flutuando sobre a base
    t.luz_radial(56, 120.5, 5.5, "C1", 0.45, glow=0.7, so_fundo=True)
    t.raio_luz((56.0, 124.0), (56.0, 116.5), "C2", "C1", 1.4, 0.4, glow=0.9)
    for (x, y) in ((51.5, 119.5), (60.5, 118.5)):
        t.fill(E(x, y, 0.4), "C2", glow=1.0, so_fundo=True)
    # base emissora (tambor de latão)
    t.pintar(R(43.0, 124.5, 69.0, 132.5, 0.5), cil_x(43, 69), chanfro=0.12)
    for x in range(46, 68, 4):
        t.sulco_v(x, 127.0, 131.0, "B1", 0.25)
    topo = E(56, 124.5, 13.0, 3.2)
    t.pintar(topo, lambda X, Y: 0.82 - 0.2 * np.clip((Y - 121.3) / 6.4, 0, 1), chanfro=0.15)
    lente = E(56, 124.4, 5.4, 1.4)
    t.fill(lente.cresce(0.5), "B0")
    t.fill(lente, lambda X, Y: rampa(CIANO, 0.75 - 0.25 * np.abs(X - 56) / 5.4), glow=1.0)
    t.pintar(R(35.5, 132.0, 76.5, 137.0, 0.7), linear(35.5, 132, 76.5, 137), chanfro=0.18)
    for x in (38, 73):
        t.rebite(x, 133.7)


# ---------- REI DE OUROS · ENGRENAGEM MESTRA ----------

def il_engrenagem_mestra(t):
    gx, gy, gr = 56.0, 103.0, 17.0
    # engrenagens menores que servem a mestra
    for (cx, cy, r, n, h, fase) in ((28.5, 108.0, 6.5, 9, 2.8, 0.25), (83.5, 108.0, 6.5, 9, 2.8, 0.75),
                                    (43.0, 125.5, 4.6, 8, 2.3, 0.1), (69.0, 125.5, 4.6, 8, 2.3, 0.6)):
        _of_engr(t, cx, cy, r, n, h, fase, k=0.92)
    for (cx, cy, r, n, h, fase) in ((19.5, 90.0, 3.4, 7, 1.9, 0.2), (92.5, 90.0, 3.4, 7, 1.9, 0.7)):
        _of_engr(t, cx, cy, r, n, h, fase, furo=0.42, k=0.85)
    # a mestra: aro, raios e janelas com o núcleo arcano aparecendo
    roda = engrenagem(gx, gy, gr, 14, 4.5, fase=0.0)
    janelas = anel(gx, gy, 6.9, 11.6)
    for i in range(6):
        a = math.radians(i * 60 - 90)
        janelas = janelas - L([(gx + math.cos(a) * 5, gy + math.sin(a) * 5),
                               (gx + math.cos(a) * 12.8, gy + math.sin(a) * 12.8)], 3.6)
    t.pintar(roda - janelas, linear(gx - 22, gy - 22, gx + 22, gy + 22, 1.05, 0.06), chanfro=0.22)
    t.fill(janelas, lambda X, Y: rampa(("K", t.arc[0], t.arc[1]),
                                        0.72 * np.exp(-((X - gx) ** 2 + (Y - gy) ** 2) / 70.0)), glow=0.3)
    t.fill(anel(gx, gy, 14.8, 15.5), "B0")
    t.fill(anel(gx, gy, 15.6, 16.2) & semiplano(1, 1, gx + gy - 2), "B3", alfa=0.8)
    for i in range(6):
        a = math.radians(i * 60 - 60)
        t.rebite(gx - 0.85 + math.cos(a) * 13.3, gy - 0.85 + math.sin(a) * 13.3)
    t.pintar(E(gx, gy, 7.2), esfera(gx, gy, 7.2, 1.0, 0.05), chanfro=0.15)
    _of_soquete(t, gx, gy, 5.0)
    t.cristal(gx, gy, 3.2, 5.0, aura=False)
    # faíscas arcanas onde os dentes se encontram
    for (cx, cy) in ((28.5, 108.0), (83.5, 108.0), (43.0, 125.5), (69.0, 125.5)):
        dx, dy = cx - gx, cy - gy
        dd = math.hypot(dx, dy)
        px, py = gx + dx / dd * (gr + 2.6), gy + dy / dd * (gr + 2.6)
        t.luz_radial(px, py, 2.2, "C1", 0.5, glow=0.8, so_fundo=True)
        t.fill(E(px, py, 0.6), "C2", glow=1.0)
        t.fill(E(px - 0.15, py - 0.15, 0.28), "WH", glow=1.0)
    # coroa sobre a mestra
    pontas = [(43.0, 64.0), (50.5, 59.5), (56.0, 54.5), (61.5, 59.5), (69.0, 64.0)]
    coroa_pts = [(43.0, 78.0), (43.0, 64.0), (47.6, 71.0), (50.5, 59.5), (53.6, 69.0), (56.0, 54.5), (58.4, 69.0),
                 (61.5, 59.5), (64.4, 71.0), (69.0, 64.0), (69.0, 78.0)]
    t.pintar(P(coroa_pts), linear(43, 54, 69, 78, 1.0, 0.1), chanfro=0.22)
    for (x, y, s) in ((50.5, 67.0, 1.6), (56.0, 64.5, 2.0), (61.5, 67.0, 1.6)):   # losangos em relevo
        lo = P([(x, y - s * 1.5), (x + s, y), (x, y + s * 1.5), (x - s, y)])
        t.pintar(lo, lambda X, Y, x=x, y=y: 0.95 - 0.35 * np.clip(((X - x) + (Y - y)) / 3 + 0.5, 0, 1),
                 esp=0.45, chanfro=0.15)
    for x0, x1 in ((45.5, 49.6), (62.4, 66.5)):
        t.fill(L([(x0, 76), (x1, 73.6)], 0.4), "B1", alfa=0.7)
    for (x, y) in pontas:
        r = 1.9 if x == 56.0 else 1.55
        t.pintar(E(x, y - 0.6, r), esfera(x, y - 0.6, r), esp=0.5)
    faixa = (R(41.5, 75.0, 70.5, 79.5) | E(56, 79.5, 14.5, 2.0)) - E(56, 75.0, 14.5, 1.6)
    t.pintar(faixa, cil_y(73.5, 81.5, 1.0, 0.04), chanfro=0.15)
    for x in (45.0, 66.0):
        t.fill(E(x + 0.5, 78.6, 1.05), t.arc[0])
        t.fill(E(x + 0.5, 78.6, 0.7), "C2", glow=1.0)
        t.fill(E(x + 0.25, 78.35, 0.28), "WH", glow=1.0)
    t.cristal(56, 77.6, 2.2, 3.6, esp=0.5)
    t.estrela(56, 46, 2)


# ---------- V DE OUROS · BRAÇADEIRA DE LATÃO ----------

_OF_BR = (56.0, 104.0, math.radians(22))       # centro e inclinação da braçadeira


def _of_br_g(pts):
    ox, oy, ang = _OF_BR
    return girar(pts, ox, oy, ang)


def _of_br_hw(ly):
    """Meia largura da braçadeira (mais fina no pulso, em cima)."""
    return 13.0 + 4.5 * (ly + 40.0) / 78.0


def _of_br_faixa(ly0, ly1, folga=0.8, ry=3.6, n=20):
    """Faixa em volta do cilindro entre ly0 e ly1 (as bordas curvam como a boca, vista de cima)."""
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
        return perfil((lx + hw) / (2 * hw))
    # corpo com a boca de cima aberta (o cilindro oco onde vai o antebraço)
    corpo = P(g([(-13.0, -40.0), (13.0, -40.0), (17.5, 38.0), (-17.5, 38.0)])) | _of_elipse(*g([(0, 38.0)])[0], 17.5, 5.0, ang)
    t.pintar(corpo, local(luz_corpo, ox, oy, ang))
    for f in (-0.55, 0.55):                                # caneluras
        a, b = g([(f * _of_br_hw(-18), -18.0), (f * _of_br_hw(20), 20.0)])
        t.fill(L([a, b], 0.55), "B1", alfa=0.8)
        a2, b2 = g([(f * _of_br_hw(-18) + 0.6, -18.0), (f * _of_br_hw(20) + 0.6, 20.0)])
        t.clarear(L([a2, b2], 0.3), "B4", 0.3)
    bx_, by_ = g([(0, -40.0)])[0]
    boca = _of_elipse(bx_, by_, 13.0, 4.4, ang)
    t.fill(boca, "K")
    parede = boca - _of_elipse(*g([(0, -37.6)])[0], 13.0, 4.4, ang)
    t.fill(parede, lambda X, Y: rampa(OURO, 0.22 + 0.06 * np.clip((X - bx_) / 10, -1, 1)))
    # tiras com fivela e a borda de baixo
    for ly0, ly1 in ((-31.0, -25.5), (23.5, 29.0)):
        t.pintar(_of_br_faixa(ly0, ly1), local(lambda lx, ly, ly0=ly0, ly1=ly1:
                                                perfil(np.clip((ly - ly0 - 1.0) / (ly1 - ly0 + 1.5), 0, 1)) * 0.7 + 0.02,
                                                ox, oy, ang), chanfro=0.12)
        hw = _of_br_hw((ly0 + ly1) / 2) + 1.0
        lm = (ly0 + ly1) / 2 + 0.6
        for k in range(-4, 5):
            p = g([(k * hw / 5.2, lm + 3.6 * math.sqrt(max(0.0, 1 - (k / 5.2) ** 2)) - 0.6)])[0]
            t.fill(E(p[0], p[1], 0.42), "B0")
        # fivela no lado direito
        fiv = P(g([(hw - 2.6, lm - 4.4), (hw + 3.8, lm - 4.4), (hw + 3.8, lm + 4.4), (hw - 2.6, lm + 4.4)]))
        furo = P(g([(hw - 1.0, lm - 2.8), (hw + 2.2, lm - 2.8), (hw + 2.2, lm + 2.8), (hw - 1.0, lm + 2.8)]))
        t.pintar(fiv - furo, linear(ox - 20, oy - 40, ox + 30, oy + 40), chanfro=0.15)
        t.pintar(L(g([(hw + 0.6, lm - 3.2), (hw + 0.6, lm + 3.2)]), 0.9), plano(0.85), contorno=False)
        t.pintar(L(g([(hw + 3.6, lm), (hw + 7.2, lm)]), 3.0), linear(ox, oy - 30, ox + 30, oy + 30, 0.8), chanfro=0.1)
    t.pintar(_of_br_faixa(34.0, 38.5, 0.9, 5.0), local(lambda lx, ly: perfil(np.clip((ly - 34) / 9.0, 0, 1)) * 0.85 + 0.05,
                                                       ox, oy, ang), chanfro=0.12)
    # borda de cima enrolada
    t.pintar(_of_elipse(bx_, by_, 14.4, 5.6, ang) - _of_elipse(bx_, by_, 12.6, 3.9, ang),
             lambda X, Y: 0.55 + 0.35 * np.clip(-((X - bx_) + (Y - by_)) / 14, -1, 1), chanfro=0.2)
    # dobradiça no lado esquerdo
    for k in range(5):
        ly = -21.0 + k * 8.4
        a, b = g([(-_of_br_hw(ly) - 0.8, ly), (-_of_br_hw(ly + 6.0) - 0.8, ly + 6.0)])
        t.pintar(L([a, b], 2.8), local(cil_x(-1.4, 1.4), (a[0] + b[0]) / 2, (a[1] + b[1]) / 2, ang), chanfro=0.1)
    # tubos finos
    tubos = [[(10.5, -9.5), (15.0, -9.5)], [(10.5, -1.5), (15.0, -1.5)],
             [(3.6, 13.0), (9.0, 13.0), (13.0, 10.0), (17.0, 7.0)],
             [(-5.2, 13.0), (-9.0, 15.0), (-11.6, 20.0)],
             [(0.0, -17.5), (0.0, -22.0)]]
    for tb in tubos:
        pts = g(tb)
        t.pintar(L(spline(pts, 6) if len(pts) > 2 else pts, 1.5), linear(ox - 20, oy - 30, ox + 20, oy + 30, 0.95, 0.08),
                 chanfro=0.12, esp=0.45)
    for p in ((15.0, -9.5), (15.0, -1.5), (-11.6, 20.0)):
        q = g([p])[0]
        t.pintar(E(q[0], q[1], 1.2), esfera(q[0], q[1], 1.2), esp=0.4)
    # cápsula de vidro com fluido arcano
    cap = L(g([(17.6, -13.0), (17.6, 4.0)]), 5.6)
    t.contorno(cap, "B2", "B1", 0.6)
    t.fill(cap, lambda X, Y: rampa(("K", "G1", "C0"), np.full_like(X, 0.4)))
    fl = cap.cresce(-0.7) & P(g([(10, -6.0), (25, -6.0), (25, 10), (10, 10)]))
    t.pintar(fl, local(lambda lx, ly: 0.38 + 0.45 * np.exp(-((lx - 17.0) / 1.5) ** 2) - 0.12 * np.clip((ly + 6) / 12, 0, 1),
                       ox, oy, ang), CIANO, contorno=False)
    t.fill(L(g([(15.6, -6.0), (19.6, -6.0)]), 0.45), "C2", glow=1.0)
    for (lx, ly, br) in ((17.2, 1.5, 0.7), (18.6, -2.5, 0.5), (16.8, -4.5, 0.4)):
        q = g([(lx, ly)])[0]
        t.fill(anel(q[0], q[1], br * 0.5, br), "WH", glow=1.0)
    t.clarear(L(g([(15.6, -12.0), (15.6, 3.0)]), 0.8), "B4", 0.35)
    t.halo_arcano(cap, 1.6, 80)
    for ly in (-13.6, 5.0):
        t.pintar(L(g([(14.4, ly), (20.8, ly)]), 3.2), linear(ox, oy - 30, ox + 30, oy + 30), chanfro=0.12, esp=0.5)
    # volante no tubo de cima
    vx, vy = g([(0.0, -19.6)])[0]
    vol = anel(vx, vy, 1.6, 2.5)
    for i in range(4):
        a = math.radians(45 + i * 90)
        vol = vol | L([(vx, vy), (vx + math.cos(a) * 2.1, vy + math.sin(a) * 2.1)], 0.55)
    t.pintar(vol, esfera(vx, vy, 2.6), chanfro=0.1, esp=0.4)
    # manômetro
    mx, my = g([(0.0, -6.0)])[0]
    t.pintar(E(mx, my, 11.0), esfera(mx, my, 11.0, 1.0, 0.04), chanfro=0.15)
    t.fill(anel(mx, my, 8.6, 9.4), "B0")
    t.fill(anel(mx, my, 9.5, 10.0) & semiplano(1, 1, mx + my - 1), "B4", alfa=0.4)
    t.fill(E(mx, my, 8.6), lambda X, Y: rampa(("K", "G2", "G3"), 0.75 - 0.08 * np.hypot(X - mx + 2, Y - my + 2)))
    za = math.degrees(ang)
    zona = arco(mx, my, 7.0, -50 + za, 30 + za, 1.4)
    t.halo_arcano(zona, 1.0, 60)
    t.fill(zona, lambda X, Y: rampa(CIANO, 0.62 + 0.2 * np.clip((Y - my) / 8, -1, 1)), glow=1.0)
    for k in range(11):
        a = math.radians(135 + k * 27 + za)
        r0 = 5.5 if k % 5 == 0 else 6.3
        t.fill(L([(mx + math.cos(a) * r0, my + math.sin(a) * r0), (mx + math.cos(a) * 7.9, my + math.sin(a) * 7.9)],
                 0.5 if k % 5 == 0 else 0.35), "B3")
    a = math.radians(-20 + za)
    t.fill(L([(mx - math.cos(a) * 2.0, my - math.sin(a) * 2.0), (mx + math.cos(a) * 7.0, my + math.sin(a) * 7.0)], 0.7),
           "B4")
    t.pintar(E(mx, my, 1.3), esfera(mx, my, 1.3), esp=0.4)
    t.clarear(arco(mx, my, 7.2, 200, 250, 0.9), "B4", 0.22)
    # mostrador pequeno (relógio)
    sx_, sy_ = g([(-1.0, 13.0)])[0]
    t.pintar(E(sx_, sy_, 5.4), esfera(sx_, sy_, 5.4, 1.0, 0.05), chanfro=0.12)
    t.fill(E(sx_, sy_, 4.0), lambda X, Y: rampa(("K", "G2"), 0.7 - 0.12 * np.hypot(X - sx_, Y - sy_)))
    for k in range(12):
        a = k * math.tau / 12 + ang
        t.fill(E(sx_ + math.cos(a) * 3.2, sy_ + math.sin(a) * 3.2, 0.3 if k % 3 else 0.42), "B3")
    t.fill(L([(sx_, sy_), (sx_ + 2.0, sy_ - 1.0)], 0.5), "B4")
    t.fill(L([(sx_, sy_), (sx_ + 0.8, sy_ - 2.8)], 0.4), "B3")
    t.fill(E(sx_, sy_, 0.55), "B4")
    for p in ((-8.5, -16.0), (8.5, -17.0), (-10.0, 4.0), (9.5, 19.0)):
        q = g([p])[0]
        t.rebite(q[0] - 0.85, q[1] - 0.85)


# ---------- XI · A FORÇA ----------

def il_forca(t):
    # lemniscata (como na Força do tarô)
    lem = []
    for i in range(241):
        a = i * math.tau / 240
        k = 1 + math.sin(a) ** 2
        lem.append((CX + 11 * math.cos(a) / k, 47 + 11 * math.sin(a) * math.cos(a) / k))
    t.pintar(L(lem, 2.3), linear(44, 42, 68, 53, 1.0, 0.12), chanfro=0.2)
    t.fill(L(lem, 0.3), "B4", alfa=0.35)
    # vapor saindo dos pistões
    for s in (-1, 1):
        t.massa_vapor([(CX + s * 30.0, 121.0, 3.4), (CX + s * 33.5, 126.5, 4.2), (CX + s * 30.5, 133.0, 4.6),
                       (CX + s * 25.5, 138.5, 3.6), (CX + s * 34.5, 135.0, 3.2)])
    # barra de ferro dobrada pela mão
    barra_pts = spline([(14.5, 100.0), (23.0, 87.5), (37.0, 78.5), (56.0, 74.5), (75.0, 78.5), (89.0, 87.5),
                        (97.5, 100.0)], 10)
    barra = L(barra_pts, 7.2)
    t.pintar(barra, lambda X, Y: 0.36 + 0.06 * np.clip((60 - Y) / 20, 0, 1), chanfro=0.32)
    t.clarear(L([(x, y - 1.9) for (x, y) in barra_pts], 1.2) & barra.cresce(-0.4), "B4", 0.55)
    t.clarear(L([(x, y + 2.2) for (x, y) in barra_pts], 1.3) & barra.cresce(-0.3), "B0", 0.45)
    for (ex, ey) in ((14.5, 100.0), (97.5, 100.0)):
        t.fill(E(ex, ey, 3.0), "B1")
        t.fill(E(ex, ey, 1.8), "B0", alfa=0.8)
    for (sx_, sy_, a) in ((26.0, 85.0, -52), (86.0, 85.0, 52)):          # marcas de tensão na dobra
        for k in (-1, 0, 1):
            p = girar([(k * 1.7, -2.7), (k * 1.7, 2.7)], sx_, sy_, math.radians(a))
            t.fill(L(p, 0.42) & barra, "B0", alfa=0.75)
    t.estrela(24, 78, 2)
    t.estrela(87, 78, 2)
    t.estrela(18, 89, 1)
    t.estrela(93, 89, 1)
    # pistões (atrás do braço)
    for x0, xj in ((35.0, 44.5), (77.0, 67.5)):
        t.pintar(L([(x0, 101.0), (xj, 101.5)], 2.2), linear(x0 - 2, 99, xj + 2, 104), chanfro=0.1)
        t.pintar(R(x0 - 1.4, 100, x0 + 1.4, 114), cil_x(x0 - 1.4, x0 + 1.4, 1.05))
        t.pintar(R(x0 - 3.6, 112, x0 + 3.6, 140, 0.4), cil_x(x0 - 3.6, x0 + 3.6))
        for ya in (111.5, 137.0):
            t.pintar(R(x0 - 4.6, ya, x0 + 4.6, ya + 3.2, 0.5), cil_x(x0 - 4.6, x0 + 4.6), chanfro=0.12)
        t.sulco_h(x0 - 3.2, x0 + 3.2, 125.0, "B1", 0.25)
        t.pintar(E(x0, 143.0, 2.2), esfera(x0, 143.0, 2.2))
        t.pintar(L([(x0, 143.0), (x0 + (6.5 if x0 < CX else -6.5), 143.5)], 2.0), linear(x0 - 4, 141, x0 + 4, 146))
        t.pintar(E(x0, 101.0, 2.5), esfera(x0, 101.0, 2.5))
    # antebraço com runas
    t.pintar(_of_trap(44, 68, 100, 41, 71, 150), _of_trap_luz(44, 68, 100, 41, 71, 150))
    painel = R(50.5, 108.0, 61.5, 140.0, 1.4)
    t.fill(painel, lambda X, Y: rampa(("K", t.arc[0]), 0.35 + 0.15 * np.exp(-((X - 56) / 3) ** 2)))
    t.contorno(painel, "B3", "B0", 0.6)
    for i, y in enumerate((111.0, 121.0, 131.0)):
        t.runa(54.5, y, (0, 1, 3)[i])
    for (ya, x0, x1) in ((101.5, 42.7, 69.3), (140.5, 40.9, 71.1), (146.5, 40.3, 71.7)):
        t.pintar(R(x0, ya, x1, ya + 3.6, 0.5), cil_x(x0, x1), chanfro=0.12)
    for x in (45.5, 66.5):
        t.rebite(x - 0.85, 120.0)
        t.rebite(x - 0.85, 130.0)
    # pulso e palma
    t.pintar(R(44.5, 94.5, 67.5, 102.0, 0.6), cil_x(44.5, 67.5, 0.95), chanfro=0.12)
    t.pintar(R(39.0, 76.0, 73.5, 98.0, 4.5), linear(39, 76, 73.5, 98, 0.95, 0.04), chanfro=0.18)
    t.rebite(43.0, 92.0)
    # dedos fechados sobre a barra
    for x0, x1, y0 in ((40.5, 48.4, 69.5), (48.9, 56.8, 66.5), (57.3, 65.2, 67.5), (65.7, 72.8, 71.0)):
        _of_dedo(t, x0, x1, y0, 85.0, (0.5,), (x1 - x0) / 2 - 0.2)
    # polegar por cima (vem da direita)
    t.pintar(E(74.5, 90.0, 6.2, 8.4), esfera(73.5, 88.0, 8.0, 1.0, 0.04), chanfro=0.15)
    t.pintar(L([(73.0, 86.0), (51.5, 84.6)], 7.2), lambda X, Y: perfil((Y - 81.2) / 8.4) * 0.95 + 0.06, chanfro=0.15)
    t.sulco_v(62.0, 81.6, 88.6, "B0", 0.3)
    t.pintar(R(48.4, 82.0, 56.0, 87.4, 2.2), lambda X, Y: 0.86 - 0.3 * np.clip((Y - 82) / 5.4, 0, 1),
             chanfro=0.15, esp=0.5)
    t.rebite(70.0, 92.5)


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
        t.pintar(m, plano(0.5 * k), chanfro=0.12, esp=0.6)
        t.pintar(P([(bx - w, by + 2.0), tip, (bx, by + 2.0)]) & m.cresce(-0.15),
                 lambda X, Y: (0.74 - 0.22 * np.clip((Y - tip[1]) / h, 0, 1)) * k, contorno=False)
        t.pintar(P([(bx, by + 2.0), tip, (bx + w, by + 2.0)]) & m.cresce(-0.15),
                 lambda X, Y: (0.36 - 0.1 * np.clip((Y - tip[1]) / h, 0, 1)) * k, contorno=False)
        if ponta:                                         # ponta em brasa violeta
            pt = m & semiplano(0, 1, tip[1] + h * 0.2)
            t.halo_arcano(pt, 1.5, 80)
            t.fill(pt, lambda X, Y: rampa(VIOLETA, 0.85 - 0.5 * np.clip((Y - tip[1]) / (h * 0.2), 0, 1)), glow=1.0)
            t.fill(E(bx - 0.2, tip[1] + 1.2, 0.35), "WH", glow=1.0)
        return m

    # espinhos de trás
    for x, h in ((37.0, 15.0), (47.0, 21.0), (65.0, 21.0), (75.0, 15.0)):
        espinho(x, tras(x, yt) + 1.0, 3.2, h, 0.6, False)
    # interior e parede de trás vista por dentro
    boca = E(cx, yt, rx, ry)
    t.fill(boca, lambda X, Y: rampa(("K", "V0"), 0.55 - 0.08 * np.abs(X - cx) / 4))
    t.luz_radial(cx, yt + 1, 9.0, "V1", 0.3, dentro=boca, glow=0.4)
    parede = boca - E(cx, yt + 4.2, rx - 0.8, ry)
    t.fill(parede, lambda X, Y: rampa(OURO, 0.22 + 0.12 * np.clip((Y - (yt - ry)) / 4, 0, 1)))
    for x in range(32, 81, 4):                             # rebites por dentro
        yy = tras(x, yt) + 2.0
        if yy < yt - 2:
            t.fill(E(x + 0.5, yy, 0.45), "B2", alfa=0.7)
    t.pintar(boca.anel(0.9), plano(0.75), contorno=False, chanfro=0.2)
    # faixa da frente
    banda = (E(cx, yb, rx, ry) | R(cx - rx, yt, cx + rx, yb)) - E(cx, yt, rx, ry)
    t.pintar(banda, cil_x(cx - rx, cx + rx, 1.0, 0.02), chanfro=0.15)
    for x in (37.5, 46.5, 65.5, 74.5):                    # emendas das placas
        y0, y1 = frente(x, yt) + 3.8, frente(x, yb) - 3.8
        t.fill(L([(x, y0), (x, y1)], 0.55), "B0", alfa=0.85)
        t.clarear(L([(x + 0.6, y0), (x + 0.6, y1)], 0.3), "B4", 0.3)
    for x in range(30, 83, 5):                             # duas fileiras de rebites grandes
        for y0, dy in ((yt, 2.4), (yb, -2.6)):
            yy = frente(x + 0.85, y0) + dy
            t.rebite(x, yy - 0.85, 1.0)
    for gx_ in (41.5, 70.5):
        gy_ = (frente(gx_, yt) + frente(gx_, yb)) / 2
        _of_engr(t, gx_, gy_, 2.9, 8, 1.6, 0.0, furo=0.4, k=0.9)
    # espinhos de ferro da frente
    for x, h, w in ((31.0, 21.0, 3.4), (42.5, 30.0, 4.0), (56.0, 42.0, 4.8), (69.5, 30.0, 4.0), (81.0, 21.0, 3.4)):
        espinho(x, frente(x, yt) - 0.6, w, h, 0.85, True)
    # rachaduras violeta saindo do cristal
    veias = [[(49.5, 115.0), (46.0, 113.4), (44.6, 116.6), (39.0, 114.8), (35.5, 118.0), (31.0, 116.2)],
             [(62.5, 115.5), (65.8, 118.6), (68.6, 115.2), (73.8, 117.6), (77.2, 114.4)],
             [(52.0, 124.5), (49.0, 127.6), (45.5, 126.4)],
             [(60.0, 124.5), (63.4, 127.2), (66.2, 125.6)],
             [(46.0, 113.4), (45.4, 110.2)], [(68.6, 115.2), (69.8, 111.8)]]
    for v in veias:
        _of_veia(t, v, banda, 1.0)
    # engaste e cristal violeta no meio
    t.pintar(E(cx, 116.0, 7.4, 10.4), esfera(cx, 116.0, 10.4, 1.0, 0.05), chanfro=0.18)
    for a in range(0, 360, 45):
        ra = math.radians(a + 22.5)
        t.rebite(cx - 0.85 + math.cos(ra) * 6.2, 116.0 - 0.85 + math.sin(ra) * 8.9, 0.6)
    _of_soquete(t, cx, 116.0, 8.0, 5.1)
    t.luz_radial(cx, 116.0, 5.0, "V1", 0.5, dentro=E(cx, 116.0, 5.1, 8.0), glow=0.6)
    t.cristal(cx, 116.0, 4.0, 7.4, aura=False)
    t.halo_arcano(E(cx, 116.0, 7.4, 10.4), 1.5, 50)
    for (x, y, tam) in ((24, 84, 1), (88, 86, 1), (56, 58, 2)):
        t.estrela(x, y, tam, ("WH", "V2", "V1", "V0"))


# ---------- VII DE PAUS (amaldiçoada) · PACTO DE CRISTAL ----------

def _of_elo(t, cx, cy, ang, deitado, k=0.9):
    """Elo de corrente: de frente (anel) ou de lado (barra), ao longo da direção ang."""
    if deitado:
        m = _of_elipse(cx, cy, 2.7, 1.65, ang, 24) - _of_elipse(cx, cy, 1.55, 0.62, ang, 20)
    else:
        m = L(girar([(-2.6, 0.0), (2.6, 0.0)], cx, cy, ang), 1.45)
    t.pintar(m, linear(cx - 3, cy - 3, cx + 3, cy + 3, k, 0.08), chanfro=0.15, esp=0.5)
    return m


def _of_corrente(t, p0, p1, passo=3.7):
    ang = math.atan2(p1[1] - p0[1], p1[0] - p0[0])
    n = int(math.hypot(p1[0] - p0[0], p1[1] - p0[1]) / passo)
    for i in range(n + 1):
        f = i / n
        _of_elo(t, p0[0] + (p1[0] - p0[0]) * f, p0[1] + (p1[1] - p0[1]) * f, ang, i % 2 == 0)


def il_pacto_cristal(t):
    # punho da mão (embaixo)
    t.pintar(_of_trap(45, 67, 136, 40, 72, 158), _of_trap_luz(45, 67, 136, 40, 72, 158))
    for (ya, x0, x1) in ((135.0, 43.5, 68.5), (152.5, 38.5, 73.5)):
        t.pintar(R(x0, ya, x1, ya + 3.6, 0.6), cil_x(x0, x1), chanfro=0.12)
    for x in (45, 51, 60, 66):
        t.rebite(x, 145.5)
    # polegar aberto
    pol = spline([(41.0, 128.0), (33.0, 120.0), (29.5, 111.0), (29.5, 103.0)], 8)
    t.pintar(L(pol, 7.0), linear(24, 98, 42, 130, 1.0, 0.05), chanfro=0.15)
    t.sulco_h(26.4, 33.2, 111.5, "B0", 0.3)
    t.rebite(29.4, 113.6)
    # palma
    palma = R(38.0, 100.0, 74.0, 138.0, 5.0)
    t.pintar(palma, linear(38, 100, 74, 138, 0.95, 0.05), chanfro=0.2)
    for x, y in ((41, 133), (69, 133), (69, 104)):
        t.rebite(x, y)
    t.fill(arco(56, 140, 14.0, 215, 325, 0.55) & palma.cresce(-1.5), "B1", alfa=0.8)
    # dedos erguidos (o juramento)
    for x0, x1, y0 in ((38.4, 46.4, 69.0), (47.3, 55.3, 63.0), (56.2, 64.2, 61.0), (65.1, 72.5, 67.0)):
        _of_dedo(t, x0, x1, y0, 104.0, (0.34, 0.67), 3.4)
    # rachaduras violeta: o pacto bebe de quem o usa
    mao = palma | R(38.4, 61, 72.5, 104)
    veias = [[(52.0, 113.0), (49.6, 117.6), (51.0, 121.4), (47.2, 126.0), (48.4, 130.2), (45.6, 134.0)],
             [(60.0, 113.0), (62.8, 117.2), (61.2, 121.6), (65.0, 125.4), (63.8, 129.6)],
             [(51.0, 121.4), (54.6, 125.0)], [(65.0, 125.4), (68.6, 124.0)],
             [(52.4, 82.0), (50.6, 77.4), (52.0, 73.2), (50.8, 69.0)],
             [(59.6, 82.0), (61.2, 76.6), (59.8, 71.4), (61.0, 66.0)]]
    for v in veias:
        _of_veia(t, v, mao, 1.0)
    t.luz_radial(56, 96, 14.0, "V1", 0.28, dentro=mao, glow=0.3)
    # cristal preso
    t.cristal(56, 96, 9.0, 18.0)
    # correntes em X, com argolas nas pontas
    for p0, p1 in (((28.0, 76.0), (84.0, 128.0)), ((84.0, 76.0), (28.0, 128.0))):
        _of_corrente(t, p0, p1)
        for (ax, ay) in (p0, p1):
            t.pintar(anel(ax, ay, 1.6, 3.0), esfera(ax, ay, 3.0, 1.0, 0.05), chanfro=0.12, esp=0.5)
    # lacre do pacto no cruzamento
    t.pintar(E(56, 102, 5.4), esfera(56, 102, 5.4, 1.0, 0.05), chanfro=0.2)
    t.fill(anel(56, 102, 3.9, 4.4), "B0")
    t.fill(E(56, 102, 3.9), lambda X, Y: rampa(("K", "V0"), np.full_like(X, 0.5)))
    t.runa(54.5, 99.5, 1)
