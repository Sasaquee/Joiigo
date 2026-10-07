# Fase 10 (D-088), grupo G1: espadas e arcanos maiores, na grade de pixel.
# Executado dentro de card_pixel.py (mesmo espaço de nomes: R, E, P, L, B, BL, Tela, cil_x, esfera...).
# Serve de mapa de ocupação das estrelas e de lista de cartas: as formas ocupam as mesmas coordenadas da versão
# em alta (hd_espadas.py), desenhadas de um jeito mais simples. Ajudantes com prefixo _esp_ (outros grupos rodam aqui).


# ---------- ajudantes (os mesmos pontos da versão em alta) ----------

def _esp_spline(pts, n=10):
    """Catmull-Rom pelos pontos (cópia do spline() do card_hd.py)."""
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


def _esp_engr_pts(cx, cy, r, dentes, h, fase=0.0, cheio=0.5, ponta=None, serra=False, sx=1.0, sy=1.0):
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
    fora_, dentro = [], []
    for i in range(n + 1):
        u = i / n
        a = math.radians(a0 + span * u)
        wd = w * (u / 0.8) ** 1.4 if u < 0.8 else w * (1 - u) / 0.2
        fora_.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
        dentro.append((cx + math.cos(a) * (r - wd), cy + math.sin(a) * (r - wd)))
    return P(fora_ + dentro[::-1])


def _esp_u(x, y, cx, cy, a0, span):
    a = math.degrees(math.atan2(y + 0.5 - cy, x + 0.5 - cx))
    return max(0.0, min(1.0, ((a - a0) % 360.0) / span))


def _esp_amostrar(pts, passo):
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


def _esp_zigue(p0, p1, n, amp, seed):
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


def _esp_rachadura(p0, ang, comp, seed, passo=3.2, ramos=True):
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


def _esp_ip(pts):
    return [(int(round(x - 0.5)), int(round(y - 0.5))) for x, y in pts]


def _esp_linha(t, pts, c, so_fundo=True, passo=1):
    for i, (x, y) in enumerate(BL(_esp_ip(pts))):
        if i % passo == 0 and (not so_fundo or t.fundo_em(x, y)):
            t.put(x, y, c)


def _esp_arco(t, cx, cy, r, a0, span, c="B2", u0=0.3, ry=None):
    """Arco de movimento pontilhado (só aparece da cauda para a cabeça)."""
    ry = r if ry is None else ry
    n = max(8, int(abs(span) / 360 * math.tau * r))
    for i in range(n + 1):
        u = i / n
        if u < u0 or (u < 0.65 and i % 2):
            continue
        a = math.radians(a0 + span * u)
        x, y = int(cx + math.cos(a) * r - 0.5 + 0.5), int(cy + math.sin(a) * ry)
        if t.fundo_em(x, y):
            t.put(x, y, c)


def _esp_cristal(t, cx, cy, w, h, ang=0.0, pal=None, aura=True):
    """Cristal girado (ponta para ang; 0 = para cima): faces da esquerda claras, da direita escuras."""
    pal = pal or t.arc
    ca, sa = math.cos(ang), math.sin(ang)

    def g(x, y):
        return (cx + x * ca - y * sa, cy + x * sa + y * ca)
    ys, yi = -h * 0.42, h * 0.45
    m = P([g(0, -h), g(w, ys), g(w, yi), g(0, h), g(-w, yi), g(-w, ys)])
    for x, y in fora(m):
        t.put(x, y, pal[0])
    for x, y in m:
        px, py = x + 0.5 - cx, y + 0.5 - cy
        lx = px * ca + py * sa
        ly = -px * sa + py * ca
        desce = max(0.0, (ly - ys) / (h - ys))
        v = (0.86 - 0.4 * desce) if lx < -0.4 else ((0.9 - 0.3 * desce) if lx < 0.6 else (0.45 - 0.3 * desce))
        t.put(x, y, pal[quant(v, x, y, 4, 0.7)])
    gx, gy = g(-w * 0.4, -h * 0.55)
    t.put(int(gx), int(gy), pal[3])
    if aura:
        t.halo_arcano(m)
    return m


def _esp_soquete(t, cx, cy, r):
    soq = E(cx, cy, r)
    t.fill(soq, "K")
    for x, y in borda(soq):
        t.put(x, y, "B0" if x + y < cx + cy else "B3")
    for x, y in soq - borda(soq):
        if bayer(x, y) < 0.3:
            t.put(x, y, t.arc[0])
    return soq


