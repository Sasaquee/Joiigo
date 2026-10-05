"""
Gera as texturas da AURA (Fase 7, D-060 a D-066): o círculo de latão com runas em cristal aos pés do
andarilho, a luz que sobe em volta do corpo e as partículas dos sinais (faísca, fiapo, brasa, casca).

Identidade (D-060, Pilar 4): máquina e magia na mesma peça. O círculo é uma peça de latão torneada
(anel externo com engrenagem fina, gravações, presilhas rebitadas, anel interno fino) e os engastes
seguram cristais com runas; filetes de cristal correm por dentro do latão. Leve, para 4 jogadores
na tela: o círculo é vazado (fundo e centro transparentes).

Como funciona
- Tudo é desenhado como campos de distância (SDF) numa grade 2x maior e reduzido com LANCZOS
  (bordas suaves, nada pixelado).
- O latão é sombreado por um mapa de altura (bisel arredondado, gravações em baixo-relevo): normal
  -> luz de cima à esquerda -> faixas suaves (D-057) -> rampa de latão da paleta (#7A5F2C, #C9A04A).
  O shader na Unity é unlit, então a luz já vem pintada.
- As máscaras de emissão (runas, luz, partículas, casca) são brancas com alfa; a Unity pinta e
  controla a força.

Saída
    Assets/_Game/Art/Aura/aura_circulo.png   1024x1024 RGBA  latão, vazado
    Assets/_Game/Art/Aura/aura_runas.png     1024x1024 RGBA  máscara (runas, filetes, leito de cristal)
    Assets/_Game/Art/Aura/aura_luz.png        256x512  RGBA  luz que sobe (v=0 = chão = linha de baixo)
    Assets/_Game/Art/Aura/aura_faisca.png      64x64   RGBA
    Assets/_Game/Art/Aura/aura_fiapo.png      128x128  RGBA
    Assets/_Game/Art/Aura/aura_brasa.png       64x64   RGBA
    Assets/_Game/Art/Aura/aura_casca.png      512x512  RGBA  sem emenda nas duas direções
    Docs/Capturas/fase7/aura_texturas.png     folha de contato

Uso (Python 3 com Pillow e numpy):
    python Tools/Aura/aura_art.py
"""

import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFont

AQUI = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(AQUI, "..", ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "_Game", "Art", "Aura")
SHEET = os.path.join(ROOT, "Docs", "Capturas", "fase7", "aura_texturas.png")
FONTES = "C:/Windows/Fonts"

TAU = 2.0 * math.pi

# --------------------------------------------------------------------------------------------------
# Geometria do círculo (frações da largura da imagem; o centro é 0,5 / 0,5). Estes números vão no
# relatório para o orquestrador alinhar a malha.
# --------------------------------------------------------------------------------------------------
N_RUNAS = 8
FASE_RUNAS = -math.pi / 2          # uma runa no topo da imagem (+V na Unity)
R_EXT = 0.460                      # borda externa do anel externo (sem os dentes)
R_EXT_IN = 0.428                   # borda interna do anel externo -> faixa 0,032 (7% do raio)
R_MEIO = 0.5 * (R_EXT + R_EXT_IN)  # 0,444: trilho dos engastes e do filete de cristal
R_DENTE = 0.471                    # ponta dos dentes da engrenagem fina
N_DENTES = 72
R_INT = 0.362                      # centro do anel interno
L_INT = 0.0060                     # meia largura do anel interno (0,356 a 0,368)
R_ENGASTE = 0.036                  # raio da coroa do engaste (sai de 0,408 a 0,480)
R_LEITO = 0.026                    # raio do leito de cristal (onde fica a runa)
R_CONTORNO = 0.0035                # contorno escuro em volta do latão
# Tudo o que é opaco cabe em r <= ~0,484 (borda da imagem em 0,5).

# Paleta (Editor/PixelPalette.cs / arte-pixel.md)
LATAO = (0xC9, 0xA0, 0x4A)
LATAO_ESC = (0x7A, 0x5F, 0x2C)
CIANO = (0x6F, 0xF0, 0xFF)
BRASA = (0xFF, 0x7A, 0x20)
CHAO = (0x2A, 0x2D, 0x45)
VIOLETA = (0xA8, 0x6B, 0xFF)       # só na folha de contato (a Unity escolhe a cor da maldição)

RAMPA_LATAO = [  # (valor de luz, cor)
    (0.00, (0x2A, 0x1F, 0x0E)),
    (0.22, (0x52, 0x3E, 0x1D)),
    (0.40, LATAO_ESC),
    (0.62, LATAO),
    (0.80, (0xE3, 0xC2, 0x6C)),
    (1.00, (0xFF, 0xF1, 0xC4)),
]
COR_CONTORNO = (0x1C, 0x16, 0x0E)
COR_LEITO = (0x13, 0x22, 0x2B)     # cristal apagado (vidro escuro, levemente frio)
COR_SULCO = (0x1E, 0x24, 0x2A)


# --------------------------------------------------------------------------------------------------
# Utilidades
# --------------------------------------------------------------------------------------------------
def grade(n):
    """Coordenadas dos centros de pixel em frações da imagem (x para a direita, y para baixo)."""
    c = (np.arange(n, dtype=np.float32) + 0.5) / n
    return np.meshgrid(c, c)


def cob(d, px):
    """Cobertura anti-serrilhada de um SDF (d em frações; px = tamanho do pixel em frações)."""
    return np.clip(0.5 - d / px, 0.0, 1.0)


def perfil(d, b):
    """Bisel arredondado: 0 na borda, 1 a partir de b para dentro."""
    t = np.clip(-d / b, 0.0, 1.0)
    return np.sqrt(t * (2.0 - t))


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def blur(a, sigma_px, wrap=False):
    """Desfoque gaussiano por FFT (periódico; sem wrap, preenche com zeros em volta)."""
    if sigma_px <= 0:
        return a
    h, w = a.shape
    pad = 0 if wrap else int(3 * sigma_px) + 2
    src = np.pad(a, pad) if pad else a
    fy = np.fft.fftfreq(src.shape[0])[:, None]
    fx = np.fft.rfftfreq(src.shape[1])[None, :]
    g = np.exp(-2.0 * (math.pi ** 2) * (sigma_px ** 2) * (fx * fx + fy * fy))
    out = np.fft.irfft2(np.fft.rfft2(src) * g, s=src.shape)
    return out[pad:pad + h, pad:pad + w] if pad else out


