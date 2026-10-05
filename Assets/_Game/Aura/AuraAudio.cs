using System.Collections.Generic;
using Game.Core.Aura;
using Game.Enemies;
using UnityEngine;

namespace Game.Aura
{
    /// <summary>
    /// Sons da aura (D-064), só no seu personagem: batimento grave com HP baixo, que acelera perto do zero, e chiado
    /// curto de vapor quando um inimigo prepara um golpe na sua direção; e um "ding" cristalino curto quando a energia
    /// enche (D-067). Os sons são sintetizados aqui (sem arquivo).
    /// </summary>
    public class AuraAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const float DangerScanInterval = 0.1f;

        [SerializeField] private AuraSettings settings;

        private AudioSource source;
        private AudioClip heartbeat;
        private AudioClip hiss;
        private AudioClip chime;
        private float nextBeat;
        private float nextScan;
        private float lastHiss = -10f;
        private readonly HashSet<int> warned = new HashSet<int>();
        private readonly List<int> stillWinding = new List<int>();

        /// <summary>Quantos batimentos e chiados tocaram (para testes).</summary>
        public int BeatsPlayed { get; private set; }
        public int HissesPlayed { get; private set; }

        /// <summary>Quantos "dings" de energia cheia tocaram (D-067, para testes).</summary>
        public int ChimesPlayed { get; private set; }

