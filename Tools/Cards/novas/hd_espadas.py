# Fase 10 (D-088), grupo G1: espadas e arcanos maiores, em alta resolução.
# Executado dentro de card_hd.py (mesmo espaço de nomes: R, E, P, L, arco, spline, cil_x, esfera, t.pintar...).
# Mesmas formas e coordenadas de px_espadas.py. Ajudantes com prefixo _esp_ para não colidir com outros grupos.


# ---------- ajudantes do grupo ----------

def _esp_engr_pts(cx, cy, r, dentes, h, fase=0.0, cheio=0.5, ponta=None, serra=False, sx=1.0, sy=1.0):
    """Pontos de uma engrenagem (como engrenagem()); serra=True faz dentes de serra (frente reta no sentido horário);
    sx/sy achatam (roda vista de lado)."""
    pts = []
    passo = math.tau / dentes
    hr = cheio / 2 * passo
    ht = (cheio / 2 - 0.1) * passo if ponta is None else ponta * passo
    for i in range(dentes):
        tc = (i + fase + 0.5) * passo
        for k in range(1, 4):
            a = tc - passo + hr + (passo - 2 * hr) * k / 4.0
            pts.append((math.cos(a) * r, math.sin(a) * r))
        pts.append((math.cos(tc - hr) * r, math.sin(tc - hr) * r))
        if serra:
            a = tc + hr - 0.06 * passo
            pts.append((math.cos(a) * (r + h), math.sin(a) * (r + h)))
            a = tc + hr
            pts.append((math.cos(a) * (r + h * 0.8), math.sin(a) * (r + h * 0.8)))
        else:
            for k in range(3):
                a = tc - ht + ht * k
                pts.append((math.cos(a) * (r + h), math.sin(a) * (r + h)))
            pts.append((math.cos(tc + hr) * r, math.sin(tc + hr) * r))
    return [(cx + x * sx, cy + y * sy) for x, y in pts]


def _esp_crescente(cx, cy, r, a0, span, w, n=48):
    """Rastro de golpe em lua: fino na cauda (a0), largo perto da cabeça, ponta afiada (a0 + span, sentido horário)."""
    fora_, dentro = [], []
    for i in range(n + 1):
        u = i / n
        a = math.radians(a0 + span * u)
        wd = w * (u / 0.8) ** 1.4 if u < 0.8 else w * (1 - u) / 0.2
        fora_.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
        dentro.append((cx + math.cos(a) * (r - wd), cy + math.sin(a) * (r - wd)))
    return P(fora_ + dentro[::-1])


def _esp_u(X, Y, cx, cy, a0, span):
    a = np.degrees(np.arctan2(Y - cy, X - cx))
    return np.clip(np.mod(a - a0, 360.0) / span, 0, 1)


def _esp_arco_fade(t, cx, cy, r, a0, span, w, c="B2", glow=0.0, so_fundo=True, ry=None, inverso=False):
    """Arco de movimento: some na cauda (a0) e fica nítido na cabeça (a0 + span); inverso troca os lados."""
    ry = r if ry is None else ry
    reg = t.regiao((cx - r - w, cy - ry - w, cx + r + w, cy + ry + w))
    if reg is None:
        return
    sl, X, Y = reg
    a = np.degrees(np.arctan2((Y - cy) / ry, (X - cx) / r))
    rel = np.mod(a - a0, 360.0)
    u = np.clip(rel / span, 0, 1)
    if inverso:
        u = 1.0 - u
    dentro = (rel <= span) * 1.0
    rho = np.hypot((X - cx) / r, (Y - cy) / ry) * min(r, ry)
    wd = w * (0.35 + 0.65 * u)
    cob = np.clip(0.5 - (np.abs(rho - min(r, ry)) - wd / 2) * K, 0, 1) * dentro * (u ** 1.3)
    t.compor(sl, cob, cor(c), glow, False, so_fundo)


def _esp_traco_fade(t, pts, w, c="B2", a0=0.0, a1=1.0, glow=0.0, so_fundo=True):
    """Traço por uma polilinha com alfa indo de a0 a a1 (rastros)."""
    n = len(pts) - 1
    for i in range(n):
        a = a0 + (a1 - a0) * (i + 0.5) / n
        if a <= 0.02:
            continue
        t.fill(L(pts[i:i + 2], w), c, glow=glow, ocupa=False, so_fundo=so_fundo, alfa=a)


def _esp_amostrar(pts, passo):
    """Pontos igualmente espaçados ao longo da polilinha: (x, y, ângulo da tangente)."""
    seg = []
    tot = 0.0
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        ll = math.hypot(bx - ax, by - ay)
        seg.append((ax, ay, bx, by, ll, tot))
        tot += ll
    out = []
    s = passo / 2
    while s < tot:
        for ax, ay, bx, by, ll, s0 in seg:
            if s0 <= s <= s0 + ll and ll > 0:
                f = (s - s0) / ll
                out.append((ax + (bx - ax) * f, ay + (by - ay) * f, math.atan2(by - ay, bx - ax)))
                break
        s += passo
    return out