def seg_dist(px, py, ax, ay, bx, by):
    abx, aby = bx - ax, by - ay
    t = ((px - ax) * abx + (py - ay) * aby) / max(abx * abx + aby * aby, 1e-12)
    t = np.clip(t, 0.0, 1.0)
    dx, dy = px - (ax + t * abx), py - (ay + t * aby)
    return np.sqrt(dx * dx + dy * dy)


def rampa(v, stops):
    v = np.clip(v, 0.0, 1.0)
    out = np.zeros(v.shape + (3,), np.float32)
    xs = [s[0] for s in stops]
    for c in range(3):
        out[..., c] = np.interp(v, xs, [s[1][c] for s in stops])
    return out


def faixas(v, n=5, suave=0.18):
    """Luz em faixas suaves (D-057): degraus com rampa curta entre eles."""
    x = np.clip(v, 0.0, 0.9999) * n
    f = x - np.floor(x)
    return (np.floor(x) + smooth(0.5 - suave, 0.5 + suave, f)) / n


def salvar_rgba(rgb, alpha, tamanho, caminho, branco=False):
    """Reduz com alfa pré-multiplicado (sem franja escura) e salva."""
    a8 = (np.clip(alpha, 0, 1) * 255 + 0.5).astype(np.uint8)
    if branco:
        rgb8 = np.full(alpha.shape + (3,), 255, np.uint8)
    else:
        rgb8 = (np.clip(rgb, 0, 255) + 0.5).astype(np.uint8)
    img = Image.fromarray(np.dstack([rgb8, a8]), "RGBA")
    if img.size != tamanho:
        img = img.convert("RGBa").resize(tamanho, Image.LANCZOS).convert("RGBA")
    if branco:  # garante RGB branco puro em todo lugar (máscara)
        a = img.getchannel("A")
        img = Image.merge("RGBA", (Image.new("L", tamanho, 255),) * 3 + (a,))
    os.makedirs(os.path.dirname(caminho), exist_ok=True)
    img.save(caminho, optimize=True)
    print("  ", os.path.relpath(caminho, ROOT), img.size)
    return img


# --------------------------------------------------------------------------------------------------
# Runas: glifos próprios, geométricos, "de engenharia arcana". Coordenadas no leito do engaste
# (raio 1); +y aponta para fora do círculo, +x segue no sentido horário de quem lê de dentro.
# l = traço, c = anel, a = arco (graus), p = polígono fechado, d = ponto
# --------------------------------------------------------------------------------------------------
GLIFOS = [
    # 0 eixo: haste atravessando um anel
    [("l", 0, -0.70, 0, -0.38), ("l", 0, 0.38, 0, 0.70), ("c", 0, 0, 0.38), ("d", 0, 0, 0.12)],
    # 1 pistão: seta para fora sobre uma base
    [("l", 0, -0.52, 0, 0.52), ("l", -0.42, 0.14, 0, 0.58), ("l", 0.42, 0.14, 0, 0.58),
     ("l", -0.40, -0.58, 0.40, -0.58)],
    # 2 engrenagem: anel com quatro dentes e cubo
    [("c", 0, 0, 0.36), ("d", 0, 0, 0.12),
     ("l", 0.36, 0, 0.66, 0), ("l", -0.36, 0, -0.66, 0), ("l", 0, 0.36, 0, 0.66), ("l", 0, -0.36, 0, -0.66)],
    # 3 faísca: raio em zigue-zague
    [("l", -0.30, 0.62, 0.26, 0.14), ("l", 0.26, 0.14, -0.26, -0.14), ("l", -0.26, -0.14, 0.30, -0.62)],
    # 4 válvula: triângulo para dentro com haste e ponto
    [("p", (-0.52, 0.42), (0.52, 0.42), (0, -0.46)), ("l", 0, -0.46, 0, -0.70), ("d", 0, 0.12, 0.11)],
    # 5 maré: travessão com meia-lua por cima e ponto por baixo
    [("l", -0.62, -0.06, 0.62, -0.06), ("a", 0, -0.06, 0.42, 15, 165), ("d", 0, -0.44, 0.11)],
    # 6 chave: haste com dois dentes desiguais
    [("l", 0, -0.68, 0, 0.68), ("l", -0.42, 0.34, 0.42, 0.34), ("l", 0, -0.14, 0.32, -0.14),
     ("l", 0.32, -0.14, 0.32, -0.36)],
    # 7 olho de cristal: losango com núcleo
    [("p", (0, 0.66), (0.44, 0), (0, -0.66), (-0.44, 0)), ("l", 0, -0.26, 0, 0.26)],
]
TRACO = 0.13  # meia espessura do traço da runa, em raios do leito


def glifo_sdf(gx, gy, glifo):
    d = np.full(gx.shape, 9.0, np.float32)
    for g in glifo:
        k = g[0]
        if k == "l":
            d = np.minimum(d, seg_dist(gx, gy, *g[1:]) - TRACO)
        elif k == "c":
            d = np.minimum(d, np.abs(np.hypot(gx - g[1], gy - g[2]) - g[3]) - TRACO)
        elif k == "d":
            d = np.minimum(d, np.hypot(gx - g[1], gy - g[2]) - g[3])
        elif k == "a":
            cx, cy, r, a0, a1 = g[1:]
            ang = np.degrees(np.arctan2(gy - cy, gx - cx))
            dentro = (ang >= a0) & (ang <= a1)
            darc = np.abs(np.hypot(gx - cx, gy - cy) - r)
            e0 = np.hypot(gx - (cx + r * math.cos(math.radians(a0))), gy - (cy + r * math.sin(math.radians(a0))))
            e1 = np.hypot(gx - (cx + r * math.cos(math.radians(a1))), gy - (cy + r * math.sin(math.radians(a1))))
            d = np.minimum(d, np.where(dentro, darc, np.minimum(e0, e1)) - TRACO)
        elif k == "p":
            pts = g[1:]
            for i in range(len(pts)):
                a, b = pts[i], pts[(i + 1) % len(pts)]
                d = np.minimum(d, seg_dist(gx, gy, a[0], a[1], b[0], b[1]) - TRACO)
    return d


