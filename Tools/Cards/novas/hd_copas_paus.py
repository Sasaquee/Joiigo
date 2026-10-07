"""
Fase 10 (D-088), grupo copas/paus: ilustrações em ALTA RESOLUÇÃO de oito cartas novas.

Executado dentro de card_hd.py (mesmo espaço de nomes: R, E, P, L, arco, engrenagem, cil_x, cil_y, esfera,
linear, local, plano, rampa, t.pintar, t.cristal etc.). As formas usam as mesmas coordenadas da versão em
pixel (novas/px_copas_paus.py), que só serve de mapa de ocupação para as estrelas.

Ajudantes deste arquivo têm o prefixo _cp_ para não colidir com os de outros grupos.
"""


# ---------- ajudantes ----------

def _cp_vidro(t, m, hx, hy, esp=0.7):
    """Vidro como no Tônico: contorno dourado, miolo escuro com um leve brilho e aro chanfrado."""
    t.contorno(m, "B2", "B1", esp)
    t.fill(m, lambda X, Y: rampa(("K", "G1", "G2"), 0.3 + 0.25 * np.exp(-((X - hx) ** 2 + (Y - hy) ** 2) / 120)))
    t.pintar(m.anel(0.9), plano(0.62), contorno=False, chanfro=0.3)


def _cp_gota_forma(cx, cy, r):
    return E(cx, cy, r).suave(P([(cx - r * 0.78, cy - r * 0.55), (cx, cy - r * 2.6), (cx + r * 0.78, cy - r * 0.55)]), 0.35)


def _cp_gota(t, cx, cy, r, halo=True):
    """Gota de água arcana (ciano), ponta para cima."""
    g = _cp_gota_forma(cx, cy, r)
    t.pintar(g, esfera(cx, cy - r * 0.2, r * 1.15, 1.0, 0.05), CIANO, claro="C0", escuro="C0", esp=0.45)
    t.fill(E(cx - r * 0.35, cy - r * 0.25, max(0.3, r * 0.28)), "WH", glow=1.0)
    if halo:
        t.halo_arcano(g, 1.5, 90)
    return g