def _esp_cristal(t, cx, cy, w, h, ang=0.0, pal=None, aura=True, esp=0.6, cintila=True):
    """O cristal facetado de Tela.cristal, girado de ang (radianos, horário; 0 = ponta para cima)."""
    pal = pal or t.arc
    ca, sa = math.cos(ang), math.sin(ang)

    def g(x, y):
        return (cx + x * ca - y * sa, cy + x * sa + y * ca)

    def ly(X, Y):
        return -(X - cx) * sa + (Y - cy) * ca
    ys, yi = -h * 0.42, h * 0.45
    om = ys + h * 0.2
    topo, base = g(0, -h), g(0, h)
    pd, pe, id_, ie, oc = g(w, ys), g(-w, ys), g(w, yi), g(-w, yi), g(0, om)
    m = P([topo, pd, id_, base, ie, pe])
    t.fill(m.cresce(esp), pal[0])
    rp = (pal[0], pal[1], pal[2], pal[3])

    def desce(X, Y):
        return (ly(X, Y) - om) / max(1.0, h - om)
    faces = [
        (P([topo, pe, oc]), plano(0.98)),
        (P([topo, pd, oc]), lambda X, Y: 0.70 + 0.06 * (ly(X, Y) + h) / h),
        (P([pe, oc, base, ie]), lambda X, Y: 0.74 - 0.42 * desce(X, Y)),
        (P([pd, oc, base, id_]), lambda X, Y: 0.44 - 0.30 * desce(X, Y)),
    ]
    for f, fn in faces:
        t.pintar(f & m, fn, rp, contorno=False)
    if w >= 3:
        t.pintar(P([g(-w * 0.55, ys + h * 0.12), g(-w * 0.12, om + h * 0.05), g(-w * 0.12, h * 0.55),
                    g(-w * 0.55, yi - h * 0.05)]), lambda X, Y: 0.9 - 0.4 * desce(X, Y), rp, contorno=False)
    t.pintar(L([oc, g(0, h * 0.92)], max(0.45, w * 0.12)), lambda X, Y: 0.92 - 0.3 * desce(X, Y), rp, contorno=False)
    t.pintar(L([g(-w + 0.3, ys + 0.2), oc, g(w - 0.3, ys + 0.2)], max(0.35, w * 0.07)), plano(0.95), rp,
             contorno=False)
    t.fill(L([g(-w * 0.42, -h * 0.44), g(-w * 0.26, -h * 0.66)], max(0.5, w * 0.16)), pal[3], glow=1.0)
    if cintila:
        sx_, sy_ = g(-w * 0.38, -h * 0.7)
        t.estrela_c(sx_, sy_, max(1.2, h * 0.24), (pal[3], pal[3], pal[2], pal[1]), glow=1.0)
    if aura:
        t.halo_arcano(m)
    return m


def _esp_soquete(t, cx, cy, r):
    """Soquete escuro com brilho ciano por dentro e aro de bronze (como na Mina de Engrenagem)."""
    soq = E(cx, cy, r)
    t.fill(soq, lambda X, Y: rampa(("K", t.arc[0]), 0.6 - 0.05 * np.hypot(X - cx, Y - cy)))
    t.fill(soq.anel(0.8), lambda X, Y: rampa(("B0", "B3"), smooth(-r / 2, r / 2, (X - cx) + (Y - cy))))
    return soq


def _esp_fenda(t, pts, larg=1.0, dentro=None):
    """Rachadura de faísca: brilho ciano em volta, fio claro e núcleo branco."""
    m = L(pts, larg * 2.6)
    if dentro is not None:
        m = m & dentro
    t.halo_arcano(L(pts, larg), 2.2, 85)
    t.clarear(m, "C1", 0.2, glow=0.35)
    f1 = L(pts, larg)
    f2 = L(pts, larg * 0.4)
    if dentro is not None:
        f1, f2 = f1 & dentro, f2 & dentro
    t.fill(f1, "C2", glow=1.0)
    t.fill(f2, "WH", glow=1.0)


def _esp_zigue(p0, p1, n, amp, seed):
    """Zigue-zague de p0 a p1 em n trechos (pontos para t.raio)."""
    rnd = random.Random(seed)
    dx, dy = p1[0] - p0[0], p1[1] - p0[1]
    ll = math.hypot(dx, dy) or 1.0
    nx, ny = -dy / ll, dx / ll
    pts = [p0]
    for i in range(1, n):
        f = i / n
        j = amp * (1 if i % 2 else -1) * rnd.uniform(0.6, 1.0)
        pts.append((p0[0] + dx * f + nx * j, p0[1] + dy * f + ny * j))
    pts.append(p1)
    return pts


# ---------- 15 · II de Espadas · Giro de Engrenagem ----------

def il_giro_engrenagem(t):
    cx, cy = CX, 96.0
    # golpe giratório: três luas de lâmina em volta (sentido horário) com fio arcano na cabeça
    for k in range(3):
        a0 = -150 + k * 120
        cres = _esp_crescente(cx, cy, 40.0, a0, 100, 6.0)
        t.pintar(cres, lambda X, Y, a0=a0: 0.18 + 0.78 * _esp_u(X, Y, cx, cy, a0, 100) ** 1.2,
                 chanfro=0.15, esp=0.5)
        _esp_arco_fade(t, cx, cy, 39.6, a0 + 30, 64, 0.55, "C2", glow=1.0, so_fundo=False)
        ha = math.radians(a0 + 100)
        t.estrela_c(cx + math.cos(ha) * 39.7, cy + math.sin(ha) * 39.7, 2.6, ("WH", "C2", "C1", "C0"), glow=1.0)
    # arcos de movimento finos por dentro
    for k in range(3):
        a0 = -115 + k * 120
        _esp_arco_fade(t, cx, cy, 30.8, a0, 75, 0.7, "B2")
        _esp_arco_fade(t, cx, cy, 33.0, a0 + 25, 55, 0.45, "B1")
    # engrenagem: aro dentado e raios curvos (vazada: o halo aparece entre os raios)
    luz = linear(cx - 27, cy - 27, cx + 27, cy + 27, 1.05, 0.05)
    raios = None
    for i in range(5):
        a = math.radians(-90 + i * 72)
        pts = [(cx + math.cos(a + b) * rr, cy + math.sin(a + b) * rr) for rr, b in ((8.5, 0.0), (12.5, 0.2), (16.5, 0.45))]
        s_ = L(spline(pts, 6), 3.6)
        raios = s_ if raios is None else (raios | s_)
    t.pintar(raios, luz, chanfro=0.18)
    eng = P(_esp_engr_pts(cx, cy, 21.0, 10, 5.0, fase=0.12))
    aro = eng - E(cx, cy, 15.8)
    t.pintar(aro, luz, chanfro=0.2)
    t.fill(anel(cx, cy, 17.6, 18.2), "B0")
    t.fill(anel(cx, cy, 18.2, 18.7) & semiplano(1, 1, cx + cy - 2), "B3", alfa=0.9)
    for i in range(10):                                    # rebites no aro
        a = (i + 0.12 + 0.5) * math.tau / 10
        t.rebite(int(cx - 0.5 + math.cos(a) * 19.9), int(cy - 0.5 + math.sin(a) * 19.9))
    # cubo e cristal no eixo
    t.pintar(E(cx, cy, 10.0), esfera(cx, cy, 10.0, 0.95, 0.05))
    t.fill(anel(cx, cy, 8.3, 8.8), "B0", alfa=0.8)
    _esp_soquete(t, cx, cy, 7.0)
    t.cristal(cx, cy, 4.0, 7.0, aura=False)
    t.luz_radial(cx, cy, 13.0, "C1", 0.18, glow=0.3, so_fundo=True)