# --------------------------------------------------------------------------------------------------
# 1 e 2 · círculo de latão + máscara das runas
# --------------------------------------------------------------------------------------------------
def circulo_e_runas(saida=1024, ss=2):
    n = saida * ss
    px = 1.0 / n
    x, y = grade(n)
    dx, dy = x - 0.5, y - 0.5
    r = np.hypot(dx, dy)
    th = np.arctan2(dy, dx)

    # quadro local do engaste mais próximo (t = tangente, m = radial para fora)
    passo = TAU / N_RUNAS
    k = np.round((th - FASE_RUNAS) / passo)
    phi = FASE_RUNAS + k * passo
    cx, cy = 0.5 + R_MEIO * np.cos(phi), 0.5 + R_MEIO * np.sin(phi)
    mx, my = np.cos(phi), np.sin(phi)
    tx, ty = -my, mx
    lt = (x - cx) * tx + (y - cy) * ty            # ao longo do anel
    lm = (x - cx) * mx + (y - cy) * my            # para fora
    # quadro da presilha (no meio entre dois engastes)
    kc = np.round((th - FASE_RUNAS - passo / 2) / passo)
    phc = FASE_RUNAS + passo / 2 + kc * passo
    ccx, ccy = 0.5 + R_MEIO * np.cos(phc), 0.5 + R_MEIO * np.sin(phc)
    ct = (x - ccx) * (-np.sin(phc)) + (y - ccy) * np.cos(phc)
    cm = (x - ccx) * np.cos(phc) + (y - ccy) * np.sin(phc)
    # distância angular (em arco) até o eixo do engaste
    s_eng = r * np.abs(np.angle(np.exp(1j * (th - phi))))

    # ---- SDFs das peças de latão ----
    d_faixa = np.abs(r - R_MEIO) - 0.5 * (R_EXT - R_EXT_IN)
    pd = TAU / N_DENTES
    a_d = np.mod(th + pd * 0.5, pd) - pd * 0.5
    s_d = np.abs(a_d) * r
    t_d = np.clip((r - (R_EXT - 0.004)) / (R_DENTE - R_EXT + 0.004), 0, 1)
    meia = 0.0090 * (1 - t_d) + 0.0052 * t_d
    d_dente = np.maximum.reduce([s_d - meia, r - R_DENTE, (R_EXT - 0.006) - r])
    d_int = np.abs(r - R_INT) - L_INT
    # coroa do engaste: disco + "orelhas" ao longo do anel (lente)
    d_coroa = np.hypot(lt, lm) - R_ENGASTE
    d_orelha = np.maximum(np.abs(lm) - 0.5 * (R_EXT - R_EXT_IN) - 0.005, np.abs(lt) - 0.056)
    d_orelha = np.maximum(d_orelha, np.hypot(np.abs(lt) - 0.020, lm) - 0.034)
    d_eng = np.minimum(d_coroa, d_orelha)
    d_leito = np.hypot(lt, lm) - R_LEITO
    # haste do engaste até o anel interno (e a plaquinha onde ela encosta)
    r0, r1 = R_INT, R_MEIO - R_ENGASTE + 0.004
    larg = 0.0062 + 0.0030 * np.clip((r - r0) / (r1 - r0), 0, 1)
    d_haste = np.maximum(s_eng - larg, np.maximum(r0 - r, r - r1))
    pcx, pcy = 0.5 + R_INT * np.cos(phi), 0.5 + R_INT * np.sin(phi)
    d_placa = np.hypot(x - pcx, y - pcy) - 0.0115
    # presilha rebitada entre os engastes
    d_pres = np.maximum(np.abs(ct) - 0.0105, np.abs(cm) - (0.5 * (R_EXT - R_EXT_IN) + 0.0045))
    d_pres = np.maximum(d_pres, np.hypot(ct, cm) - 0.026)
    d_rebite = np.hypot(ct, cm) - 0.0058

    d_tudo = np.minimum.reduce([d_faixa, d_dente, d_int, d_eng, d_haste, d_placa, d_pres])

    # ---- mapa de altura ----
    H = np.zeros_like(r)
    H = np.maximum(H, 0.70 * perfil(d_dente, 0.0035))
    H = np.maximum(H, 1.00 * perfil(d_faixa, 0.0065))
    H = np.maximum(H, 0.85 * perfil(d_int, 0.0045))
    H = np.maximum(H, 0.80 * perfil(d_haste, 0.0040))
    H = np.maximum(H, 0.95 * perfil(d_placa, 0.0045))
    H = np.maximum(H, 1.25 * perfil(d_pres, 0.0040))
    H = np.maximum(H, 1.40 * perfil(d_eng, 0.0075))
    # rebite em cúpula
    rb = np.clip(-d_rebite / 0.0058, 0, 1)
    H = np.where(d_rebite < 0, np.maximum(H, 1.25 + 0.45 * np.sqrt(rb * (2 - rb))), H)
    # leito do cristal: rebaixado
    H = np.minimum(H, 1.40 - 0.85 * perfil(d_leito, 0.0040))
    # sulco do filete de cristal no meio da faixa (interrompido pelas presilhas e engastes)
    sulco_faixa = cob(np.abs(r - R_MEIO) - 0.0032, px) * (d_pres > 0) * (d_eng > 0)
    sulco_haste = cob(s_eng - 0.0020, px) * (r > r0 + 0.004) * (r < r1) * (d_placa > 0)
    gema_placa = cob(np.hypot(x - pcx, y - pcy) - 0.0050, px)
    sulco = np.maximum.reduce([sulco_faixa, sulco_haste, gema_placa])
    H = H - 0.30 * blur(sulco, 1.2)
    # gravações: duas linhas finas acompanhando a faixa e tracinhos de mostrador na borda externa
    grav = cob(np.abs(r - (R_EXT_IN + 0.0050)) - 0.0011, px)
    grav = np.maximum(grav, cob(np.abs(r - (R_EXT - 0.0050)) - 0.0011, px))
    pt = TAU / 160
    s_t = np.abs(np.mod(th + pt / 2, pt) - pt / 2) * r
    longo = (np.abs(np.mod(th / pt + 0.5, 5) - 0.5) < 0.5)
    tick = cob(s_t - 0.0008, px) * ((r > R_EXT - 0.0105) & (r < R_EXT - 0.0050))
    tick = np.maximum(tick, cob(s_t - 0.0008, px) * longo * ((r > R_EXT_IN + 0.0050) & (r < R_EXT_IN + 0.0100)))
    grav = np.maximum(grav, tick) * (d_pres > 0) * (d_eng > 0) * (d_faixa < -0.0015)
    # gravação no anel interno: tracejado
    pi_ = TAU / 48
    s_i = np.abs(np.mod(th + pi_ / 2, pi_) - pi_ / 2) * r
    grav_int = cob(np.abs(r - R_INT) - 0.0010, px) * cob(s_i - 0.0085, px) * (d_placa > 0.002)
    grav = np.maximum(grav, grav_int)
    H = H - 0.12 * blur(grav, 0.9)
    # anel de ranhura no topo da coroa do engaste
    grav_c = cob(np.abs(np.hypot(lt, lm) - 0.5 * (R_ENGASTE + R_LEITO) - 0.0005) - 0.0011, px) * (d_coroa < -0.003)
    H = H - 0.10 * blur(grav_c, 0.9)

    # ---- luz ----
    kh = 0.0065  # escala da altura: bisel ~45°
    gy_, gx_ = np.gradient(H, px)
    nx, ny, nz = -gx_ * kh, -gy_ * kh, np.ones_like(H)
    nl = np.sqrt(nx * nx + ny * ny + nz * nz)
    nx, ny, nz = nx / nl, ny / nl, nz / nl
    L = np.array([-0.55, -0.55, 0.63])
    L = L / np.linalg.norm(L)
    dif = np.clip(nx * L[0] + ny * L[1] + nz * L[2], 0, 1)
    Hh = L + np.array([0, 0, 1.0])
    Hh = Hh / np.linalg.norm(Hh)
    esp = np.clip(nx * Hh[0] + ny * Hh[1] + nz * Hh[2], 0, 1) ** 60
    # oclusão: o que fica abaixo da vizinhança escurece
    ao = np.clip(1.0 - 1.6 * np.clip(blur(H, 6.0) - H, 0, None), 0.45, 1.0)
    # brilho anisotrópico de latão torneado: mais claro do lado da luz, mais escuro do lado oposto
    th_luz = math.atan2(-1.0, -1.0)
    brilho = 0.07 * np.cos(th - th_luz) + 0.05 * np.cos(2 * (th - th_luz))
    rng = np.random.default_rng(7)
    torneado = np.interp(r, np.linspace(0, 0.5, 900), rng.normal(0, 1, 900)).astype(np.float32)
    v = 0.10 + 0.86 * dif + brilho + 0.015 * torneado
    v = v * ao + 0.55 * esp
    v = faixas(v, n=6, suave=0.22)
    cor = rampa(v, RAMPA_LATAO)

    # leito de cristal (vidro escuro com reflexo no alto à esquerda) e fundo dos sulcos
    leito = cob(d_leito + 0.0012, px)
    # reflexo: ponto de luz do lado de cima à esquerda do leito, em coordenadas da imagem
    rx = (x - cx) / R_LEITO + 0.42
    ry = (y - cy) / R_LEITO + 0.42
    refl = np.clip(1 - np.hypot(rx, ry * 1.6) / 0.30, 0, 1) ** 1.5
    fundo = np.clip(1.0 - 0.55 * (np.hypot(lt, lm) / R_LEITO), 0.3, 1.0)
    cor_leito = np.array(COR_LEITO, np.float32) * (0.75 + 0.45 * fundo[..., None])
    cor_leito = cor_leito + refl[..., None] * np.array([150, 190, 200], np.float32) * 0.8
    cor = cor * (1 - leito[..., None]) + cor_leito * leito[..., None]
    s_mix = np.clip(sulco * (1 - leito), 0, 1)[..., None] * 0.85
    cor = cor * (1 - s_mix) + np.array(COR_SULCO, np.float32) * s_mix

    # linhas de contato entre peças (contorno interno, combina com o contorno do jogo)
    linha = np.maximum.reduce([
        cob(np.abs(d_eng) - 0.0011, px) * (d_faixa < 0.0005),
        cob(np.abs(d_pres) - 0.0010, px) * (d_faixa < 0.0005),
        cob(np.abs(d_haste) - 0.0009, px) * (d_int > -0.001) * (d_eng > 0),
        cob(np.abs(d_placa) - 0.0010, px) * (d_int < 0.0005),
        cob(np.abs(d_dente) - 0.0008, px) * (d_faixa > -0.0008) * (d_faixa < 0.0030) * (d_eng > 0),
    ])
    cor = cor * (1 - 0.75 * linha[..., None]) + np.array(COR_CONTORNO, np.float32) * 0.75 * linha[..., None]

    # contorno escuro por fora + sombra de contato bem leve
    a_metal = cob(d_tudo, px)
    a_cont = cob(d_tudo - R_CONTORNO, px)
    cor = cor * a_metal[..., None] + np.array(COR_CONTORNO, np.float32) * (1 - a_metal[..., None])
    alpha = np.maximum(a_metal, 0.92 * a_cont)
    sombra = blur(np.roll(np.roll(a_cont, int(0.004 * n), 0), int(0.003 * n), 1), 0.004 * n) * 0.30
    alpha = alpha + (1 - alpha) * sombra
    img_c = salvar_rgba(cor, alpha, (saida, saida), os.path.join(OUT_DIR, "aura_circulo.png"))

    # ---- máscara das runas ----
    runa = np.zeros_like(r)
    for i in range(N_RUNAS):
        ph = FASE_RUNAS + i * passo
        ex, ey = 0.5 + R_MEIO * math.cos(ph), 0.5 + R_MEIO * math.sin(ph)
        j0, j1 = int((ex - 0.04) * n), int((ex + 0.04) * n) + 1
        i0, i1 = int((ey - 0.04) * n), int((ey + 0.04) * n) + 1
        sx, sy = x[i0:i1, j0:j1] - ex, y[i0:i1, j0:j1] - ey
        mxx, myy = math.cos(ph), math.sin(ph)
        gx = (sx * (-myy) + sy * mxx) / R_LEITO
        gy = (sx * mxx + sy * myy) / R_LEITO
        dg = glifo_sdf(gx, gy, GLIFOS[i % len(GLIFOS)]) * R_LEITO
        runa[i0:i1, j0:j1] = np.maximum(runa[i0:i1, j0:j1], cob(dg, px))
    filete = np.maximum.reduce([
        cob(np.abs(r - R_MEIO) - 0.0025, px) * (d_pres > 0.0015) * (d_eng > 0.0015),
        cob(s_eng - 0.0014, px) * (r > r0 + 0.006) * (r < r1 - 0.001) * (d_placa > 0.0015),
        cob(np.hypot(x - pcx, y - pcy) - 0.0042, px),
    ])
    leito_m = cob(d_leito + 0.0016, px)
    halo = blur(np.maximum(runa, 0.5 * filete), 0.0060 * n)
    halo = np.clip(halo * 2.0, 0, 1)
    halo_eng = blur(runa, 0.016 * n)
    halo_eng = np.clip(halo_eng / max(halo_eng.max(), 1e-6), 0, 1)
    am = np.maximum.reduce([runa, 0.88 * filete, 0.24 * leito_m, 0.45 * halo, 0.30 * halo_eng])
    img_r = salvar_rgba(None, am, (saida, saida), os.path.join(OUT_DIR, "aura_runas.png"), branco=True)
    return img_c, img_r