def _esp_fenda(t, pts, dentro=None):
    """Rachadura de faísca: fio ciano com reflexo branco e aura."""
    m = set(BL(_esp_ip(pts)))
    if dentro is not None:
        m &= dentro
    for i, (x, y) in enumerate(sorted(m)):
        t.put(x, y, "WH" if i % 4 == 0 else "C2")
    t.halo_arcano(m, 2, 85)


def _esp_estrela_c(t, x, y, tam=1):
    t.estrela(int(x), int(y), tam, ("WH", "C2", "C1", "C0"))


# ---------- 15 · II de Espadas · Giro de Engrenagem ----------

def il_giro_engrenagem(t):
    cx, cy = CX, 96.0
    for k in range(3):
        a0 = -150 + k * 120
        cres = _esp_crescente(cx, cy, 40.0, a0, 100, 6.0)
        t.pintar(cres, lambda x, y, a0=a0: 0.18 + 0.78 * _esp_u(x, y, cx, cy, a0, 100) ** 1.2, chanfro=0.15)
        _esp_arco(t, cx, cy, 39.6, a0 + 30, 64, "C2", 0.5)
        ha = math.radians(a0 + 100)
        _esp_estrela_c(t, cx + math.cos(ha) * 39.7, cy + math.sin(ha) * 39.7, 2)
    for k in range(3):
        a0 = -115 + k * 120
        _esp_arco(t, cx, cy, 30.8, a0, 75, "B2")
        _esp_arco(t, cx, cy, 33.0, a0 + 25, 55, "B1")
    luz = linear(cx - 27, cy - 27, cx + 27, cy + 27, 1.05, 0.05)
    raios = set()
    for i in range(5):
        a = math.radians(-90 + i * 72)
        pts = [(cx + math.cos(a + b) * rr, cy + math.sin(a + b) * rr) for rr, b in ((8.5, 0.0), (12.5, 0.2), (16.5, 0.45))]
        raios |= L(_esp_spline(pts, 6), 3.6)
    t.pintar(raios, luz, chanfro=0.18)
    aro = P(_esp_engr_pts(cx, cy, 21.0, 10, 5.0, fase=0.12)) - E(cx, cy, 15.8)
    t.pintar(aro, luz, chanfro=0.2)
    for x, y in borda(E(cx, cy, 18.2)):
        if (x, y) in aro:
            t.put(x, y, "B0")
    for i in range(10):
        a = (i + 0.12 + 0.5) * math.tau / 10
        t.rebite(int(cx - 0.5 + math.cos(a) * 19.9), int(cy - 0.5 + math.sin(a) * 19.9))
    t.pintar(E(cx, cy, 10.0), esfera(cx, cy, 10.0, 0.95, 0.05))
    _esp_soquete(t, cx, cy, 7.0)
    t.cristal(cx, cy, 4.0, 7.0, aura=False)


# ---------- 16 · IV de Espadas · Chicote de Corrente ----------