def _cp_cano(t, p0, p1, larg, chanfro=0.0, esp=ESP):
    """Cano reto com luz de cilindro ao longo do eixo."""
    ang = math.atan2(p1[1] - p0[1], p1[0] - p0[0])
    m = L([p0, p1], larg)
    meio = ((p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2)
    t.pintar(m, local(cil_x(-larg / 2, larg / 2), meio[0], meio[1], ang + math.pi / 2), chanfro=chanfro, esp=esp)
    return m


def _cp_anel_cano(t, p0, p1, f, larg, grossura=1.3):
    """Anel (braçadeira) em volta de um cano reto, na fração f do comprimento."""
    ang = math.atan2(p1[1] - p0[1], p1[0] - p0[0])
    nx, ny = math.cos(ang + math.pi / 2), math.sin(ang + math.pi / 2)
    px, py = p0[0] + (p1[0] - p0[0]) * f, p0[1] + (p1[1] - p0[1]) * f
    h = larg / 2
    m = L([(px - nx * h, py - ny * h), (px + nx * h, py + ny * h)], grossura)
    t.pintar(m, local(cil_x(-h, h), px, py, ang + math.pi / 2), chanfro=0.1, esp=0.4)


def _cp_eletrico(t, pts, seed, larg=1.0):
    """Arco elétrico que também aparece por cima de peças (o raio() do card_hd só pinta a borda no fundo)."""
    rnd = random.Random(seed)
    fino = [pts[0]]
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        dx, dy = bx - ax, by - ay
        ll = math.hypot(dx, dy) or 1
        nx, ny = -dy / ll, dx / ll
        for f in (0.35, 0.7):
            j = rnd.uniform(-0.5, 0.5)
            fino.append((ax + dx * f + nx * j, ay + dy * f + ny * j))
        fino.append((bx, by))
    t.fill(L(fino, 1.9 * larg), t.arc[1], glow=1.0, alfa=0.55)
    t.fill(L(fino, 1.05 * larg), t.arc[2], glow=1.0)
    t.fill(L(fino, 0.42 * larg), t.arc[3], glow=1.0)


def _cp_manometro(t, cx, cy, r, ponteiro):
    """Manômetro: aro de latão, mostrador escuro, marcas (as últimas em ciano) e ponteiro."""
    t.pintar(E(cx, cy, r), esfera(cx, cy, r, 1.05, 0.1), claro="B0", escuro="B0")
    ri = r * 0.74
    t.fill(E(cx, cy, ri), lambda X, Y: rampa(("K", "G2"), 0.6 - 0.07 * np.hypot(X - cx, Y - cy)))
    t.fill(anel(cx, cy, ri - 0.4, ri), "B0")
    for i in range(7):
        a = math.radians(150 + i * 40)
        p0 = (cx + math.cos(a) * ri * 0.66, cy + math.sin(a) * ri * 0.66)
        p1 = (cx + math.cos(a) * ri * 0.88, cy + math.sin(a) * ri * 0.88)
        if i >= 5:
            t.fill(L([p0, p1], 0.45), "C2", glow=1.0)
        else:
            t.fill(L([p0, p1], 0.45), "B3" if i % 2 == 0 else "B2")
    a = math.radians(ponteiro)
    t.fill(L([(cx, cy), (cx + math.cos(a) * ri * 0.8, cy + math.sin(a) * ri * 0.8)], 0.5), "B4")
    t.fill(E(cx, cy, max(0.5, r * 0.12)), "B3")


# ---------- VI DE COPAS · VAPOR CONDENSADO ----------

def il_vapor_condensado(t):
    ex = 46.0                                       # eixo do alambique
    # baforadas escapando pela válvula do capitel
    t.vapor([(60.0, 51.0, 2.6), (55.0, 57.0, 3.5), (50.0, 63.5, 2.4)])
    # suporte com queimador de cristal
    t.pintar(R(26, 144, 66, 148, 0.6), linear(26, 144, 66, 148), chanfro=0.18)
    for a, b in (((33, 133), (30, 144)), ((59, 133), (62, 144))):
        t.pintar(L([a, b], 2.4), linear(26, 128, 66, 148), chanfro=0.1)
    t.luz_radial(ex, 139.5, 5.0, "C1", 0.45, glow=0.6, so_fundo=True)
    t.cristal(ex, 139.5, 2.6, 4.2)
    for x in (29, 61):
        t.rebite(x, 145)
    # caldeirão
    pote = E(ex, 120.0, 16.0, 13.0)
    t.pintar(pote, esfera(ex, 118.0, 16.0))
    cinta = pote & R(0, 115.5, W, 119.5)
    fe = esfera(ex, 118.0, 16.0)
    t.pintar(cinta, lambda X, Y: fe(X, Y) * 0.8 + 0.02, contorno=False, chanfro=0.2)
    t.fill(pote & R(0, 119.5, W, 120.2), "B0", alfa=0.9)
    for i in range(-2, 3):
        t.rebite(int(ex + i * 5.5), 116)
    # aro do suporte
    t.pintar(R(28, 130, 64, 134, 0.5), linear(28, 130, 64, 134), chanfro=0.15)
    # gargalo e flanges
    t.pintar(R(41, 98, 51, 108, 0.3), cil_x(41, 51))
    t.pintar(R(37, 105, 55, 109, 0.5), cil_x(37, 55), chanfro=0.12)
    # capitel de vidro (onde o vapor condensa)
    domo = E(ex, 84.0, 14.0, 13.0) & semiplano(0, 1, 97.5)
    _cp_vidro(t, domo, ex - 4, 80)
    for (cx_, cy_, rr) in ((41.0, 88.0, 3.2), (48.0, 85.0, 3.8), (44.0, 80.5, 2.8)):   # vapor girando por dentro
        t.fill(arco(cx_, cy_, rr, 180, 340, 0.55), "B2", alfa=0.7)
    for a, rr in ((-55, 1.0), (-28, 1.15), (0, 1.25), (24, 1.1), (-80, 0.8), (205, 0.85)):   # gotas na parede
        aa = math.radians(a)
        _cp_gota(t, ex + math.cos(aa) * 11.2, 84.0 + math.sin(aa) * 10.2, rr, halo=False)
    t.clarear(arco(ex, 84.0, 11.0, 200, 255, 0.8, ry=10.0), "B4", 0.8)
    t.pintar(R(36, 96, 56, 100, 0.5), cil_x(36, 56), chanfro=0.12)
    # válvula no topo
    t.pintar(R(43, 68.5, 49, 72.5, 0.4), cil_x(43, 49), chanfro=0.1)
    t.pintar(E(ex, 67.5, 1.6), esfera(ex, 67.5, 1.6), esp=0.5)
    # bico do alambique: sai do capitel, desce em diagonal com a camisa de resfriamento
    a0, a1, a2 = (58.0, 81.0), (71.0, 86.0), (86.0, 101.0)
    _cp_cano(t, a0, a1, 4.0)
    t.pintar(E(a1[0], a1[1], 2.5), esfera(a1[0], a1[1], 2.5))
    _cp_cano(t, a1, a2, 4.0)
    _cp_cano(t, (74.0, 89.0), (82.0, 97.0), 7.0, chanfro=0.12)          # camisa de resfriamento
    for f in (0.0, 0.5, 1.0):
        _cp_anel_cano(t, (74.0, 89.0), (82.0, 97.0), f, 8.2)
    t.pintar(E(a0[0], a0[1], 2.6), esfera(a0[0], a0[1], 2.6), esp=0.5)
    t.pintar(R(84, 100, 88, 108, 0.4), cil_x(84, 88))
    t.pintar(R(83, 106, 89, 109, 0.4), cil_x(83, 89), chanfro=0.1)
    # gotas pingando
    _cp_gota(t, 86.0, 112.0, 1.1)
    _cp_gota(t, 86.0, 118.5, 1.4)
    # frasco que recebe a água arcana
    copo = R(79, 127, 93, 146, 1.8)
    _cp_vidro(t, copo, 82, 132)
    agua = copo.cresce(-1.0) & semiplano(0, -1, -136.0)
    t.pintar(agua, lambda X, Y: 0.95 - 0.55 * np.clip((Y - 136) / 9.0, 0, 1) - 0.12 * np.abs(X - 86) / 6,
             CIANO, contorno=False)
    t.fill(R(80, 135.6, 92, 136.5), "WH", glow=1.0, alfa=0.8)
    t.fill(anel(86.0, 136.0, 2.0, 2.6, 0.5, 0.9), "WH", glow=1.0, alfa=0.7)
    t.clarear(L([(81.6, 130), (81.6, 143)], 0.7), "B4", 0.7)
    t.pintar(R(78, 125, 94, 128, 0.6), cil_x(78, 94), chanfro=0.1)
    t.halo_arcano(copo, 2.0, 70)


# ---------- VIII DE COPAS · FRASCO DE FAÍSCA ----------

def il_frasco_faisca(t):
    cx, cy = CX, 107.0
    corpo = E(cx, cy, 18.0, 23.0)
    ombro = P([(41, 96), (50.5, 78), (61.5, 78), (71, 96)])
    vidro = corpo.suave(ombro, 3.0) | R(50.5, 67, 61.5, 80)
    # a luz da faísca vaza pelo vidro
    t.luz_radial(cx, cy, 24.0, "C0", 0.45, glow=0.25, so_fundo=True)
    _cp_vidro(t, vidro, 48, 98)
    dentro = vidro.cresce(-1.0)
    # a faísca presa
    t.luz_radial(cx, cy, 15.0, "C0", 0.85, dentro=dentro, glow=0.4)
    t.luz_radial(cx, cy, 9.0, "C1", 0.7, dentro=dentro, glow=0.7)
    for k, pts in enumerate((
            [(55, 106), (50, 101), (47, 95), (44.5, 90)],
            [(57, 106), (62, 101), (64, 95), (67.5, 90)],
            [(55, 108), (48, 111), (45, 117), (41.5, 120.5)],
            [(57, 108), (64, 112), (66, 118), (70.5, 120.5)],
            [(56, 109), (54.5, 116), (57, 122)])):
        _cp_eletrico(t, P5(pts), 31 + k, 1.0)
    for x, y in ((45.0, 90.5), (68.0, 90.5), (42.0, 121.0), (71.0, 121.0)):   # onde toca o vidro
        t.estrela_c(x, y, 1.9, ("WH", "C2", "C1", "C0"), glow=1.0, brilho=0.3)
    t.estrela_c(cx + 0.5, cy + 0.5, 9.5, ("WH", "WH", "C2", "C1"), diag=3.8, glow=1.0, brilho=0.5)
    t.fill(E(cx + 0.5, cy + 0.5, 1.8), "WH", glow=1.0)
    # reflexos do vidro
    t.clarear(arco(cx, cy, 14.8, 195, 245, 0.85, ry=19.5), "B4", 0.8)
    t.clarear(L([(52.6, 70), (52.6, 78)], 0.7), "B4", 0.7)
    # copo de latão no fundo
    copo = (corpo.cresce(0.25) & semiplano(0, -1, -122.5))
    t.pintar(copo, lambda X, Y: perfil((X - 38) / 36) * 0.95 + 0.04, chanfro=0.2)
    t.fill(corpo & R(0, 122.5, W, 123.3), "B0", alfa=0.9)
    for x in (43, 50, 61, 68):
        t.rebite(x, 125)
    # arame de cobre: gaiola sobre o vidro
    for a0, a1 in ((-76, 76), (104, 256)):
        fio = arco(cx, cy, 10.5, a0, a1, 1.0, 40, 23.5) & semiplano(0, 1, 123.0)
        t.pintar(fio, linear(38, 84, 74, 130, 0.85), esp=0.35)
    # colar do gargalo
    t.pintar(R(48.5, 78, 63.5, 82, 0.5), cil_x(48.5, 63.5), chanfro=0.12)
    # arame enrolado no gargalo
    for y in (69.0, 72.0, 75.0):
        t.pintar(L([(50.2, y + 1.6), (61.8, y)], 0.95), plano(0.55), esp=0.35)
    # rolha
    rolha = P([(49.5, 55), (62.5, 55), (61.2, 68), (50.8, 68)])
    t.pintar(rolha, lambda X, Y: perfil((X - 49.5) / 13.0) * 0.55 + 0.02, chanfro=0.15)
    for y in (58.5, 62.0):
        t.clarear(R(51, y, 61, y + 0.5), "B0", 0.45)
    t.pintar(E(cx, 54.6, 4.0, 1.5), plano(0.75), esp=0.4)
    # arame por cima da rolha, até o colar
    for p in ([(52.5, 55.0), (50.2, 69.5)], [(59.5, 55.0), (61.8, 69.5)]):
        t.pintar(L(p, 0.9), plano(0.6), esp=0.35)
    t.pintar(L(spline([(61.8, 72.0), (65.5, 72.5), (67.0, 75.5)], 6), 0.95), plano(0.6), esp=0.35)
    t.pintar(anel(67.2, 77.4, 1.0, 1.9), esfera(67.2, 77.4, 1.9), esp=0.35)


# ---------- X DE COPAS · CÁLICE CHEIO ----------

def il_calice_cheio(t):
    # raios de luz atrás dos cristais
    for i in range(18):
        a = i * math.tau / 18 - math.pi / 2
        r0, r1 = 15, (25 if i % 2 == 0 else 20)
        t.raio_luz((CX + math.cos(a) * r0, 50 + math.sin(a) * r0), (CX + math.cos(a) * r1, 50 + math.sin(a) * r1),
                   "C1", "C0", 0.42, 0.3, glow=0.8)
    # pé
    t.pintar(R(31, 134, 81, 139, 0.8), linear(31, 134, 81, 139), chanfro=0.18)
    t.pintar(P([(49, 121), (63, 121), (75, 134.5), (37, 134.5)]), cil_x(37, 75), chanfro=0.12)
    for x in (34, 77):
        t.rebite(x, 136)
    t.clarear(arco(CX, 136.0, 16, 200, 340, 0.5, ry=7.0) & R(37, 121, 75, 134), "B4", 0.3)
    # haste e nó com engrenagem
    t.pintar(R(52, 100, 60, 122, 0.3), cil_x(52, 60))
    for y0 in (99.5, 118.5):
        t.pintar(R(48.5, y0, 63.5, y0 + 3.5, 0.5), cil_x(48.5, 63.5), chanfro=0.12)
    t.pintar(engrenagem(CX, 110.5, 5.6, 10, 1.8, fase=0.5), linear(48, 103, 64, 119, 1.0, 0.08), chanfro=0.15)
    t.fill(E(CX, 110.5, 3.0), lambda X, Y: rampa(("K", "C0"), np.full_like(X, 0.6)))
    t.pintar(E(CX, 110.5, 2.2), esfera(CX, 110.5, 2.2, 1.0, 0.05), CIANO, contorno=False)
    # taça
    perfil_taca = spline([(29.5, 66), (31.5, 79), (37.5, 89.5), (46.5, 97), (56, 99.5), (65.5, 97), (74.5, 89.5),
                          (80.5, 79), (82.5, 66)], 8)
    taca = P(perfil_taca)
    t.pintar(taca, lambda X, Y: perfil((X - 29) / 54.0) * 0.95 + 0.06 - 0.16 * np.clip((Y - 66) / 34.0, 0, 1),
             chanfro=0.2)
    faixa = taca & R(0, 73.5, W, 77.5)                  # faixa gravada com rebites
    t.fill(taca & R(0, 73.5, W, 74.2), "B0", alfa=0.85)
    t.fill(taca & R(0, 77.0, W, 77.7), "B0", alfa=0.85)
    t.clarear(faixa & R(0, 74.2, W, 75.0), "B4", 0.25)
    for x in range(34, 80, 5):
        t.rebite(x, 74.8, 0.7)
    for i in range(-3, 4):                              # gomos na parte de baixo
        x = CX + i * 7.0
        t.clarear(arco(x, 79.0, 3.6, 20, 160, 0.5) & taca.cresce(-1.2), "B1", 0.6)
    # gemas na frente
    for gx, gy, rr in ((CX, 87.0, 3.0), (42.5, 84.0, 1.7), (69.5, 84.0, 1.7)):
        t.fill(E(gx, gy, rr + 0.8), "B0")
        t.pintar(E(gx, gy, rr + 0.7).anel(0.7), esfera(gx, gy, rr + 0.7), contorno=False)
        t.pintar(E(gx, gy, rr), esfera(gx, gy, rr, 1.0, 0.0), CIANO, contorno=False)
        t.fill(E(gx - rr * 0.35, gy - rr * 0.35, rr * 0.3), "WH", glow=1.0)
    # borda e cristal líquido transbordando
    aro = E(CX, 66.0, 27.0, 5.5) - E(CX, 66.0, 24.0, 3.6)
    t.pintar(aro, lambda X, Y: perfil((X - 29) / 54.0) * 0.95 + 0.08, chanfro=0.2)
    liquido = E(CX, 66.0, 24.0, 3.6) | (E(CX, 65.0, 23.0, 4.6) & semiplano(0, 1, 66.0))
    t.pintar(liquido, lambda X, Y: 0.95 - 0.35 * np.clip(np.abs(X - CX) / 24, 0, 1) - 0.25 * np.clip((Y - 61) / 8, 0, 1),
             CIANO, contorno=False)
    # escorrendo pela taça e caindo dos lados
    for pts, gotas in (
            ([(47.5, 69.5), (47.0, 74.0), (47.6, 79.5)], [(47.6, 82.5, 1.2)]),
            ([(62.0, 70.0), (62.5, 73.0), (62.0, 76.0)], [(62.0, 78.5, 1.0)]),
            ([(30.0, 66.5), (27.5, 71.0), (26.8, 80.0)], [(26.8, 82.5, 1.25), (26.8, 92.0, 1.05), (26.8, 100.0, 0.85)]),
            ([(82.0, 66.5), (84.5, 71.0), (85.2, 80.0)], [(85.2, 82.5, 1.25), (85.2, 91.0, 1.05)])):
        rio = L(spline(pts, 6), 1.8)
        t.halo_arcano(rio, 1.5, 90)
        t.fill(rio.cresce(0.4), "C0")
        t.pintar(rio, lambda X, Y: 0.62 - 0.1 * np.clip((Y - 66) / 14, 0, 1), CIANO, contorno=False)
        t.fill(mover(L(spline(pts, 6), 0.45), -0.3, 0), "C2", glow=1.0, alfa=0.9)
        for gx, gy, rr in gotas:
            _cp_gota(t, gx, gy, rr)
    # cristais que brotam do líquido
    t.cristal(45.5, 56.0, 3.4, 6.5)
    t.cristal(66.5, 56.0, 3.4, 6.5)
    t.cristal(CX, 48.5, 6.0, 11.5)
    t.fill(E(CX, 63.6, 14.0, 1.0), "WH", glow=1.0, alfa=0.35)
    for x, y, c in ((37.0, 47.0, 1.6), (75.0, 45.0, 1.9), (40.0, 60.5, 1.1), (72.0, 61.0, 1.1)):
        t.estrela_c(x, y, c, ("WH", "C2", "C1", "C0"), glow=1.0, brilho=0.3)


# ---------- PAJEM DE COPAS · TÔNICO FRACO ----------

def il_tonico_fraco(t):
    cx = CX
    # etiqueta de latão pendurada
    t.pintar(L(spline([(61.0, 83.5), (66.0, 85.0), (73.5, 90.0)], 6), 0.6), plano(0.45), contorno=False)
    etq = P(girar([(-3.8, -5.2), (3.8, -5.2), (3.8, 5.2), (-3.8, 5.2)], 76.0, 96.5, 0.22))
    t.pintar(etq, lambda X, Y: perfil((X - 71) / 10.0) * 0.85 + 0.05, chanfro=0.18)
    t.fill(E(74.6, 91.8, 0.8), "K")
    for k in range(3):
        y = 95.0 + k * 2.2
        t.clarear(L(girar([(-2.4, y - 96.5), (2.2 - k * 0.8, y - 96.5)], 76.0, 96.5, 0.22), 0.45), "B0", 0.7)
    # vidro de ombro reto (frasquinho de bolso)
    corpo = R(42, 94, 70, 128, 4.0)
    ombro = P([(43.0, 97), (50.5, 87.5), (61.5, 87.5), (69.0, 97)])
    vidro = corpo | ombro | R(51, 80, 61, 89)
    _cp_vidro(t, vidro, 50, 104)
    dentro = corpo.cresce(-1.0)
    # um resto de óleo no fundo
    sup = F(lambda X, Y: (116.5 + np.sin(X * 0.7) * 0.6) - Y, TUDO)
    oleo = dentro & sup
    t.fill(oleo, lambda X, Y: rampa(OURO, 0.66 - np.hypot(X - cx, Y - 122) / 14.0 * 0.5))
    t.fill(oleo & F(lambda X, Y: Y - (116.5 + np.sin(X * 0.7) * 0.6) - 0.7, TUDO), "B3")
    t.luz_radial(cx, 122.0, 4.5, "C1", 0.35, dentro=oleo, glow=0.4)
    t.cristal(cx, 121.5, 2.0, 3.4, aura=False, esp=0.45)
    for bx, by, r in ((48.0, 120.0, 0.55), (63.5, 122.0, 0.7), (60.0, 119.0, 0.45)):
        t.fill(E(bx, by, r), "B4")
    # plaquinha gravada no vidro
    placa = R(47, 100, 65, 109, 0.8)
    t.pintar(placa, lambda X, Y: perfil((X - 47) / 18.0) * 0.8 + 0.06, chanfro=0.18)
    t.fill(placa.anel(0.5).cresce(-0.9), "B1", alfa=0.6)
    t.fill(_cp_gota_forma(CX, 106.2, 1.6).anel(0.55), "B1")                            # gota gravada
    for x in (48.5, 62.0):
        t.rebite(x, 103.6, 0.6)
    # reflexos
    t.clarear(L([(45.2, 98.5), (45.2, 124.0)], 0.8), "B4", 0.75)
    t.clarear(L([(52.8, 81.5), (52.8, 88.0)], 0.6), "B4", 0.6)
    # boca, tampa e botão
    t.pintar(R(49, 77, 63, 81, 0.5), cil_x(49, 63), chanfro=0.12)
    t.pintar(R(51, 70, 61, 77.5, 0.6), cil_x(51, 61), chanfro=0.12)
    t.sulco_h(51.4, 60.6, 73.0)
    t.pintar(E(cx, 68.2, 2.4), esfera(cx, 68.2, 2.4), esp=0.5)


# ---------- III DE PAUS · PASSO DE PISTÃO ----------

# a bota é desenhada em pé e depois girada: inclinada para a frente, o pistão do calcanhar empurrando o chão
_CP_PASSO = (47.0, 140.5, 0.18, -4.0, 0.0)          # pivô x, pivô y, ângulo (rad, tela), deslocamento x, y


def _cp_girada(m, ox, oy, ang, dx=0.0, dy=0.0):
    """Forma girada de ang (radianos, sentido da tela) em torno de (ox, oy) e deslocada de (dx, dy)."""
    c, s = math.cos(ang), math.sin(ang)
    a = m.fn

    def f(X, Y):
        px, py = X - dx - ox, Y - dy - oy
        return a(ox + px * c + py * s, oy - px * s + py * c)
    x0, y0, x1, y1 = m.bb
    cs = girar([(x0 - ox, y0 - oy), (x1 - ox, y0 - oy), (x0 - ox, y1 - oy), (x1 - ox, y1 - oy)], ox, oy, ang)
    xs = [p[0] + dx for p in cs]
    ys = [p[1] + dy for p in cs]
    return F(f, (min(xs), min(ys), max(xs), max(ys)))


def _cp_gfn(fn, ox, oy, ang, dx=0.0, dy=0.0):
    """Função de luz acompanhando a peça girada."""
    c, s = math.cos(ang), math.sin(ang)

    def f(X, Y):
        px, py = X - dx - ox, Y - dy - oy
        return fn(ox + px * c + py * s, oy - px * s + py * c)
    return f


def _cp_gp(x, y, ox, oy, ang, dx=0.0, dy=0.0):
    p = girar([(x - ox, y - oy)], ox, oy, ang)[0]
    return p[0] + dx, p[1] + dy


def il_passo_pistao(t):
    G = lambda m: _cp_girada(m, *_CP_PASSO)          # noqa: E731
    GF = lambda fn: _cp_gfn(fn, *_CP_PASSO)          # noqa: E731
    GP = lambda x, y: _cp_gp(x, y, *_CP_PASSO)       # noqa: E731

    def rebite(x, y, r=0.85):
        px, py = GP(x + 0.85, y + 0.85)
        t.rebite(px - 0.85, py - 0.85, r)

    # rastros da velocidade atrás da bota
    for y, x0, comp in ((54.5, 45.5, 17), (64.5, 42.5, 21), (75.5, 39.5, 17), (87.5, 36.5, 19), (100.5, 33.0, 14)):
        t.raio_luz((x0, y), (x0 - comp, y), "B3", "B1", 0.55, 0.25)
    # vapor estourando dos dois lados do pistão
    t.massa_vapor([(13.5, 130.0, 3.0), (18.5, 134.0, 4.0), (25.5, 137.0, 4.6), (33.0, 140.0, 4.0)])
    t.vapor([(70.5, 133.0, 2.2), (65.0, 137.0, 3.0), (59.0, 140.0, 3.4)])
    # pistão sob o calcanhar
    t.pintar(G(R(36.5, 138, 57.5, 143, 0.8)), GF(linear(36, 138, 58, 143)), chanfro=0.18)
    t.pintar(G(R(43.5, 128, 50.5, 139)), GF(cil_x(43.5, 50.5, 1.05)))
    t.clarear(G(R(44.6, 130, 45.5, 137)), "B4", 0.6)
    t.pintar(G(R(38, 120, 56, 129, 0.6)), GF(cil_x(38, 56)), chanfro=0.12)
    t.pintar(G(R(36.5, 126.5, 57.5, 129.5, 0.4)), GF(cil_x(36.5, 57.5)), chanfro=0.12)
    for x in (40, 52):
        rebite(x, 122)
    # cilindro nas costas do cano da bota
    t.pintar(G(R(32.5, 74, 37.5, 118, 0.6)), GF(cil_x(32.5, 37.5)))
    for y0 in (72.0, 92.0, 114.0):
        t.pintar(G(R(31.5, y0, 38.5, y0 + 3, 0.4)), GF(cil_x(31.5, 38.5)), chanfro=0.12)
    # bota de couro (rampa mais escura que o latão)
    cano = P([(39, 52), (63, 52), (64.5, 98), (38, 101)])
    pe_pts = spline([(38, 99), (62, 97), (71, 100), (79, 104), (84, 108.5), (86.5, 113), (86.5, 118)], 6)
    pe = P(pe_pts + [(38, 118)]) | E(80.0, 112.5, 6.5, 5.8)
    bota = cano | pe
    t.pintar(G(bota), GF(lambda X, Y: perfil((X - 37) / 30.0) * 0.5 + 0.03 - 0.06 * np.clip((X - 66) / 22, 0, 1)),
             chanfro=0.18)
    t.clarear(G(L(spline([(41.5, 56.0), (41.0, 80.0), (41.5, 98.0)], 6), 0.9)), "B3", 0.45)
    t.clarear(G(arco(64.0, 116.0, 13.0, 225, 290, 0.7, ry=12.0) & pe.cresce(-1.2)), "B3", 0.4)   # dobra do peito do pé
    # costura (pesponto) do peito do pé
    cost = spline([(44, 101.5), (62, 100), (70, 102.5), (77, 106)], 8)
    for k in range(0, len(cost) - 1, 2):
        t.fill(G(L(cost[k:k + 2], 0.45)), "B3", alfa=0.8)
    # biqueira de latão
    biq = pe & E(84.5, 112.5, 8.5, 8.5)
    t.pintar(G(biq), GF(lambda X, Y: perfil((X - 76) / 12.0) * 0.95 + 0.06), chanfro=0.2, esp=0.5)
    rebite(78.2, 109.5, 0.65)
    rebite(77.6, 114.0, 0.65)
    # sola com pesponto
    t.pintar(G(R(36, 117, 89, 121.5, 1.0)), GF(linear(36, 117, 89, 122, 0.8, 0.0)), chanfro=0.18)
    for x in range(40, 87, 3):
        t.fill(G(E(x + 0.5, 119.3, 0.35)), "B3")
    # tiras com fivelas
    for y0 in (63.0, 79.0):
        tira = R(36, y0, 66, y0 + 4.5) & cano.cresce(0.5)
        t.pintar(G(tira), GF(lambda X, Y: perfil((X - 37) / 30.0) * 0.38 + 0.02), chanfro=0.15)
        fiv = R(53.5, y0 - 1.6, 61.5, y0 + 6.1, 0.7)
        t.pintar(G(fiv.anel(1.3)), GF(lambda X, Y: perfil((X - 53.5) / 8.0) * 0.95 + 0.05), chanfro=0.12, esp=0.45)
        t.pintar(G(L([(54.5, y0 + 2.25), (61.0, y0 + 2.25)], 0.85)), plano(0.85), esp=0.3)
    # punho de latão
    t.pintar(G(R(36, 46.5, 66, 54.5, 1.2)), GF(cil_x(36, 66)), chanfro=0.15)
    t.fill(G(R(37, 50.0, 65, 50.7)), "B1")
    t.clarear(G(R(37, 50.7, 65, 51.0)), "B4", 0.3)
    for x in (39, 47, 55, 62):
        rebite(x, 51.6, 0.7)
    # engrenagem do tornozelo com cristal
    ex, ey = GP(51.0, 99.5)
    eng = G(engrenagem(51.0, 99.5, 6.5, 10, 2.2, fase=0.5))
    t.pintar(eng, linear(ex - 9, ey - 9, ex + 9, ey + 9, 1.0, 0.1), chanfro=0.2)
    soq = E(ex, ey, 4.4)
    t.fill(soq, lambda X, Y: rampa(("K", t.arc[0]), np.full_like(X, 0.5)))
    t.fill(soq.anel(0.7), lambda X, Y: rampa(("B0", "B3"), smooth(-3, 3, (X - ex) + (Y - ey))))
    t.cristal(ex, ey, 2.5, 4.0)
    # tubo do tornozelo até o pistão
    tubo = G(L(spline([(57.2, 104.0), (59.5, 112.0), (56.0, 120.5)], 6), 1.8))
    t.pintar(tubo, linear(ex, ey, ex + 10, ey + 22), esp=0.4)


# ---------- VIII DE PAUS · CORAÇÃO DE CALDEIRA ----------

def _cp_coracao():
    lobos = E(44.5, 91.0, 12.8).suave(E(67.5, 91.0, 12.8), 0.8)
    return lobos.suave(P([(32.4, 95.0), (79.6, 95.0), (56.0, 129.0)]), 2.0)


def il_coracao_caldeira(t):
    cx, cy = CX, 100.0
    # pulsos arcanos dos dois lados
    for k, r in enumerate((33.0, 37.5, 42.0)):
        lim = 26 - k * 5
        alfa = 1.0 - k * 0.25
        for a0 in (0.0, 180.0):
            arc_ = arco(cx, cy, r, a0 - lim, a0 + lim, 0.9, 30)
            t.halo_arcano(arc_, 1.6, 70)
            t.fill(arc_, "C1", glow=alfa, alfa=alfa)
            t.fill(arco(cx, cy, r, a0 - lim * 0.6, a0 + lim * 0.6, 0.4, 30), "WH", glow=alfa, alfa=alfa)
    # canos que saem dos lobos (as veias), com vapor nas pontas
    t.vapor([(18.0, 51.0, 2.4), (21.5, 56.5, 3.2)])
    t.vapor([(94.0, 51.0, 2.4), (90.5, 56.5, 3.2)])
    for sx in (-1, 1):
        pts = [(CX + sx * 12, 82.0), (CX + sx * 15, 71.0), (CX + sx * 22, 64.5), (CX + sx * 29, 63.5)]
        t.pintar(L(spline(pts, 8), 3.8), linear(22, 58, 90, 86, 1.0, 0.05), chanfro=0.1)
        fx = CX + sx * 30.5
        t.pintar(R(fx - 1.6, 59.5, fx + 1.6, 67.5, 0.4), cil_x(fx - 1.6, fx + 1.6), chanfro=0.15)
        t.pintar(R(CX + sx * 15 - 3, 72, CX + sx * 15 + 3, 74.5, 0.4), cil_x(CX + sx * 15 - 3, CX + sx * 15 + 3),
                 chanfro=0.1)
    # cano do manômetro
    t.pintar(R(54, 68, 58, 84), cil_x(54, 58))
    # o coração
    cor_ = _cp_coracao()
    t.pintar(cor_, esfera(cx, 97.0, 30.0, 1.02, 0.02), chanfro=0.15)
    t.clarear(arco(44.5, 91.0, 9.5, 200, 260, 1.0) & cor_.cresce(-1.0), "B4", 0.45)
    t.clarear(arco(67.5, 91.0, 9.5, 220, 270, 0.8) & cor_.cresce(-1.0), "B4", 0.3)
    # costura central e rebites na borda
    t.fill(L([(CX, 84.0), (CX, 93.0)], 0.7), "B0", alfa=0.85)
    t.fill(L([(CX, 113.5), (CX, 126.5)], 0.7), "B0", alfa=0.85)
    for y in (88, 117, 122):
        t.rebite(CX - 0.85, y, 0.7)
    for ox, a0, a1 in ((44.5, 150, 290), (67.5, 250, 390)):
        for i in range(6):
            a = math.radians(a0 + (a1 - a0) * i / 5)
            t.rebite(ox - 0.85 + math.cos(a) * 10.0, 91.0 - 0.85 + math.sin(a) * 10.0, 0.75)
    for f in (0.3, 0.55, 0.8):                     # ao longo das bordas de baixo
        ex_, ey_ = 32.4 + 23.6 * f, 95.0 + 34.0 * f
        for sx in (-1, 1):
            x = CX + sx * (ex_ + 0.82 * 2.8 - CX)
            t.rebite(x - 0.85, ey_ - 0.57 * 2.8 - 0.85, 0.75)
    # vigia com o cristal que pulsa
    vx, vy = cx, 103.0
    t.pintar(anel(vx, vy, 6.6, 9.0), esfera(vx, vy, 9.0, 1.0, 0.1), chanfro=0.15)
    for i in range(8):
        a = i * math.tau / 8 + math.tau / 16
        t.rebite(vx - 0.85 + math.cos(a) * 7.8, vy - 0.85 + math.sin(a) * 7.8, 0.55)
    janela = E(vx, vy, 6.6)
    t.fill(janela, lambda X, Y: rampa(("K", "C0", "C1"), 0.75 - 0.09 * np.hypot(X - vx, Y - vy)), glow=0.5)
    t.fill(anel(vx, vy, 4.6, 5.0), "C2", glow=1.0, alfa=0.6)
    t.cristal(vx, vy, 3.4, 5.6, aura=False, esp=0.5)
    t.clarear(arco(vx, vy, 5.6, 200, 250, 0.6), "WH", 0.7, glow=1.0)
    # manômetro
    t.pintar(R(51, 72, 61, 75, 0.4), cil_x(51, 61), chanfro=0.1)
    _cp_manometro(t, CX, 63.0, 7.0, -35)
    # torneira de dreno na ponta
    t.pintar(R(54.2, 126, 57.8, 132, 0.3), cil_x(54.2, 57.8))
    t.pintar(R(51, 131, 61, 133.5, 0.4), cil_x(51, 61), chanfro=0.1)


# ---------- II DE PAUS · PAVIO CURTO ----------

def il_pavio_curto(t):
    bx = 42.0                                       # eixo da bobina
    fx, fy = 80.0, 85.5                             # a faísca na ponta do pavio
    # base
    t.pintar(R(22, 135, 90, 140, 0.8), linear(22, 135, 90, 140), chanfro=0.18)
    for x in (25, 86):
        t.rebite(x, 137)
    # carretel: flange de baixo, espiras de cobre e flange de cima
    t.pintar(R(25, 128, 59, 135, 1.4), cil_x(25, 59), chanfro=0.15)
    t.pintar(R(30, 79, 54, 129), cil_x(30, 54, 0.5, -0.02))
    for i in range(10):
        ey = 124.8 - i * 4.8
        espira = E(bx, ey, 14.6, 3.0) - E(bx, ey - 0.8, 12.6, 1.8)
        costas = espira & semiplano(0, 1, ey) & (R(-10, 0, 30, H) | R(54, 0, W + 10, H))
        frente = espira & semiplano(0, -1, -(ey - 0.5))
        t.fill(costas, lambda X, Y: rampa(OURO, 0.2 + 0.08 * np.clip((X - 27) / 30, 0, 1)))
        t.fill(mover(frente, 0, 0.8) - espira, "B0", alfa=0.9)
        t.pintar(frente, cil_x(27.4, 56.6, 0.92), contorno=False, chanfro=0.12)
    t.pintar(R(25, 73, 59, 80, 1.4), cil_x(25, 59), chanfro=0.15)
    for x in (28, 55):
        t.rebite(x, 75.5, 0.7)
        t.rebite(x, 130.5, 0.7)
    # eixo com o cristal
    t.pintar(R(37.5, 67, 46.5, 73.5, 0.5), cil_x(37.5, 46.5), chanfro=0.12)
    t.cristal(bx, 59.5, 3.8, 7.2)
    # cartucho do pavio
    t.pintar(R(64, 100, 88, 135.5, 1.4), cil_x(64, 88))
    for y0 in (104.0, 127.5):
        t.pintar(R(63, y0, 89, y0 + 3.2, 0.5), cil_x(63, 89), chanfro=0.12)
    for x in (66, 84):
        t.rebite(x, 105, 0.7)
        t.rebite(x, 128.5, 0.7)
    t.pintar(R(70, 111, 82, 123, 0.8).anel(0.8), plano(0.35), contorno=False)          # janela com o cristal da carga
    t.fill(R(70.8, 111.8, 81.2, 122.2, 0.5), lambda X, Y: rampa(("K", t.arc[0]), np.full_like(X, 0.6)))
    t.cristal(76.0, 117.0, 2.4, 4.0, aura=False, esp=0.45)
    t.pintar(R(66.5, 95, 85.5, 101, 0.7), cil_x(66.5, 85.5), chanfro=0.12)
    t.pintar(R(73.5, 91.5, 78.5, 95.5, 0.4), cil_x(73.5, 78.5), chanfro=0.1)
    # fio de cobre da bobina ao cartucho
    t.pintar(L(spline([(58.5, 77.0), (63.0, 80.0), (64.0, 90.0), (66.5, 97.5)], 8), 1.3), plano(0.55), esp=0.35)
    t.pintar(E(66.8, 97.8, 1.2), esfera(66.8, 97.8, 1.2), esp=0.35)
    # pavio curtinho e trançado, a ponta queimando
    pav = spline([(76.0, 92.0), (76.6, 89.4), (78.6, 87.0)], 6)
    t.pintar(L(pav, 2.3), lambda X, Y: 0.5 + 0.06 * np.sin((X + Y) * 3.2), esp=0.45, chanfro=0.25)
    for k in range(0, len(pav) - 1, 2):
        x, y = pav[k]
        t.fill(L([(x - 0.7, y - 0.2), (x + 0.5, y + 0.5)], 0.35), "B1", alfa=0.85)
    t.fill(L([(77.9, 87.7), (79.0, 86.5)], 2.0), "B0")
    # o arco do cristal acende o pavio
    t.raio(P5([(46, 54), (53, 50), (60, 53), (67, 52), (73, 58), (77, 66), (79, 75), (79.5, 81)]), ponta=False, seed=7)
    # a faísca: brilho, fagulhas em leque e estrela
    t.luz_radial(fx, fy, 9.0, "B3", 0.5, so_fundo=True)
    for i in range(14):
        a = math.radians(-170 + i * (160 / 13))
        r1 = 7.5 + 3.5 * ((i * 7) % 3)
        t.raio_luz((fx + math.cos(a) * 2.5, fy + math.sin(a) * 2.5), (fx + math.cos(a) * r1, fy + math.sin(a) * r1),
                   "B4", "B2", 0.36, 0.3)
    t.estrela_c(fx, fy, 6.8, ("B4", "B4", "B3", "B2"), diag=2.8, brilho=0.5)
    t.estrela_c(fx, fy, 2.6, ("WH", "C2", "C1", "C0"), glow=1.0, brilho=0.45)
    for x, y, c in ((88.0, 80.0, 1.3), (90.0, 89.0, 1.0), (71.5, 79.5, 1.0), (84.0, 73.5, 0.9)):
        t.estrela_c(x, y, c, ("B4", "B3", "B3", "B2"), brilho=0.15)
    for x, y in ((92.0, 84.0), (86.5, 93.0), (74.0, 76.0), (89.0, 76.0)):
        t.fill(E(x, y, 0.45), "B4")


# ---------- V DE PAUS · FORNALHA FAMINTA ----------

_CP_BOCA = (CX, 109.0, 17.0, 10.5)                 # centro e semieixos da boca


def _cp_dentes(cx, cy, rx, ry):
    """Dentes de engrenagem (trapézios de ponta chata) nas bordas de cima e de baixo da boca."""
    m = None
    for xs, lado, comp in (((44.5, 50.3, 56.0, 61.7, 67.5), -1, 4.2), ((47.2, 53.0, 59.0, 64.8), 1, 3.4)):
        for x in xs:
            u = (x - cx) / rx
            yb = cy + lado * ry * math.sqrt(max(0.0, 1 - u * u))
            ponta = yb - lado * comp
            d = P([(x - 2.4, yb + lado * 1.2), (x + 2.4, yb + lado * 1.2), (x + 1.15, ponta), (x - 1.15, ponta)])
            m = d if m is None else (m | d)
    return m


def il_fornalha_faminta(t):
    bx, by, rx, ry = _CP_BOCA
    # fumaça da chaminé
    t.vapor([(67.0, 33.0, 2.2), (62.0, 36.5, 3.0)])
    # chaminé e tampa
    t.pintar(R(50, 44, 62, 66), cil_x(50, 62))
    t.pintar(R(47, 40, 65, 45.5, 0.6), cil_x(47, 65), chanfro=0.15)
    t.pintar(R(48.5, 54, 63.5, 57, 0.4), cil_x(48.5, 63.5), chanfro=0.12)
    # pés abertos
    for sx in (-1, 1):
        pe = P([(CX + sx * 20, 131), (CX + sx * 14, 131), (CX + sx * 18, 145), (CX + sx * 25.5, 145)])
        t.pintar(pe, linear(28, 128, 84, 146), chanfro=0.15)
        t.pintar(E(CX + sx * 21.8, 145.0, 4.2, 1.6), linear(28, 140, 84, 148), esp=0.5)
    # pescoço e tampa do fogão
    t.pintar(P([(40, 69), (72, 69), (70, 89), (42, 89)]), cil_x(40, 72, 0.95), chanfro=0.15)
    t.pintar(R(35, 64, 77, 70, 1.2), cil_x(35, 77), chanfro=0.18)
    for x in (39, 72):
        t.rebite(x, 66)
    # barriga de ferro
    barriga = E(CX, 107.0, 28.0, 25.0)
    t.pintar(barriga, esfera(CX, 104.0, 29.0, 0.95, 0.03), chanfro=0.15)
    for i in range(18):
        a = i * math.tau / 18
        t.rebite(CX - 0.85 + math.cos(a) * 24.8, 107.0 - 0.85 + math.sin(a) * 21.8, 0.75)
    t.pintar(R(31, 128, 81, 134, 1.4), cil_x(31, 81), chanfro=0.18)
    t.sulco_h(32.0, 80.0, 130.4)
    # manômetro no pescoço
    _cp_manometro(t, CX, 77.5, 5.2, -25)
    # boca: lábio de latão com as engrenagens das juntas
    boca = E(bx, by, rx, ry)
    labio = E(bx, by, rx + 3.6, ry + 3.4) - boca
    for sx in (-1, 1):
        gx = bx + sx * (rx + 2.8)
        t.pintar(engrenagem(gx, by, 4.4, 9, 2.0, fase=0.25), linear(gx - 7, by - 7, gx + 7, by + 7, 1.0, 0.08),
                 chanfro=0.2)
    t.pintar(labio, linear(bx - 21, by - 14, bx + 21, by + 14, 1.0, 0.1), chanfro=0.2)
    for sx in (-1, 1):
        gx = bx + sx * (rx + 2.8)
        t.fill(E(gx, by, 1.6), "B0")
        t.pintar(E(gx, by, 1.15), esfera(gx, by, 1.15), contorno=False)
    # o fogo lá dentro
    t.fill(boca, lambda X, Y: rampa(("B0", "B1", "B2", "B3", "B4"),
                                    np.clip(1.12 - np.hypot((X - bx) / rx, (Y - by - 6) / (ry * 1.2)) * 0.85, 0, 1)))
    t.fill(boca & semiplano(0, 1, by - 6), lambda X, Y: rampa(("K", "B0"), np.clip((Y - (by - ry)) / 5, 0, 1)),
           alfa=0.55)
    for cxf, h, w, inc in ((44.0, 8.0, 2.0, -1.2), (49.5, 12.0, 2.5, -0.8), (62.5, 12.0, 2.5, 0.8), (68.0, 8.0, 2.0, 1.2)):
        t.chama(cxf, by + 10.0, h, w, inc)
    t.luz_radial(bx, by + 7, 9.0, "B4", 0.45, dentro=boca)
    # o cristal sendo mastigado
    t.luz_radial(bx, by - 1.5, 7.0, "C1", 0.45, dentro=boca, glow=0.6)
    t.cristal(bx, by - 1.0, 4.4, 8.2, aura=False)
    # dentes por cima do cristal
    dentes = _cp_dentes(bx, by, rx, ry) - labio.cresce(-0.4)
    t.pintar(dentes, linear(bx - 18, by - 8, bx + 18, by + 8, 1.05, 0.12), chanfro=0.2, esp=0.5)
    # lascas do cristal e brasas voando
    for (sx_, sy_, w, h) in ((33.0, 92.0, 1.4, 2.2), (80.0, 95.0, 1.2, 1.9), (76.0, 86.0, 0.9, 1.4)):
        lo = P([(sx_, sy_ - h), (sx_ + w, sy_), (sx_, sy_ + h), (sx_ - w, sy_)])
        t.pintar(lo, lambda X, Y, sx_=sx_, sy_=sy_: 0.85 - 0.45 * np.clip((X - sx_ + Y - sy_ + 2) / 4, 0, 1), CIANO,
                 claro="C0", escuro="C0", esp=0.4)
        t.halo_arcano(lo, 1.4, 80)
    for x, y, c in ((26.0, 84.0, 1.3), (86.0, 82.0, 1.1), (88.0, 112.0, 0.9), (24.0, 120.0, 1.0), (75.0, 52.0, 0.9)):
        t.estrela_c(x, y, c, ("B4", "B3", "B3", "B2"), brilho=0.2)
    for x, y in ((30.0, 77.0), (83.0, 72.0), (38.0, 56.0), (90.0, 101.0)):
        t.fill(E(x, y, 0.5), "B3")