# --------------------------------------------------------------------------------------------------
# 3 · luz que sobe (cilindro aberto: u = volta, v = altura; a linha de baixo do PNG é v = 0)
# --------------------------------------------------------------------------------------------------
def luz(w=256, h=512, ss=2):
    W, Hh = w * ss, h * ss
    u = (np.arange(W, dtype=np.float32) + 0.5) / W
    v = 1.0 - (np.arange(Hh, dtype=np.float32) + 0.5) / Hh   # v=1 em cima
    U, V = np.meshgrid(u, v)
    rng = random.Random(60)
    # base: brilho que nasce no chão e some até o topo
    base = 0.36 * (1 - V) ** 3.0 + 0.30 * np.exp(-V / 0.05)
    raios = np.zeros_like(U)
    centros = [0.04, 0.19, 0.35, 0.50, 0.63, 0.81]
    for c in centros:
        larg = rng.uniform(0.022, 0.045)
        alt = rng.uniform(0.55, 0.95)
        forca = rng.uniform(0.45, 0.75)
        onda = rng.uniform(0.004, 0.010)
        fase = rng.uniform(0, TAU)
        cc = c + onda * np.sin(V * rng.uniform(5, 9) + fase)          # leve ondulação
        du = np.abs(np.mod(U - cc + 0.5, 1.0) - 0.5)                    # distância periódica
        perfil_u = np.exp(-(du / larg) ** 2) + 0.35 * np.exp(-(du / (larg * 0.35)) ** 2)
        perfil_v = (1 - smooth(0.0, alt, V)) * smooth(-0.05, 0.10, V)
        raios += forca * perfil_u * perfil_v
    # respiro horizontal leve (periódico) para a base não ficar uniforme
    resp = 1 + 0.10 * np.sin(TAU * 2 * U + 1.3) + 0.06 * np.sin(TAU * 5 * U + 0.4)
    a = (base * resp + raios * (1 - V) ** 0.8)
    a = a * (1 - smooth(0.80, 1.0, V))       # topo zera de verdade
    a = np.clip(a, 0, 1)
    a[0:ss, :] = 0                            # primeira linha (v = 1) transparente
    return salvar_rgba(None, a, (w, h), os.path.join(OUT_DIR, "aura_luz.png"), branco=True)