        public void Configure(AuraSettings auraSettings) => settings = auraSettings;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // aviso para quem joga, não um som do mundo
            heartbeat = MakeHeartbeat();
            hiss = MakeHiss();
            chime = MakeChime();
        }

        // Os clipes são criados em código: sem isto, cada jogador que sai deixa três clipes órfãos na memória.
        // O AudioSource é componente deste objeto e vai junto com ele.
        private void OnDestroy()
        {
            if (source != null)
                source.Stop();
            if (heartbeat != null)
                Destroy(heartbeat);
            if (hiss != null)
                Destroy(hiss);
            if (chime != null)
                Destroy(chime);
        }

        /// <summary>"Ding" de energia cheia (D-067), só no seu personagem. O PlayerAura chama uma vez quando a energia enche.</summary>
        public void PlayFullChime(bool isLocal)
        {
            if (!isLocal || settings == null || source == null)
                return;
            source.PlayOneShot(chime, settings.fullChimeVolume);
            ChimesPlayed++;
        }

        /// <summary>Chamado pelo PlayerAura a cada quadro.</summary>
        public void Tick(AuraState state, bool isLocal, float dt)
        {
            if (!isLocal || settings == null || source == null)
                return;

            // Batimento (HeartbeatBpm já é 0 com HP alto ou caído).
            if (state.HeartbeatBpm > 0f)
            {
                if (Time.time >= nextBeat)
                {
                    source.PlayOneShot(heartbeat, settings.heartbeatVolume);
                    BeatsPlayed++;
                    nextBeat = Time.time + 60f / state.HeartbeatBpm;
                }
            }
            else
            {
                nextBeat = Time.time;
            }

            if (Time.time >= nextScan && (state.Signals & AuraSignals.Downed) == 0)
            {
                nextScan = Time.time + DangerScanInterval;
                ScanDanger();
            }
        }

        /// <summary>Um chiado por preparação de golpe que aponta para você, com intervalo mínimo entre chiados.</summary>
        private void ScanDanger()
        {
            stillWinding.Clear();
            Vector3 me = transform.position;
            float cosLimit = Mathf.Cos(settings.dangerHalfAngle * Mathf.Deg2Rad);
            bool newThreat = false;
            foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (!enemy.IsSpawned || enemy.Phase != EnemyPhase.Windup)
                    continue;
                Vector3 to = me - enemy.transform.position;
                to.y = 0f;
                float distance = to.magnitude;
                if (distance > settings.dangerRadius)
                    continue;
                Vector3 facing = enemy.transform.forward;
                facing.y = 0f;
                if (distance > 0.01f && Vector3.Dot(facing.normalized, to / distance) < cosLimit)
                    continue;
                int id = enemy.GetInstanceID();
                stillWinding.Add(id);
                if (!warned.Contains(id))
                    newThreat = true;
            }
            warned.Clear();
            foreach (int id in stillWinding)
                warned.Add(id);

            if (newThreat && Time.time - lastHiss >= settings.hissCooldown)
            {
                source.PlayOneShot(hiss, settings.hissVolume);
                HissesPlayed++;
                lastHiss = Time.time;
            }
        }

        // ---------- Síntese ----------

        /// <summary>"Tum-tum" grave: duas batidas senoidais curtas com queda rápida.</summary>
        private static AudioClip MakeHeartbeat()
        {
            int n = Mathf.RoundToInt(SampleRate * 0.42f);
            var data = new float[n];
            Thump(data, 0f, 58f, 0.9f);
            Thump(data, 0.17f, 46f, 0.65f);
            var clip = AudioClip.Create("AuraBatimento", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void Thump(float[] data, float start, float frequency, float gain)
        {
            int s0 = Mathf.RoundToInt(start * SampleRate);
            int len = Mathf.RoundToInt(0.16f * SampleRate);
            for (int i = 0; i < len && s0 + i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Min(1f, t / 0.008f) * Mathf.Exp(-t * 26f);
                float f = frequency * (1f + 0.6f * Mathf.Exp(-t * 40f)); // leve queda de altura, soa como pancada
                data[s0 + i] += gain * env * Mathf.Sin(2f * Mathf.PI * f * t);
            }
        }

        /// <summary>Chiado de vapor: ruído filtrado com ataque rápido e cauda curta.</summary>
        private static AudioClip MakeHiss()
        {
            int n = Mathf.RoundToInt(SampleRate * 0.38f);
            var data = new float[n];
            var rng = new System.Random(7);
            float low = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)rng.NextDouble() * 2f - 1f;
                low += 0.18f * (white - low);
                float bright = white - low; // tira o grave: fica o "sss"
                float env = Mathf.Min(1f, t / 0.02f) * Mathf.Exp(-t * 7f);
                data[i] = 0.55f * env * bright;
            }
            var clip = AudioClip.Create("AuraChiado", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// "Ding" cristalino: um toque de sino de vidro, agudo e limpo, que some sozinho. Parciais de taça (não
        /// harmônicos), cada uma caindo mais rápido que a de baixo; a fundamental vem em dobro, levemente desafinada,
        /// para um brilho que ondula. Agudo e sem ruído, não se confunde com o batimento (grave) nem com o chiado.
        /// </summary>
        private static AudioClip MakeChime()
        {
            const float f0 = 1318.5f; // Mi6
            float[] ratio = { 1f, 1.004f, 2.32f, 4.25f, 6.63f };
            float[] gain = { 0.5f, 0.35f, 0.22f, 0.1f, 0.05f };
            float[] decay = { 3.2f, 3.6f, 7f, 13f, 22f };
            int n = Mathf.RoundToInt(SampleRate * 1.1f);
            var data = new float[n];
            float peak = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float attack = Mathf.Min(1f, t / 0.003f); // sem estalo no início
                float tail = Mathf.Min(1f, (n - i) / (SampleRate * 0.05f)); // sem estalo no fim
                float v = 0f;
                for (int k = 0; k < ratio.Length; k++)
                    v += gain[k] * Mathf.Exp(-t * decay[k]) * (float)System.Math.Sin(2.0 * System.Math.PI * f0 * ratio[k] * t); // fase em double: agudo sem chiado de arredondamento
                v *= attack * tail;
                data[i] = v;
                peak = Mathf.Max(peak, Mathf.Abs(v));
            }
            float norm = peak > 0f ? 0.7f / peak : 1f;
            for (int i = 0; i < n; i++)
                data[i] *= norm;
            var clip = AudioClip.Create("AuraDing", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
