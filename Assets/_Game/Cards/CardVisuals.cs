using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Cards
{
    /// <summary>
    /// Fábrica de visuais das cartas, só no cliente (inclui o host). Cada visual é um objeto curto, criado em
    /// runtime com partículas, malhas e luzes, e some sozinho. Estilo semi-realista sombrio (D-016, D-017):
    /// vapor cinza, brasa laranja, cristal ciano. Só desenho: o dano e o alcance são decididos pelo host.
    ///
    /// Convenção de (posição, direção, tamanho) por visual:
    ///  dash: início da investida, direção, comprimento percorrido.
    ///  cone_vapor: origem do cone, direção, alcance.
    ///  arc_chain: início do segmento, direção, comprimento.
    ///  mine_place / mine_explode: ponto no chão, qualquer direção, raio da explosão.
    ///  shield: posição do jogador, direção em que ele olha, duração em segundos (segue o Transform dado).
    ///  heal: posição do jogador.
    ///  grenade_throw: ponto de saída, VETOR até o ponto de queda (não normalizado), tempo de voo.
    ///  grenade_explode / burst / pulse: centro, qualquer direção, raio.
    ///  slash_wide: posição do jogador, direção do corte, alcance (arco fixo de 160°).
    /// </summary>
    public static class CardVisuals
    {
        /// <summary>Ordem fixa: o índice é o que trafega na rede (só acrescente no fim).</summary>
        public static readonly string[] Ids =
        {
            "dash", "cone_vapor", "arc_chain", "mine_place", "mine_explode", "shield", "heal",
            "grenade_throw", "grenade_explode", "burst", "slash_wide", "pulse"
        };

        /// <summary>Materiais do projeto. Definido pelo PlayerCards ao nascer.</summary>
        public static CardVisualLibrary Library { get; set; }

        // Cores (tons escuros e dessaturados; o brilho vem do material aditivo).
        private static readonly Color SteamGrey = new Color(0.62f, 0.67f, 0.72f, 0.32f);
        private static readonly Color SteamWarm = new Color(0.72f, 0.66f, 0.6f, 0.34f);
        private static readonly Color CyanTint = new Color(0.7f, 1f, 1f, 1f);
        private static readonly Color CyanBright = new Color(0.45f, 0.95f, 1f, 1f);
        private static readonly Color EmberTint = new Color(1f, 0.78f, 0.5f, 1f);
        private static readonly Color EmberDeep = new Color(1f, 0.5f, 0.2f, 1f);
        private static readonly Color OilGlow = new Color(1f, 0.72f, 0.36f, 1f);
        private static readonly Color Violet = new Color(0.62f, 0.42f, 1f, 1f);
        private static readonly Color CursedFill = new Color(0.62f, 0.08f, 0.32f, 1f);
        private static readonly Color CursedEdge = new Color(1f, 0.3f, 0.55f, 1f);

        private const float SlashHalfAngle = 80f;
        private const int FanSegments = 20;
        private const int RingSegments = 48;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static Material fxAlpha, fxAdditive;
        private static Material fallbackSteam, fallbackAdditive, fallbackBrass, fallbackCrystal;
        private static Mesh ringMesh, cylinderMesh, sphereMesh;
        private static readonly Dictionary<int, Mesh> fillMeshes = new Dictionary<int, Mesh>();
        private static readonly Dictionary<int, Mesh> edgeMeshes = new Dictionary<int, Mesh>();

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
                    case "pulse": PlayPulse(position, size); break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Visual de carta '{id}' falhou: {e.Message}");
            }
        }

        // ---------- Visuais ----------

        private static void PlayDash(Vector3 start, Vector3 dir, float length)
        {
            dir = Flat(dir);
            length = Mathf.Max(length, 0.4f);
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
            Vector3 mid = start + dir * (length * 0.5f) + Vector3.up * 0.7f;
            Transform root = Root("Fx_Investida", mid, rot, 2f).transform;
            Vector3 box = new Vector3(0.6f, 0.5f, length);

            // Rastro de vapor ao longo do caminho.
            var steam = Emitter(root, "Vapor", mid, rot, Steam);
            Configure(steam, 0.3f, 0.5f, 0.9f, 0.1f, 0.5f, 0.35f, 0.65f, SteamGrey, SteamGrey, -0.05f);
            Shape(steam, ParticleSystemShapeType.Box, scale: box);
            Burst(steam, Mathf.Max(6, Mathf.RoundToInt(length * 5f)));
            Grow(steam, 0.6f, 1.6f);
            Fade(steam, 0.1f);
            steam.Play();

            // Faíscas de runa: o impulso é arcano.
            var runes = Emitter(root, "Runas", mid, rot, Arcane);
            Configure(runes, 0.3f, 0.25f, 0.5f, 0.5f, 1.5f, 0.06f, 0.12f, CyanTint, CyanBright);
            Shape(runes, ParticleSystemShapeType.Box, scale: box);
            Burst(runes, Mathf.Max(6, Mathf.RoundToInt(length * 4f)));
            Fade(runes);
            runes.Play();

            // Fagulhas de atrito do pistão.
            var embers = Emitter(root, "Fagulhas", start + Vector3.up * 0.15f, rot, Ember);
            Configure(embers, 0.3f, 0.25f, 0.5f, 1f, 2.5f, 0.04f, 0.08f, EmberTint, EmberDeep, 0.6f);
            Shape(embers, ParticleSystemShapeType.Sphere, radius: 0.3f);
            Burst(embers, 8);
            Fade(embers);
            embers.Play();

            Flash(mid, CyanBright, 2.5f, 5f, 0.25f);
        }

        private static void PlayCone(Vector3 origin, Vector3 dir, float range)
        {
            dir = Flat(dir);
            range = Mathf.Max(range, 0.5f);
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
            Transform root = Root("Fx_Sopro", origin, rot, 1.2f).transform;
            const float life = 0.5f;

            var steam = Emitter(root, "Vapor", origin, rot, Steam);
            Configure(steam, 0.3f, life * 0.9f, life * 1.1f, range / life * 0.8f, range / life * 1.05f, 0.4f, 0.8f,
                SteamWarm, SteamWarm, -0.02f);
            Shape(steam, ParticleSystemShapeType.Cone, radius: 0.12f, angle: 26f);
            Rate(steam, 150f);
            Grow(steam, 0.6f, 1.7f);
            Fade(steam, 0.08f);
            steam.Play();

            // Runas fracas dentro do vapor: a queima do arcano.
            var runes = Emitter(root, "Runas", origin, rot, Arcane);
            Configure(runes, 0.3f, life * 0.8f, life * 1f, range / life * 0.7f, range / life, 0.05f, 0.09f,
                CyanTint, CyanBright);
            Shape(runes, ParticleSystemShapeType.Cone, radius: 0.1f, angle: 18f);
            Rate(runes, 32f);
            Fade(runes);
            runes.Play();
        }

        private static void PlayArc(Vector3 from, Vector3 dir, float length)
        {
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            length = Mathf.Max(0.2f, length);
            Vector3 to = from + dir * length;
            int points = Mathf.Clamp(Mathf.CeilToInt(length / 0.45f) + 1, 3, 24);

            GameObject root = Root("Fx_Arco", from, Quaternion.identity, 0.22f);
            LineRenderer bolt = Line(root.transform, "Raio", points, 0.12f, 0.06f);
            LineRenderer glow = Line(root.transform, "Brilho", points, 0.4f, 0.22f);

            Vector3 side = Vector3.Cross(dir, Vector3.up);
            side = side.sqrMagnitude > 0.01f ? side.normalized : Vector3.right;
            Vector3 up = Vector3.Cross(side, dir).normalized;
            var block = new MaterialPropertyBlock();

            root.GetComponent<CardVisualLife>().OnTick = t =>
            {
                // O raio treme a cada quadro: nunca fica no mesmo desenho.
                for (int i = 0; i < points; i++)
                {
                    float f = i / (float)(points - 1);
                    Vector3 p = Vector3.Lerp(from, to, f);
                    if (i > 0 && i < points - 1)
                    {
                        float amplitude = 0.07f + 0.18f * Mathf.Sin(f * Mathf.PI);
                        p += side * (Random.Range(-1f, 1f) * amplitude) + up * (Random.Range(-1f, 1f) * amplitude);
                    }
                    bolt.SetPosition(i, p);
                    glow.SetPosition(i, p);
                }
                float fade = 1f - t;
                Paint(bolt, block, new Color(0.85f, 1f, 1f), fade);
                Paint(glow, block, CyanBright, 0.35f * fade);
            };
            root.GetComponent<CardVisualLife>().OnTick(0f);

            // Impacto: faíscas curtas e um clarão onde a descarga chega.
            var sparks = Emitter(root.transform, "Impacto", to, Quaternion.identity, Arcane);
            Configure(sparks, 0.2f, 0.15f, 0.35f, 2f, 5f, 0.05f, 0.1f, CyanTint, CyanBright);
            Shape(sparks, ParticleSystemShapeType.Sphere, radius: 0.1f);
            Burst(sparks, 10);
            Fade(sparks);
            sparks.Play();
            Flash(to, CyanBright, 3f, 4f, 0.2f);
        }

        private static void PlayMinePlace(Vector3 position)
        {
            Quaternion flat = Quaternion.Euler(-90f, 0f, 0f);
            Transform root = Root("Fx_MinaPlantada", position, Quaternion.identity, 1.6f).transform;

            // Anel de poeira arcana que se abre no chão.
            var ring = Emitter(root, "Anel", position + Vector3.up * 0.05f, flat, Arcane);
            Configure(ring, 0.2f, 0.5f, 0.8f, 0.4f, 0.8f, 0.05f, 0.09f, CyanTint, CyanBright);
            Shape(ring, ParticleSystemShapeType.Circle, radius: 0.35f, thickness: 0f);
            Burst(ring, 14);
            Fade(ring);
            ring.Play();

            var steam = Emitter(root, "Vapor", position + Vector3.up * 0.1f, flat, Steam);
            Configure(steam, 0.3f, 0.8f, 1.2f, 0.1f, 0.3f, 0.3f, 0.5f, SteamGrey, SteamGrey, -0.03f);
            Shape(steam, ParticleSystemShapeType.Circle, radius: 0.3f);
            Burst(steam, 6);
            Grow(steam, 0.6f, 1.5f);
            Fade(steam, 0.1f);
            steam.Play();

            var sparks = Emitter(root, "Fagulhas", position + Vector3.up * 0.1f, Quaternion.identity, Ember);
            Configure(sparks, 0.2f, 0.25f, 0.5f, 1f, 2.2f, 0.04f, 0.07f, EmberTint, EmberDeep, 1f);
            Shape(sparks, ParticleSystemShapeType.Sphere, radius: 0.15f);
            Burst(sparks, 6);
            Fade(sparks);
            sparks.Play();

            Ring(position + Vector3.up * 0.04f, 0.9f, CyanBright, 0.35f);
        }

        private static void PlayMineExplode(Vector3 position, float radius)
        {
            radius = Mathf.Max(radius, 1f);
            float scale = radius / 2.5f;
            Transform root = Root("Fx_MinaExplode", position, Quaternion.identity, 2.4f).transform;

            var sparks = Emitter(root, "Fagulhas", position, Quaternion.identity, Ember);
            Configure(sparks, 0.3f, 0.4f, 0.9f, 4f * scale, 9f * scale, 0.05f, 0.1f, EmberTint, EmberDeep, 1.2f);
            Shape(sparks, ParticleSystemShapeType.Sphere, radius: 0.3f);
            Burst(sparks, 40);
            Fade(sparks);
            sparks.Play();

            var steam = Emitter(root, "Vapor", position + Vector3.up * 0.3f, Quaternion.identity, Steam);
            Configure(steam, 0.4f, 1f, 1.6f, 0.5f * scale, 2f * scale, 0.6f, 1.1f, SteamGrey, SteamGrey, -0.1f);
            Shape(steam, ParticleSystemShapeType.Sphere, radius: 0.5f);
            Burst(steam, 14);
            Grow(steam, 0.6f, 1.8f);
            Fade(steam, 0.08f);
            steam.Play();

            var arcane = Emitter(root, "Cristal", position + Vector3.up * 0.2f, Quaternion.identity, Arcane);
            Configure(arcane, 0.3f, 0.3f, 0.7f, 3f * scale, 6f * scale, 0.06f, 0.12f, CyanTint, CyanBright);
            Shape(arcane, ParticleSystemShapeType.Sphere, radius: 0.3f);
            Burst(arcane, 24);
            Fade(arcane);
            arcane.Play();

            Ring(position + Vector3.up * 0.05f, radius, EmberDeep, 0.35f);
            Ring(position + Vector3.up * 0.06f, radius * 0.8f, CyanBright, 0.45f);
            Flash(position + Vector3.up * 0.6f, EmberDeep, 6f, radius * 3f, 0.3f);
        }

        private static void PlayShield(Vector3 position, Vector3 dir, float duration, Transform follow)
        {
            EnsureMeshes();
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

            // Disco de latão, de pé, com a face para a frente.
            MeshRenderer disc = MeshChild(root.transform, "Disco", cylinderMesh, Brass);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            disc.transform.localScale = new Vector3(1.25f, 0.035f, 1.25f);

            // Cristal no centro.
            MeshRenderer boss = MeshChild(root.transform, "Cristal", sphereMesh, Crystal);
            boss.transform.localPosition = new Vector3(0f, 0f, 0.07f);
            boss.transform.localScale = Vector3.one * 0.2f;

            // Aro ciano fino na borda.
            LineRenderer rim = Line(root.transform, "Aro", 40, 0.05f, 0.05f);
            rim.useWorldSpace = false;
            rim.loop = true;
            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2f;
                rim.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.66f, Mathf.Sin(a) * 0.66f, 0.05f));
            }

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
                // Aparece e some com um pequeno estalo de escala.
                float s = Mathf.Clamp01(t * duration / 0.08f) * Mathf.Clamp01((1f - t) * duration / 0.15f);
                root.transform.localScale = Vector3.one * Mathf.Max(0.001f, s);
                float pulse = 0.75f + 0.25f * Mathf.Sin(t * duration * 14f);
                Paint(rim, block, CyanBright, pulse);
                light.intensity = 1.4f * pulse;
            };
            life.OnTick(0f);
        }

        private static void PlayHeal(Vector3 position)
        {
            Quaternion flat = Quaternion.Euler(-90f, 0f, 0f);
            Transform root = Root("Fx_Tonico", position, Quaternion.identity, 2f).transform;

            // Óleo aquecido subindo.
            var oil = Emitter(root, "Oleo", position + Vector3.up * 0.1f, flat, Ember);
            Configure(oil, 0.5f, 0.9f, 1.4f, 0.3f, 0.8f, 0.08f, 0.16f, OilGlow, EmberTint, -0.05f);
            Shape(oil, ParticleSystemShapeType.Circle, radius: 0.6f, thickness: 1f);
            Burst(oil, 16);
            Fade(oil, 0.15f);
            oil.Play();

            // Partículas de cristal no meio do óleo.
            var motes = Emitter(root, "Cristais", position + Vector3.up * 0.1f, flat, Arcane);
            Configure(motes, 0.5f, 1f, 1.5f, 0.2f, 0.6f, 0.05f, 0.1f, CyanTint, CyanBright, -0.03f);
            Shape(motes, ParticleSystemShapeType.Circle, radius: 0.55f, thickness: 1f);
            Burst(motes, 14);
            Fade(motes, 0.15f);
            motes.Play();

            Ring(position + Vector3.up * 0.04f, 0.9f, OilGlow * 0.5f, 0.5f);
            Flash(position + Vector3.up, OilGlow, 3f, 5f, 0.9f);
        }

        private static void PlayGrenadeThrow(Vector3 from, Vector3 toLanding, float flightTime)
        {
            EnsureMeshes();
            flightTime = Mathf.Max(0.05f, flightTime);
            GameObject root = Root("Fx_Granada", from, Quaternion.identity, flightTime + 0.05f);

            MeshRenderer shard = MeshChild(root.transform, "Cristal", sphereMesh, Crystal);
            shard.transform.localScale = new Vector3(0.14f, 0.26f, 0.14f);

            var trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.startWidth = 0.16f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.05f;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.sharedMaterial = Arcane;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(CyanTint, 0f), new GradientColorKey(Violet, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
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
        }

        private static void PlayGrenadeExplode(Vector3 position, float radius)
        {
            radius = Mathf.Max(radius, 1f);
            float scale = radius / 3f;
            Transform root = Root("Fx_GranadaExplode", position, Quaternion.identity, 2.2f).transform;

            // Estilhaços de cristal.
            var shards = Emitter(root, "Estilhacos", position + Vector3.up * 0.3f, Quaternion.identity, Arcane);
            Configure(shards, 0.3f, 0.4f, 0.9f, 3f * scale, 8f * scale, 0.05f, 0.11f, CyanTint, CyanBright, 0.6f);
            Shape(shards, ParticleSystemShapeType.Sphere, radius: 0.25f);
            Burst(shards, 36);
            Fade(shards);
            shards.Play();

            var violet = Emitter(root, "Arcano", position + Vector3.up * 0.3f, Quaternion.identity, Arcane);
            Configure(violet, 0.3f, 0.5f, 1f, 1f * scale, 4f * scale, 0.08f, 0.16f, Violet, CyanBright);
            Shape(violet, ParticleSystemShapeType.Sphere, radius: 0.3f);
            Burst(violet, 14);
            Fade(violet);
            violet.Play();

            var steam = Emitter(root, "Vapor", position + Vector3.up * 0.2f, Quaternion.identity, Steam);
            Configure(steam, 0.4f, 0.8f, 1.3f, 0.4f * scale, 1.5f * scale, 0.4f, 0.8f, SteamGrey, SteamGrey, -0.08f);
            Shape(steam, ParticleSystemShapeType.Sphere, radius: 0.4f);
            Burst(steam, 8);
            Grow(steam, 0.6f, 1.6f);
            Fade(steam, 0.08f);
            steam.Play();

            Ring(position + Vector3.up * 0.05f, radius, CyanBright, 0.4f);
            Ring(position + Vector3.up * 0.06f, radius * 0.85f, Violet, 0.55f);
            Flash(position + Vector3.up * 0.7f, CyanBright, 7f, radius * 3f, 0.3f);
        }

        private static void PlayBurst(Vector3 position, float radius)
        {
            radius = Mathf.Max(radius, 1f);
            float scale = radius / 5f;
            Transform root = Root("Fx_Chamine", position, Quaternion.identity, 2.8f).transform;

            // Vapor em abundância, subindo.
            var steam = Emitter(root, "Vapor", position + Vector3.up * 0.4f, Quaternion.identity, Steam);
            Configure(steam, 0.5f, 1.2f, 1.9f, 1.5f * scale, 4f * scale, 0.8f, 1.5f, SteamWarm, SteamGrey, -0.12f);
            Shape(steam, ParticleSystemShapeType.Sphere, radius: 0.6f);
            Burst(steam, 28);
            Grow(steam, 0.5f, 1.8f);
            Fade(steam, 0.08f);
            steam.Play();

            // Poeira arcana que sai junto com o vapor.
            var dust = Emitter(root, "Runas", position + Vector3.up * 0.3f, Quaternion.identity, Arcane);
            Configure(dust, 0.4f, 0.6f, 1.2f, 2f * scale, 6f * scale, 0.06f, 0.13f, CyanTint, CyanBright, -0.05f);
            Shape(dust, ParticleSystemShapeType.Sphere, radius: 0.5f);
            Burst(dust, 30);
            Fade(dust);
            dust.Play();

            var embers = Emitter(root, "Brasas", position + Vector3.up * 0.3f, Quaternion.identity, Ember);
            Configure(embers, 0.4f, 0.5f, 1f, 2f * scale, 5f * scale, 0.04f, 0.09f, EmberTint, EmberDeep, 0.5f);
            Shape(embers, ParticleSystemShapeType.Sphere, radius: 0.4f);
            Burst(embers, 14);
            Fade(embers);
            embers.Play();

            // Anel arcano de vapor que se abre até o raio todo.
            Ring(position + Vector3.up * 0.05f, radius, CyanBright, 0.5f);
            Ring(position + Vector3.up * 0.07f, radius * 0.8f, new Color(0.8f, 0.85f, 0.9f), 0.7f);
            Flash(position + Vector3.up * 0.8f, CyanTint, 6f, radius * 2.5f, 0.35f);
        }

        private static void PlaySlash(Vector3 position, Vector3 dir, float range)
        {
            EnsureMeshes();
            Vector3 forward = Flat(dir);
            range = Mathf.Max(range, 0.5f);

            GameObject root = Root("Fx_Corte", position + Vector3.up * 0.1f,
                Quaternion.LookRotation(forward, Vector3.up), 0.25f);
            MeshRenderer fill = MeshChild(root.transform, "Preenchimento", FanFill(SlashHalfAngle), FxAlpha);
            MeshRenderer edge = MeshChild(root.transform, "Borda", FanEdge(SlashHalfAngle), FxAdditive);
            var block = new MaterialPropertyBlock();

            root.GetComponent<CardVisualLife>().OnTick = t =>
            {
                // O arco abre rápido (ease-out) e some; a borda dura um pouco mais.
                float open = 1f - (1f - t) * (1f - t);
                float s = Mathf.Lerp(0.7f, 1f, open) * range;
                root.transform.localScale = new Vector3(s, 1f, s);
                Paint(fill, block, CursedFill, 0.5f * (1f - t) * (1f - t));
                Paint(edge, block, CursedEdge, 0.95f * (1f - t));
            };
            root.GetComponent<CardVisualLife>().OnTick(0f);

            // Fagulhas rubras ao longo do arco.
            Vector3 middle = position + forward * (range * 0.7f) + Vector3.up * 0.9f;
            var sparks = Emitter(root.transform, "Fagulhas", middle, Quaternion.LookRotation(forward, Vector3.up), Ember);
            Configure(sparks, 0.2f, 0.2f, 0.45f, 1f, 3f, 0.04f, 0.08f, CursedEdge, EmberDeep, 0.4f);
            Shape(sparks, ParticleSystemShapeType.Box, scale: new Vector3(range * 1.3f, 0.5f, 0.3f));
            Burst(sparks, 12);
            Fade(sparks);
            sparks.Play();

            Flash(position + forward * (range * 0.5f) + Vector3.up, CursedEdge, 4f, range * 2f, 0.2f);
        }

        private static void PlayPulse(Vector3 position, float radius)
        {
            radius = Mathf.Max(radius, 0.8f);
            Quaternion flat = Quaternion.Euler(-90f, 0f, 0f);
            Transform root = Root("Fx_Pulso", position, Quaternion.identity, 1f).transform;

            // Anel de faíscas que corre para fora.
            var ring = Emitter(root, "Faiscas", position, flat, Arcane);
            Configure(ring, 0.2f, 0.35f, 0.45f, radius / 0.4f * 0.9f, radius / 0.4f * 1.05f, 0.06f, 0.11f, CyanTint, CyanBright);
            Shape(ring, ParticleSystemShapeType.Circle, radius: 0.25f, thickness: 0f);
            Burst(ring, 24);
            Fade(ring);
            ring.Play();

            Ring(position, radius, CyanBright, 0.4f);
            Ring(position + Vector3.up * 0.02f, radius * 0.7f, new Color(0.9f, 1f, 1f), 0.28f);
            Flash(position + Vector3.up * 0.8f, CyanBright, 4f, radius * 2f, 0.25f);
        }

        // ---------- Construção ----------

        private static GameObject Root(string name, Vector3 position, Quaternion rotation, float life)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(position, rotation);
            go.AddComponent<CardVisualLife>().Life = life;
            return go;
        }

        /// <summary>Sistema de partículas parado e vazio, com o material e os módulos básicos.</summary>
        private static ParticleSystem Emitter(Transform parent, string name, Vector3 position, Quaternion rotation, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private static void Configure(ParticleSystem ps, float duration, float lifeMin, float lifeMax, float speedMin,
            float speedMax, float sizeMin, float sizeMax, Color colorA, Color colorB, float gravity = 0f)
        {
            var main = ps.main;
            main.duration = duration;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.gravityModifier = gravity;
        }

        private static void Burst(ParticleSystem ps, int count)
        {
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        private static void Rate(ParticleSystem ps, float perSecond)
        {
            var emission = ps.emission;
            emission.rateOverTime = perSecond;
        }

        private static void Shape(ParticleSystem ps, ParticleSystemShapeType type, float radius = 0.3f,
            float angle = 25f, float thickness = 1f, Vector3? scale = null)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = type;
            shape.radius = radius;
            shape.angle = angle;
            shape.radiusThickness = thickness;
            if (scale.HasValue)
                shape.scale = scale.Value;
        }

        private static void Grow(ParticleSystem ps, float from, float to)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
        }

        /// <summary>Some no fim da vida; com fadeIn, também aparece devagar no começo.</summary>
        private static void Fade(ParticleSystem ps, float fadeIn = 0f)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(fadeIn > 0f ? 0f : 1f, 0f),
                    new GradientAlphaKey(1f, Mathf.Max(fadeIn, 0.01f)),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;
        }

        private static void Flash(Vector3 position, Color color, float intensity, float range, float life)
        {
            var go = new GameObject("Clarao");
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;

            var visual = go.AddComponent<CardVisualLife>();
            visual.Life = life;
            visual.OnTick = t => light.intensity = intensity * (1f - t) * (1f - t);
        }

        /// <summary>Anel achatado no chão que se abre até o raio e some.</summary>
        private static void Ring(Vector3 position, float radius, Color color, float life, float startScale = 0.15f)
        {
            EnsureMeshes();
            var go = new GameObject("Anel");
            go.transform.position = position;
            MeshRenderer renderer = MeshChild(go.transform, "Malha", ringMesh, FxAdditive);
            var block = new MaterialPropertyBlock();

            var visual = go.AddComponent<CardVisualLife>();
            visual.Life = life;
            visual.OnTick = t =>
            {
                float open = 1f - (1f - t) * (1f - t);
                float s = Mathf.Lerp(startScale, 1f, open) * radius;
                go.transform.localScale = new Vector3(s, 1f, s);
                Paint(renderer, block, color, 0.8f * (1f - t) * (1f - t));
            };
            visual.OnTick(0f);
        }

        private static MeshRenderer MeshChild(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        /// <summary>Linha com N pontos, material aditivo sem textura (a cor vai pelo MaterialPropertyBlock).</summary>
        private static LineRenderer Line(Transform parent, string name, int points, float startWidth, float endWidth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = points;
            line.useWorldSpace = true;
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.numCapVertices = 2;
            line.alignment = LineAlignment.View;
            line.sharedMaterial = FxAdditive;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static void Paint(Renderer renderer, MaterialPropertyBlock block, Color color, float alpha)
        {
            color.a = alpha;
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color); // URP Unlit
            block.SetColor(ColorId, color);     // Sprites/Default (reserva)
            renderer.SetPropertyBlock(block);
        }

        // ---------- Materiais ----------

        private static Material Steam =>
            Library != null && Library.steam != null ? Library.steam : FallbackParticle(ref fallbackSteam, false);

        private static Material Ember =>
            Library != null && Library.ember != null ? Library.ember : FallbackParticle(ref fallbackAdditive, true);

        private static Material Arcane =>
            Library != null && Library.arcane != null ? Library.arcane : FallbackParticle(ref fallbackAdditive, true);

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

        private static Material FxAlpha
        {
            get
            {
                if (fxAlpha == null)
                    fxAlpha = CreateFxMaterial(false);
                return fxAlpha;
            }
        }

        private static Material FxAdditive
        {
            get
            {
                if (fxAdditive == null)
                    fxAdditive = CreateFxMaterial(true);
                return fxAdditive;
            }
        }

        /// <summary>Unlit transparente sem textura, para anéis, leques e linhas (cor por MaterialPropertyBlock).</summary>
        private static Material CreateFxMaterial(bool additive)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            bool urp = shader != null;
            if (!urp)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            var m = new Material(shader)
            {
                name = additive ? "CardFxAditivo (runtime)" : "CardFxAlfa (runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            if (urp)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", additive ? 2f : 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Cull", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetShaderPassEnabled("ShadowCaster", false);
            }
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        /// <summary>Reserva quando a biblioteca não tem o material de partícula: quadrados simples.</summary>
        private static Material FallbackParticle(ref Material cache, bool additive)
        {
            if (cache != null)
                return cache;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            var m = new Material(shader) { name = "CardParticula (runtime)", hideFlags = HideFlags.HideAndDontSave };
            SetFloat(m, "_Surface", 1f);
            SetFloat(m, "_Blend", additive ? 2f : 0f);
            SetFloat(m, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloat(m, "_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            SetFloat(m, "_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            cache = m;
            return m;
        }

        private static Material FallbackLit(Color color, float metallic, float smoothness, Color? emission)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
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

        private static void SetFloat(Material m, string property, float value)
        {
            if (m.HasProperty(property))
                m.SetFloat(property, value);
        }

        // ---------- Malhas ----------

        private static void EnsureMeshes()
        {
            if (ringMesh == null)
                ringMesh = BuildRing(0.86f);
            if (cylinderMesh == null)
                cylinderMesh = PrimitiveMesh(PrimitiveType.Cylinder);
            if (sphereMesh == null)
                sphereMesh = PrimitiveMesh(PrimitiveType.Sphere);
        }

        /// <summary>Pega a malha de uma primitiva e descarta o objeto (que traria um collider).</summary>
        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            return mesh;
        }

        /// <summary>Anel achatado de raio externo 1 no plano XZ.</summary>
        private static Mesh BuildRing(float inner)
        {
            var vertices = new Vector3[RingSegments * 2];
            var triangles = new int[RingSegments * 6];
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i / (float)RingSegments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                vertices[i * 2] = dir * inner;
                vertices[i * 2 + 1] = dir;

                int next = (i + 1) % RingSegments;
                int t = i * 6;
                triangles[t] = i * 2;
                triangles[t + 1] = i * 2 + 1;
                triangles[t + 2] = next * 2 + 1;
                triangles[t + 3] = i * 2;
                triangles[t + 4] = next * 2 + 1;
                triangles[t + 5] = next * 2;
            }
            return MakeMesh("AnelCarta", vertices, triangles);
        }

        private static Mesh FanFill(float halfAngle)
        {
            int key = Mathf.RoundToInt(halfAngle);
            if (fillMeshes.TryGetValue(key, out Mesh cached) && cached != null)
                return cached;

            var vertices = new Vector3[FanSegments + 2];
            var triangles = new int[FanSegments * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i <= FanSegments; i++)
                vertices[i + 1] = ArcPoint(halfAngle, i, 1f);
            for (int i = 0; i < FanSegments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            Mesh mesh = MakeMesh("LequeCarta", vertices, triangles);
            fillMeshes[key] = mesh;
            return mesh;
        }

        private static Mesh FanEdge(float halfAngle)
        {
            int key = Mathf.RoundToInt(halfAngle);
            if (edgeMeshes.TryGetValue(key, out Mesh cached) && cached != null)
                return cached;

            const float width = 0.08f;
            var vertices = new Vector3[(FanSegments + 1) * 2];
            var triangles = new int[FanSegments * 6];
            for (int i = 0; i <= FanSegments; i++)
            {
                vertices[i * 2] = ArcPoint(halfAngle, i, 1f - width);
                vertices[i * 2 + 1] = ArcPoint(halfAngle, i, 1f);
            }
            for (int i = 0; i < FanSegments; i++)
            {
                int a = i * 2;
                int t = i * 6;
                triangles[t] = a;
                triangles[t + 1] = a + 1;
                triangles[t + 2] = a + 3;
                triangles[t + 3] = a;
                triangles[t + 4] = a + 3;
                triangles[t + 5] = a + 2;
            }
            Mesh mesh = MakeMesh("BordaCarta", vertices, triangles);
            edgeMeshes[key] = mesh;
            return mesh;
        }

        private static Vector3 ArcPoint(float halfAngleDegrees, int index, float radius)
        {
            float angle = Mathf.Lerp(-halfAngleDegrees, halfAngleDegrees, index / (float)FanSegments) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
        }

        private static Mesh MakeMesh(string name, Vector3[] vertices, int[] triangles)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------- Utilidades ----------

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }

        private static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