def il_chicote_corrente(t):
    ctrl = [(46, 123), (60, 120), (72, 112), (76, 100), (68, 90), (52, 85), (38, 79), (33, 68), (39, 58), (51, 53),
            (61, 50), (67, 46)]
    cam = _esp_spline(ctrl, 16)
    for i0, i1, lado in ((24, 70, 1), (90, 140, -1)):
        tr = []
        for i in range(i0, i1):
            (ax, ay), (bx, by) = cam[i], cam[i + 1]
            ll = math.hypot(bx - ax, by - ay) or 1
            nx, ny = -(by - ay) / ll * lado, (bx - ax) / ll * lado
            tr.append((ax + nx * 6.0, ay + ny * 6.0))
        _esp_linha(t, tr[len(tr) // 3:], "B2")
    cabo = L([(44.5, 126), (38.5, 150)], 5.6)
    t.pintar(cabo, local(cil_x(-2.8, 2.8), 41.5, 138, math.atan2(24, -6) - math.pi / 2), chanfro=0.12)
    for k in range(6):
        y = 129 + k * 3.6
        x = 44.5 - (y - 126) * 0.25
        for p in BL(_esp_ip([(x - 3.2, y + 0.4), (x + 3.0, y - 1.0)])):
            if p in cabo:
                t.put(*p, "B0")
    t.pintar(L([(39.0, 124.2), (51.0, 127.2)], 2.6), linear(38, 122, 52, 129), chanfro=0.15)
    for x, y in ((38.6, 124.1), (51.4, 127.3)):
        t.pintar(E(x, y, 1.9), esfera(x, y, 1.9))
    t.pintar(E(37.8, 153.0, 3.6), esfera(37.8, 153.0, 3.6))
    t.put(37, 152, "C2")
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
            t.pintar(elo, linear(x - 4, y - 4, x + 4, y + 4, 1.0, 0.1), chanfro=0.2)
        else:
            de_lado.append((x, y, ux, uy, s))
    for x, y, ux, uy, s in de_lado:
        d = 2.7 * s
        t.pintar(L([(x - ux * d, y - uy * d), (x + ux * d, y + uy * d)], 1.9 * s),
                 linear(x - 3, y - 3, x + 3, y + 3, 0.95, 0.05), chanfro=0.15)
    xe, ye, ae = elos[-1]
    ux, uy = math.cos(ae), math.sin(ae)
    vx, vy = xe + ux * 3.4, ye + uy * 3.4
    t.pintar(L([(xe + ux * 1.4, ye + uy * 1.4), (vx, vy)], 3.6), linear(vx - 3, vy - 3, vx + 3, vy + 3), chanfro=0.15)
    h = 7.5
    kx, ky = vx + ux * (h + 0.6), vy + uy * (h + 0.6)
    _esp_cristal(t, kx, ky, 3.8, h, ae + math.pi / 2)
    ang = math.degrees(ae)
    _esp_arco(t, kx, ky, 11.0, ang - 150, 110, "C2")
    _esp_arco(t, kx, ky, 14.0, ang + 40, 100, "C1")
    for dx, dy, tam in ((10, -9, 1), (13, 3, 1), (-4, -12, 1), (5, 11, 0)):
        _esp_estrela_c(t, kx + dx, ky + dy, tam)


# ---------- 17 · IX de Espadas · Tempestade de Faíscas ----------

def il_tempestade_faiscas(t):
    cx, cy = CX, 104.0
    t.pintar(R(32, 136, 80, 142), linear(32, 136, 80, 142), chanfro=0.18)
    t.pintar(R(40, 129, 72, 136), cil_x(40, 72), chanfro=0.12)
    for x in (35, 76):
        t.rebite(x, 138)
    for x in (44, 67):
        t.rebite(x, 131)
    t.pintar(R(48, 110, 64, 130), cil_x(48, 64, 0.6, -0.02))
    topo = (cx, 79.0)
    for k, ang in enumerate((-66, -33, 0, 33, 66)):
        comp = (34, 39, 42, 39, 34)[k]
        a = math.radians(ang)
        ux, uy = math.sin(a), -math.cos(a)
        p0 = (topo[0] + ux * 3, topo[1] + uy * 3)
        pm0 = (p0[0] + ux * comp * 0.55, p0[1] + uy * comp * 0.55)
        pm1 = (p0[0] + ux * (comp * 0.55 + 3.2), p0[1] + uy * (comp * 0.55 + 3.2))
        p1 = (p0[0] + ux * comp, p0[1] + uy * comp)
        t.raio(_esp_ip(_esp_zigue(p0, pm0, 3, 1.8, 17 + k)), ponta=False)
        t.raio(_esp_ip(_esp_zigue(pm1, p1, 3, 1.6, 47 + k)), ponta=True)
        _esp_estrela_c(t, (pm0[0] + pm1[0]) / 2, (pm0[1] + pm1[1]) / 2, 1)
    toro = E(cx, cy, 29.0, 12.0) - E(cx, cy - 1.5, 15.0, 4.2)

    def luz_toro(x, y):
        rho = math.hypot((x + 0.5 - cx) / 22.0, (y + 0.5 - cy + 0.6) / 8.2)
        sg = max(-1.0, min(1.0, (y + 0.5 - cy) / 3.0))
        return perfil(0.5 + sg * (rho - 1.0) / 0.7) * 0.92 + 0.1 * (cx - x - 0.5) / 29.0 - 0.04
    tras = {p for p in toro if p[1] + 0.5 < cy - 0.5}
    frente = toro - tras
    t.pintar(tras, luz_toro, chanfro=0.12)
    t.cristal(cx, 94.0, 5.5, 14.0)
    t.pintar(frente, luz_toro, chanfro=0.12)
    for i in range(36):                                    # espiras
        ph = (i + 0.5) * math.tau / 36
        pi_ = (cx + math.cos(ph) * 14.0, cy - 1.5 + math.sin(ph) * 3.6)
        po = (cx + math.cos(ph) * 30.0, cy + math.sin(ph) * 13.0)
        meia = frente if math.sin(ph) > 0 else tras
        for p in BL(_esp_ip([pi_, po])):
            if p in meia:
                t.put(*p, "B1")
    for sx in (-1, 1):
        bx = cx + sx * 29.5
        t.pintar(E(bx, cy, 2.4), esfera(bx, cy, 2.4))
        t.raio(_esp_ip(_esp_zigue((bx + sx * 1.5, cy - 1.5), (bx + sx * 8, cy - 9), 3, 1.2, 90 + sx)), ponta=False)
        _esp_estrela_c(t, bx + sx * 8, cy - 9, 1)


# ---------- 18 · X de Espadas · Martelo a Vapor ----------

_ESP_MARTELO_ANG = math.radians(30)
_ESP_MARTELO_O = (46.0, 124.0)


def il_martelo_vapor(t):
    ox, oy = _ESP_MARTELO_O
    ang = _ESP_MARTELO_ANG

    def g(pts):
        return girar(pts, ox, oy, ang)

    def loc(fn):
        return local(fn, ox, oy, ang)
    for d, comp in ((-7.5, 13.0), (0.0, 17.0), (7.5, 12.0)):     # rastros do golpe
        pts = []
        for k in range(15):
            fi = math.radians(comp * k / 14.0 * 0.62)
            x, y = -25.0, d + 86.0
            pts.append((x * math.cos(fi) - y * math.sin(fi), x * math.sin(fi) + y * math.cos(fi) - 86.0))
        _esp_linha(t, g(pts)[:9], "B2")
    chao = R(14, 145, 98, 150)
    t.pintar(chao, linear(14, 145, 98, 150, 0.72, 0.05), chanfro=0.18)
    for pts in ([(53, 145.6), (47, 147.6), (40, 146.5), (32, 148.8), (24, 147.6)],
                [(53, 145.6), (60, 148.0), (68, 146.6), (77, 149.0), (86, 147.8)]):
        _esp_fenda(t, pts, dentro=chao)
    cabo = P(g([(-2.5, -9), (2.5, -9), (2.5, -84), (-2.5, -84)]))
    t.pintar(cabo, loc(cil_x(-2.5, 2.5)))
    t.pintar(P(g([(-4.2, -63), (4.2, -63), (4.2, -85), (-4.2, -85)])), loc(cil_x(-4.2, 4.2, 0.95)), chanfro=0.12)
    px_, py_ = g([(0, -88.2)])[0]
    t.pintar(E(px_, py_, 3.4), esfera(px_, py_, 3.4))
    t.pintar(P(g([(-6.5, -29), (6.5, -29), (6.5, -51), (-6.5, -51)])), loc(cil_x(-6.5, 6.5)), chanfro=0.12)
    for y0 in (-29.5, -54.0):
        t.pintar(P(g([(-8.5, y0), (8.5, y0), (8.5, y0 + 3.5), (-8.5, y0 + 3.5)])), loc(cil_x(-8.5, 8.5)), chanfro=0.15)
    t.fill(P(g([(-3.5, -43), (3.5, -43), (3.5, -37), (-3.5, -37)])), "C1")
    t.pintar(P(g([(10.0, -44.5), (13.5, -44.5), (13.5, -36.0), (10.0, -36.0)])), loc(cil_x(10, 13.5)), chanfro=0.12)
    vx, vy = g([(14.0, -42.0)])[0]
    t.vapor([(vx + 3.0, vy - 2.0, 2.0), (vx + 6.5, vy - 6.5, 2.8), (vx + 11.0, vy - 12.0, 3.6)])
    t.pintar(P(g([(-5.5, -9), (5.5, -9), (3.8, -15), (-3.8, -15)])), loc(cil_x(-5.5, 5.5)), chanfro=0.15)
    corpo = P(g([(-16, -9), (16, -9), (18, -7), (18, 7), (16, 9), (-16, 9), (-18, 7), (-18, -7)]))
    t.pintar(corpo, loc(cil_y(-9, 9, 0.95, 0.04)), chanfro=0.22)
    for x0, x1 in ((17.0, 24.0), (-24.0, -17.0)):
        face = P(g([(x0, -9.5), (x0 + 1.2, -11), (x1 - 1.2, -11), (x1, -9.5), (x1, 9.5), (x1 - 1.2, 11),
                    (x0 + 1.2, 11), (x0, 9.5)]))
        t.pintar(face, loc(cil_y(-11, 11, 1.0, 0.03)), chanfro=0.22)
    t.pintar(E(ox, oy, 7.6), esfera(ox, oy, 7.6, 0.95, 0.05))
    _esp_soquete(t, ox, oy, 5.6)
    _esp_cristal(t, ox, oy, 3.2, 5.4, ang, aura=False)
    for p0, p1 in (((58, 141), (46, 131)), ((65, 142), (79, 136)), ((62, 139), (66, 126)), ((54, 143), (40, 140))):
        _esp_linha(t, [p0, p1], "C2")
        _esp_estrela_c(t, p1[0], p1[1], 1)
    t.massa_vapor([(38.0, 149.5, 3.8), (45.0, 146.0, 5.0), (53.0, 142.5, 6.0), (62.0, 141.5, 6.5), (71.0, 144.0, 6.0),
                   (79.0, 147.5, 5.0), (86.0, 150.5, 3.6), (56.0, 149.5, 5.0), (67.0, 150.0, 5.0)])


# ---------- 31 · VII · O Carro de Vapor ----------

def il_carro_vapor(t):
    cx = CX
    t.massa_vapor([(56.0, 37.0, 5.0), (46.0, 39.5, 4.5), (66.0, 39.5, 4.5), (37.0, 43.0, 3.6), (75.0, 43.0, 3.6),
                   (29.0, 47.0, 2.8), (83.0, 47.0, 2.8)])
    t.pintar(R(12, 146, 100, 150), linear(12, 146, 100, 150, 0.7, 0.05), chanfro=0.15)
    cab = R(30, 80, 82, 132) | E(cx, 81.0, 26.0, 7.0)
    t.pintar(cab, linear(30, 74, 82, 132, 0.62, 0.0), chanfro=0.2)
    for x0 in (32.5, 75.5):
        painel = R(x0, 86, x0 + 5, 117)
        t.fill(painel, "K")
        t.contorno(painel, "B3", "B1")
        for k in range(3):
            t.runa(int(x0 + 1), int(88.5 + k * 9.5), k + (0 if x0 < 50 else 2))
    t.pintar(R(51, 52, 61, 80), cil_x(51, 61))
    t.pintar(P([(45, 45), (67, 45), (62, 53), (50, 53)]), cil_x(45, 67), chanfro=0.15)
    t.pintar(R(44, 43, 68, 46.5), cil_x(44, 68), chanfro=0.12)
    t.pintar(R(49, 64, 63, 67.5), cil_x(49, 63), chanfro=0.12)
    for wx in (23.0, 89.0):
        wy = 114.0
        roda = P(_esp_engr_pts(wx, wy, 23.0, 18, 4.0, fase=0.25, sx=0.36))
        t.pintar(roda, linear(wx - 10, wy - 27, wx + 10, wy + 27, 1.0, 0.06), chanfro=0.2)
        for x, y in borda(E(wx, wy, 6.2, 19.0)):
            t.put(x, y, "B0")
        t.pintar(E(wx, wy, 3.2, 7.0), esfera(wx, wy, 5.0, 1.0, 0.05))
        sx = -1 if wx < cx else 1
        t.vapor([(wx + sx * 8, 141.0, 3.2), (wx + sx * 12.5, 137.0, 2.4)])
    by_ = 102.0
    t.pintar(E(cx, by_, 18.0), esfera(cx, by_, 18.0, 1.0, 0.04))
    for x, y in borda(E(cx, by_, 15.3)):
        t.put(x, y, "B0")
    for i in range(14):
        a = i * math.tau / 14
        t.rebite(int(cx - 0.5 + math.cos(a) * 16.6), int(by_ - 0.5 + math.sin(a) * 16.6))
    for i in range(16):
        a = i * math.tau / 16 + 0.1
        r1 = 15.0 if i % 2 == 0 else 13.0
        _esp_linha(t, [(cx + math.cos(a) * 10.5, by_ + math.sin(a) * 10.5), (cx + math.cos(a) * r1, by_ + math.sin(a) * r1)],
                   "C1", so_fundo=False)
    _esp_soquete(t, cx, by_, 8.5)
    t.cristal(cx, by_, 4.6, 8.0, aura=False)
    lt = P([(37, 121), (75, 121), (68, 145), (44, 145)])
    t.pintar(lt, linear(37, 121, 75, 145, 0.85, 0.05), chanfro=0.18)
    for k in range(7):
        for p in BL(_esp_ip([(40.0 + k * 5.333, 124.5), (46.0 + k * 3.333, 143.5)])):
            if p in lt:
                t.put(*p, "B0")
    t.pintar(R(36, 119, 76, 123.5), cil_y(119, 123.5), chanfro=0.12)
    for sx in (-1, 1):
        t.pintar(E(cx + sx * 21, 121.0, 3.2), esfera(cx + sx * 21, 121.0, 3.2))


# ---------- 32 · XIII · A Ceifadora de Engrenagens ----------

def il_ceifadora_engrenagens(t):
    for p0, ang, comp, seed in (((80, 50), -25, 24, 3), ((88, 84), 35, 13, 5), ((84, 120), 55, 22, 8),
                                ((24, 118), 125, 24, 11), ((20, 58), -150, 14, 13), ((42, 140), 150, 18, 17)):
        for pts, larg in _esp_rachadura(p0, math.radians(ang), comp, seed):
            _esp_fenda(t, pts)
    h0, h1 = (75.0, 157.0), (60.0, 54.0)
    ang = math.atan2(h0[1] - h1[1], h0[0] - h1[0]) - math.pi / 2
    t.pintar(L([h0, h1], 4.2), local(cil_x(-2.1, 2.1), (h0[0] + h1[0]) / 2, (h0[1] + h1[1]) / 2, ang))
    for f in (0.2, 0.52, 0.88):
        x, y = h1[0] + (h0[0] - h1[0]) * f, h1[1] + (h0[1] - h1[1]) * f
        t.pintar(L([(x - 3.3, y + 0.5), (x + 3.3, y - 0.5)], 2.2), linear(x - 3, y - 2, x + 3, y + 2), chanfro=0.12)
    mx, my = h1[0] + (h0[0] - h1[0]) * 0.62, h1[1] + (h0[1] - h1[1]) * 0.62
    t.pintar(L([(mx, my), (mx + 9.5, my - 3.5)], 2.6), linear(mx, my - 5, mx + 10, my + 2), chanfro=0.12)
    t.pintar(E(mx + 10.2, my - 3.8, 2.2), esfera(mx + 10.2, my - 3.8, 2.2))
    t.pintar(E(h0[0], h0[1] + 0.5, 2.8), esfera(h0[0], h0[1] + 0.5, 2.8))
    bcx, bcy, br = 60.0, 95.0, 44.0
    a0, a1 = -84.0, -183.0
    nd = 17

    def ang_u(u):
        return math.radians(a0 + (a1 - a0) * u)

    def larg(u):
        return 15.5 * (1 - u) ** 0.7 + 0.2

    def miolo(u):
        a = ang_u(u)
        rr = br - larg(u)
        return (bcx + math.cos(a) * rr + 3.0 * u, bcy + math.sin(a) * rr + 4.0 * u)
    fora_ = []
    for i in range(nd * 6 + 1):
        u = i / (nd * 6)
        k = i % 6
        rr = br + 4.6 * (1 - 0.6 * u) * (k / 5.0)
        a = ang_u(u)
        fora_.append((bcx + math.cos(a) * rr, bcy + math.sin(a) * rr))
        if k == 5 and i < nd * 6:
            fora_.append((bcx + math.cos(a) * br, bcy + math.sin(a) * br))
    lam = P(fora_[::-1] + [miolo(i / 60) for i in range(61)])

    def luz_lamina(x, y):
        rho = math.hypot(x + 0.5 - bcx, y + 0.5 - bcy)
        v = 0.4 + 0.52 * max(0.0, min(1.0, (rho - (br - 14)) / 11.0))
        return v + 0.1 * max(-1.0, min(1.0, (bcx - x) / 30))
    t.pintar(lam, luz_lamina, chanfro=0.22)
    for u in (0.1, 0.27, 0.44, 0.6):
        a = ang_u(u)
        rr = br - larg(u) / 2 - 1.0
        hx, hy = bcx + math.cos(a) * rr + 1.5 * u, bcy + math.sin(a) * rr + 2.0 * u
        furo = E(hx, hy, 2.6 * (1 - u * 0.6))
        t.fill(furo, "K")
        t.fill(encolher(furo, 1), "C1")
    for u in (0.3, 0.58, 0.84):
        x, y = miolo(u)
        _esp_estrela_c(t, x + 0.5, y + 0.8, 1)
    hx, hy = 61.0, 55.0
    t.pintar(P(_esp_engr_pts(hx, hy, 8.0, 10, 2.8, fase=0.1)), linear(hx - 11, hy - 11, hx + 11, hy + 11, 1.0, 0.08),
             chanfro=0.2)
    _esp_soquete(t, hx, hy, 5.6)
    t.cristal(hx, hy, 3.2, 5.4)
    for (x, y, a) in ((26, 132, 25), (33, 146, -30), (44, 140, 60)):
        d = P(girar([(-2.0, 1.6), (-1.2, -1.6), (1.2, -1.6), (2.0, 1.6)], x, y, math.radians(a)))
        t.pintar(d, linear(x - 2, y - 2, x + 2, y + 2), chanfro=0.15)


# ---------- 33 · XVII · A Estrela de Cristal ----------

def il_estrela_cristal(t):
    cx, cy = CX, 94.0
    for i in range(16):
        a = i * math.tau / 16 + math.tau / 32
        r1 = 27 if i % 2 == 0 else 22
        _esp_linha(t, [(cx + math.cos(a) * 12, cy + math.sin(a) * 12), (cx + math.cos(a) * r1, cy + math.sin(a) * r1)],
                   "C0", passo=2)
    peq = []
    for k in range(4):
        a = math.radians(-45 + k * 90)
        gx, gy = cx + math.cos(a) * 33.5, cy + math.sin(a) * 33.5
        peq.append((gx, gy))
        t.pintar(P(_esp_engr_pts(gx, gy, 4.0, 8, 1.8, fase=0.1 * k)), linear(gx - 6, gy - 6, gx + 6, gy + 6, 1.0, 0.08),
                 chanfro=0.18)
        t.fill(E(gx, gy, 1.6), "K")
        t.put(int(gx), int(gy), "C2")
    pontas = []
    for k in range(8):
        a = k * math.tau / 8
        longo = k % 2 == 0
        h = 15.5 if longo else 9.5
        r = 7.0 + h
        w = 4.6 if longo else 3.3
        _esp_cristal(t, cx + math.sin(a) * r, cy - math.cos(a) * r, w, h, a)
        pontas.append((cx + math.sin(a) * (r + h), cy - math.cos(a) * (r + h)))
    for k in range(4):
        tl = pontas[2 * k]
        for j in (k - 1, k):
            g = peq[j % 4]
            ga = math.atan2(g[1] - tl[1], g[0] - tl[0])
            p0 = (tl[0] + math.cos(ga) * 1.5, tl[1] + math.sin(ga) * 1.5)
            p1 = (g[0] - math.cos(ga) * 6.0, g[1] - math.sin(ga) * 6.0)
            t.raio(_esp_ip(_esp_zigue(p0, p1, 4, 1.4, 100 + k * 7 + j)), ponta=False)
    t.pintar(P(_esp_engr_pts(cx, cy, 8.5, 12, 2.4)), linear(cx - 11, cy - 11, cx + 11, cy + 11, 1.05, 0.06), chanfro=0.2)
    _esp_soquete(t, cx, cy, 6.0)
    t.cristal(cx, cy, 3.4, 5.6, aura=False)
    for p in pontas[::2]:
        _esp_estrela_c(t, p[0], p[1], 2)


CARDS.append(("giro_engrenagem", "II DE ESPADAS", "GIRO DE ENGRENAGEM", "minor", 34))
CARDS.append(("chicote_corrente", "IV DE ESPADAS", "CHICOTE DE CORRENTE", "minor", 34))
CARDS.append(("tempestade_faiscas", "IX DE ESPADAS", "TEMPESTADE DE FAÍSCAS", "minor", 34))
CARDS.append(("martelo_vapor", "X DE ESPADAS", "MARTELO A VAPOR", "minor", 34))
CARDS.append(("carro_vapor", "VII", "O CARRO DE VAPOR", "major", 34))
CARDS.append(("ceifadora_engrenagens", "XIII", "A CEIFADORA DE ENGRENAGENS", "major", 34))
CARDS.append(("estrela_cristal", "XVII", "A ESTRELA DE CRISTAL", "major", 34))