# --------------------------------------------------------------------------------------------------
# 4, 5, 6 · partículas
# --------------------------------------------------------------------------------------------------
def faisca(s=64, ss=4):
    n = s * ss
    x, y = grade(n)
    dx, dy = (x - 0.5) * 2, (y - 0.5) * 2         # -1..1
    rr = np.hypot(dx, dy)
    nucleo = np.exp(-(rr / 0.09) ** 2)
    def braco(a, b):
        return np.exp(-np.abs(b) / (0.035 + 0.05 * np.clip(1 - np.abs(a), 0, 1) ** 3)) * np.clip(1 - np.abs(a) / 0.92, 0, 1) ** 1.6
    estrela = np.maximum(braco(dx, dy), braco(dy, dx))
    # braços diagonais menores
    d1, d2 = (dx + dy) / math.sqrt(2), (dx - dy) / math.sqrt(2)
    diag = 0.35 * np.maximum(braco(d1 * 1.9, d2), braco(d2 * 1.9, d1))
    brilho = 0.35 * np.exp(-(rr / 0.38) ** 2)
    a = np.clip(nucleo + estrela + diag + brilho, 0, 1) * smooth(1.0, 0.85, rr)
    return salvar_rgba(None, a, (s, s), os.path.join(OUT_DIR, "aura_faisca.png"), branco=True)


def brasa(s=64, ss=4):
    n = s * ss
    x, y = grade(n)
    dx, dy = (x - 0.5) * 2, (y - 0.5) * 2
    ang = np.arctan2(dy, dx)
    # contorno levemente irregular (brasa, não bolinha perfeita)
    rr = np.hypot(dx, dy) * (1 + 0.025 * np.sin(3 * ang + 0.7) + 0.015 * np.sin(5 * ang + 2.1))
    nucleo = smooth(0.22, 0.12, rr)
    meio = 0.65 * np.exp(-(rr / 0.30) ** 2)
    halo = 0.40 * np.exp(-(rr / 0.62) ** 2)
    a = np.clip(nucleo + meio + halo, 0, 1) * smooth(1.0, 0.86, np.hypot(dx, dy))
    return salvar_rgba(None, a, (s, s), os.path.join(OUT_DIR, "aura_brasa.png"), branco=True)