# ---------- 16 · IV de Espadas · Chicote de Corrente ----------

def il_chicote_corrente(t):
    ctrl = [(46, 123), (60, 120), (72, 112), (76, 100), (68, 90), (52, 85), (38, 79), (33, 68), (39, 58), (51, 53),
            (61, 50), (67, 46)]
    cam = spline(ctrl, 16)
    # rastros do chicote do lado de fora das curvas
    for i0, i1, lado in ((24, 70, 1), (90, 140, -1)):
        tr = []
        for i in range(i0, i1):
            (ax, ay), (bx, by) = cam[i], cam[i + 1]
            ll = math.hypot(bx - ax, by - ay) or 1
            nx, ny = -(by - ay) / ll * lado, (bx - ax) / ll * lado
            tr.append((ax + nx * 6.0, ay + ny * 6.0))
        _esp_traco_fade(t, tr, 0.7, "B2", 0.0, 0.9)
        _esp_traco_fade(t, [(x - (cam[i0 + k][0] - x) * 0.4, y - (cam[i0 + k][1] - y) * 0.4)
                            for k, (x, y) in enumerate(tr)][::2], 0.45, "B1", 0.0, 0.7)
    # cabo
    cabo = L([(44.5, 126), (38.5, 150)], 5.6)
    t.pintar(cabo, local(cil_x(-2.8, 2.8), 41.5, 138, math.atan2(24, -6) - math.pi / 2), chanfro=0.12)
    for k in range(6):
        y = 129 + k * 3.6
        x = 44.5 - (y - 126) * 0.25
        tira = L([(x - 3.2, y + 0.4), (x + 3.0, y - 1.0)], 0.7)
        t.fill(tira & cabo, "B0")
        t.clarear(mover(tira, 0, 0.6) & cabo, "B4", 0.3)
    t.pintar(L([(39.0, 124.2), (51.0, 127.2)], 2.6), linear(38, 122, 52, 129), chanfro=0.15)
    for x, y in ((38.6, 124.1), (51.4, 127.3)):
        t.pintar(E(x, y, 1.9), esfera(x, y, 1.9))
    t.pintar(E(37.8, 153.0, 3.6), esfera(37.8, 153.0, 3.6))
    t.pintar(E(37.8, 153.0, 1.3), lambda X, Y: 0.75 - 0.3 * ((X - 37.8) + (Y - 153)) / 2.6, CIANO, contorno=True,
             claro="C0", escuro="C0", esp=0.4)
    # corrente: elos de frente e de lado alternados, afinando para a ponta
    elos = _esp_amostrar(cam, 5.6)
    n = len(elos)
    de_lado = []
    for i, (x, y, a) in enumerate(elos):
        s = 1.28 - 0.38 * i / max(1, n - 1)
        ux, uy = math.cos(a), math.sin(a)
        if i % 2 == 0:
            d = 1.6 * s
            p0, p1 = (x - ux * d, y - uy * d), (x + ux * d, y + uy * d)
            elo = L([p0, p1], 4.9 * s) - L([p0, p1], 1.7 * s)
            t.pintar(elo, linear(x - 4, y - 4, x + 4, y + 4, 1.0, 0.1), chanfro=0.2, esp=0.55)
        else:
            de_lado.append((x, y, ux, uy, s))
    for x, y, ux, uy, s in de_lado:
        d = 2.7 * s
        barra = L([(x - ux * d, y - uy * d), (x + ux * d, y + uy * d)], 1.9 * s)
        t.pintar(barra, linear(x - 3, y - 3, x + 3, y + 3, 0.95, 0.05), chanfro=0.15, esp=0.5)
    # ponta: virola de latão e cristal na direção do golpe
    xe, ye, ae = elos[-1]
    ux, uy = math.cos(ae), math.sin(ae)
    vx, vy = xe + ux * 3.4, ye + uy * 3.4
    vir = L([(xe + ux * 1.4, ye + uy * 1.4), (vx, vy)], 3.6)
    t.pintar(vir, linear(vx - 3, vy - 3, vx + 3, vy + 3), chanfro=0.15)
    h = 7.5
    kx, ky = vx + ux * (h + 0.6), vy + uy * (h + 0.6)
    t.luz_radial(kx, ky, 9.0, "C1", 0.25, glow=0.4, so_fundo=True)
    _esp_cristal(t, kx, ky, 3.8, h, ae + math.pi / 2)
    # estalo do chicote: arcos arcanos e faíscas na ponta
    ang = math.degrees(ae)
    _esp_arco_fade(t, kx, ky, 11.0, ang - 150, 110, 0.6, "C2", glow=1.0)
    _esp_arco_fade(t, kx, ky, 14.0, ang + 40, 100, 0.5, "C1", glow=1.0)
    for dx, dy, c in ((10, -9, 1.6), (13, 3, 1.2), (-4, -12, 1.3), (5, 11, 1.0)):
        t.estrela_c(kx + dx, ky + dy, c, ("WH", "C2", "C1", "C0"), glow=1.0)


