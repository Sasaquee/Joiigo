using Game.Cameras;
using Game.Player;
using UnityEngine;
using UnityEngine.Rendering;
using static Game.Cards.FxKit;

namespace Game.Cards
{
    /// <summary>
    /// Fábrica de visuais das cartas, só no cliente (inclui o host). Cada visual é um objeto curto, criado em
    /// runtime com partículas, malhas e luzes, e some sozinho. Refeitos para o 3D pixelado (D-039, D-042):
    /// quadrados grandes e opacos em vez de partículas macias, anéis e leques grossos, clarões brancos fortes,
    /// peças de latão voando e tremor de câmera nas skills grandes. Só desenho: o dano e o alcance são decididos pelo host.
    ///
    /// Convenção de (posição, direção, tamanho) por visual:
    ///  dash: início da investida, direção, comprimento percorrido.
    ///  cone_vapor: origem do cone, direção, alcance.
    ///  arc_chain: início do segmento, direção, comprimento.
    ///  mine_place / mine_explode: ponto no chão, qualquer direção, raio da explosão.
    ///  shield: posição do jogador, direção em que ele olha, duração em segundos (segue o Transform dado).
    ///  heal: posição do jogador.
    ///  grenade_throw: ponto de saída, VETOR até o ponto de queda (não normalizado), tempo de voo.
    ///  grenade_explode / burst / pulse: centro, qualquer direção, raio (pulse: a direção é para onde o escudo olha).
    ///  slash_wide: posição do jogador, direção do corte, alcance (arco fixo de 160°).
    ///  slash_arc: posição do jogador, direção do corte (o Y carrega a meia-abertura em graus), alcance (arco com a abertura dada).
    /// </summary>
    public static class CardVisuals
    {
        /// <summary>Ordem fixa: o índice é o que trafega na rede (só acrescente no fim).</summary>
        public static readonly string[] Ids =
        {
            "dash", "cone_vapor", "arc_chain", "mine_place", "mine_explode", "shield", "heal",
            "grenade_throw", "grenade_explode", "burst", "slash_wide", "pulse", "slash_arc"
        };

        /// <summary>Materiais do projeto (latão e cristal das peças em malha). Definido pelo PlayerCards ao nascer.</summary>
        public static CardVisualLibrary Library { get; set; }

        // Corte amaldiçoado (Lâmina Sedenta).
        private static readonly Color CursedFill = new Color(0.8f, 0.1f, 0.38f, 1f);
        private static readonly Color CursedCore = new Color(1f, 0.82f, 0.9f, 1f);
        private static readonly Color CursedEdge = new Color(1f, 0.3f, 0.55f, 1f);

        private const float SlashHalfAngle = 80f;

        // Quando o escudo bloqueou por último (o visual do escudo brilha na hora).
        private static float lastBlockTime = -10f;
        private static Vector3 lastBlockPosition;

        // Quadros do raio: ligado, apagado, ligado, enfraquecendo.
        private static readonly float[] ArcFlicker = { 1f, 0.4f, 1f, 0.7f };

        private static Material fallbackBrass, fallbackCrystal;

        public static int IndexOf(string id) => System.Array.IndexOf(Ids, id);

        public static void PlayIndex(int index, Vector3 position, Vector3 direction, float size, Transform follow = null)
        {
            if (index >= 0 && index < Ids.Length)
                Play(Ids[index], position, direction, size, follow);
        }