def fiapo(s=128, ss=4):
    n = s * ss
    x, y = grade(n)

    def caminho(m, desloc, giro, esc):
        """Voluta: sobe em S e se enrola numa espiral no alto (t = 0 embaixo, 1 no fim da volta)."""
        pts = []
        topo_x = 0.40 + 0.11 * math.sin(math.pi * 1.25 + 0.3)
        for i in range(m + 1):
            t = i / m
            if t < 0.62:
                q = t / 0.62
                px_ = 0.40 + 0.11 * math.sin(q * math.pi * 1.25 + 0.3) + desloc * q
                py_ = 0.90 - 0.50 * q
            else:
                q = (t - 0.62) / 0.38
                ang = math.pi * 0.10 + q * math.pi * giro
                raio = 0.17 * esc * (1 - 0.62 * q)
                cxp, cyp = topo_x + desloc + 0.17 * esc, 0.40
                px_ = cxp - raio * math.cos(ang)
                py_ = cyp - raio * math.sin(ang)
            pts.append((px_, py_, t))
        return pts

    def fio(pts, largura, forca_max):
        d = np.full(x.shape, 9.0, np.float32)
        peso = np.zeros_like(x)
        for i in range(len(pts) - 1):
            ax, ay, ta = pts[i]
            bx, by, tb = pts[i + 1]
            t = 0.5 * (ta + tb)
            larg = largura * (0.25 + 0.75 * math.sin(math.pi * min(1.0, t * 1.10)) ** 0.8)
            di = seg_dist(x, y, ax, ay, bx, by) - larg
            forca = forca_max * (0.30 + 0.70 * math.sin(math.pi * t) ** 0.7)
            mais = di < d
            d = np.where(mais, di, d)
            peso = np.where(mais, forca, peso)
        return cob(d, 1.0 / n) * peso

    principal = fio(caminho(120, 0.0, 1.55, 1.0), 0.024, 1.0)
    segundo = fio(caminho(120, 0.05, 1.25, 0.80), 0.012, 0.55)
    terceiro = fio(caminho(100, -0.04, 0.90, 0.60), 0.008, 0.40)
    nucleo = np.maximum.reduce([principal, segundo, terceiro])
    # fumaça: véu largo e suave + fios nítidos por dentro
    a = 0.85 * blur(nucleo, 0.030 * n) * 1.6 + 0.70 * blur(nucleo, 0.008 * n)
    a = np.clip(a, 0, 1)
    borda = np.minimum.reduce([x, y, 1 - x, 1 - y])
    a = a * smooth(0.0, 0.08, borda)
    return salvar_rgba(None, a, (s, s), os.path.join(OUT_DIR, "aura_fiapo.png"), branco=True)


# --------------------------------------------------------------------------------------------------
# 7 · casca de cristal do escudo (sem emenda): triangulação de uma rede hexagonal sacudida
# --------------------------------------------------------------------------------------------------
def casca(s=512, ss=2):
    T = s * ss
    NX, NY = 7, 8                       # 7 x 8: proporção 0,875 ~ hexagonal (0,866)
    ex, ey = 1.0 / NX, 1.0 / NY
    rng = np.random.default_rng(66)
    jit = rng.uniform(-0.17, 0.17, (NY, NX, 2))
    def P(i, j):
        jj, ii = j % NY, i % NX
        ox = (i + 0.5 * (j % 2)) * ex + jit[jj, ii, 0] * ex
        oy = j * ey + jit[jj, ii, 1] * ey
        return np.array([ox, oy])
    tris, arestas = [], set()
    for j in range(NY):
        for i in range(NX):
            # vizinhos na linha de baixo dependem da paridade (linhas ímpares deslocadas)
            if j % 2 == 0:
                bl, br = (i - 1, j + 1), (i, j + 1)
            else:
                bl, br = (i, j + 1), (i + 1, j + 1)
            a, b = (i, j), (i + 1, j)
            tris.append((a, b, br))
            tris.append((a, br, bl))
            for e in ((a, b), (a, br), (a, bl)):
                arestas.add(e)
    ys, xs = np.mgrid[0:T, 0:T].astype(np.float32)
    xs, ys = (xs + 0.5) / T, (ys + 0.5) / T
    preench = np.zeros((T, T), np.float32)
    d_linha = np.full((T, T), 9.0, np.float32)
    forca_l = np.zeros((T, T), np.float32)
    offs = [(ox, oy) for ox in (-1, 0, 1) for oy in (-1, 0, 1)]

    def janela(pts, margem):
        lo = pts.min(0) - margem
        hi = pts.max(0) + margem
        return lo, hi

    for idx, (a, b, c) in enumerate(tris):
        A, B, C = P(*a), P(*b), P(*c)
        base = 0.05 + 0.13 * rng.random()
        ang = rng.uniform(0, TAU)
        gdir = np.array([math.cos(ang), math.sin(ang)])
        cen = (A + B + C) / 3
        for ox, oy in offs:
            o = np.array([ox, oy], np.float32)
            pts = np.array([A, B, C]) + o
            lo, hi = janela(pts, 0.002)
            j0, j1 = max(0, int(lo[0] * T)), min(T, int(hi[0] * T) + 1)
            i0, i1 = max(0, int(lo[1] * T)), min(T, int(hi[1] * T) + 1)
            if j0 >= j1 or i0 >= i1:
                continue
            X, Y = xs[i0:i1, j0:j1], ys[i0:i1, j0:j1]
            (x1, y1), (x2, y2), (x3, y3) = pts
            den = (y2 - y3) * (x1 - x3) + (x3 - x2) * (y1 - y3)
            l1 = ((y2 - y3) * (X - x3) + (x3 - x2) * (Y - y3)) / den
            l2 = ((y3 - y1) * (X - x3) + (x1 - x3) * (Y - y3)) / den
            l3 = 1 - l1 - l2
            dentro = (l1 >= 0) & (l2 >= 0) & (l3 >= 0)
            g = ((X - (cen[0] + ox)) * gdir[0] + (Y - (cen[1] + oy)) * gdir[1]) / ex
            val = np.clip(base + 0.16 * g, 0.0, 0.32)
            preench[i0:i1, j0:j1] = np.where(dentro, val, preench[i0:i1, j0:j1])
    for (a, b) in arestas:
        A, B = P(*a), P(*b)
        f = 0.55 + 0.45 * rng.random()
        for ox, oy in offs:
            o = np.array([ox, oy], np.float32)
            pa, pb = A + o, B + o
            lo, hi = janela(np.array([pa, pb]), 0.01)
            j0, j1 = max(0, int(lo[0] * T)), min(T, int(hi[0] * T) + 1)
            i0, i1 = max(0, int(lo[1] * T)), min(T, int(hi[1] * T) + 1)
            if j0 >= j1 or i0 >= i1:
                continue
            di = seg_dist(xs[i0:i1, j0:j1], ys[i0:i1, j0:j1], pa[0], pa[1], pb[0], pb[1])
            mais = di < d_linha[i0:i1, j0:j1]
            d_linha[i0:i1, j0:j1] = np.where(mais, di, d_linha[i0:i1, j0:j1])
            forca_l[i0:i1, j0:j1] = np.where(mais, f, forca_l[i0:i1, j0:j1])
    linha = cob(d_linha - 0.0016, 1.0 / T) * forca_l
    brilho_l = blur(linha, 0.006 * T, wrap=True)
    # pontos de brilho em alguns vértices
    vert = np.zeros((T, T), np.float32)
    for j in range(NY):
        for i in range(NX):
            if rng.random() < 0.35:
                p = P(i, j)
                for ox, oy in offs:
                    q = p + np.array([ox, oy])
                    j0, j1 = max(0, int((q[0] - 0.03) * T)), min(T, int((q[0] + 0.03) * T) + 1)
                    i0, i1 = max(0, int((q[1] - 0.03) * T)), min(T, int((q[1] + 0.03) * T) + 1)
                    if j0 >= j1 or i0 >= i1:
                        continue
                    rr = np.hypot(xs[i0:i1, j0:j1] - q[0], ys[i0:i1, j0:j1] - q[1])
                    vert[i0:i1, j0:j1] = np.maximum(vert[i0:i1, j0:j1], np.exp(-(rr / 0.008) ** 2))
    a = np.clip(np.maximum.reduce([preench, linha, 0.5 * brilho_l * 2.0, vert]), 0, 1)
    return salvar_rgba(None, a, (s, s), os.path.join(OUT_DIR, "aura_casca.png"), branco=True)