# ---------- 17 · IX de Espadas · Tempestade de Faíscas ----------

def il_tempestade_faiscas(t):
    cx, cy = CX, 104.0
    # pedestal e coluna
    t.pintar(R(32, 136, 80, 142, 0.5), linear(32, 136, 80, 142), chanfro=0.18)
    t.pintar(R(40, 129, 72, 136, 0.4), cil_x(40, 72), chanfro=0.12)
    for x in (35, 76):
        t.rebite(x, 138)
    for x in (44, 67):
        t.rebite(x, 131)
    t.pintar(R(48, 110, 64, 130), cil_x(48, 64, 0.6, -0.02))
    for y in (118, 124):
        t.sulco_h(48.3, 63.7, y)
    # raios em leque (atrás do toro, saem da ponta do cristal)
    topo = (cx, 79.0)
    for k, ang in enumerate((-66, -33, 0, 33, 66)):
        comp = (34, 39, 42, 39, 34)[k]
        a = math.radians(ang)
        ux, uy = math.sin(a), -math.cos(a)
        p0 = (topo[0] + ux * 3, topo[1] + uy * 3)
        meio = 0.55
        pm0 = (p0[0] + ux * comp * meio, p0[1] + uy * comp * meio)
        pm1 = (p0[0] + ux * (comp * meio + 3.2), p0[1] + uy * (comp * meio + 3.2))
        p1 = (p0[0] + ux * comp, p0[1] + uy * comp)
        t.raio(_esp_zigue(p0, pm0, 3, 1.8, 17 + k), ponta=False, seed=31 + k)
        t.raio(_esp_zigue(pm1, p1, 3, 1.6, 47 + k), ponta=True, seed=61 + k)
        nx, ny = (pm0[0] + pm1[0]) / 2, (pm0[1] + pm1[1]) / 2
        t.estrela_c(nx, ny, 2.0, ("WH", "C2", "C1", "C0"), glow=1.0)
    # toro de cobre enrolado: metade de trás
    tor_cx, tor_cy = cx, cy
    fora_ = E(tor_cx, tor_cy, 29.0, 12.0)
    furo = E(tor_cx, tor_cy - 1.5, 15.0, 4.2)
    toro = fora_ - furo

    def luz_toro(X, Y):
        rho = np.hypot((X - tor_cx) / 22.0, (Y - tor_cy + 0.6) / 8.2)
        sg = np.clip((Y - tor_cy) / 3.0, -1, 1)
        v = perfil(0.5 + sg * (rho - 1.0) / 0.7) * 0.92
        return v + 0.1 * (tor_cx - X) / 29.0 - 0.04
    tras = toro & semiplano(0, 1, tor_cy - 0.5)
    frente = toro & semiplano(0, -1, -(tor_cy - 0.5))

    def espiras(meia, sinal):
        for i in range(36):
            ph = (i + 0.5) * math.tau / 36
            if (math.sin(ph) > 0) != (sinal > 0) and abs(math.sin(ph)) > 0.15:
                continue
            pi_ = (tor_cx + math.cos(ph) * 14.0, tor_cy - 1.5 + math.sin(ph) * 3.6)
            po = (tor_cx + math.cos(ph) * 30.0, tor_cy + math.sin(ph) * 13.0)
            t.fill(L([pi_, po], 0.5) & meia.cresce(-0.15), "B0", alfa=0.85)
            q0 = (pi_[0] + 0.55, pi_[1] + 0.25)
            q1 = (po[0] + 0.55, po[1] + 0.25)
            t.clarear(L([q0, q1], 0.35) & meia.cresce(-0.15), "B4", 0.25)
    t.pintar(tras, luz_toro, contorno=True, chanfro=0.12)
    espiras(tras, -1)
    # cristal saindo do furo
    t.luz_radial(cx, 92, 14.0, "C1", 0.22, glow=0.3, so_fundo=True)
    t.cristal(cx, 94.0, 5.5, 14.0)
    # metade da frente
    t.pintar(frente, luz_toro, contorno=True, chanfro=0.12)
    espiras(frente, 1)
    t.fill(arco(tor_cx, tor_cy - 1.5, 15.0, 15, 165, 0.5, 40, 4.2) & frente, "B0", alfa=0.8)
    # terminais e faíscas pequenas nas laterais do toro
    for sx in (-1, 1):
        bx = cx + sx * 29.5
        t.pintar(E(bx, cy, 2.4), esfera(bx, cy, 2.4))
        t.raio(_esp_zigue((bx + sx * 1.5, cy - 1.5), (bx + sx * 8, cy - 9), 3, 1.2, 90 + sx), ponta=False, seed=7 + sx)
        t.estrela_c(bx + sx * 8, cy - 9, 1.6, ("WH", "C2", "C1", "C0"), glow=1.0)


# ---------- 18 · X de Espadas · Martelo a Vapor ----------

_ESP_MARTELO_ANG = math.radians(30)     # o martelo inclinado: cabeça embaixo à esquerda, cabo subindo para a direita
_ESP_MARTELO_O = (46.0, 124.0)          # centro da cabeça


def _esp_mg(pts):
    return girar(pts, _ESP_MARTELO_O[0], _ESP_MARTELO_O[1], _ESP_MARTELO_ANG)


