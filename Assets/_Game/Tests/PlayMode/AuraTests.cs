using System.Collections;
using Game.Aura;
using Game.Cards;
using Game.Combat;
using Game.Core.Aura;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Net;
using Game.Player;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Fase 7 — aura de ponta a ponta no host (D-060 a D-066): a aura acompanha o HP, mostra o escudo e quem caiu,
    /// troca de paleta e o anel do seu personagem fica por fora do círculo. Energia cheia dá um pulso e um "ding" (D-067).
    /// </summary>
    public class AuraTests
    {
        private GameObject player;
        private PlayerAura aura;
        private NetworkHealth health;

        [UnitySetUp]
        public IEnumerator SobeHost()
        {
            yield return ArenaTestScene.Load();
            Assert.IsTrue(Object.FindFirstObjectByType<NetSession>().Host(), "Host iniciou");
            float timeout = 5f;
            while (NetworkManager.Singleton.LocalClient?.PlayerObject == null && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            player = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
            aura = player.GetComponent<PlayerAura>();
            health = player.GetComponent<NetworkHealth>();
            Assert.IsNotNull(aura, "Jogador tem a aura (reconstruir: Game > Setup > Construir Arena)");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Encerra()
        {
            AuraPaletteSwitch.Set(AuraPalette.Normal);
            yield return ArenaTestScene.Cleanup();
        }

        // A aura suaviza a mudança de HP (AuraSettings.smoothing): espera o bastante para chegar perto do alvo.
        private IEnumerator Settle() => WaitSeconds(5f / Mathf.Max(0.1f, aura.Settings.smoothing));

        private static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
                yield return null;
        }

        private static IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
                yield return null;
        }

        [UnityTest]
        public IEnumerator HpCheio_AuraLarga_HpBaixo_EncolheEFalha()
        {
            Assert.AreEqual(1f, aura.Current.Radius, 0.02f, "HP cheio: aura no tamanho cheio (D-061)");
            Assert.AreEqual(0f, aura.Current.Flicker, 1e-3f);

            health.ServerApplyDamage(new DamagePacket(health.Max * 0.9f, 0f), 0);
            yield return Settle();
            Assert.Less(aura.Current.Radius, 0.7f, "Pouco HP: aura menor");
            Assert.Less(aura.Current.Intensity, 0.4f, "Pouco HP: aura mais fraca");
            Assert.Greater(aura.Current.Flicker, 0f, "Perto do zero: falha como lâmpada");
            Assert.Greater(aura.Current.HeartbeatBpm, 0f, "Perto do zero: batimento");
        }

        [UnityTest]
        public IEnumerator HpBaixo_BatimentoTocaNoSeuPersonagem()
        {
            var audio = player.GetComponent<AuraAudio>();
            Assert.IsNotNull(audio, "Jogador tem os sons da aura (D-064)");
            Assert.AreEqual(0, audio.BeatsPlayed, "HP cheio: sem batimento");
            health.ServerApplyDamage(new DamagePacket(health.Max * 0.9f, 0f), 0);
            yield return WaitSeconds(2.5f);
            Assert.Greater(audio.BeatsPlayed, 0, "HP baixo: o batimento toca");
        }

        [UnityTest]
        public IEnumerator Escudo_MostraCascaDeCristal()
        {
            var shield = player.GetComponent<PlayerShield>();
            if (shield == null)
                shield = player.AddComponent<PlayerShield>();
            shield.Activate(player.GetComponent<PlayerCards>(), 3f, 0f, 0f, 1f, 1f, 60f);
            yield return WaitFrames(5);
            Assert.IsTrue((aura.Signals & AuraSignals.Shield) != 0, "Escudo vira sinal da aura (D-063)");
            Assert.IsTrue(player.GetComponent<AuraVisual>().ShellVisible, "Casca de cristal visível");
        }

        [UnityTest]
        public IEnumerator Caido_SoBrasa()
        {
            health.ServerApplyDamage(new DamagePacket(health.Max * 10f, 0f), 0);
            float timeout = 2f;
            while (!player.GetComponent<PlayerLife>().IsDowned && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            yield return WaitFrames(3);
            Assert.AreEqual(AuraSignals.Downed, aura.Signals, "Caído: só o sinal de caído (D-063)");
            Assert.AreEqual(0f, aura.Current.SparkRate, 1e-4f, "Caído: sem faíscas");
        }

        [UnityTest]
        public IEnumerator PaletaAlternativa_TrocaAsCores()
        {
            AuraColor normal = aura.Current.Base;
            AuraPaletteSwitch.Set(AuraPalette.Alternative);
            yield return null;
            yield return null;
            AuraColor alt = aura.Current.Base;
            Assert.IsFalse(Mathf.Approximately(normal.R, alt.R) && Mathf.Approximately(normal.G, alt.G)
                && Mathf.Approximately(normal.B, alt.B), "Paleta alternativa muda a cor da aura (D-065)");
        }

        [UnityTest]
        public IEnumerator CirculoDeLatao_TemUvParaATextura()
        {
            // Bug: o círculo usava o FxKit.Quad, sem UV; a textura era lida num canto transparente e o círculo sumia.
            Transform circle = player.transform.Find("Aura/Giro/Circulo");
            Assert.IsNotNull(circle, "A aura tem o círculo de latão (D-060)");
            Mesh mesh = circle.GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(mesh.vertexCount, mesh.uv.Length, "Círculo com UV em todos os vértices");
            var renderer = circle.GetComponent<MeshRenderer>();
            Assert.IsNotNull(renderer.sharedMaterial.GetTexture("_BaseMap"), "Círculo com a textura de latão");
            yield break;
        }

        [UnityTest]
        public IEnumerator AnelDoSeuPersonagem_FicaPorForaDaAura()
        {
            Transform marker = player.transform.Find("MarcadorLocal");
            Assert.IsNotNull(marker);
            float ringRadius = 1.08f * marker.localScale.x; // raio do AnelMarcador.fbx (build_props.py)
            Assert.Greater(ringRadius, aura.Settings.fullRadius, "Anel branco por fora do círculo cheio (D-066)");
            yield break;
        }

        // ---------- Pulso de energia cheia (D-067) ----------

        private static IEnumerator WaitFor(System.Func<bool> condition, float timeout)
        {
            while (!condition() && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
        }

        // Equipa uma skill que custa energia e a usa, para a energia sair de cheia.
        private void Spend(PlayerCards cards, string cardId, int slot)
        {
            int id = cards.FindCardId(cardId);
            Assert.GreaterOrEqual(id, 0, $"Carta {cardId} existe no banco");
            cards.ServerGiveCard(id);
            cards.RequestEquip(id, SlotType.Skill, slot);
            Assert.AreEqual(id, cards.GetSlot(SlotType.Skill, slot), $"{cardId} equipada");
            Vector3 aim = player.transform.position + player.transform.forward * 6f;
            Assert.IsTrue(cards.ServerUseSkill(slot, aim), $"Usou {cardId} (gasta energia)");
        }

        [UnityTest]
        public IEnumerator NasceComEnergiaCheia_NaoPulsa()
        {
            var visual = player.GetComponent<AuraVisual>();
            var audio = player.GetComponent<AuraAudio>();
            Assert.Greater(player.GetComponent<PlayerCards>().EnergyFraction, 0.999f, "O jogador nasce com a energia cheia");
            yield return WaitFrames(10);
            Assert.IsTrue(aura.Current.RunesFull, "Runas cheias");
            Assert.AreEqual(0, visual.PulsesPlayed, "Nascer cheio não é encher: sem pulso (D-067)");
            Assert.AreEqual(0, audio.ChimesPlayed, "Nascer cheio: sem ding");
        }

        [UnityTest]
        public IEnumerator EnergiaEnche_UmPulsoEUmDing_NaoRepete_EPulsaDeNovoAoEncherOutraVez()
        {
            player.GetComponent<PlayerInputReader>().enabled = false; // sem mouse no teste
            var cards = player.GetComponent<PlayerCards>();
            var visual = player.GetComponent<AuraVisual>();
            var audio = player.GetComponent<AuraAudio>();
            Assert.IsNotNull(visual);
            Assert.IsNotNull(audio);

            // Gasta: a energia sai de cheia (rearma o pulso), sem pulsar.
            Spend(cards, "pistao_runico", 0);
            yield return WaitFor(() => !aura.Current.RunesFull, 2f);
            Assert.IsFalse(aura.Current.RunesFull, "Gastou: runas deixam de estar cheias");
            // O pulso só rearma abaixo de fullPulseRearmBelow (histerese): espera a energia mostrada descer até lá.
            yield return WaitSeconds(0.8f);
            Assert.AreEqual(0, visual.PulsesPlayed, "Gastar não pulsa");

            // Enche: um pulso e um ding.
            cards.AddEnergy(10000f);
            yield return WaitFor(() => visual.PulsesPlayed > 0, 3f);
            Assert.AreEqual(1, visual.PulsesPlayed, "Encheu: um pulso (D-067)");
            Assert.AreEqual(1, audio.ChimesPlayed, "Encheu: um ding no seu personagem");
            Assert.IsTrue(visual.PulseActive, "A onda está correndo");
            Assert.IsTrue(player.transform.Find("Aura/PulsoOnda").GetComponent<MeshRenderer>().enabled, "Onda visível");

            // Continua cheia: não repete, e a onda acaba.
            yield return WaitSeconds(aura.Settings.fullPulseDuration + 1f);
            Assert.IsTrue(aura.Current.RunesFull);
            Assert.AreEqual(1, visual.PulsesPlayed, "Continua cheia: não pulsa de novo");
            Assert.AreEqual(1, audio.ChimesPlayed, "Continua cheia: não toca de novo");
            Assert.IsFalse(visual.PulseActive, "A onda acabou");
            Assert.IsFalse(player.transform.Find("Aura/PulsoOnda").GetComponent<MeshRenderer>().enabled, "Onda escondida");

            // Gasta e enche de novo: pulsa de novo.
            Spend(cards, "mina_engrenagem", 1);
            yield return WaitFor(() => !aura.Current.RunesFull, 2f);
            Assert.IsFalse(aura.Current.RunesFull, "Gastou de novo");
            yield return WaitSeconds(0.8f); // a energia mostrada precisa passar de fullPulseRearmBelow para rearmar
            cards.AddEnergy(10000f);
            yield return WaitFor(() => visual.PulsesPlayed > 1, 3f);
            Assert.AreEqual(2, visual.PulsesPlayed, "Encheu outra vez: pulsa outra vez");
            Assert.AreEqual(2, audio.ChimesPlayed, "Encheu outra vez: toca outra vez");
        }

        [UnityTest]
        public IEnumerator LevantarComEnergiaCheia_NaoPulsa()
        {
            // Bug: caído tem RunesFull falso; ao levantar com a energia cheia, a borda falso -> verdadeiro pulsava.
            var visual = player.GetComponent<AuraVisual>();
            var audio = player.GetComponent<AuraAudio>();
            var life = player.GetComponent<PlayerLife>();
            Assert.IsTrue(aura.Current.RunesFull, "Começa com a energia cheia");

            health.ServerApplyDamage(new DamagePacket(health.Max * 10f, 0f), 0);
            yield return WaitFor(() => life.IsDowned, 2f);
            Assert.IsTrue(life.IsDowned, "Caiu");
            yield return WaitFor(() => !life.IsDowned, life.Settings.downedDuration + 3f);
            Assert.IsFalse(life.IsDowned, "Levantou (fim do tempo caído)");
            yield return WaitSeconds(1f);

            Assert.IsTrue(aura.Current.RunesFull, "De pé, com a energia cheia");
            Assert.AreEqual(0, visual.PulsesPlayed, "Levantar cheio não é encher: sem pulso (D-067)");
            Assert.AreEqual(0, audio.ChimesPlayed, "Levantar cheio: sem ding");
        }
    }
}
