using System.Collections.Generic;
using Game.Core.Aura;
using Game.Enemies;
using UnityEngine;

namespace Game.Aura
{
    /// <summary>
    /// Sons da aura (D-064), só no seu personagem: batimento grave com HP baixo, que acelera perto do zero, e chiado
    /// curto de vapor quando um inimigo prepara um golpe na sua direção. Os dois sons são sintetizados aqui (sem arquivo).
    /// </summary>
    public class AuraAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const float DangerScanInterval = 0.1f;

        [SerializeField] private AuraSettings settings;

        private AudioSource source;
        private AudioClip heartbeat;
        private AudioClip hiss;
        private float nextBeat;
        private float nextScan;
        private float lastHiss = -10f;
        private readonly HashSet<int> warned = new HashSet<int>();
        private readonly List<int> stillWinding = new List<int>();

        /// <summary>Quantos batimentos e chiados tocaram (para testes).</summary>
        public int BeatsPlayed { get; private set; }
        public int HissesPlayed { get; private set; }

        public void Configure(AuraSettings auraSettings) => settings = auraSettings;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // aviso para quem joga, não um som do mundo
            heartbeat = MakeHeartbeat();
            hiss = MakeHiss();
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
    }
}