def il_martelo_vapor(t):
    g = _esp_mg
    ox, oy = _ESP_MARTELO_O
    ang = _ESP_MARTELO_ANG

    def loc(fn):
        return local(fn, ox, oy, ang)
    # rastros do golpe saindo da face de trás, pelo arco do balanço (pivô nas mãos)
    for d, comp, w in ((-7.5, 13.0, 0.8), (0.0, 17.0, 0.9), (7.5, 12.0, 0.8)):
        pts = []
        for k in range(15):
            fi = math.radians(comp * k / 14.0 * 0.62)
            x, y = -25.0, d + 86.0                          # relativo ao pivô (0, -86)
            pts.append((x * math.cos(fi) - y * math.sin(fi), x * math.sin(fi) + y * math.cos(fi) - 86.0))
        _esp_traco_fade(t, g(pts), w, "B2", 0.95, 0.0)
    # chão e rachaduras arcanas do impacto
    chao = R(14, 145, 98, 150, 0.5)
    t.pintar(chao, linear(14, 145, 98, 150, 0.72, 0.05), chanfro=0.18)
    for pts in ([(53, 145.6), (47, 147.6), (40, 146.5), (32, 148.8), (24, 147.6)],
                [(53, 145.6), (60, 148.0), (68, 146.6), (77, 149.0), (86, 147.8)],
                [(40, 146.5), (38, 150.5)], [(68, 146.6), (70, 150.5)]):
        _esp_fenda(t, pts, 0.7, dentro=chao)
    # cabo
    cabo = P(g([(-2.5, -9), (2.5, -9), (2.5, -84), (-2.5, -84)]))
    t.pintar(cabo, loc(cil_x(-2.5, 2.5)))
    pega = P(g([(-4.2, -63), (4.2, -63), (4.2, -85), (-4.2, -85)]))
    t.pintar(pega, loc(cil_x(-4.2, 4.2, 0.95)), chanfro=0.12)
    for k in range(6):
        tira = L(g([(-4.2, -64.5 - k * 3.6), (4.2, -66.5 - k * 3.6)]), 0.7)
        t.fill(tira & pega, "B0")
        t.clarear(mover(tira, 0, 0.6) & pega, "B4", 0.3)
    px_, py_ = g([(0, -88.2)])[0]
    t.pintar(E(px_, py_, 3.4), esfera(px_, py_, 3.4))
    t.fill(anel(px_, py_, 1.1, 1.8), "B0")
    # pistão no cabo
    t.pintar(P(g([(-1.8, -14), (1.8, -14), (1.8, -28), (-1.8, -28)])), loc(cil_x(-1.8, 1.8, 1.1, 0.05)))
    cil = P(g([(-6.5, -29), (6.5, -29), (6.5, -51), (-6.5, -51)]))
    t.pintar(cil, loc(cil_x(-6.5, 6.5)), chanfro=0.12)
    for yy in (-34.0, -46.5):
        t.fill(L(g([(-6.2, yy), (6.2, yy)]), 0.6) & cil, "B1")
        t.clarear(L(g([(-6.2, yy + 0.7), (6.2, yy + 0.7)]), 0.35) & cil, "B4", 0.3)
    for y0 in (-29.5, -54.0):
        fl = P(g([(-8.5, y0), (8.5, y0), (8.5, y0 + 3.5), (-8.5, y0 + 3.5)]))
        t.pintar(fl, loc(cil_x(-8.5, 8.5)), chanfro=0.15)
        for xx in (-6.2, 5.0):
            rx, ry = g([(xx, y0 + 1.0)])[0]
            t.rebite(int(rx), int(ry))
    vis = P(g([(-3.5, -43), (3.5, -43), (3.5, -37), (-3.5, -37)]))
    t.contorno(vis, "B1", "B0", 0.8)
    t.pintar(vis, loc(lambda X, Y: 0.15 + 0.62 * np.exp(-((Y + 40.3) / 1.6) ** 2)), CIANO, contorno=False)
    t.fill(L(g([(-2.0, -40.3), (2.0, -40.3)]), 0.6), "C2", glow=1.0)
    wx, wy = g([(-1.5, -40.8)])[0]
    t.fill(E(wx, wy, 0.5), "WH", glow=1.0)
    # válvula com escape de vapor
    t.pintar(P(g([(6.5, -42), (10.5, -42), (10.5, -38.5), (6.5, -38.5)])), loc(cil_y(-42, -38.5)))
    t.pintar(P(g([(10.0, -44.5), (13.5, -44.5), (13.5, -36.0), (10.0, -36.0)])), loc(cil_x(10, 13.5)), chanfro=0.12)
    vx, vy = g([(14.0, -42.0)])[0]
    t.vapor([(vx + 3.0, vy - 2.0, 2.0), (vx + 6.5, vy - 6.5, 2.8), (vx + 11.0, vy - 12.0, 3.6)])
    # cabeça de latão: bloco chanfrado e faces de bater largas nas pontas
    t.pintar(P(g([(-5.5, -9), (5.5, -9), (3.8, -15), (-3.8, -15)])), loc(cil_x(-5.5, 5.5)), chanfro=0.15)
    corpo = P(g([(-16, -9), (16, -9), (18, -7), (18, 7), (16, 9), (-16, 9), (-18, 7), (-18, -7)]))
    t.pintar(corpo, loc(cil_y(-9, 9, 0.95, 0.04)), chanfro=0.22)
    for xx in (-13.0, 13.0):
        t.fill(L(g([(xx, -8.4), (xx, 8.4)]), 0.6) & corpo, "B1")
        t.clarear(L(g([(xx + 0.7, -8.4), (xx + 0.7, 8.4)]), 0.35) & corpo, "B4", 0.3)
    for s_ in (-1, 1):
        x0, x1 = (17.0, 24.0) if s_ > 0 else (-24.0, -17.0)
        face = P(g([(x0, -9.5), (x0 + 1.2, -11), (x1 - 1.2, -11), (x1, -9.5), (x1, 9.5), (x1 - 1.2, 11),
                    (x0 + 1.2, 11), (x0, 9.5)]))
        t.pintar(face, loc(cil_y(-11, 11, 1.0, 0.03)), chanfro=0.22)
        xm = x1 - 2.2 if s_ > 0 else x0 + 2.2
        t.fill(L(g([(xm, -10.2), (xm, 10.2)]), 0.55) & face, "B1", alfa=0.9)
    for (xx, yy) in ((-10, -5.5), (10, -5.5), (-10, 5.5), (10, 5.5)):
        rx, ry = g([(xx, yy)])[0]
        t.rebite(int(rx), int(ry))
    # cristal no miolo da cabeça
    t.pintar(E(ox, oy, 7.6), esfera(ox, oy, 7.6, 0.95, 0.05))
    _esp_soquete(t, ox, oy, 5.6)
    _esp_cristal(t, ox, oy, 3.2, 5.4, ang, aura=False)
    # faíscas e nuvem de vapor do impacto, na frente da quina que bate
    for p0, p1 in (((58, 141), (46, 131)), ((65, 142), (79, 136)), ((62, 139), (66, 126)), ((54, 143), (40, 140))):
        t.raio_luz(p0, p1, "WH", "C1", 0.5, 0.4, glow=1.0, so_fundo=True)
        t.estrela_c(p1[0], p1[1], 1.3, ("WH", "C2", "C1", "C0"), glow=1.0)
    t.massa_vapor([(38.0, 149.5, 3.8), (45.0, 146.0, 5.0), (53.0, 142.5, 6.0), (62.0, 141.5, 6.5), (71.0, 144.0, 6.0),
                   (79.0, 147.5, 5.0), (86.0, 150.5, 3.6), (56.0, 149.5, 5.0), (67.0, 150.0, 5.0)])