        /// <summary>Cria o visual. Uma falha aqui vira aviso: desenho nunca pode quebrar o jogo.</summary>
        public static void Play(string id, Vector3 position, Vector3 direction, float size, Transform follow = null)
        {
            if (!IsFinite(position) || !IsFinite(direction) || !float.IsFinite(size))
                return;

            try
            {
                switch (id)
                {
                    case "dash": PlayDash(position, direction, size); break;
                    case "cone_vapor": PlayCone(position, direction, size); break;
                    case "arc_chain": PlayArc(position, direction, size); break;
                    case "mine_place": PlayMinePlace(position); break;
                    case "mine_explode": PlayMineExplode(position, size); break;
                    case "shield": PlayShield(position, direction, size, follow); break;
                    case "heal": PlayHeal(position); break;
                    case "grenade_throw": PlayGrenadeThrow(position, direction, size); break;
                    case "grenade_explode": PlayGrenadeExplode(position, size); break;
                    case "burst": PlayBurst(position, size); break;
                    case "slash_wide": PlaySlash(position, direction, size); break;
                    case "pulse": PlayPulse(position, direction, size); break;
                    case "slash_arc": PlaySlashArc(position, direction, size); break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Visual de carta '{id}' falhou: {e.Message}");
            }
        }

        // ---------- Visuais ----------

        /// <summary>Investida: faixa de velocidade, 3 silhuetas fantasmas chapadas, blocos de vapor e faíscas ciano.</summary>
        private static void PlayDash(Vector3 start, Vector3 dir, float length)
        {
            dir = Flat(dir);
            length = Mathf.Max(length, 0.4f);
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 mid = start + dir * (length * 0.5f);
            GameObject rootGo = Root("Fx_Investida", mid, rot, 0.75f);
            Transform root = rootGo.transform;
            var block = new MaterialPropertyBlock();

            // Faixa de velocidade no chão: larga e ciano, com um miolo branco mais fino.
            MeshRenderer streak = MeshChild(root, "Faixa", Quad, FlatAdditive);
            streak.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            MeshRenderer streakCore = MeshChild(root, "FaixaMiolo", Quad, FlatAdditive);
            streakCore.transform.localPosition = new Vector3(0f, 0.14f, 0f);

            // Silhuetas de pós-imagem: cápsulas de cor chapada, a primeira onde o jogador estava.
            float[] along = { 0f, 0.35f, 0.7f };
            float[] ghostAlpha = { 0.6f, 0.45f, 0.3f };
            var ghosts = new MeshRenderer[along.Length];
            for (int i = 0; i < ghosts.Length; i++)
            {
                ghosts[i] = MeshChild(root, "Silhueta", Capsule, FlatAlpha);
                ghosts[i].transform.SetPositionAndRotation(start + dir * (length * along[i]) + Vector3.up * 0.85f, rot);
                ghosts[i].transform.localScale = new Vector3(0.7f, 0.85f, 0.7f);
            }

            rootGo.GetComponent<CardVisualLife>().OnTick = t =>
            {
                float fade = 1f - t;
                float width = Mathf.Lerp(1.1f, 0.2f, t);
                streak.transform.localScale = new Vector3(width, 1f, length + 0.6f);
                streakCore.transform.localScale = new Vector3(width * 0.4f, 1f, length + 0.4f);
                Paint(streak, block, CyanBright, 0.6f * fade * fade);
                Paint(streakCore, block, CyanWhite, fade * fade * fade);
                for (int i = 0; i < ghosts.Length; i++)
                {
                    Vector3 s = ghosts[i].transform.localScale;
                    ghosts[i].transform.localScale = new Vector3(0.7f * (1f - t * 0.4f), s.y, 0.7f * (1f - t * 0.4f));
                    Paint(ghosts[i], block, CyanBright, ghostAlpha[i] * fade);
                }
            };
            rootGo.GetComponent<CardVisualLife>().OnTick(0f);

            // Blocos de vapor ao longo do caminho.
            var steam = Emitter(root, "Vapor", start, rot, PuffParticle);
            Configure(steam, 0.7f, 0.45f, 0.7f, 0f, 0f, 0.4f, 0.7f, SteamWhite, SteamWarm, -0.05f);
            Grow(steam, 0.8f, 1.7f);
            Fade(steam, 0.05f);
            steam.Play();
            int puffs = Mathf.Clamp(Mathf.RoundToInt(length * 1.8f), 4, 10);
            for (int i = 0; i < puffs; i++)
            {
                float f = i / (float)(puffs - 1);
                Vector3 p = start + dir * (length * f) + side * Random.Range(-0.4f, 0.4f) + Vector3.up * Random.Range(0.1f, 0.5f);
                Vector3 v = side * Random.Range(-0.8f, 0.8f) + Vector3.up * Random.Range(0.4f, 1f) - dir * Random.Range(0.2f, 1f);
                Emit(steam, p, v, Random.Range(0.45f, 0.75f), Color.Lerp(SteamWhite, SteamWarm, Random.value), Random.Range(0.45f, 0.7f));
            }

            // Faíscas de runa ciano: o impulso é arcano.
            var runes = Emitter(root, "Runas", start, rot, SolidParticle);
            Configure(runes, 0.5f, 0.25f, 0.45f, 0f, 0f, 0.1f, 0.18f, CyanWhite, CyanBright, 0.4f);
            Shrink(runes);
            runes.Play();
            int sparks = Mathf.Clamp(Mathf.RoundToInt(length * 3f), 6, 18);
            for (int i = 0; i < sparks; i++)
            {
                Vector3 p = start + dir * (length * Random.value) + side * Random.Range(-0.5f, 0.5f) + Vector3.up * Random.Range(0.2f, 1.4f);
                Vector3 v = -dir * Random.Range(1f, 3.5f) + side * Random.Range(-1f, 1f) + Vector3.up * Random.Range(0f, 1.2f);
                Emit(runes, p, v, Random.Range(0.1f, 0.18f), Random.value < 0.5f ? CyanWhite : CyanBright, Random.Range(0.25f, 0.45f));
            }

            // Fagulhas de atrito do pistão, onde ele partiu.
            Sparks(root, start + Vector3.up * 0.15f, 8, 1.5f, 4f, 0.1f, 0.16f, EmberHot, EmberOrange, 0.2f, 0.4f, 1.5f, 0.3f);

            Pop(start + Vector3.up * 0.9f, CyanWhite, 0.8f, 0.16f);
            Flash(mid + Vector3.up, CyanBright, 3.5f, 6f, 0.25f);
            CameraShake.AddAt(start, 0.05f, 0.1f);
        }

        /// <summary>Sopro de caldeira: blocos grandes de vapor quente, núcleo laranja e runas ciano em quadrados, mais um leque no chão.</summary>
        private static void PlayCone(Vector3 origin, Vector3 dir, float range)
        {
            dir = Flat(dir);
            range = Mathf.Max(range, 0.5f);
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
            GameObject rootGo = Root("Fx_Sopro", origin, rot, 0.8f);
            Transform root = rootGo.transform;
            const float life = 0.5f;

            var steam = Emitter(root, "Vapor", origin, rot, PuffParticle);
            Configure(steam, 0.3f, life * 0.9f, life * 1.1f, range / life * 0.8f, range / life * 1.05f, 0.7f, 1.1f,
                SteamWarm, SteamWhite, -0.05f);
            Shape(steam, ParticleSystemShapeType.Cone, radius: 0.15f, angle: 24f);
            Rate(steam, 70f);
            Grow(steam, 0.5f, 1.8f);
            Fade(steam, 0.08f);
            steam.Play();

            // Núcleo quente, perto da boca do vapor.
            var core = Emitter(root, "Nucleo", origin, rot, GlowParticle);
            Configure(core, 0.25f, 0.2f, 0.3f, range / 0.25f * 0.45f, range / 0.25f * 0.8f, 0.35f, 0.55f, EmberHot, EmberOrange);
            Shape(core, ParticleSystemShapeType.Cone, radius: 0.1f, angle: 14f);
            Rate(core, 60f);
            Fade(core);
            core.Play();

            // Runas queimando no vapor: quadrados ciano bem visíveis.
            var runes = Emitter(root, "Runas", origin, rot, SolidParticle);
            Configure(runes, 0.3f, life * 0.8f, life, range / life * 0.9f, range / life * 1.1f, 0.12f, 0.2f, CyanWhite, CyanBright);
            Shape(runes, ParticleSystemShapeType.Cone, radius: 0.1f, angle: 20f);
            Rate(runes, 26f);
            Shrink(runes);
            runes.Play();

            // Leque no chão: mostra a área do sopro (o cone de dano usa meia-abertura de 30° por padrão).
            float groundY = origin.y - 1f;
            GameObject fanGo = Root("Fx_SoproChao", new Vector3(origin.x, groundY + 0.06f, origin.z), rot, 0.4f);
            MeshRenderer fan = MeshChild(fanGo.transform, "Leque", Fan(30f), FlatAdditive);
            fan.transform.localScale = new Vector3(range, 1f, range);
            MeshRenderer band = MeshChild(fanGo.transform, "Borda", FanBand(30f, 0.9f), FlatAdditive);
            band.transform.localScale = new Vector3(range, 1f, range);
            var block = new MaterialPropertyBlock();
            fanGo.GetComponent<CardVisualLife>().OnTick = t =>
            {
                Paint(fan, block, EmberOrange, 0.18f * (1f - t));
                Paint(band, block, EmberHot, 0.55f * (1f - t));
            };
            fanGo.GetComponent<CardVisualLife>().OnTick(0f);

            Flash(origin + dir * 1.5f, EmberOrange, 2.4f, 5.5f, 0.3f);
        }

        /// <summary>Arco voltaico: raios grossos e irregulares que piscam 4 vezes (miolo branco-ciano, franja violeta), com estouro no alvo.</summary>
        private static void PlayArc(Vector3 from, Vector3 dir, float length)
        {
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            length = Mathf.Max(0.2f, length);
            Vector3 to = from + dir * length;
            int points = Mathf.Clamp(Mathf.CeilToInt(length / 0.6f) + 1, 3, 16);

            GameObject root = Root("Fx_Arco", from, Quaternion.identity, 0.22f);
            LineRenderer fringe = Line(root.transform, "Franja", points, 0.55f, 0.4f);
            LineRenderer glow = Line(root.transform, "Brilho", points, 0.3f, 0.22f);
            LineRenderer core = Line(root.transform, "Miolo", points, 0.15f, 0.11f);

            Vector3 side = Vector3.Cross(dir, Vector3.up);
            side = side.sqrMagnitude > 0.01f ? side.normalized : Vector3.right;
            Vector3 lift = Vector3.Cross(side, dir).normalized;
            var block = new MaterialPropertyBlock();
            int lastFrame = -1;

            var life = root.GetComponent<CardVisualLife>();
            life.OnTick = t =>
            {
                int frame = Mathf.Min(ArcFlicker.Length - 1, Mathf.FloorToInt(t * ArcFlicker.Length));
                if (frame != lastFrame)
                {
                    // A cada quadro o raio ganha outro desenho irregular (zigue-zague).
                    lastFrame = frame;
                    for (int i = 0; i < points; i++)
                    {
                        float f = i / (float)(points - 1);
                        Vector3 p = Vector3.Lerp(from, to, f);
                        if (i > 0 && i < points - 1)
                        {
                            float amplitude = 0.12f + 0.3f * Mathf.Sin(f * Mathf.PI);
                            float sign = i % 2 == 0 ? 1f : -1f;
                            p += side * (sign * amplitude * Random.Range(0.4f, 1f)) + lift * (Random.Range(-1f, 1f) * amplitude * 0.6f);
                        }
                        fringe.SetPosition(i, p);
                        glow.SetPosition(i, p);
                        core.SetPosition(i, p);
                    }
                }

                float a = ArcFlicker[frame];
                if (frame == ArcFlicker.Length - 1)
                    a *= 1f - Mathf.Clamp01(t * ArcFlicker.Length - frame) * 0.8f;
                Paint(fringe, block, Violet, 0.55f * a);
                Paint(glow, block, CyanBright, 0.7f * a);
                Paint(core, block, new Color(0.9f, 1f, 1f), a);
            };
            life.OnTick(0f);

            // Impacto: estilhaços de faísca em blocos e um estouro branco-ciano onde a descarga chega.
            Sparks(root.transform, to, 10, 2f, 5.5f, 0.1f, 0.18f, CyanWhite, Violet, 0.2f, 0.4f, 1f, 0.15f);
            Pop(to, CyanWhite, 0.45f, 0.14f);
            Flash(to, CyanBright, 3.5f, 4.5f, 0.2f);
        }

        /// <summary>Mina plantada: marca de engrenagem ciano que gira no chão, anel de runa e poeira.</summary>
        private static void PlayMinePlace(Vector3 position)
        {
            GameObject rootGo = Root("Fx_MinaPlantada", position, Quaternion.identity, 0.9f);
            Transform root = rootGo.transform;

            MeshRenderer gear = MeshChild(root, "Engrenagem", Gear(8, 0.35f, 0f), FlatAdditive);
            gear.transform.position = position + Vector3.up * 0.06f;
            var block = new MaterialPropertyBlock();
            rootGo.GetComponent<CardVisualLife>().OnTick = t =>
            {
                float s = 0.6f * EaseOutBack(Mathf.Clamp01(t / 0.22f));
                gear.transform.localScale = new Vector3(s, 1f, s);
                gear.transform.localRotation = Quaternion.Euler(0f, t * 220f, 0f);
                float a = t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f;
                Paint(gear, block, CyanBright, 0.9f * a);
            };
            rootGo.GetComponent<CardVisualLife>().OnTick(0f);

            GroundRing(position + Vector3.up * 0.04f, 1f, CyanBright, 0.4f);
            Puffs(root, position + Vector3.up * 0.15f, 5, 0.3f, 0.9f, 0.3f, 0.5f, SteamWhite, SteamWarm, 0.5f, 0.8f, -0.02f, 0.3f);
            Sparks(root, position + Vector3.up * 0.1f, 6, 1f, 2.5f, 0.1f, 0.16f, EmberHot, EmberOrange, 0.2f, 0.4f, 1.5f, 0.2f);
            Sparks(root, position + Vector3.up * 0.1f, 6, 1.2f, 3f, 0.1f, 0.14f, CyanWhite, CyanBright, 0.25f, 0.45f, 0.8f, 0.3f);
            Pop(position + Vector3.up * 0.2f, CyanWhite, 0.45f, 0.14f);
        }

        /// <summary>Explosão da mina: engrenagens de latão voando, clarão laranja chapado, onda no chão e nuvem de fumaça.</summary>
        private static void PlayMineExplode(Vector3 position, float radius)
        {
            radius = Mathf.Max(radius, 1f);
            float scale = radius / 2.5f;
            float ground = position.y - 0.1f;
            Transform root = Root("Fx_MinaExplode", position, Quaternion.identity, 1.6f).transform;

            Pop(position + Vector3.up * 0.4f, WhiteHot, radius * 0.5f, 0.18f);
            Pop(position + Vector3.up * 0.08f, EmberOrange, radius, 0.26f, true, 0.2f);
            GroundRing(position + Vector3.up * 0.06f, radius, EmberOrange, 0.4f);
            GroundRing(position + Vector3.up * 0.07f, radius * 0.75f, CyanBright, 0.5f);

            Sparks(root, position + Vector3.up * 0.3f, 30, 4f * scale, 9f * scale, 0.12f, 0.22f, EmberHot, EmberOrange, 0.35f, 0.8f, 1.5f, 0.3f);
            Sparks(root, position + Vector3.up * 0.3f, 14, 3f * scale, 6f * scale, 0.1f, 0.18f, CyanWhite, CyanBright, 0.3f, 0.6f, 1f, 0.3f);
            Puffs(root, position + Vector3.up * 0.4f, 9, 0.5f * scale, 2f * scale, 0.7f, 1.2f, SmokeDark, SteamWarm, 0.9f, 1.4f, -0.12f, 0.5f);

            // Fragmentos de engrenagem de latão.
            Debris(position + Vector3.up * 0.3f, ground, 7, Gear(7, 0.3f, 0.3f), Brass, 0.22f, 0.38f,
                3f * scale, 6.5f * scale, 4f, 7f, 1.2f);

            Flash(position + Vector3.up * 0.6f, EmberOrange, 7f, radius * 3f, 0.3f);
            CameraShake.AddAt(position, CameraShake.Medium * 1.4f, 0.35f);
        }

        /// <summary>Broquel: disco de latão com engrenagem girando, aro ciano grosso que pulsa e campo translúcido. Brilha quando bloqueia.</summary>
        private static void PlayShield(Vector3 position, Vector3 dir, float duration, Transform follow)
        {
            Vector3 forward = Flat(dir);
            var offset = new Vector3(0f, 0.95f, 1.1f);
            duration = Mathf.Max(0.2f, duration);

            GameObject root = Root("Fx_Broquel", position + forward * offset.z + Vector3.up * offset.y,
                Quaternion.LookRotation(forward, Vector3.up), duration + 0.05f);
            var life = root.GetComponent<CardVisualLife>();
            if (follow != null)
            {
                life.Follow = follow;
                life.FollowOffset = offset;
                life.SnapToFollow();
            }

            Quaternion faceZ = Quaternion.Euler(90f, 0f, 0f); // leva o plano XZ das malhas para o plano XY (de frente)

            // Disco de latão, de pé, com a face para a frente.
            MeshRenderer disc = MeshChild(root.transform, "Disco", Cylinder, Brass);
            disc.transform.localRotation = faceZ;
            disc.transform.localScale = new Vector3(1.25f, 0.05f, 1.25f);

            // Engrenagem de latão no centro, girando devagar: o "canto" do broquel.
            MeshRenderer cog = MeshChild(root.transform, "Engrenagem", Gear(10, 0.35f, 0.18f), Brass);
            cog.transform.localPosition = new Vector3(0f, 0f, 0.07f);
            cog.transform.localScale = Vector3.one * 0.42f;
            cog.transform.localRotation = faceZ;

            // Cristal no meio da engrenagem.
            MeshRenderer boss = MeshChild(root.transform, "Cristal", Sphere, Crystal);
            boss.transform.localPosition = new Vector3(0f, 0f, 0.16f);
            boss.transform.localScale = Vector3.one * 0.24f;

            // Aro ciano grosso e campo translúcido atrás dele.
            MeshRenderer rim = MeshChild(root.transform, "Aro", Ring(0.82f), FlatAdditive);
            rim.transform.localPosition = new Vector3(0f, 0f, 0.06f);
            rim.transform.localRotation = faceZ;
            MeshRenderer field = MeshChild(root.transform, "Campo", Disc, FlatAlpha);
            field.transform.localPosition = new Vector3(0f, 0f, 0.03f);
            field.transform.localRotation = faceZ;
            field.transform.localScale = new Vector3(0.74f, 1f, 0.74f);

            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0f, 0.4f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = CyanBright;
            light.range = 3.5f;
            light.intensity = 1.4f;
            light.shadows = LightShadows.None;

            var block = new MaterialPropertyBlock();
            life.OnTick = t =>
            {
                float time = t * duration;
                // Aparece e some com um pequeno estalo de escala.
                float s = Mathf.Clamp01(time / 0.08f) * Mathf.Clamp01((1f - t) * duration / 0.15f);
                root.transform.localScale = Vector3.one * Mathf.Max(0.001f, s);

                float pulse = 0.75f + 0.25f * Mathf.Sin(time * 14f);
                float flare = BlockFlare(root.transform.position);
                float rimScale = 0.74f * (1f + 0.05f * Mathf.Sin(time * 14f) + 0.14f * flare);
                rim.transform.localScale = new Vector3(rimScale, 1f, rimScale);
                Paint(rim, block, Color.Lerp(CyanBright, Color.white, flare), Mathf.Clamp01(pulse + flare));
                Paint(field, block, CyanBright, 0.12f + 0.1f * pulse + 0.35f * flare);
                cog.transform.localRotation = Quaternion.Euler(0f, 0f, time * 90f) * faceZ;
                light.intensity = 1.4f * pulse + 3f * flare;
            };
            life.OnTick(0f);

            // Estalo de entrada.
            Vector3 center = position + forward * offset.z + Vector3.up * offset.y;
            Pop(center, CyanWhite, 0.55f, 0.12f);
            Sparks(root.transform, center, 6, 1.5f, 3.5f, 0.1f, 0.16f, CyanWhite, CyanBright, 0.2f, 0.35f, 0.6f, 0.4f);
        }

        /// <summary>Tônico: coluna de luz de óleo subindo, brilho quente no chão, gotas de óleo e cristaizinhos ciano em quadrados.</summary>
        private static void PlayHeal(Vector3 position)
        {
            GameObject rootGo = Root("Fx_Tonico", position, Quaternion.identity, 1.5f);
            Transform root = rootGo.transform;
            var block = new MaterialPropertyBlock();

            // Coluna de luz quente (sobe e some) e brilho no chão.
            MeshRenderer column = MeshChild(root, "Coluna", Cylinder, FlatAdditive);
            MeshRenderer glow = MeshChild(root, "BrilhoChao", Disc, FlatAdditive);
            glow.transform.position = position + Vector3.up * 0.06f;
            rootGo.GetComponent<CardVisualLife>().OnTick = t =>
            {
                float rise = Mathf.Clamp01(t / 0.25f);
                float h = Mathf.Lerp(0.3f, 1.8f, rise);
                // O cilindro primitivo tem altura 2 e raio 0,5: a altura total é 2 x escala em Y.
                column.transform.position = position + Vector3.up * (h * 0.5f);
                column.transform.localScale = new Vector3(1.1f, h * 0.5f, 1.1f);
                float fade = 1f - t;
                Paint(column, block, OilGlow, 0.3f * fade * fade);
                float pulse = 0.85f + 0.15f * Mathf.Sin(t * 30f);
                float gs = 1.05f * pulse;
                glow.transform.localScale = new Vector3(gs, 1f, gs);
                Paint(glow, block, OilGlow, 0.5f * fade);
            };
            rootGo.GetComponent<CardVisualLife>().OnTick(0f);

            // Gotas de óleo (quentes) e cristaizinhos (ciano) subindo em volta do jogador.
            var oil = Emitter(root, "Oleo", position, Quaternion.identity, SolidParticle);
            Configure(oil, 1f, 0.8f, 1.2f, 0f, 0f, 0.12f, 0.2f, OilGlow, EmberHot, -0.02f);
            Shrink(oil);
            oil.Play();
            var motes = Emitter(root, "Cristais", position, Quaternion.identity, SolidParticle);
            Configure(motes, 1f, 0.8f, 1.3f, 0f, 0f, 0.08f, 0.12f, CyanWhite, CyanBright, -0.03f);
            Shrink(motes);
            motes.Play();
            for (int i = 0; i < 14; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                Vector3 p = position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(0.2f, 0.65f) + Vector3.up * 0.1f;
                Emit(oil, p, Vector3.up * Random.Range(1.2f, 2.4f), Random.Range(0.12f, 0.2f), Random.value < 0.5f ? OilGlow : EmberHot, Random.Range(0.8f, 1.2f));
            }
            for (int i = 0; i < 10; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                Vector3 p = position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(0.2f, 0.6f) + Vector3.up * 0.2f;
                Emit(motes, p, Vector3.up * Random.Range(1.5f, 2.8f), Random.Range(0.08f, 0.12f), Random.value < 0.5f ? CyanWhite : CyanBright, Random.Range(0.8f, 1.3f));
            }

            GroundRing(position + Vector3.up * 0.05f, 1f, OilGlow, 0.5f);
            Flash(position + Vector3.up, OilGlow, 3.5f, 6f, 0.9f);
        }

        /// <summary>Granada no ar: cristal que gira com rastro ciano e um anel no ponto de queda que se fecha.</summary>
        private static void PlayGrenadeThrow(Vector3 from, Vector3 toLanding, float flightTime)
        {
            flightTime = Mathf.Max(0.05f, flightTime);
            GameObject root = Root("Fx_Granada", from, Quaternion.identity, flightTime + 0.05f);

            MeshRenderer shard = MeshChild(root.transform, "Cristal", Cube, Crystal);
            shard.transform.localScale = new Vector3(0.18f, 0.3f, 0.18f);

            var trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.startWidth = 0.22f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.05f;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.sharedMaterial = GlowParticle;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(CyanWhite, 0f), new GradientColorKey(Violet, 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;

            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(root.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = CyanBright;
            light.range = 3f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;

            // Arco parabólico simples, com a altura proporcional à distância.
            float height = Mathf.Clamp(toLanding.magnitude * 0.15f, 0.6f, 2.2f);
            root.GetComponent<CardVisualLife>().OnTick = t =>
            {
                float f = Mathf.Clamp01(t * (flightTime + 0.05f) / flightTime);
                Vector3 p = from + toLanding * f;
                p.y += 4f * height * f * (1f - f);
                root.transform.position = p;
                root.transform.rotation = Quaternion.Euler(t * 700f, t * 360f, 0f);
            };

            // Marcador de queda: anel que fecha sobre o ponto em que a granada vai cair.
            Vector3 landing = from + toLanding;
            GameObject marker = Root("Fx_GranadaAlvo", new Vector3(landing.x, landing.y + 0.06f, landing.z), Quaternion.identity, flightTime + 0.05f);
            MeshRenderer ring = MeshChild(marker.transform, "Anel", Ring(0.7f), FlatAdditive);
            var block = new MaterialPropertyBlock();
            marker.GetComponent<CardVisualLife>().OnTick = t =>
            {
                float s = Mathf.Lerp(1.4f, 0.6f, t);
                marker.transform.localScale = new Vector3(s, 1f, s);
                Paint(ring, block, CyanBright, 0.45f + 0.35f * Mathf.Sin(t * 40f));
            };
            marker.GetComponent<CardVisualLife>().OnTick(0f);
        }

        /// <summary>Granada de cristal: estouro branco-ciano, estilhaços de cristal em malha, blocos de faísca ciano e violeta.</summary>
        private static void PlayGrenadeExplode(Vector3 position, float radius)
        {
            radius = Mathf.Max(radius, 1f);
            float scale = radius / 3f;
            float ground = position.y - 0.1f;
            Transform root = Root("Fx_GranadaExplode", position, Quaternion.identity, 1.5f).transform;

            Pop(position + Vector3.up * 0.5f, CyanWhite, radius * 0.5f, 0.18f);
            Pop(position + Vector3.up * 0.08f, CyanBright, radius, 0.26f, true, 0.2f);
            GroundRing(position + Vector3.up * 0.05f, radius, CyanBright, 0.4f);
            GroundRing(position + Vector3.up * 0.06f, radius * 0.85f, Violet, 0.55f);

            Sparks(root, position + Vector3.up * 0.3f, 34, 3f * scale, 8f * scale, 0.1f, 0.2f, CyanWhite, CyanBright, 0.3f, 0.7f, 0.8f, 0.25f);
            Sparks(root, position + Vector3.up * 0.3f, 14, 1f * scale, 4f * scale, 0.14f, 0.24f, Violet, CyanBright, 0.4f, 0.8f, 0.3f, 0.3f);
            Puffs(root, position + Vector3.up * 0.2f, 7, 0.4f * scale, 1.5f * scale, 0.5f, 0.9f, SteamWhite, SteamWhite, 0.7f, 1.2f, -0.1f, 0.4f);

            // Estilhaços de cristal em malha.
            Debris(position + Vector3.up * 0.3f, ground, 7, Cube, Crystal, 0.14f, 0.26f,
                3f * scale, 7f * scale, 4f, 7.5f, 1f);

            Flash(position + Vector3.up * 0.7f, CyanBright, 7f, radius * 3f, 0.3f);
            CameraShake.AddAt(position, CameraShake.Medium * 1.5f, 0.38f);
        }

        /// <summary>Chaminé Partida: anel de vapor em blocos que se abre, engrenagens de latão em estilhaços, anel de runa ciano e engrenagem de runa no chão.</summary>
        private static void PlayBurst(Vector3 position, float radius)
        {
            radius = Mathf.Max(radius, 1f);
            float scale = radius / 5f;
            float ground = position.y - 0.1f;
            GameObject rootGo = Root("Fx_Chamine", position, Quaternion.identity, 2f);
            Transform root = rootGo.transform;

            // Anel de vapor: blocos grandes saindo em círculo até o raio todo, e um anel interno mais branco.
            var steam = Emitter(root, "AnelVapor", position, Quaternion.identity, PuffParticle);
            Configure(steam, 0.6f, 0.45f, 0.55f, 0f, 0f, 0.9f, 1.4f, SteamWhite, SteamWarm, -0.12f);
            Grow(steam, 0.8f, 1.8f);
            Fade(steam, 0.06f);
            steam.Play();
            float outward = radius / 0.5f;
            for (int i = 0; i < 26; i++)
            {
                float a = (i + Random.Range(-0.3f, 0.3f)) / 26f * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Emit(steam, position + Vector3.up * 0.5f + d * 0.8f, d * (outward * Random.Range(0.85f, 1f)) + Vector3.up * 0.8f,
                    Random.Range(0.9f, 1.4f), Color.Lerp(SteamWhite, SteamWarm, Random.value), Random.Range(0.45f, 0.55f));
            }
            for (int i = 0; i < 14; i++)
            {
                float a = (i + Random.value) / 14f * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Emit(steam, position + Vector3.up * 0.6f + d * 0.4f, d * (outward * Random.Range(0.4f, 0.6f)) + Vector3.up * 1.6f,
                    Random.Range(0.7f, 1.1f), WhiteHot, Random.Range(0.5f, 0.65f));
            }
            Puffs(root, position + Vector3.up * 0.4f, 8, 0.5f * scale, 2f * scale, 0.8f, 1.3f, SteamWarm, SteamWhite, 0.9f, 1.4f, -0.2f, 0.5f);

            // Estilhaços de engrenagem de latão.
            Debris(position + Vector3.up * 0.4f, ground, 8, Gear(8, 0.3f, 0.3f), Brass, 0.3f, 0.45f,
                4f * scale, 7f * scale, 5f, 8f, 1.3f);

            // Runas ciano em quadrados saindo da borda do anel.
            var runes = Emitter(root, "Runas", position, Quaternion.identity, SolidParticle);
            Configure(runes, 0.6f, 0.5f, 0.9f, 0f, 0f, 0.14f, 0.22f, CyanWhite, CyanBright, 0.2f);
            Shrink(runes);
            runes.Play();
            for (int i = 0; i < 16; i++)
            {
                float a = (i + Random.value) / 16f * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Emit(runes, position + Vector3.up * 0.3f + d * (radius * 0.5f), d * Random.Range(2f, 4f) + Vector3.up * Random.Range(1f, 3f),
                    Random.Range(0.14f, 0.22f), Random.value < 0.5f ? CyanWhite : CyanBright, Random.Range(0.5f, 0.9f));
            }

            // Anéis no chão e a engrenagem de runa girando.
            GroundRing(position + Vector3.up * 0.05f, radius, CyanBright, 0.55f);
            GroundRing(position + Vector3.up * 0.07f, radius * 0.8f, WhiteHot, 0.7f);
            MeshRenderer rune = MeshChild(root, "EngrenagemRuna", Gear(12, 0.55f, 0f), FlatAdditive);
            rune.transform.position = position + Vector3.up * 0.08f;
            var block = new MaterialPropertyBlock();
            rootGo.GetComponent<CardVisualLife>().OnTick = t =>
            {
                float time = t * 2f;
                float s = radius * 0.9f * (1f - (1f - Mathf.Clamp01(time / 0.25f)) * (1f - Mathf.Clamp01(time / 0.25f)) * 0.7f);
                rune.transform.localScale = new Vector3(s, 1f, s);
                rune.transform.localRotation = Quaternion.Euler(0f, time * 120f, 0f);
                Paint(rune, block, CyanBright, 0.75f * Mathf.Clamp01(1f - (time - 0.4f) / 0.8f));
            };
            rootGo.GetComponent<CardVisualLife>().OnTick(0f);

            Pop(position + Vector3.up * 0.6f, CyanWhite, radius * 0.35f, 0.2f);
            Pop(position + Vector3.up * 0.08f, WhiteHot, radius * 0.6f, 0.3f, true, 0.2f);
            Flash(position + Vector3.up * 0.8f, CyanWhite, 7f, radius * 2.5f, 0.4f);
            CameraShake.AddAt(position, CameraShake.Large, 0.5f);
        }

        /// <summary>Lâmina Sedenta: o mesmo crescente varrido do golpe básico, mas largo, grosso e carmim (corte amaldiçoado).</summary>
        private static void PlaySlash(Vector3 position, Vector3 dir, float range)
        {
            Vector3 forward = Flat(dir);
            range = Mathf.Max(range, 0.5f);

            SwingVisual.Play(position, forward, range, SlashHalfAngle, CursedFill, CursedCore, CursedEdge, 0.62f);
            Sparks(Root("Fx_CorteFaiscas", position, Quaternion.identity, 0.6f).transform, position + forward * (range * 0.7f) + Vector3.up * 0.8f,
                12, 2f, 6f, 0.12f, 0.22f, CursedCore, CursedEdge, 0.25f, 0.5f, 1.2f, 0.5f);
            Flash(position + forward * (range * 0.5f) + Vector3.up, CursedEdge, 4.5f, range * 2f, 0.22f);
            CameraShake.AddAt(position, CameraShake.Small * 1.5f, 0.2f);
        }

        /// <summary>
        /// Chicote de Corrente e Martelo a Vapor: o mesmo crescente do golpe, nas cores quentes do latão e com a abertura e o alcance
        /// do efeito (o Y da direção é a meia-abertura em graus). Sem a abertura (Y não positivo) usa o arco de 160° da Lâmina.
        /// </summary>
        private static void PlaySlashArc(Vector3 position, Vector3 dir, float range)
        {
            Vector3 forward = Flat(dir);
            range = Mathf.Max(range, 0.5f);
            float halfAngle = dir.y > 0f ? Mathf.Clamp(dir.y, 5f, 180f) : SlashHalfAngle;
            // Arco estreito (chicote) fica mais fino; arco largo (martelo) mais grosso.
            float thickness = Mathf.Clamp(0.3f + halfAngle / 180f, 0.3f, 0.62f);

            SwingVisual.Play(position, forward, range, halfAngle, EmberOrange, WhiteHot, CyanBright, thickness);
            Sparks(Root("Fx_CorteArco", position, Quaternion.identity, 0.6f).transform, position + forward * (range * 0.7f) + Vector3.up * 0.8f,
                12, 2f, 6f, 0.12f, 0.22f, EmberHot, EmberOrange, 0.25f, 0.5f, 1.2f, 0.5f);
            Flash(position + forward * (range * 0.5f) + Vector3.up, EmberHot, 4.5f, range * 2f, 0.22f);
            CameraShake.AddAt(position, CameraShake.Small * 1.5f, 0.2f);
        }

        /// <summary>Pulso do broquel (ao bloquear): onda de faíscas ciano, anéis grossos no chão e ondulações no disco do escudo.</summary>
        private static void PlayPulse(Vector3 position, Vector3 direction, float radius)
        {
            radius = Mathf.Max(radius, 0.8f);
            Vector3 forward = Flat(direction);
            GameObject rootGo = Root("Fx_Pulso", position, Quaternion.identity, 1f);
            Transform root = rootGo.transform;

            // Anel de faíscas em quadrados que corre para fora.
            var ring = Emitter(root, "Faiscas", position, Quaternion.identity, SolidParticle);
            Configure(ring, 0.5f, 0.3f, 0.4f, 0f, 0f, 0.12f, 0.2f, CyanWhite, CyanBright);
            Shrink(ring);
            ring.Play();
            for (int i = 0; i < 22; i++)
            {
                float a = (i + Random.value) / 22f * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Emit(ring, position + Vector3.up * 0.3f + d * 0.4f, d * (radius / 0.33f * Random.Range(0.85f, 1.05f)) + Vector3.up * Random.Range(0f, 1f),
                    Random.Range(0.12f, 0.2f), Random.value < 0.5f ? CyanWhite : CyanBright, Random.Range(0.3f, 0.4f));
            }

            GroundRing(position + Vector3.up * 0.04f, radius, CyanBright, 0.4f);
            GroundRing(position + Vector3.up * 0.05f, radius * 0.7f, CyanWhite, 0.28f);

            // Ondulações no disco do escudo (à frente do jogador), e o visual do escudo brilha.
            Vector3 shieldCenter = position + forward * 1.1f + Vector3.up * 0.9f;
            Ripple(shieldCenter, forward, 1.1f, CyanWhite, 0.28f, 0f);
            Ripple(shieldCenter, forward, 1.5f, CyanBright, 0.35f, 0.08f);
            lastBlockTime = Time.time;
            lastBlockPosition = position;

            Pop(shieldCenter, CyanWhite, 0.6f, 0.14f);
            Flash(position + Vector3.up * 0.8f, CyanBright, 4f, radius * 2f, 0.25f);
            CameraShake.AddAt(position, CameraShake.Small * 1.3f, 0.2f);
        }

        // ---------- Construção ----------

        /// <summary>Anel vertical (de frente para forward) que se abre e some; delay atrasa o começo.</summary>
        private static void Ripple(Vector3 center, Vector3 forward, float radius, Color color, float life, float delay)
        {
            var go = new GameObject("Ondulacao");
            go.transform.SetPositionAndRotation(center, Quaternion.LookRotation(forward, Vector3.up));
            MeshRenderer r = MeshChild(go.transform, "Malha", Ring(0.78f), FlatAdditive);
            r.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var block = new MaterialPropertyBlock();

            var visual = go.AddComponent<CardVisualLife>();
            visual.Life = life + delay;
            visual.OnTick = t =>
            {
                float time = t * (life + delay) - delay;
                if (time < 0f)
                {
                    go.transform.localScale = Vector3.zero;
                    return;
                }
                float k = Mathf.Clamp01(time / life);
                float open = 1f - (1f - k) * (1f - k);
                float s = Mathf.Lerp(0.3f, 1f, open) * radius;
                go.transform.localScale = new Vector3(s, s, s);
                Paint(r, block, color, 1f - k * k);
            };
            visual.OnTick(0f);
        }

        /// <summary>Quanto o escudo está brilhando por ter bloqueado agora (0 a 1), se o bloqueio foi perto dele.</summary>
        private static float BlockFlare(Vector3 shieldPosition)
        {
            float age = Time.time - lastBlockTime;
            if (age < 0f || age > 0.3f)
                return 0f;
            Vector3 d = shieldPosition - lastBlockPosition;
            d.y = 0f;
            if (d.sqrMagnitude > 2.5f * 2.5f)
                return 0f;
            return 1f - age / 0.3f;
        }

        // ---------- Materiais em malha ----------

        private static Material Brass
        {
            get
            {
                if (Library != null && Library.brass != null)
                    return Library.brass;
                if (fallbackBrass == null)
                    fallbackBrass = FallbackLit(new Color(0.78f, 0.62f, 0.3f), 0.9f, 0.5f, null);
                return fallbackBrass;
            }
        }

        private static Material Crystal
        {
            get
            {
                if (Library != null && Library.crystal != null)
                    return Library.crystal;
                if (fallbackCrystal == null)
                    fallbackCrystal = FallbackLit(new Color(0.3f, 0.9f, 0.95f), 0f, 0.9f, new Color(0.2f, 1.4f, 1.6f));
                return fallbackCrystal;
            }
        }

        private static Material FallbackLit(Color color, float metallic, float smoothness, Color? emission)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            var m = new Material(shader) { name = "CardLit (runtime)", hideFlags = HideFlags.HideAndDontSave };
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", color);
            SetFloat(m, "_Metallic", metallic);
            SetFloat(m, "_Smoothness", smoothness);
            if (emission.HasValue && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            return m;
        }
    }
}
