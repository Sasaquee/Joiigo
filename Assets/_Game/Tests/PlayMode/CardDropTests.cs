using System.Collections;
using System.Collections.Generic;
using Game.Cards;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Dice;
using Game.Enemies;
using Game.Net;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Fase 6 — carta no chão e D20, de ponta a ponta no host: a carta aparece, o F (ServerInteract) rola o dado no host,
    /// todos recebem a rolagem (RollStarted) e as cartas chegam depois da rolagem. O resultado é forçado pela
    /// ferramenta de debug do serviço (só existe no editor), o que deixa o teste determinístico.
    /// </summary>
    public class CardDropTests
    {
        private PlayerCards cards;
        private CardDropService service;
        private WaveSpawner spawner;
        private Game.UI.DiceRollUi diceUi;
        private int rolledSeen;
        private Vector3[] ambushSeen;

        [UnitySetUp]
        public IEnumerator SobeHost()
        {
            yield return ArenaTestScene.Load();
            var session = Object.FindFirstObjectByType<NetSession>();
            Assert.IsTrue(session.Host(), "Host iniciou");

            float timeout = 5f;
            while (NetworkManager.Singleton.LocalClient?.PlayerObject == null && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(NetworkManager.Singleton.LocalClient?.PlayerObject, "Jogador nasceu");
            cards = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerCards>();
            service = Object.FindFirstObjectByType<CardDropService>();
            spawner = Object.FindFirstObjectByType<WaveSpawner>();
            Assert.IsNotNull(service, "Cena tem o CardDropService (reconstruir: Game > Setup > Construir Arena)");
            diceUi = Object.FindFirstObjectByType<Game.UI.DiceRollUi>();
            Assert.IsNotNull(diceUi, "Cena tem a tela do dado");

            rolledSeen = 0;
            ambushSeen = null;
            CardDropService.RollStarted += OnRoll;
            CardDropService.AmbushRevealed += OnAmbush;
        }

        [UnityTearDown]
        public IEnumerator Encerra()
        {
            CardDropService.RollStarted -= OnRoll;
            CardDropService.AmbushRevealed -= OnAmbush;
            CardDropService.DebugForceNextRoll(null);
            yield return ArenaTestScene.Cleanup();
        }

        private void OnRoll(ulong roller, int result, float duration) => rolledSeen = result;

        private void OnAmbush(Vector3[] positions) => ambushSeen = positions;

        private float WaitForGrant => service.Settings.rollDuration + service.Settings.grantDelay + 0.6f;

        private IEnumerator PickUpWithRoll(int forced)
        {
            Vector3 near = cards.transform.position + Vector3.forward * 1.5f;
            FloorCard card = service.ServerSpawnFloorCard(new Vector3(near.x, 0f, near.z));
            Assert.IsNotNull(card, "A carta apareceu no chão");
            yield return null;

            CardDropService.DebugForceNextRoll(forced);
            card.ServerInteract(NetworkManager.Singleton.LocalClientId);
            yield return null;
            Assert.AreEqual(forced, rolledSeen, "Todos recebem a rolagem com o número do host");
            Assert.IsFalse(card != null && card.IsSpawned, "A carta some do chão ao ser pega");
        }

        [UnityTest]
        public IEnumerator CartaNoChao_PegarRolaODadoEEntregaDepoisDaRolagem()
        {
            int before = cards.Inventory.Count;
            yield return PickUpWithRoll(10);
            Assert.AreEqual(before, cards.Inventory.Count, "A carta só chega depois que o dado para");
            yield return new WaitForSeconds(WaitForGrant);
            Assert.AreEqual(before + 1, cards.Inventory.Count, "10 entrega uma carta");
            Assert.AreEqual(10, service.LastRoll);
        }

        [UnityTest]
        public IEnumerator CartaEntregue_TocaARevelacao()
        {
            // Bug: a revelação só existia se a Arena fosse a primeira cena do jogo; recarregada, não aparecia.
            var reveal = Object.FindFirstObjectByType<Game.UI.CardRevealFx>();
            Assert.IsNotNull(reveal, "A Arena carregada tem a revelação de carta");
            yield return PickUpWithRoll(10);
            float timeout = WaitForGrant + 1f;
            while (reveal.ActiveCount == 0 && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            Assert.Greater(reveal.ActiveCount, 0, "A carta entregue pelo dado é revelada na tela");
        }

        [UnityTest]
        public IEnumerator Vinte_EntregaArcanoMaior()
        {
            yield return PickUpWithRoll(20);
            yield return new WaitForSeconds(WaitForGrant);
            bool hasMajor = false;
            foreach (int id in cards.Inventory)
                hasMajor |= cards.Database.Get(id).arcana == Arcana.Major;
            Assert.IsTrue(hasMajor, "20 entrega um arcano maior (D-048)");
        }

        [UnityTest]
        public IEnumerator Um_EntregaAmaldicoadaEChamaEmboscada()
        {
            yield return PickUpWithRoll(1);
            yield return new WaitForSeconds(WaitForGrant);

            bool hasCursed = false;
            foreach (int id in cards.Inventory)
                hasCursed |= cards.Database.Get(id).cursed;
            Assert.IsTrue(hasCursed, "1 entrega a carta amaldiçoada");

            int near = 0;
            float reach = service.Settings.ambushRadius + 1.5f;
            foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                if (enemy.IsAlive && Vector3.Distance(enemy.transform.position, cards.transform.position) <= reach)
                    near++;
            Assert.GreaterOrEqual(near, service.Settings.ambushMin, "Emboscada: inimigos surgem em volta do jogador (D-049)");
        }

        // ---------- O 1 teatral (D-068): só apresentação ----------

        [UnityTest]
        public IEnumerator Um_DadoVermelhoVeuEscuroEBaque()
        {
            yield return PickUpWithRoll(1);
            Assert.IsTrue(diceUi.IsCritical, "O 1 entra no modo teatral (D-068)");
            Assert.IsTrue(diceUi.VeilActive, "O véu escuro entra durante a rolagem do 1");

            yield return new WaitForSeconds(service.Settings.rollDuration + 0.2f);
            Assert.IsTrue(diceUi.VeilActive, "O véu segura com o dado parado no 1");
            Assert.Greater(diceUi.VeilStrength, 0.9f, "Com o dado parado, o véu está cheio");
            Assert.Greater(diceUi.CriticalTint, 0.9f, "O dado parou vermelho");
            Assert.AreEqual(1, diceUi.ThudsPlayed, "Um baque grave quando o dado assenta no 1");
        }

        [UnityTest]
        public IEnumerator OutroNumero_DadoEVeuComoAntes()
        {
            yield return PickUpWithRoll(10);
            Assert.IsFalse(diceUi.IsCritical, "Fora do 1 não há modo teatral");
            yield return new WaitForSeconds(service.Settings.rollDuration + 0.2f);
            Assert.IsFalse(diceUi.IsCritical, "Fora do 1 não há modo teatral");
            Assert.IsFalse(diceUi.VeilActive, "Fora do 1 a tela não escurece");
            Assert.AreEqual(0f, diceUi.CriticalTint, "Fora do 1 o dado continua latão e ciano");
            Assert.AreEqual(0, diceUi.ThudsPlayed, "Fora do 1 não há baque");
        }

        [UnityTest]
        public IEnumerator Um_VeuSomeDepois()
        {
            yield return PickUpWithRoll(1);
            Assert.IsTrue(diceUi.VeilActive, "O véu entrou");
            var s = service.Settings;
            float timeout = s.rollDuration + s.veilHold + s.veilFadeOut + 1f;
            while (diceUi.VeilActive && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsFalse(diceUi.VeilActive, "O véu some depois da emboscada");
            Assert.IsFalse(diceUi.IsCritical, "O modo teatral termina");
            Assert.AreEqual(0f, diceUi.CriticalTint, "O dado volta ao latão para a próxima rolagem");
        }

        [UnityTest]
        public IEnumerator Um_VeuFicaAbaixoDaHudEDoDado()
        {
            // Revisão do D-068: o véu era filho da tela do dado (última da Canvas) e cobria a barra de cartas.
            yield return PickUpWithRoll(1);
            RectTransform veil = diceUi.Veil;
            Assert.IsNotNull(veil, "A tela do dado tem o véu");
            Assert.IsTrue(diceUi.VeilActive, "O véu está na tela");

            Transform canvas = diceUi.GetComponentInParent<Canvas>().rootCanvas.transform;
            Assert.AreEqual(canvas, veil.parent, "O véu é filho direto da Canvas");

            var bar = Object.FindFirstObjectByType<Game.UI.SkillBar>();
            Assert.IsNotNull(bar, "Cena tem a barra de cartas");
            Transform barTop = ChildOf(canvas, bar.transform);
            Transform diceTop = ChildOf(canvas, diceUi.transform);
            Assert.IsNotNull(barTop, "A barra de cartas está na Canvas");
            Assert.Less(veil.GetSiblingIndex(), barTop.GetSiblingIndex(), "O véu fica abaixo da barra de cartas");
            Assert.Less(veil.GetSiblingIndex(), diceTop.GetSiblingIndex(), "O dado fica acima do véu");
        }

        /// <summary>O ancestral de `t` que é filho direto de `root` (null se `t` não está sob `root`).</summary>
        private static Transform ChildOf(Transform root, Transform t)
        {
            while (t != null && t.parent != root)
                t = t.parent;
            return t;
        }

        [UnityTest]
        public IEnumerator Um_EmboscadaMostraOSurgimentoDeCadaInimigo()
        {
            var fx = Object.FindFirstObjectByType<AmbushFx>();
            Assert.IsNotNull(fx, "O serviço do dado cria o efeito de surgimento");

            yield return PickUpWithRoll(1);
            float timeout = WaitForGrant + 1f;
            while (ambushSeen == null && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            Assert.IsNotNull(ambushSeen, "A emboscada avisa todos onde os inimigos surgiram (ShowAmbushRpc)");
            Assert.That(ambushSeen.Length, Is.InRange(service.Settings.ambushMin, service.Settings.ambushMax),
                "A emboscada continua com 2 a 3 inimigos (D-049)");

            var enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            foreach (Vector3 position in ambushSeen)
            {
                bool found = false;
                foreach (var enemy in enemies)
                {
                    Vector3 d = enemy.transform.position - position;
                    d.y = 0f;
                    found |= enemy.IsAlive && d.magnitude < 1f;
                }
                Assert.IsTrue(found, "Cada surgimento é onde um inimigo nasceu");
            }

            Assert.AreEqual(ambushSeen.Length, fx.Shown, "Um surgimento por inimigo");
            Assert.AreEqual(ambushSeen.Length, fx.ActiveCount, "Os surgimentos estão no chão agora");
            Assert.AreEqual(1, fx.RumblesPlayed, "Um ronco grave por emboscada");
        }

        [UnityTest]
        public IEnumerator Repetida_MelhoraAQualidadeDaQueJaTem()
        {
            // Dá todas as comuns como gastas: qualquer comum que o dado entregar será repetida.
            var grants = new System.Collections.Generic.List<CardGrant>();
            for (int id = 0; id < cards.Database.Count; id++)
            {
                var data = cards.Database.Get(id);
                if (data.rarity == Rarity.Common && !data.cursed && data.arcana == Arcana.Minor)
                    grants.Add(new CardGrant(id, GrantKind.New, CardQuality.Worn));
            }
            cards.ServerApplyGrants(grants);
            yield return null;
            int countBefore = cards.Inventory.Count;

            yield return PickUpWithRoll(3); // 2–6: comum gasta
            yield return new WaitForSeconds(WaitForGrant);

            int upgraded = 0;
            foreach (var g in grants)
                if (cards.QualityOf(g.CardId) == CardQuality.Good)
                    upgraded++;
            Assert.AreEqual(1, upgraded, "Uma das comuns subiu de gasta para boa (D-051)");
            Assert.AreEqual(countBefore, cards.Inventory.Count, "Melhoria não cria carta nova");
        }

        // ---------- A carta do fim da onda surge onde caiu o último inimigo (D-082) ----------

        /// <summary>Largada e espera a primeira onda inteira nascer (a fila das bocas esvazia).</summary>
        private IEnumerator StartFirstWave()
        {
            Object.FindFirstObjectByType<MatchState>().ServerStart();
            float timeout = spawner.Settings.firstWaveDelay + 5f;
            while ((spawner.AliveCount == 0 || spawner.PendingCount > 0) && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            Assert.Greater(spawner.AliveCount, 1, "A primeira onda nasceu");
            Assert.AreEqual(0, spawner.PendingCount, "A fila das bocas esvaziou");
        }

        private static List<EnemyController> AliveEnemies()
        {
            var list = new List<EnemyController>();
            foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                if (enemy.IsAlive)
                    list.Add(enemy);
            return list;
        }

        private static void Kill(EnemyController enemy) =>
            enemy.Health.ServerApplyDamage(new DamagePacket(enemy.Health.Max * 100f, 0f), NetworkManager.Singleton.LocalClientId);

        private static float Planar(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        private static IEnumerator WaitForCard(System.Action<FloorCard> found, float timeout)
        {
            FloorCard card = null;
            while ((card = Object.FindFirstObjectByType<FloorCard>()) == null && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            found(card);
        }

        [UnityTest]
        public IEnumerator CartaDoFimDaOnda_SurgeOndeCaiuOUltimoInimigo()
        {
            // Vale na arena atual (sem NavMesh a carta usa a posição de morte) e no mapa novo (ponto andável).
            yield return StartFirstWave();

            var alive = AliveEnemies();
            for (int i = 1; i < alive.Count; i++)
                Kill(alive[i]);
            yield return null;
            yield return null;
            Assert.IsNull(Object.FindFirstObjectByType<FloorCard>(), "Com um inimigo em pé a onda não acabou: nada de carta ainda");

            Vector3 deathPoint = alive[0].transform.position;
            Kill(alive[0]);
            FloorCard card = null;
            yield return WaitForCard(c => card = c, 3f);
            Assert.IsNotNull(card, "A carta apareceu no fim da onda (D-050)");

            float radius = service.Settings.dropNavSampleRadius;
            bool walkableHere = NavMesh.SamplePosition(deathPoint, out _, 0.1f, NavMesh.AllAreas);
            bool navMeshNear = NavMesh.SamplePosition(deathPoint, out _, radius, NavMesh.AllAreas);
            float distance = Planar(card.transform.position, deathPoint);
            if (walkableHere || !navMeshNear)
                Assert.Less(distance, 0.15f, "A carta surge exatamente onde caiu o último inimigo (D-082)");
            else
                Assert.LessOrEqual(distance, radius + 0.1f, "Ponto não andável: a carta vai para o andável mais próximo");
        }

        [UnityTest]
        public IEnumerator CartaDoFimDaOnda_PontoNaoAndavel_VaiParaOChaoAndavelMaisProximo()
        {
            // precisa do mapa novo (P2/P3) e do bake da NavMesh
            yield return StartFirstWave();

            // Um ponto logo fora da NavMesh, junto à borda do chão andável: a borda fica a uma folga das paredes, então meio
            // metro para o outro lado dela ainda é chão físico, mas já não é NavMesh.
            Assert.IsTrue(NavMesh.SamplePosition(cards.transform.position, out NavMeshHit onMesh, 3f, NavMesh.AllAreas),
                "Sem NavMesh perto do jogador (precisa do mapa novo e do bake)");
            Assert.IsTrue(NavMesh.FindClosestEdge(onMesh.position, out NavMeshHit edge, NavMesh.AllAreas), "A NavMesh tem borda");

            Vector3 outside = default;
            bool found = false;
            foreach (float side in new[] { 1f, -1f })
            {
                Vector3 candidate = edge.position + edge.normal * (0.5f * side);
                candidate.y = edge.position.y;
                if (!NavMesh.SamplePosition(candidate, out _, 0.05f, NavMesh.AllAreas))
                {
                    outside = candidate;
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, "Achei um ponto fora da NavMesh junto da borda");

            // Os da onda caem, menos um; um inimigo posto no ponto de fora é o último a cair.
            var alive = AliveEnemies();
            for (int i = 1; i < alive.Count; i++)
                Kill(alive[i]);
            var last = spawner.ServerSpawn(spawner.Settings.enemyTypes[0], outside);
            Assert.IsNotNull(last, "Inimigo nasceu");
            last.enabled = false; // parado no ponto de fora: sem a IA ele não anda para a NavMesh antes de cair
            yield return null;
            Kill(alive[0]);
            yield return null;
            yield return null;
            Assert.IsNull(Object.FindFirstObjectByType<FloorCard>(), "Ainda há um inimigo em pé");

            Vector3 deathPoint = last.transform.position;
            Kill(last);
            FloorCard card = null;
            yield return WaitForCard(c => card = c, 3f);
            Assert.IsNotNull(card, "A carta apareceu");

            Assert.IsTrue(NavMesh.SamplePosition(card.transform.position, out _, 0.1f, NavMesh.AllAreas),
                "A carta fica em chão andável, mesmo com o último inimigo caído fora dele");
            Assert.LessOrEqual(Planar(card.transform.position, deathPoint), service.Settings.dropNavSampleRadius + 0.1f,
                "E perto de onde ele caiu");
        }

        // ---------- Emboscada em chão andável (D-049, passe do mapa) ----------

        [UnityTest]
        public IEnumerator Emboscada_JuntoDaParede_ContinuaComDoisATresInimigosEmChaoAndavel()
        {
            // precisa do mapa novo (P2/P3) e do bake da NavMesh
            Assert.IsTrue(NavMesh.SamplePosition(cards.transform.position, out NavMeshHit onMesh, 3f, NavMesh.AllAreas),
                "Sem NavMesh perto do jogador (precisa do mapa novo e do bake)");
            Assert.IsTrue(NavMesh.FindClosestEdge(onMesh.position, out NavMeshHit edge, NavMesh.AllAreas), "A NavMesh tem borda");

            // Jogador na borda do chão andável: parte do anel de 5 m da emboscada cai dentro de prédio ou fora da NavMesh.
            var controller = cards.GetComponent<CharacterController>();
            controller.enabled = false;
            cards.transform.position = edge.position;
            controller.enabled = true;
            yield return null;

            var floor = service.ServerSpawnFloorCard(cards.transform.position);
            Assert.IsNotNull(floor, "A carta apareceu no chão");
            yield return null;

            CardDropService.DebugForceNextRoll(1);
            floor.ServerInteract(NetworkManager.Singleton.LocalClientId);
            float timeout = WaitForGrant + 1f;
            while (ambushSeen == null && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            Assert.IsNotNull(ambushSeen, "A emboscada nasceu");
            Assert.That(ambushSeen.Length, Is.InRange(service.Settings.ambushMin, service.Settings.ambushMax),
                "A emboscada continua com 2 a 3 inimigos (D-049)");

            var path = new NavMeshPath();
            foreach (Vector3 position in ambushSeen)
            {
                Assert.IsTrue(NavMesh.SamplePosition(position, out NavMeshHit hit, 0.3f, NavMesh.AllAreas),
                    $"Inimigo da emboscada nasceu fora do chão andável: {position}");
                Assert.IsTrue(NavMesh.CalculatePath(edge.position, hit.position, NavMesh.AllAreas, path));
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, $"O jogador alcança o inimigo em {position}");
            }
        }
    }
}