# ---------- 31 · VII · O Carro de Vapor ----------

def il_carro_vapor(t):
    cx = CX
    # vapor da chaminé, puxado para trás pela corrida
    t.massa_vapor([(56.0, 37.0, 5.0), (46.0, 39.5, 4.5), (66.0, 39.5, 4.5), (37.0, 43.0, 3.6), (75.0, 43.0, 3.6),
                   (29.0, 47.0, 2.8), (83.0, 47.0, 2.8)])
    # chão e trilhos que fogem para o fundo
    t.pintar(R(12, 146, 100, 150, 0.5), linear(12, 146, 100, 150, 0.7, 0.05), chanfro=0.15)
    for sx in (-1, 1):
        for k in range(3):
            x0 = cx + sx * (14 + k * 12)
            _esp_traco_fade(t, [(x0 + sx * (k + 1) * 2.0 * f, 151 + 6 * f) for f in (0, 0.5, 1)], 0.6, "B1", 0.9, 0.3)
    # cabine atrás da caldeira, com runas nas laterais
    cab = R(30, 80, 82, 132, 1.0) | E(cx, 81.0, 26.0, 7.0)
    t.pintar(cab, linear(30, 74, 82, 132, 0.62, 0.0), chanfro=0.2)
    t.fill(anel(cx, 81.0, 22.5, 23.2, 5.2, 5.9) & semiplano(0, 1, 81), "B0", alfa=0.8)
    for x in range(34, 80, 6):                               # rebites na borda do teto
        dx = (x + 0.85 - cx) / 26.0
        t.rebite(x, int(81 - 7 * math.sqrt(max(0.0, 1 - dx * dx)) + 1.6))
    for x0 in (32.5, 75.5):
        painel = R(x0, 86, x0 + 5, 117, 0.6)
        t.fill(painel, lambda X, Y: rampa(("K", "G2"), np.full_like(X, 0.6)))
        t.contorno(painel, "B3", "B1", 0.55)
        for k in range(3):
            t.runa(x0 + 1, 88.5 + k * 9.5, k + (0 if x0 < 50 else 2))
    # chaminé em funil
    t.pintar(R(51, 52, 61, 80), cil_x(51, 61))
    t.pintar(P([(45, 45), (67, 45), (62, 53), (50, 53)]), cil_x(45, 67), chanfro=0.15)
    t.pintar(R(44, 43, 68, 46.5, 0.5), cil_x(44, 68), chanfro=0.12)
    t.pintar(R(49, 64, 63, 67.5, 0.4), cil_x(49, 63), chanfro=0.12)
    # rodas dentadas enormes dos lados (vistas de quina)
    for wx in (23.0, 89.0):
        wy = 114.0
        roda = P(_esp_engr_pts(wx, wy, 23.0, 18, 4.0, fase=0.25, sx=0.36))
        t.pintar(roda, linear(wx - 10, wy - 27, wx + 10, wy + 27, 1.0, 0.06), chanfro=0.2)
        t.fill(E(wx, wy, 6.2, 19.0) - E(wx, wy, 5.4, 18.0), "B0", alfa=0.9)
        t.pintar(E(wx, wy, 3.2, 7.0), esfera(wx, wy, 5.0, 1.0, 0.05))
        for k in range(6):
            a = k * math.tau / 6 + 0.3
            p1 = (wx + math.cos(a) * 5.2, wy + math.sin(a) * 17.0)
            t.fill(L([(wx + math.cos(a) * 1.6, wy + math.sin(a) * 5.5), p1], 1.0), "B1", alfa=0.9)
        t.fill(E(wx, wy, 1.2, 2.4), "B0")
        # poeira e vapor nos pés das rodas
        sx = -1 if wx < cx else 1
        t.vapor([(wx + sx * 8, 141.0, 3.2), (wx + sx * 12.5, 137.0, 2.4)])
    # caldeira vista de frente: tampa redonda com o farol de cristal
    by_ = 102.0
    t.pintar(E(cx, by_, 18.0), esfera(cx, by_, 18.0, 1.0, 0.04))
    t.fill(anel(cx, by_, 14.6, 15.3), "B0")
    t.pintar(anel(cx, by_, 10.0, 14.6), esfera(cx, by_, 14.6, 0.9, 0.0), contorno=False, chanfro=0.15)
    for i in range(14):
        a = i * math.tau / 14
        t.rebite(int(cx - 0.5 + math.cos(a) * 16.6), int(by_ - 0.5 + math.sin(a) * 16.6))
    for i in range(16):                                    # facho do farol
        a = i * math.tau / 16 + 0.1
        r0, r1 = 10.5, (15.0 if i % 2 == 0 else 13.0)
        t.raio_luz((cx + math.cos(a) * r0, by_ + math.sin(a) * r0), (cx + math.cos(a) * r1, by_ + math.sin(a) * r1),
                   "C2", "C1", 0.38, 0.4, glow=0.8, so_fundo=False)
    _esp_soquete(t, cx, by_, 8.5)
    t.luz_radial(cx, by_, 7.0, "C1", 0.35, glow=0.5)
    t.cristal(cx, by_, 4.6, 8.0, aura=False)
    # limpa-trilhos e para-choques
    lt = P([(37, 121), (75, 121), (68, 145), (44, 145)])
    t.pintar(lt, linear(37, 121, 75, 145, 0.85, 0.05), chanfro=0.18)
    for k in range(7):
        xa = 40.0 + k * 5.333
        xb = 46.0 + k * 3.333
        t.fill(L([(xa, 124.5), (xb, 143.5)], 0.75) & lt, "B0")
        t.clarear(L([(xa + 0.7, 124.5), (xb + 0.7, 143.5)], 0.3) & lt, "B4", 0.3)
    t.pintar(R(36, 119, 76, 123.5, 0.5), cil_y(119, 123.5), chanfro=0.12)
    for sx in (-1, 1):
        t.pintar(E(cx + sx * 21, 121.0, 3.2), esfera(cx + sx * 21, 121.0, 3.2))