# --------------------------------------------------------------------------------------------------
# 8 · folha de contato
# --------------------------------------------------------------------------------------------------
def fonte(tam):
    for nome in ("georgiab.ttf", "GARABD.TTF", "arialbd.ttf"):
        try:
            return ImageFont.truetype(os.path.join(FONTES, nome), tam)
        except OSError:
            pass
    return ImageFont.load_default()


def emissivo(fundo, mascara, cor, forca=1.0):
    """fundo RGB + máscara branca com alfa, somada (aditiva) com a cor."""
    f = np.asarray(fundo.convert("RGB"), np.float32)
    m = np.asarray(mascara.getchannel("A"), np.float32)[..., None] / 255.0
    c = np.array(cor, np.float32)
    out = f + m * c * forca
    # núcleo quase branco onde a máscara satura (cristal aceso)
    out = out + np.clip(m * forca - 0.75, 0, 1) * 255 * 0.6
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGB")


def tingido(mascara, cor, fundo=CHAO, forca=1.0, tam=None):
    if tam:
        mascara = mascara.resize(tam, Image.LANCZOS)
    base = Image.new("RGB", mascara.size, fundo)
    return emissivo(base, mascara, cor, forca)


def circulo_composto(circ, runas, cor, tam, forca=1.0, achatar=1.0, luzes=None):
    w = tam
    h = max(1, int(round(tam * achatar)))
    c = circ.resize((w, h), Image.LANCZOS)
    r = runas.resize((w, h), Image.LANCZOS)
    base = Image.new("RGB", (w, h), CHAO)
    base.paste(c, (0, 0), c)
    return emissivo(base, r, cor, forca)


