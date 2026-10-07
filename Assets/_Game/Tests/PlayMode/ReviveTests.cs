using System;
using System.Collections;
using System.Collections.Generic;
using Game.Aura;
using Game.Cameras;
using Game.Combat;
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
    /// Levantar um aliado segurando E por perto (D-083), na lógica do host. Só há um cliente real nos testes, então os
    /// aliados são instâncias extras do prefab do jogador, spawnadas pelo host e registradas em PlayerRevive.TestAllies;
    /// o "segurando E" entra por PlayerRevive.ServerSetHolding, o mesmo ponto que o RPC do dono usa.
    /// Precisa do prefab do jogador reconstruído (Game > Setup > Construir Arena) com o PlayerRevive.
    /// </summary>
    public class ReviveTests
    {
        private const float Hold = 0.5f;
        private const float Radius = 2.5f;
        private const float Fraction = 0.5f;

        private NetworkPlayer player;
        private PlayerLife life;
        private NetworkHealth health;
        private PlayerRevive revive;
        private PlayerAura aura;
        private AuraVisual visual;
        private CombatSettings combat;
        private ReviveSettings reviveSettings;
        private readonly List<GameObject> allies = new List<GameObject>();
        private DummyTarget dummy;

        [UnitySetUp]
        public IEnumerator SobeHost()
        {
            yield return ArenaTestScene.Load();
            Assert.IsTrue(UnityEngine.Object.FindFirstObjectByType<NetSession>().Host(), "Host iniciou");

            float timeout = 5f;
            while (NetworkManager.Singleton.LocalClient?.PlayerObject == null && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            player = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<NetworkPlayer>();
            player.GetComponent<PlayerInputReader>().enabled = false; // sem mouse nem teclado no teste
            life = player.GetComponent<PlayerLife>();
            health = player.GetComponent<NetworkHealth>();
            revive = player.GetComponent<PlayerRevive>();
            aura = player.GetComponent<PlayerAura>();
            visual = player.GetComponent<AuraVisual>();
            Assert.IsNotNull(revive, "Jogador tem o PlayerRevive (reconstruir: Game > Setup > Construir Arena)");

            // Números do teste, independentes dos assets.
            combat = ScriptableObject.CreateInstance<CombatSettings>();
            combat.downedDuration = 5f;
            life.Settings = combat;
            reviveSettings = NewReviveSettings(Hold);
            revive.Settings = reviveSettings;
            PlayerRevive.TestAllies.Clear();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Encerra()
        {
            PlayerRevive.TestAllies.Clear();
            foreach (var ally in allies)
            {
                if (ally == null)
                    continue;
                var no = ally.GetComponent<NetworkObject>();
                if (no != null && no.IsSpawned)
                    no.Despawn(true);
                else
                    UnityEngine.Object.Destroy(ally);
            }
            allies.Clear();
            if (dummy != null)
                UnityEngine.Object.Destroy(dummy.gameObject);
            if (combat != null)
                UnityEngine.Object.Destroy(combat);
            if (reviveSettings != null)
                UnityEngine.Object.Destroy(reviveSettings);
            yield return ArenaTestScene.Cleanup();
        }

        private static ReviveSettings NewReviveSettings(float holdSeconds)
        {
            var s = ScriptableObject.CreateInstance<ReviveSettings>();
            s.holdSeconds = holdSeconds;
            s.reviveRadius = Radius;
            s.graceAfterRelease = 0.3f;
            s.reviveHealthFraction = Fraction;
            s.rescuerFrozen = true;
            return s;
        }

        /// <summary>Aliado de teste: outra cópia do prefab do jogador, spawnada pelo host, registrada como aliada.</summary>
        private PlayerRevive SpawnAlly(Vector3 position)
        {
            var prefab = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
            var go = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
            go.GetComponent<NetworkObject>().Spawn(true);
            allies.Add(go);

            go.GetComponent<PlayerInputReader>().enabled = false;
            go.GetComponent<PlayerLife>().Settings = combat;
            var allyRevive = go.GetComponent<PlayerRevive>();
            allyRevive.Settings = reviveSettings;
            PlayerRevive.TestAllies.Add(allyRevive);

            // O NetworkPlayer do aliado é "do dono" (o host) e puxou a câmera para ele: devolve ao jogador de verdade.
            var cam = Camera.main;
            if (cam != null && cam.TryGetComponent(out CameraFollow follow))
                follow.Target = player.transform;
            return allyRevive;
        }

        private Vector3 Near(float distance) => player.transform.position + Vector3.right * distance;

        private void Fall() => health.ServerApplyDamage(new DamagePacket(100000f, 0f), 0);

        private static IEnumerator WaitFor(Func<bool> condition, float seconds)
        {
            float end = Time.time + seconds;
            while (!condition() && Time.time < end)
                yield return null;
        }

        private static void Teleport(Component c, Vector3 position)
        {
            var controller = c.GetComponent<CharacterController>();
            controller.enabled = false;
            c.transform.position = position;
            controller.enabled = true;
        }

        private static float Planar(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // ---------- Levantar ----------

        [UnityTest]
        public IEnumerator AliadoSegurandoPerto_LevantaNoLugarDaQuedaComAVidaDeReviveHealthFraction()
        {
            var ally = SpawnAlly(Near(1.5f));
            // Afasta quem vai cair do spawn: levantar é NO LUGAR da queda, não no spawn (D-003).
            Teleport(player, player.transform.position + Vector3.forward * 2f);
            Teleport(ally, Near(1.5f));
            yield return null;

            Fall();
            Assert.IsTrue(life.IsDowned, "Caiu");
            Vector3 fallPosition = player.transform.position;
            bool pulsou = false;
            life.Revived += () => pulsou = true;

            ally.ServerSetHolding(true);
            yield return WaitFor(() => !life.IsDowned, Hold + 2f);

            Assert.IsFalse(life.IsDowned, "O aliado levantou");
            Assert.IsTrue(life.CanAct);
            Assert.Less(Planar(player.transform.position, fallPosition), 0.3f, "Levantou no lugar da queda");
            Assert.IsTrue(health.IsAlive);
            Assert.AreEqual(health.Max * Fraction, health.Current, 0.01f, "Voltou com reviveHealthFraction da vida");
            Assert.AreEqual(0f, life.ReviveProgress, 1e-3f, "Progresso zerado depois de levantar");
            yield return WaitFor(() => pulsou, 1f);
            Assert.IsTrue(pulsou, "Todos recebem o aviso de que levantou (pulso da aura)");
        }

        [UnityTest]
        public IEnumerator Levantar_NaoDemoraMenosQueHoldSeconds()
        {
            var ally = SpawnAlly(Near(1.5f));
            Fall();
            float start = Time.time;
            ally.ServerSetHolding(true);

            yield return WaitFor(() => !life.IsDowned, Hold + 2f);

            Assert.IsFalse(life.IsDowned);
            Assert.GreaterOrEqual(Time.time - start, Hold - 0.05f, "Levantou rápido demais");
        }

        [UnityTest]
        public IEnumerator Levantar_ProgressoDaAuraSobeSemNumeroEAsRunasAcendem()
        {
            Assert.IsNotNull(aura, "Jogador tem a aura");
            reviveSettings.holdSeconds = 1.6f;
            var ally = SpawnAlly(Near(1.5f));
            Fall();
            yield return WaitFor(() => (aura.Signals & Game.Core.Aura.AuraSignals.Downed) != 0, 1f);
            Assert.IsNotNull(visual, "Jogador tem o desenho da aura");

            Assert.AreEqual(0f, aura.Current.ReviveProgress, 1e-3f, "Sem aliado, sem progresso");
            float radiusAtFall = aura.Current.Radius;
            float intensityAtFall = aura.Current.Intensity;
            Assert.AreEqual(0, visual.ReviveRunesLit, "Nenhuma runa acesa antes de o aliado segurar");

            ally.ServerSetHolding(true);
            yield return WaitFor(() => life.ReviveProgress >= 0.4f, 2f);
            yield return null;
            yield return null;

            Assert.IsTrue(life.IsDowned, "Ainda caído no meio do levantar");
            Assert.GreaterOrEqual(aura.Current.ReviveProgress, 0.35f, "A aura de quem caiu mostra o progresso");
            Assert.Greater(aura.Current.Radius, radiusAtFall, "A aura ganha raio");
            Assert.Greater(aura.Current.Intensity, intensityAtFall, "A aura ganha luz");
            Assert.Greater(visual.ReviveRunesLit, 0, "Já há runas acesas em anel");
            Assert.Less(visual.ReviveRunesLit, 8, "Mas o anel ainda não fechou");

            yield return WaitFor(() => !life.IsDowned, 3f);
            yield return null;
            yield return null;
            Assert.AreEqual(0, visual.ReviveRunesLit, "De pé: o anel de runas some");
        }

        [UnityTest]
        public IEnumerator Levantar_SairDoRaioZeraOProgresso()
        {
            reviveSettings.holdSeconds = 2f;
            var ally = SpawnAlly(Near(1.5f));
            Fall();
            ally.ServerSetHolding(true);
            yield return WaitFor(() => life.ReviveProgress >= 0.2f, 2f);
            Assert.GreaterOrEqual(life.ReviveProgress, 0.2f, "Começou a levantar");

            Teleport(ally, Near(Radius + 3f)); // saiu do raio, ainda segurando E
            yield return WaitFor(() => life.ReviveProgress <= 0f, reviveSettings.graceAfterRelease + 1f);

            Assert.AreEqual(0f, life.ReviveProgress, 1e-3f, "Saiu do raio: zerou");
            Assert.IsTrue(life.IsDowned, "Continua caído");
            Assert.IsNull(revive.ServerRescuer, "Ninguém levanta agora");
        }

        [UnityTest]
        public IEnumerator Levantar_SoltarEZeraOProgresso()
        {
            reviveSettings.holdSeconds = 2f;
            var ally = SpawnAlly(Near(1.5f));
            Fall();
            ally.ServerSetHolding(true);
            yield return WaitFor(() => life.ReviveProgress >= 0.2f, 2f);

            ally.ServerSetHolding(false);
            yield return WaitFor(() => life.ReviveProgress <= 0f, reviveSettings.graceAfterRelease + 1f);

            Assert.AreEqual(0f, life.ReviveProgress, 1e-3f);
            Assert.IsTrue(life.IsDowned);
        }

        [UnityTest]
        public IEnumerator Levantar_AliadoLongeNaoLevantaEAQuedaAcabaNoSpawn()
        {
            combat.downedDuration = 0.6f;
            Vector3 spawn = player.transform.position;
            Teleport(player, spawn + Vector3.forward * 2f);
            var ally = SpawnAlly(Near(Radius + 3f));
            ally.ServerSetHolding(true); // segura E, mas longe demais
            yield return null;

            Fall();
            yield return WaitFor(() => !life.IsDowned, 3f);
            yield return null;

            Assert.IsFalse(life.IsDowned, "O tempo da queda acabou (D-003)");
            Assert.AreEqual(health.Max, health.Current, 0.001f, "Voltou no spawn com a vida cheia, não a de reviveHealthFraction");
            Assert.Less(Planar(player.transform.position, spawn), 0.5f, "Voltou no spawn");
        }

        [UnityTest]
        public IEnumerator Levantar_SemAliadoAQuedaAcabaNoSpawnComoSempre()
        {
            combat.downedDuration = 0.4f;
            Vector3 spawn = player.transform.position;
            Teleport(player, spawn + Vector3.forward * 2f);
            yield return null;

            Fall();
            yield return WaitFor(() => !life.IsDowned, 3f);
            yield return null;

            Assert.IsFalse(life.IsDowned);
            Assert.AreEqual(health.Max, health.Current, 0.001f);
            Assert.Less(Planar(player.transform.position, spawn), 0.5f, "Voltou no spawn");
        }

        [UnityTest]
        public IEnumerator Levantar_NaoLevantaASiMesmo()
        {
            Fall();
            revive.ServerSetHolding(true); // o próprio caído "segura E"
            yield return new WaitForSeconds(Hold + 0.5f);

            Assert.IsTrue(life.IsDowned, "Quem caiu não se levanta sozinho");
            Assert.AreEqual(0f, life.ReviveProgress, 1e-3f);
        }

        [UnityTest]
        public IEnumerator Levantar_AliadoEstaPertoMasNaoSegura_NaoLevanta()
        {
            SpawnAlly(Near(1.5f)); // perto, sem segurar E
            Fall();
            yield return new WaitForSeconds(Hold + 0.4f);

            Assert.IsTrue(life.IsDowned);
            Assert.AreEqual(0f, life.ReviveProgress, 1e-3f);
        }

        [UnityTest]
        public IEnumerator Levantar_UmCaidoSoAvancaComUmAliadoDeCadaVez()
        {
            reviveSettings.holdSeconds = 1f;
            var near = SpawnAlly(Near(1.0f));
            var far = SpawnAlly(Near(2.0f));
            Fall();
            near.ServerSetHolding(true);
            far.ServerSetHolding(true);

            yield return new WaitForSeconds(0.6f);

            Assert.IsTrue(life.IsDowned, "Dois aliados não somam: ainda falta tempo");
            Assert.Less(life.ReviveProgress, 0.9f, "Com dois aliados o progresso não anda em dobro");
            Assert.AreSame(near, revive.ServerRescuer, "Só o mais perto levanta");
            Assert.IsTrue(near.GetComponent<PlayerLife>().IsReviving, "O mais perto fica parado levantando");
            Assert.IsFalse(far.GetComponent<PlayerLife>().IsReviving, "O outro continua livre");

            yield return WaitFor(() => !life.IsDowned, 2f);
            Assert.IsFalse(life.IsDowned, "Levantou no fim");
        }

        // ---------- O aliado que levanta fica exposto ----------

        [UnityTest]
        public IEnumerator Levantar_AliadoFicaParadoENaoAtacaEnquantoSegura_SoltarDevolveOControle()
        {
            reviveSettings.holdSeconds = 3f;
            var ally = SpawnAlly(Near(1.5f));
            var allyLife = ally.GetComponent<PlayerLife>();
            var allyNet = ally.GetComponent<NetworkPlayer>();
            var allyCombat = ally.GetComponent<PlayerCombat>();
            Assert.IsTrue(allyLife.CanAct, "Livre antes de segurar");

            var go = new GameObject("AlvoTeste");
            go.transform.position = ally.transform.position + Vector3.forward * 1.5f;
            var sphere = go.AddComponent<SphereCollider>();
            sphere.center = new Vector3(0f, 1f, 0f);
            sphere.radius = 0.4f;
            dummy = go.AddComponent<DummyTarget>();

            Fall();
            ally.ServerSetHolding(true);
            yield return WaitFor(() => allyLife.IsReviving, 1f);
            Assert.IsTrue(allyLife.IsReviving, "O aliado entrou em 'levantando'");
            Assert.IsFalse(allyLife.CanAct);

            // Tenta andar e atacar enquanto segura E: nada acontece.
            Vector3 before = ally.transform.position;
            float until = Time.time + 0.4f;
            while (Time.time < until)
            {
                allyNet.SubmitLocalIntent(Vector3.forward, null);
                yield return null;
            }
            allyCombat.RequestAttack(ally.transform.position + Vector3.forward * 5f);
            yield return new WaitForSeconds(0.3f);
            Assert.Less(Planar(ally.transform.position, before), 0.05f, "Aliado parado enquanto levanta");
            Assert.AreEqual(0f, dummy.Taken, 0.001f, "Aliado não ataca enquanto levanta");

            // Soltar E devolve o controle.
            ally.ServerSetHolding(false);
            yield return WaitFor(() => allyLife.CanAct, 1f);
            Assert.IsTrue(allyLife.CanAct, "Soltou E: controle de volta");

            allyCombat.RequestAttack(ally.transform.position + Vector3.forward * 5f);
            yield return new WaitForSeconds(0.4f);
            Assert.Greater(dummy.Taken, 0f, "Soltou E: o aliado volta a atacar (o alvo de teste está no alcance)");

            before = ally.transform.position;
            until = Time.time + 0.4f;
            while (Time.time < until)
            {
                allyNet.SubmitLocalIntent(Vector3.back, null);
                yield return null;
            }
            Assert.Greater(Planar(ally.transform.position, before), 0.3f, "Voltou a andar");
        }

        [UnityTest]
        public IEnumerator Levantar_ConfiguracaoSemCongelar_AliadoContinuaLivre()
        {
            reviveSettings.holdSeconds = 3f;
            reviveSettings.rescuerFrozen = false;
            var ally = SpawnAlly(Near(1.5f));
            var allyLife = ally.GetComponent<PlayerLife>();
            Fall();
            ally.ServerSetHolding(true);
            yield return WaitFor(() => life.ReviveProgress > 0.1f, 1f);

            Assert.Greater(life.ReviveProgress, 0.05f, "O aliado levanta mesmo sem ficar parado");
            Assert.IsTrue(allyLife.CanAct, "rescuerFrozen desligado: o aliado se mexe");
        }

        [UnityTest]
        public IEnumerator Levantar_QuemCaiuNoMeioDoLevantarLiberaOOutro()
        {
            // O aliado que levanta cai: deixa de ficar 'levantando' e o progresso de quem estava no chão recua.
            reviveSettings.holdSeconds = 3f;
            var ally = SpawnAlly(Near(1.5f));
            var allyLife = ally.GetComponent<PlayerLife>();
            Fall();
            ally.ServerSetHolding(true);
            yield return WaitFor(() => allyLife.IsReviving, 1f);
            Assert.IsTrue(allyLife.IsReviving);

            ally.GetComponent<NetworkHealth>().ServerApplyDamage(new DamagePacket(100000f, 0f), 0);
            yield return null;
            yield return null;

            Assert.IsTrue(allyLife.IsDowned, "O aliado caiu");
            Assert.IsFalse(allyLife.IsReviving, "Caído não levanta ninguém");
            yield return WaitFor(() => life.ReviveProgress <= 0f, 2f);
            Assert.AreEqual(0f, life.ReviveProgress, 1e-3f);
        }
    }
}