# ---------- 32 · XIII · A Ceifadora de Engrenagens ----------

def _esp_rachadura(p0, ang, comp, seed, passo=3.2, ramos=True):
    """Rachadura no ar: trechos curtos com desvios e ramos menores. Devolve [(polilinha, largura)]."""
    rnd = random.Random(seed)
    out = []

    def cresce(p, a, c, larg, nivel):
        pts = [p]
        x, y = p
        while c > 0:
            a += math.radians(rnd.uniform(-38, 38))
            s = min(c, passo * rnd.uniform(0.7, 1.3))
            x, y = x + math.cos(a) * s, y + math.sin(a) * s
            pts.append((x, y))
            c -= s
            if ramos and nivel < 2 and c > 4 and rnd.random() < 0.3:
                cresce((x, y), a + math.radians(rnd.choice((-1, 1)) * rnd.uniform(35, 60)), c * 0.45, larg * 0.7,
                       nivel + 1)
        out.append((pts, larg))
    cresce(p0, ang, comp, 0.95, 0)
    return out


def il_ceifadora_engrenagens(t):
    # rachaduras de faísca no ar em volta (o golpe corta o próprio ar)
    for p0, ang, comp, seed in (((80, 50), -25, 24, 3), ((88, 84), 35, 13, 5), ((84, 120), 55, 22, 8),
                                ((24, 118), 125, 24, 11), ((20, 58), -150, 14, 13), ((42, 140), 150, 18, 17)):
        for pts, larg in _esp_rachadura(p0, math.radians(ang), comp, seed):
            _esp_fenda(t, pts, larg)
    # cabo (gadanha) levemente inclinado
    h0, h1 = (75.0, 157.0), (60.0, 54.0)
    ang = math.atan2(h0[1] - h1[1], h0[0] - h1[0]) - math.pi / 2
    cabo = L([h0, h1], 4.2)
    t.pintar(cabo, local(cil_x(-2.1, 2.1), (h0[0] + h1[0]) / 2, (h0[1] + h1[1]) / 2, ang))
    for f in (0.2, 0.52, 0.88):                             # anéis de latão no cabo
        x, y = h1[0] + (h0[0] - h1[0]) * f, h1[1] + (h0[1] - h1[1]) * f
        t.pintar(L([(x - 3.3, y + 0.5), (x + 3.3, y - 0.5)], 2.2), linear(x - 3, y - 2, x + 3, y + 2), chanfro=0.12)
    mx, my = h1[0] + (h0[0] - h1[0]) * 0.62, h1[1] + (h0[1] - h1[1]) * 0.62      # manete
    t.pintar(L([(mx, my), (mx + 9.5, my - 3.5)], 2.6), linear(mx, my - 5, mx + 10, my + 2), chanfro=0.12)
    t.pintar(E(mx + 10.2, my - 3.8, 2.2), esfera(mx + 10.2, my - 3.8, 2.2))
    t.pintar(E(h0[0], h0[1] + 0.5, 2.8), esfera(h0[0], h0[1] + 0.5, 2.8))
    # lâmina: lua de engrenagem com dentes de serra no dorso
    bcx, bcy, br = 60.0, 95.0, 44.0
    a0, a1 = -84.0, -183.0
    nd = 17

    def ang_u(u):
        return math.radians(a0 + (a1 - a0) * u)

    def larg(u):
        return 15.5 * (1 - u) ** 0.7 + 0.2

    def miolo(u, extra=0.0):                                # borda de dentro (fio) e a sua curva
        a = ang_u(u)
        rr = br - larg(u) + extra
        return (bcx + math.cos(a) * rr + 3.0 * u, bcy + math.sin(a) * rr + 4.0 * u)
    fora_ = []
    for i in range(nd * 6 + 1):
        u = i / (nd * 6)
        k = i % 6
        hd = 4.6 * (1 - 0.6 * u)
        rr = br + hd * (k / 5.0)
        a = ang_u(u)
        fora_.append((bcx + math.cos(a) * rr, bcy + math.sin(a) * rr))
        if k == 5 and i < nd * 6:
            fora_.append((bcx + math.cos(a) * br, bcy + math.sin(a) * br))
    dentro = [miolo(i / 60) for i in range(61)]
    lam = P(fora_[::-1] + dentro)

    def luz_lamina(X, Y):
        rho = np.hypot(X - bcx, Y - bcy)
        v = 0.4 + 0.52 * smooth(br - 14, br - 3, rho)
        return v + 0.1 * np.clip((bcx - X) / 30, -1, 1)
    t.pintar(lam, luz_lamina, chanfro=0.22)
    t.fill(L([miolo(i / 40, 0.9) for i in range(41)], 0.55) & lam, "B4", alfa=0.85)     # fio afiado
    t.fill(arco(bcx, bcy, br - 1.1, a1 + 6, a0, 0.5) & lam, "B1", alfa=0.8)            # sulco do dorso
    for u in (0.1, 0.27, 0.44, 0.6):                        # furos de alívio de engrenagem, com luz arcana
        a = ang_u(u)
        rr = br - larg(u) / 2 - 1.0
        hx, hy = bcx + math.cos(a) * rr + 1.5 * u, bcy + math.sin(a) * rr + 2.0 * u
        rh = 2.6 * (1 - u * 0.6)
        t.fill(E(hx, hy, rh), "K")
        t.fill((E(hx, hy, rh + 0.6) - E(hx, hy, rh)) & semiplano(0, -1, -hy - 0.3), "B3", alfa=0.9)
        t.luz_radial(hx, hy, rh * 0.9, "C1", 0.55, dentro=E(hx, hy, rh), glow=0.7)
        t.fill(E(hx - rh * 0.25, hy - rh * 0.25, rh * 0.28), "C2", glow=1.0)
    # faíscas no fio da lâmina
    for u in (0.3, 0.58, 0.84):
        x, y = miolo(u)
        t.estrela_c(x + 0.5, y + 0.8, 1.8, ("WH", "C2", "C1", "C0"), glow=1.0)
    # cubo de engrenagem no topo do cabo, com o cristal
    hx, hy = 61.0, 55.0
    cubo = P(_esp_engr_pts(hx, hy, 8.0, 10, 2.8, fase=0.1))
    t.pintar(cubo, linear(hx - 11, hy - 11, hx + 11, hy + 11, 1.0, 0.08), chanfro=0.2)
    _esp_soquete(t, hx, hy, 5.6)
    t.cristal(hx, hy, 3.2, 5.4)
    # dentes partidos caindo
    for (x, y, a) in ((26, 132, 25), (33, 146, -30), (44, 140, 60)):
        d = P(girar([(-2.0, 1.6), (-1.2, -1.6), (1.2, -1.6), (2.0, 1.6)], x, y, math.radians(a)))
        t.pintar(d, linear(x - 2, y - 2, x + 2, y + 2), chanfro=0.15, esp=0.5)
        for k in range(3):
            yy = y - 4.0 - k * 2.2
            t.fill(L([(x, yy), (x, yy - 0.9)], 0.45), "B1" if k == 0 else "B0", alfa=1 - k * 0.25, so_fundo=True)