def folha(imgs):
    circ, runas, luz_i, fai, fia, bra, cas = imgs
    W = 1760
    bg = (0x16, 0x17, 0x24)
    sheet = Image.new("RGB", (W, 1700), bg)
    dr = ImageDraw.Draw(sheet)
    ft, fp, fpp = fonte(30), fonte(17), fonte(15)

    def painel(x0, y0, w, h):
        dr.rectangle([x0, y0, x0 + w - 1, y0 + h - 1], fill=CHAO)

    def rot(x0, y0, txt, f=fp, cor=(0xEB, 0xE0, 0xC7)):
        dr.text((x0, y0), txt, font=f, fill=cor)

    rot(30, 18, "Joiigo · Fase 7 · texturas da aura (D-060 a D-066)", ft)
    # linha 1: ampliado ciano e laranja
    y = 70
    G = 560
    for i, (cor, nome) in enumerate([(CIANO, "runas ciano (#6FF0FF)"), (BRASA, "runas laranja (#FF7A20), bônus de dano")]):
        x0 = 30 + i * (G + 20)
        painel(x0, y, G, G)
        sheet.paste(circulo_composto(circ, runas, cor, G), (x0, y))
        rot(x0, y + G + 6, f"{nome} · 1024 reduzido a {G} px")
    # coluna da direita: tamanho de jogo
    xg = 30 + 2 * (G + 20)
    painel(xg, y, W - xg - 30, G)
    rot(xg + 10, y + 8, "180 px", fpp)
    rot(xg + 205, y + 8, "120 px", fpp)
    rot(xg + 340, y + 8, "180 px, achatado (50°)", fpp)
    yy = y + 30
    for cor, forca, nome in [(CIANO, 1.0, "normal"), (CIANO, 2.2, "energia cheia"), (BRASA, 1.0, "bônus de dano")]:
        sheet.paste(circulo_composto(circ, runas, cor, 180, forca), (xg + 10, yy))
        sheet.paste(circulo_composto(circ, runas, cor, 120, forca), (xg + 205, yy + 30))
        sheet.paste(circulo_composto(circ, runas, cor, 180, forca, achatar=0.766), (xg + 340, yy + 20))
        rot(xg + 340, yy + 162, nome, fpp)
        yy += 172

    # linha 2: latão sozinho, máscara, luz, cilindro, emendas
    y2 = y + G + 46
    T2 = 300
    painel(30, y2, T2, T2)
    c_only = circ.resize((T2, T2), Image.LANCZOS)
    sheet.paste(c_only, (30, y2), c_only)
    rot(30, y2 + T2 + 6, "aura_circulo.png")
    x1 = 30 + T2 + 20
    painel(x1, y2, T2, T2)
    sheet.paste(tingido(runas, (255, 255, 255), tam=(T2, T2)), (x1, y2))
    rot(x1, y2 + T2 + 6, "aura_runas.png (máscara)")
    xl = x1 + T2 + 20
    painel(xl, y2, 150, T2)
    sheet.paste(tingido(luz_i, (255, 255, 255), tam=(150, T2)), (xl, y2))
    rot(xl, y2 + T2 + 6, "aura_luz.png")
    xc = xl + 170
    painel(xc, y2, 300, T2)
    sheet.paste(cilindro(circ, runas, luz_i, 300, T2), (xc, y2))
    rot(xc, y2 + T2 + 6, "luz num cilindro (esboço)")
    xs_ = xc + 320
    painel(xs_, y2, W - xs_ - 30, T2)
    l2 = luz_i.resize((90, 180), Image.LANCZOS)
    for k in range(5):
        sheet.paste(tingido(l2, CIANO), (xs_ + k * 90, y2))
    c2 = cas.resize((120, 120), Image.LANCZOS)
    for k in range(3):
        sheet.paste(tingido(c2, CIANO, forca=0.9), (xs_ + k * 120, y2 + T2 - 120))
    rot(xs_, y2 + T2 + 6, "emendas: luz 5x e casca 3x lado a lado")

    # linha 3: partículas (ampliadas + tamanho real), casca, quatro auras
    y3 = y2 + T2 + 46
    P = 200
    for i, (img, cor, nome) in enumerate([(fai, CIANO, "aura_faisca.png"), (fia, VIOLETA, "aura_fiapo.png (violeta)"),
                                          (bra, BRASA, "aura_brasa.png")]):
        x0 = 30 + i * (P + 90)
        painel(x0, y3, P + 70, P)
        sheet.paste(tingido(img, cor, tam=(P, P)), (x0, y3))
        real = tingido(img, cor)
        sheet.paste(real, (x0 + P + 35 - real.size[0] // 2, y3 + P // 2 - real.size[1] // 2))
        rot(x0, y3 + P + 6, nome)
    xk = 30 + 3 * (P + 90)
    painel(xk, y3, P, P)
    sheet.paste(tingido(cas, CIANO, forca=0.9, tam=(P, P)), (xk, y3))
    rot(xk, y3 + P + 6, "aura_casca.png")
    xm = xk + P + 20
    painel(xm, y3, W - xm - 30, P)
    sheet.paste(mock_jogo(circ, runas, luz_i, fai, fia, bra, cas, W - xm - 30, P), (xm, y3))
    rot(xm, y3 + P + 6, "normal · energia cheia · bônus · caído (esboço)")
    sheet = sheet.crop((0, 0, W, y3 + P + 40))
    os.makedirs(os.path.dirname(SHEET), exist_ok=True)
    sheet.save(SHEET)
    print("  ", os.path.relpath(SHEET, ROOT), sheet.size)


def cilindro(circ, runas, luz_i, w, h, cor=CIANO, tam=180, forca_luz=0.55):
    """Esboço rápido: círculo achatado no chão + luz enrolada num cilindro, vista inclinada."""
    base = Image.new("RGB", (w, h), CHAO)
    ch = 0.766
    cc = circulo_composto(circ, runas, cor, tam, 1.0, achatar=ch)
    cx, cy = w // 2, int(h * 0.72)
    base.paste(cc, (cx - tam // 2, cy - cc.size[1] // 2))
    arr = np.asarray(base, np.float32)
    L = np.asarray(luz_i.getchannel("A"), np.float32) / 255.0
    Rc = tam * 0.36
    altura = h * 0.62
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    add = np.zeros((h, w), np.float32)
    for frente in (False, True):
        # parametrização: x = cx + Rc*sin(a); y_chão = cy + Rc*ch*cos(a)*... (frente = metade de baixo)
        X = (xs - cx) / Rc
        ok = np.abs(X) < 1
        a = np.arcsin(np.clip(X, -1, 1))
        if not frente:
            a = math.pi - a
        yb = cy + Rc * ch * np.cos(a) * (1 if frente else 1)
        vv = (yb - ys) / altura
        uu = np.mod(a / TAU, 1.0)
        m = ok & (vv >= 0) & (vv <= 1)
        iu = np.clip((uu * (L.shape[1] - 1)).astype(int), 0, L.shape[1] - 1)
        iv = np.clip(((1 - vv) * (L.shape[0] - 1)).astype(int), 0, L.shape[0] - 1)
        val = np.where(m, L[iv, iu], 0)
        add += val * forca_luz
    arr = arr + add[..., None] * np.array(cor, np.float32)
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB")


def mock_jogo(circ, runas, luz_i, fai, fia, bra, cas, w, h):
    base = Image.new("RGB", (w, h), CHAO)
    arr = np.asarray(base, np.float32).copy()
    estados = [(CIANO, 1.0, 0.55), (CIANO, 2.0, 0.65), (BRASA, 1.0, 0.45), (CIANO, 0.35, 0.18)]
    for k, (cor, f, fl) in enumerate(estados):
        tam = 120
        sub = cilindro(circ, runas, luz_i, 150, h, cor, tam, fl)
        if k == 3:  # caído: só brasa
            sub = cilindro(circ, runas, luz_i, 150, h, cor, tam, 0.0)
            s = np.asarray(sub, np.float32)
            b = np.asarray(bra.resize((14, 14), Image.LANCZOS).getchannel("A"), np.float32)[..., None] / 255
            for (bx, by) in [(70, 150), (86, 160), (60, 166)]:
                s[by:by + 14, bx:bx + 14] += b * np.array(BRASA, np.float32)
            sub = Image.fromarray(np.clip(s, 0, 255).astype(np.uint8))
        x0 = 4 + k * ((w - 8) // 4)
        sw = min(150, w - x0)
        arr[:, x0:x0 + sw] = np.maximum(arr[:, x0:x0 + sw], np.asarray(sub, np.float32)[:, :sw])
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB")


def main():
    print("Gerando texturas da aura em", os.path.relpath(OUT_DIR, ROOT))
    circ, runas = circulo_e_runas()
    luz_i = luz()
    fai = faisca()
    fia = fiapo()
    bra = brasa()
    cas = casca()
    folha((circ, runas, luz_i, fai, fia, bra, cas))


if __name__ == "__main__":
    main()
