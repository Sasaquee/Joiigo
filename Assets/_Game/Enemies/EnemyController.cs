using System.Collections.Generic;
using Game.Combat;
using Game.Core.AI;
using Game.Core.Combat;
using Game.Core.Math;
using Game.Net;
using Unity.Netcode;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>Fase do inimigo publicada pelo host. Os clientes só usam para desenhar o aviso (D-024) e a morte (D-026).</summary>
    public enum EnemyPhase : byte
    {
        Idle,
        Moving,
        Windup,
        Recover,
        Dead
    }

    /// <summary>Busca de jogadores vivos, compartilhada por inimigos e projéteis (só no host).</summary>
    public static class EnemyTargets
    {
        public static bool TryGetAlivePlayer(NetworkClient client, out NetworkHealth health)
        {
            health = null;
            var obj = client?.PlayerObject;
            if (obj == null || !obj.TryGetComponent(out NetworkPlayer _))
                return false;
            health = obj.GetComponent<NetworkHealth>();
            return health != null && health.IsAlive;
        }

        /// <summary>Jogador vivo mais próximo no plano. Null se não houver.</summary>
        public static NetworkHealth Nearest(Vector3 from, out float planarDistance)
        {
            planarDistance = float.MaxValue;
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer)
                return null;

            NetworkHealth best = null;
            var clients = manager.ConnectedClientsList;
            for (int i = 0; i < clients.Count; i++)
            {
                if (!TryGetAlivePlayer(clients[i], out var health))
                    continue;
                Vector3 p = health.transform.position;
                float d = new Vector2(p.x - from.x, p.z - from.z).magnitude;
                if (d < planarDistance)
                {
                    planarDistance = d;
                    best = health;
                }
            }
            return best;
        }
    }

    /// <summary>
    /// Inimigo em rede. O host roda a IA (EnemyBrain do Core), move, ataca e causa dano;
    /// publica a fase e a posição (NetworkTransform). Os clientes só desenham: cristal que acende e
    /// peça que recua no aviso (D-024), flash ao levar dano e desmontar na morte (D-026).
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(NetworkHealth))]
    public class EnemyController : NetworkBehaviour
    {
        // Convenção dos modelos: filhos "Corpo", "Parte_*", "Cristal" e "Arma".
        private const string CrystalName = "Cristal";
        private const string WeaponName = "Arma";
        private const string PartPrefix = "Parte_";

        [SerializeField] private EnemyDefinition definition;
        [Tooltip("Material do cristal apagado, usado na morte.")]
        [SerializeField] private Material crystalOff;
        [Tooltip("Material das faíscas e do vapor da morte.")]
        [SerializeField] private Material particleMaterial;
        [Tooltip("Só o drone: prefab do projétil.")]
        [SerializeField] private GameObject projectilePrefab;

        [Header("Visual (sem efeito no jogo)")]
        [SerializeField, Min(1f)] private float glowMultiplier = 3.5f;
        [SerializeField, Min(0f)] private float recoilDistance = 0.3f;
        [SerializeField, Min(0f)] private float lungeDistance = 0.25f;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.12f;
        [SerializeField] private Color flashColor = new Color(1f, 0.92f, 0.75f, 1f);

        private static readonly RaycastHit[] hitBuffer = new RaycastHit[16];

        private struct Slot
        {
            public Renderer Renderer;
            public int Index;
            public Color BaseColor;
            public bool HasBaseColor;
            public Color BaseEmission;
            public bool HasEmission;
        }

        private readonly NetworkVariable<byte> phase = new NetworkVariable<byte>((byte)EnemyPhase.Idle,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private CharacterController controller;
        private NetworkHealth health;
        private EnemyBrain brain;

        // Host
        private float verticalSpeed;
        private Vector3 lastPosition;
        private bool hasLastPosition;
        private bool deadServer;
        private bool despawning;
        private float deathTimer;
        private int avoidSign = 1;
        private bool warnedNoProjectile;

        // Visual
        private readonly List<Slot> crystalSlots = new List<Slot>();
        private readonly List<Slot> bodySlots = new List<Slot>();
        private MaterialPropertyBlock block;
        private Transform crystalRoot;
        private Transform weapon;
        private Vector3 weaponBaseLocal;
        private Vector3 muzzleLocal;
        private bool hasMuzzle;
        private float windupStart;
        private float recoil;
        private float glow = 1f;
        private float flashTimer;
        private bool flashApplied;
        private bool dismantled;

        public EnemyDefinition Definition => definition;

        public EnemyPhase Phase => (EnemyPhase)phase.Value;

        /// <summary>Em pé e lutando (para a contagem de ondas). Nos clientes lê a fase publicada.</summary>
        public bool IsAlive => IsSpawned && !deadServer && Phase != EnemyPhase.Dead;

        public NetworkHealth Health => health;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<NetworkHealth>();
            CacheVisuals();
        }

        /// <summary>Host: define o tipo antes do Spawn(). Vida, resistências e tamanho vêm da definição.</summary>
        public void ServerInit(EnemyDefinition def)
        {
            definition = def;
            brain = new EnemyBrain(def.ToBrainParams());

            float height = Mathf.Max(1.8f, def.bodyRadius * 2f);
            controller.radius = def.bodyRadius;
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);
            hasLastPosition = false;

            health.ServerInitialize(def.maxHealth, def.resistances.ToCore(), def.bodyRadius);
        }

        public override void OnNetworkSpawn()
        {
            phase.OnValueChanged += OnPhaseChanged;
            health.Damaged += OnDamaged;

            // O controlador só anda no host; nos clientes o NetworkTransform move o transform.
            controller.enabled = IsServer;

            if (IsServer)
            {
                if (brain == null && definition != null)
                    ServerInit(definition);
                deadServer = false;
                despawning = false;
                health.Depleted += OnDepleted;
                phase.Value = (byte)EnemyPhase.Idle;
            }
            else if (Phase == EnemyPhase.Dead)
            {
                Dismantle(); // entrou depois da morte
            }
        }

        public override void OnNetworkDespawn()
        {
            phase.OnValueChanged -= OnPhaseChanged;
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Depleted -= OnDepleted;
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || brain == null || definition == null)
                return;

            float dt = Time.deltaTime;
            if (deadServer)
            {
                deathTimer -= dt;
                if (deathTimer <= 0f && !despawning)
                {
                    despawning = true;
                    NetworkObject.Despawn(true);
                }
                return;
            }

            ServerThink(dt);
        }

        // ---------- Host: IA ----------

        private void ServerThink(float dt)
        {
            ResyncController();

            NetworkHealth target = EnemyTargets.Nearest(transform.position, out float distance);
            BrainOutput output = brain.Tick(target != null, distance, dt);

            Vector3 toTarget = Vector3.zero;
            if (target != null)
            {
                Vector3 d = target.transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.0001f)
                    toTarget = d.normalized;
            }

            if (toTarget != Vector3.zero && output.State != BrainState.Recover)
                Face(toTarget, dt);

            Vector3 moveDir = output.Move switch
            {
                BrainMove.Toward => toTarget,
                BrainMove.Away => -toTarget,
                _ => Vector3.zero
            };
            Move(moveDir == Vector3.zero ? moveDir : Steer(moveDir), dt);

            if (output.AttackReleased)
                ServerAttack(target);

            byte published = (byte)ToPhase(output.State);
            if (phase.Value != published)
                phase.Value = published;
        }

        private static EnemyPhase ToPhase(BrainState state) => state switch
        {
            BrainState.Chase => EnemyPhase.Moving,
            BrainState.Windup => EnemyPhase.Windup,
            BrainState.Recover => EnemyPhase.Recover,
            _ => EnemyPhase.Idle
        };

        /// <summary>Se algo moveu o transform por fora (spawn, teleporte), o CharacterController ainda guarda a posição antiga.</summary>
        private void ResyncController()
        {
            if (!hasLastPosition || (transform.position - lastPosition).sqrMagnitude > 0.000001f)
            {
                controller.enabled = false;
                controller.enabled = true;
            }
        }

        private void Move(Vector3 direction, float dt)
        {
            verticalSpeed = controller.isGrounded ? -1f : verticalSpeed + Physics.gravity.y * dt;
            Vector3 velocity = direction * definition.moveSpeed;
            controller.Move(new Vector3(velocity.x, verticalSpeed, velocity.z) * dt);
            lastPosition = transform.position;
            hasLastPosition = true;
        }

        private void Face(Vector3 direction, float dt)
        {
            Quaternion want = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, definition.turnSpeed * dt);
        }

        /// <summary>Desvio simples de pilares: se há obstáculo fixo à frente, tenta ângulos maiores, alternando o lado.</summary>
        private Vector3 Steer(Vector3 desired)
        {
            if (!Blocked(desired))
                return desired;

            for (int step = 1; step <= 3; step++)
            {
                float angle = 35f * step;
                Vector3 a = Quaternion.AngleAxis(angle * avoidSign, Vector3.up) * desired;
                if (!Blocked(a))
                    return a;
                Vector3 b = Quaternion.AngleAxis(-angle * avoidSign, Vector3.up) * desired;
                if (!Blocked(b))
                {
                    avoidSign = -avoidSign; // continua pelo lado que abriu
                    return b;
                }
            }
            return desired;
        }

        private bool Blocked(Vector3 direction)
        {
            float probeRadius = Mathf.Min(definition.bodyRadius * 0.8f, 0.6f);
            Vector3 origin = transform.position + Vector3.up * 1f;
            float distance = definition.bodyRadius + 1f;
            int count = Physics.SphereCastNonAlloc(origin, probeRadius, direction, hitBuffer, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var c = hitBuffer[i].collider;
                if (c.transform.IsChildOf(transform) || c.attachedRigidbody != null)
                    continue;
                if (c.GetComponentInParent<NetworkHealth>() != null)
                    continue; // jogadores e outros inimigos: o CharacterController resolve
                if (hitBuffer[i].distance <= 0f)
                    continue; // já começou dentro (piso, parede de spawn)
                return true;
            }
            return false;
        }

        private void ServerAttack(NetworkHealth target)
        {
            var packet = new DamagePacket(definition.damage, definition.arcaneFraction);

            Vector3 aim = transform.forward;
            if (target != null)
            {
                Vector3 d = target.transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.0001f)
                    aim = d.normalized;
            }

            if (definition.usesProjectile)
            {
                SpawnProjectile(aim);
                return;
            }

            // Corpo a corpo: acerta quem estiver no arco para onde o inimigo está virado.
            Vector3 pos = transform.position;
            Vector3 fwd = transform.forward;
            var origin = new Float2(pos.x, pos.z);
            var facing = new Float2(fwd.x, fwd.z);
            var clients = NetworkManager.ConnectedClientsList;
            for (int i = 0; i < clients.Count; i++)
            {
                if (!EnemyTargets.TryGetAlivePlayer(clients[i], out var victim))
                    continue;
                Vector3 vp = victim.transform.position;
                if (ArcHit.IsInArc(origin, facing, new Float2(vp.x, vp.z), definition.attackRange,
                        definition.meleeHalfAngle, victim.Radius))
                    victim.ServerApplyDamage(packet, NetworkManager.ServerClientId);
            }
        }

        private void SpawnProjectile(Vector3 direction)
        {
            if (projectilePrefab == null)
            {
                if (!warnedNoProjectile)
                {
                    warnedNoProjectile = true;
                    Debug.LogWarning($"{name}: sem prefab de projétil.");
                }
                return;
            }

            Vector3 muzzle = hasMuzzle ? transform.TransformPoint(muzzleLocal) : transform.position + Vector3.up * 1.2f;
            muzzle += direction * 0.4f;
            muzzle.y = Mathf.Max(muzzle.y, 0.9f); // na altura do tronco do jogador, longe do piso

            var go = Instantiate(projectilePrefab, muzzle, Quaternion.LookRotation(direction, Vector3.up));
            var projectile = go.GetComponent<EnemyProjectile>();
            projectile.ServerInit(definition.damage, definition.arcaneFraction, definition.projectileSpeed,
                definition.projectileRadius, definition.projectileLifetime, direction);
            go.GetComponent<NetworkObject>().Spawn(true);
        }

        private void OnDepleted()
        {
            if (deadServer)
                return;
            deadServer = true;
            brain?.Reset();
            deathTimer = definition != null ? definition.debrisLifetime : 4f;
            controller.enabled = false; // o corpo não bloqueia mais ninguém
            phase.Value = (byte)EnemyPhase.Dead;
        }

        // ---------- Todos: visual ----------

        private void CacheVisuals()
        {
            crystalRoot = FindDeep(transform, CrystalName);
            weapon = FindDeep(transform, WeaponName);
            if (weapon != null)
            {
                weaponBaseLocal = weapon.localPosition;
                var weaponRenderers = weapon.GetComponentsInChildren<Renderer>();
                if (weaponRenderers.Length > 0)
                {
                    Bounds b = weaponRenderers[0].bounds;
                    for (int i = 1; i < weaponRenderers.Length; i++)
                        b.Encapsulate(weaponRenderers[i].bounds);
                    muzzleLocal = transform.InverseTransformPoint(b.center);
                    hasMuzzle = true;
                }
            }

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer)
                    continue;
                bool isCrystal = crystalRoot != null && r.transform.IsChildOf(crystalRoot);
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null)
                        continue;
                    var slot = new Slot { Renderer = r, Index = i };
                    if (m.HasProperty("_BaseColor"))
                    {
                        slot.HasBaseColor = true;
                        slot.BaseColor = m.GetColor("_BaseColor");
                    }
                    if (m.HasProperty("_EmissionColor"))
                    {
                        slot.HasEmission = true;
                        slot.BaseEmission = m.GetColor("_EmissionColor");
                    }
                    (isCrystal ? crystalSlots : bodySlots).Add(slot);
                }
            }
        }

        private static Transform FindDeep(Transform root, string childName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name == childName)
                    return t;
            return null;
        }

        private void OnPhaseChanged(byte previous, byte current)
        {
            var next = (EnemyPhase)current;
            if (next == EnemyPhase.Windup)
                windupStart = Time.time;
            else if (next == EnemyPhase.Recover && (EnemyPhase)previous == EnemyPhase.Windup)
                recoil = -lungeDistance / Mathf.Max(0.01f, recoilDistance); // a peça dá o golpe para a frente
            else if (next == EnemyPhase.Dead)
                Dismantle();
        }

        private void OnDamaged(float applied)
        {
            flashTimer = flashDuration;
        }

        private void LateUpdate()
        {
            if (!IsSpawned || !IsClient || dismantled)
                return;

            float dt = Time.deltaTime;
            bool windup = Phase == EnemyPhase.Windup;
            float windupT = 0f;
            if (windup)
            {
                float length = definition != null ? Mathf.Max(0.01f, definition.windupTime) : 0.6f;
                windupT = Mathf.Clamp01((Time.time - windupStart) / length);
            }

            UpdateGlow(windup, windupT, dt);
            UpdateRecoil(windup, windupT, dt);
            UpdateFlash(dt);
        }

        private void UpdateGlow(bool windup, float windupT, float dt)
        {
            float target = windup ? Mathf.Lerp(1f, glowMultiplier, windupT * windupT) : 1f;
            float next = windup ? target : Mathf.MoveTowards(glow, 1f, 12f * dt);
            if (Mathf.Approximately(next, glow))
                return;
            glow = next;

            block ??= new MaterialPropertyBlock();
            foreach (var s in crystalSlots)
            {
                if (!s.HasEmission || s.Renderer == null)
                    continue;
                if (Mathf.Approximately(glow, 1f))
                {
                    s.Renderer.SetPropertyBlock(null, s.Index);
                    continue;
                }
                s.Renderer.GetPropertyBlock(block, s.Index);
                block.SetColor("_EmissionColor", s.BaseEmission * glow);
                s.Renderer.SetPropertyBlock(block, s.Index);
            }
        }

        private void UpdateRecoil(bool windup, float windupT, float dt)
        {
            if (weapon == null)
                return;

            if (windup)
                recoil = windupT * windupT * (3f - 2f * windupT); // suave até o fim do aviso
            else
                recoil = Mathf.MoveTowards(recoil, 0f, 8f * dt); // solta e volta ao lugar

            Transform parent = weapon.parent;
            Vector3 back = parent != null ? parent.InverseTransformVector(-transform.forward) : -transform.forward;
            weapon.localPosition = weaponBaseLocal + back * (recoil * recoilDistance);
        }

        private void UpdateFlash(float dt)
        {
            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                float f = Mathf.Clamp01(flashTimer / flashDuration);
                block ??= new MaterialPropertyBlock();
                foreach (var s in bodySlots)
                {
                    if (!s.HasBaseColor || s.Renderer == null)
                        continue;
                    s.Renderer.GetPropertyBlock(block, s.Index);
                    block.SetColor("_BaseColor", Color.Lerp(s.BaseColor, flashColor, f));
                    s.Renderer.SetPropertyBlock(block, s.Index);
                }
                flashApplied = true;
            }
            else if (flashApplied)
            {
                flashApplied = false;
                foreach (var s in bodySlots)
                    if (s.Renderer != null)
                        s.Renderer.SetPropertyBlock(null, s.Index);
            }
        }

        // ---------- Todos: morte (D-026) ----------

        private static bool IsDetachable(Transform t) =>
            t.name.StartsWith(PartPrefix) || t.name == WeaponName || t.name == CrystalName;

        /// <summary>
        /// Solta as peças com impulso, apaga o cristal e solta faíscas e vapor. As peças viram objetos
        /// locais (sem pai), então somem por um timer próprio mesmo que o objeto de rede suma antes.
        /// </summary>
        private void Dismantle()
        {
            if (dismantled || !IsClient)
                return;
            dismantled = true;

            float life = definition != null ? definition.debrisLifetime : 4f;

            // Cristal apaga: tira o brilho do aviso e troca o material.
            if (crystalOff != null)
            {
                foreach (var s in crystalSlots)
                {
                    if (s.Renderer == null)
                        continue;
                    s.Renderer.SetPropertyBlock(null, s.Index);
                    var mats = s.Renderer.sharedMaterials;
                    if (s.Index < mats.Length)
                        mats[s.Index] = crystalOff;
                    s.Renderer.sharedMaterials = mats;
                }
            }

            if (weapon != null)
                weapon.localPosition = weaponBaseLocal;

            Bounds bounds = new Bounds(transform.position + Vector3.up, Vector3.zero);
            var renderers = GetComponentsInChildren<Renderer>();
            bool first = true;
            foreach (var r in renderers)
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            Vector3 center = bounds.center;

            var pieces = new List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t != transform && IsDetachable(t))
                    pieces.Add(t);

            foreach (var piece in pieces)
                Detach(piece, center, life);

            SpawnBurst(crystalRoot != null ? crystalRoot.position : center, life);
        }

        private static void Detach(Transform piece, Vector3 center, float life)
        {
            piece.SetParent(null, true);

            var box = piece.gameObject.AddComponent<BoxCollider>();
            if (TryLocalBounds(piece, out Bounds local))
            {
                box.center = local.center;
                box.size = Vector3.Max(local.size, Vector3.one * 0.05f);
            }
            else
            {
                box.size = Vector3.one * 0.2f;
            }

            var body = piece.gameObject.AddComponent<Rigidbody>();
            body.mass = 1f;
            Vector3 pieceCenter = piece.TransformPoint(box.center);
            Vector3 outward = pieceCenter - center;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Random.onUnitSphere;
            Vector3 impulse = (outward + Vector3.up * 0.9f).normalized * Random.Range(3f, 6f);
            body.AddForce(impulse, ForceMode.VelocityChange);
            body.AddTorque(Random.insideUnitSphere * 8f, ForceMode.VelocityChange);

            Destroy(piece.gameObject, life);
        }

        /// <summary>Caixa que envolve as malhas da peça, no espaço local dela.</summary>
        private static bool TryLocalBounds(Transform piece, out Bounds result)
        {
            result = default;
            bool any = false;
            foreach (var mf in piece.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null)
                    continue;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 p = piece.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }
            return any;
        }

        private void SpawnBurst(Vector3 position, float life)
        {
            Material material = particleMaterial;
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                if (shader != null)
                    material = new Material(shader);
            }

            var root = new GameObject("RestosFx");
            root.transform.position = position;
            ConfigureSparks(root.AddComponent<ParticleSystem>(), material);

            var steam = new GameObject("Vapor");
            steam.transform.SetParent(root.transform, false);
            ConfigureSteam(steam.AddComponent<ParticleSystem>(), material);

            Destroy(root, Mathf.Max(3f, life));
        }

        private static void ConfigureSparks(ParticleSystem ps, Material material)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.45f, 0.1f));
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            ps.Play();
        }

        private static void ConfigureSteam(ParticleSystem ps, Material material)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.8f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startColor = new Color(0.55f, 0.6f, 0.65f, 0.35f);
            main.gravityModifier = -0.12f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            ps.Play();
        }
    }
}