# ---------- 33 · XVII · A Estrela de Cristal ----------

def il_estrela_cristal(t):
    cx, cy = CX, 94.0
    # brilho e raios finos atrás
    t.luz_radial(cx, cy, 30.0, "C0", 0.35, glow=0.2, so_fundo=True)
    for i in range(16):
        a = i * math.tau / 16 + math.tau / 32
        r0, r1 = 12, (27 if i % 2 == 0 else 22)
        t.raio_luz((cx + math.cos(a) * r0, cy + math.sin(a) * r0), (cx + math.cos(a) * r1, cy + math.sin(a) * r1),
                   "C1", "C0", 0.4, 0.3, glow=0.7)
    # engrenagens pequenas nas diagonais
    peq = []
    for k in range(4):
        a = math.radians(-45 + k * 90)
        gx, gy = cx + math.cos(a) * 33.5, cy + math.sin(a) * 33.5
        peq.append((gx, gy))
        g_ = P(_esp_engr_pts(gx, gy, 4.0, 8, 1.8, fase=0.1 * k))
        t.pintar(g_, linear(gx - 6, gy - 6, gx + 6, gy + 6, 1.0, 0.08), chanfro=0.18, esp=0.5)
        t.fill(E(gx, gy, 1.6), "K")
        t.fill(E(gx, gy, 0.9), "C2", glow=1.0)
        t.fill(E(gx - 0.3, gy - 0.3, 0.4), "WH", glow=1.0)
    # pontas de cristal: quatro longas nos eixos, quatro curtas nas diagonais
    pontas = []
    for k in range(8):
        a = k * math.tau / 8                                # 0 = para cima, horário
        longo = k % 2 == 0
        h = 15.5 if longo else 9.5
        r = 7.0 + h
        w = 4.6 if longo else 3.3
        px_, py_ = cx + math.sin(a) * r, cy - math.cos(a) * r
        _esp_cristal(t, px_, py_, w, h, a, aura=True, esp=0.55, cintila=longo)
        pontas.append((cx + math.sin(a) * (r + h), cy - math.cos(a) * (r + h)))
    # raios saltando entre as pontas longas e as engrenagens pequenas
    for k in range(4):
        tl = pontas[2 * k]
        for j in (k - 1, k):
            g = peq[j % 4]
            ga = math.atan2(g[1] - tl[1], g[0] - tl[0])
            p0 = (tl[0] + math.cos(ga) * 1.5, tl[1] + math.sin(ga) * 1.5)
            p1 = (g[0] - math.cos(ga) * 6.0, g[1] - math.sin(ga) * 6.0)
            t.raio(_esp_zigue(p0, p1, 4, 1.4, 100 + k * 7 + j), ponta=False, seed=200 + k * 7 + j)
    # cubo: engrenagem com o núcleo de cristal
    eng = P(_esp_engr_pts(cx, cy, 8.5, 12, 2.4, fase=0.0))
    t.pintar(eng, linear(cx - 11, cy - 11, cx + 11, cy + 11, 1.05, 0.06), chanfro=0.2)
    _esp_soquete(t, cx, cy, 6.0)
    t.cristal(cx, cy, 3.4, 5.6, aura=False)
    for p in pontas[::2]:
        t.estrela_c(p[0], p[1], 2.4, ("WH", "C2", "C1", "C0"), glow=1.0)
