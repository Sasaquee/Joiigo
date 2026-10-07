using System;
using System.Collections;
using System.Collections.Generic;
using Game.Cards;
using Game.Combat;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Session;
using Game.Dice;
using Game.Enemies;
using Game.Net;
using Game.Player;
using Game.UI;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Queda total (D-084, D-086, D-087): com todos os jogadores caídos a tela escurece, a arena zera, as cartas somem e todos
    /// acordam no spawn. No solo a partida recomeça sozinha; no coop espera a alavanca. Só há um cliente de verdade nos testes:
    /// o segundo jogador do coop é uma instância extra do prefab, spawnada pelo host e declarada pelo gancho MatchReset.TestExtraPlayers.
    ///
    /// Precisam do MatchReset na cena: reconstruir a arena (Game > Setup > Construir Arena) com MatchResetSetup.Apply no ArenaBuilder.
    /// </summary>
    public class WipeTests
    {
        private const string SemMatchReset =
            "A cena Arena não tem o MatchReset: rode Game > Setup > Construir Arena com MatchResetSetup.Apply(arena) no ArenaBuilder.";

        private NetworkPlayer player;
        private PlayerLife life;
        private NetworkHealth health;
        private PlayerCards cards;
        private MatchState match;
        private WaveSpawner spawner;
        private CardDropService drops;
        private MatchReset reset;
        private WipeFadeUi ui;
        private MatchSettings settings;
        private readonly List<PlayerLife> extras = new List<PlayerLife>();

        [UnitySetUp]
        public IEnumerator SobeHost()
        {
            yield return ArenaTestScene.Load();
            Assert.IsTrue(Object.FindFirstObjectByType<NetSession>().Host(), "Host iniciou");
            yield return WaitFor(() => NetworkManager.Singleton.LocalClient?.PlayerObject != null, 5f);

            player = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<NetworkPlayer>();
            player.GetComponent<PlayerInputReader>().enabled = false; // sem mouse no teste
            life = player.GetComponent<PlayerLife>();
            health = player.GetComponent<NetworkHealth>();
            cards = player.GetComponent<PlayerCards>();
            match = Object.FindFirstObjectByType<MatchState>();
            spawner = Object.FindFirstObjectByType<WaveSpawner>();
            drops = Object.FindFirstObjectByType<CardDropService>();
            reset = Object.FindFirstObjectByType<MatchReset>();
            Assert.IsNotNull(reset, SemMatchReset);
            Assert.IsNotNull(match);
            Assert.IsNotNull(spawner);
            Assert.IsNotNull(drops);

            // Tempos curtos, independentes do asset (o asset tem 2 s, 0,6 s e 1 s).
            settings = ScriptableObject.CreateInstance<MatchSettings>();
            settings.wipeFadeSeconds = 0.4f;
            settings.wipeHoldBlackSeconds = 0.2f;
            settings.wipeFadeInSeconds = 0.3f;
            settings.wipeSoundVolume = 0.01f; // toca (conta), mas quase mudo
            reset.Settings = settings;

            ui = WipeFadeUi.EnsureOnCanvas();
            Assert.IsNotNull(ui, "A tela da queda total se criou sob a Canvas UI");
            extras.Clear();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Encerra()
        {
            MatchReset.TestExtraPlayers = null;
            foreach (var extra in extras)
                if (extra != null)
                    Object.Destroy(extra.gameObject);
            extras.Clear();
            if (settings != null)
                Object.Destroy(settings);
            yield return ArenaTestScene.Cleanup();
        }

        // ---------- Ajudantes ----------

        private static IEnumerator WaitFor(Func<bool> condition, float timeout)
        {
            while (!condition() && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static void Kill(PlayerLife who) =>
            who.GetComponent<NetworkHealth>().ServerApplyDamage(new DamagePacket(100000f, 0f), 0);

        private void Teleport(Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
        }

        private static int FindCard(PlayerCards of, Func<CardData, bool> match)
        {
            for (int id = 0; id < of.Database.Count; id++)
                if (of.Database.Get(id) != null && match(of.Database.Get(id)))
                    return id;
            return -1;
        }

        /// <summary>Dá cartas ao jogador: uma skill perfeita equipada e usada (gasta energia), uma passiva e uma sobra no inventário.</summary>
        private IEnumerator GiveCards(PlayerCards to)
        {
            int skill = FindCard(to, c => c.Kind == CardKind.Skill && c.energyCost > 0f);
            int passive = FindCard(to, c => c.Kind == CardKind.Passive);
            Assert.GreaterOrEqual(skill, 0, "O banco tem uma skill com custo");
            Assert.GreaterOrEqual(passive, 0, "O banco tem uma passiva");

            to.ServerApplyGrants(new[] { new CardGrant(skill, GrantKind.New, CardQuality.Perfect) });
            to.ServerGiveCard(passive);
            to.ServerGiveCard(skill); // repetida no inventário
            to.RequestEquip(skill, SlotType.Skill, 0);
            to.RequestEquip(passive, SlotType.Passive, 0);
            to.Path.Record(new[] { "engrenagem", "vapor" });
            yield return null;

            Assert.AreEqual(skill, to.GetSlot(SlotType.Skill, 0), "Skill equipada");
            Assert.AreEqual(CardQuality.Perfect, to.QualityOf(skill));
            Assert.IsTrue(to.ServerUseSkill(0, to.transform.position + to.transform.forward * 5f), "Usou a skill (gasta energia)");
            Assert.Less(to.Energy, to.EnergyMax - 0.01f, "Gastou energia");
            Assert.Greater(to.Path.Counts.Count, 0, "O caminho tem tags");
            Assert.Greater(to.Inventory.Count, 0, "Sobrou carta no inventário");
        }

        private void AssertSemCartas(PlayerCards of, string quem)
        {
            Assert.AreEqual(0, of.Inventory.Count, $"{quem}: inventário vazio");
            foreach (SlotType slot in new[] { SlotType.Skill, SlotType.Passive, SlotType.Equipment, SlotType.Belt })
                for (int i = 0; i < CardRules.SlotCount(slot); i++)
                    Assert.AreEqual(Loadout.Empty, of.GetSlot(slot, i), $"{quem}: {slot} {i} vazio");
            for (int i = 0; i < CardRules.BeltSlots; i++)
                Assert.AreEqual(0, of.BeltCount(i), $"{quem}: cinto {i} sem cópias");
            Assert.AreEqual(0, of.Path.Counts.Count, $"{quem}: caminho de tags zerado");
            for (int id = 0; id < of.Database.Count; id++)
                Assert.AreEqual(CardQuality.Good, of.QualityOf(id), $"{quem}: qualidade da carta {id} no padrão");
            float start = of.Settings != null ? of.Settings.startEnergyFraction : 1f;
            Assert.AreEqual(of.EnergyMax * start, of.Energy, 0.5f, $"{quem}: energia no valor de início");
        }

        private IEnumerator SpawnFakePlayer()
        {
            GameObject prefab = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
            GameObject fake = Object.Instantiate(prefab, player.transform.position + Vector3.right * 1.5f, player.transform.rotation);
            fake.GetComponent<NetworkObject>().Spawn();
            yield return null;
            fake.GetComponent<PlayerInputReader>().enabled = false; // o host é dono dos dois; só o real lê o teclado
            extras.Add(fake.GetComponent<PlayerLife>());
            MatchReset.TestExtraPlayers = () => extras;
            Assert.IsTrue(extras[0].IsSpawned, "O jogador extra nasceu na rede");
        }

        // ---------- Solo ----------

        [UnityTest]
        public IEnumerator Solo_QuedaTotal_EscureceZeraAArenaEAPartidaRecomecaSozinha()
        {
            Vector3 spawn = player.transform.position;

            // Partida em andamento: primeira onda no ar, cartas, carta no chão, um inimigo caído virando sucata.
            match.ServerStart();
            yield return WaitFor(() => spawner.AliveCount > 0, spawner.Settings.firstWaveDelay + 5f);
            Assert.Greater(spawner.AliveCount, 0, "A primeira onda nasceu");
            yield return GiveCards(cards);
            drops.ServerSpawnFloorCard(spawn + new Vector3(3f, 0f, 3f));
            Assert.AreEqual(1, Object.FindObjectsByType<FloorCard>(FindObjectsSortMode.None).Length, "Carta no chão");
            var enemy = spawner.ServerSpawn(spawner.Settings.enemyTypes[0], spawn + new Vector3(-8f, 0f, 8f));
            yield return null;
            enemy.Health.ServerApplyDamage(new DamagePacket(enemy.Health.Max * 100f, 0f), NetworkManager.Singleton.LocalClientId);
            Teleport(spawn + new Vector3(4f, 0f, 4f)); // longe do spawn, para provar a volta

            // O único jogador cai: queda total no solo (D-084, igual ao coop).
            Kill(life);
            Assert.IsTrue(life.IsDowned, "Caiu");
            yield return WaitFor(() => ui.FadeAlpha > 0.3f, 1.5f);
            Assert.Greater(ui.FadeAlpha, 0.3f, "A tela escurece (D-086)");
            Assert.IsTrue(ui.Active);
            Assert.AreEqual(1, reset.WipesStarted);
            Assert.GreaterOrEqual(ui.SoundsPlayed, 1, "O som grave tocou");
            Assert.AreNotEqual(WipePhase.Idle, reset.Phase);

            // Ao fim: tudo zerado e a partida recomeça sozinha (solo, D-087).
            yield return WaitFor(() => reset.RestartsCompleted == 1, settings.wipeFadeSeconds + settings.wipeHoldBlackSeconds + 2f);
            Assert.AreEqual(1, reset.RestartsCompleted, "O recomeço terminou");
            Assert.IsTrue(reset.LastRestartWasSolo);
            yield return null;
            yield return null;

            Assert.AreEqual(0, Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None).Length, "Nenhum inimigo, nem sucata");
            Assert.AreEqual(0, spawner.AliveCount);
            Assert.AreEqual(0, spawner.PendingCount, "Fila das bocas vazia");
            Assert.AreEqual(0, spawner.WavesReleased, "A onda voltou à 1");
            Assert.AreEqual(0, Object.FindObjectsByType<FloorCard>(FindObjectsSortMode.None).Length, "Sem carta no chão");
            Assert.AreEqual(0, drops.CardsOnFloor.Count);

            AssertSemCartas(cards, "Jogador");

            Assert.IsFalse(life.IsDowned, "Acordou de pé");
            Assert.IsTrue(life.CanAct);
            Assert.AreEqual(health.Max, health.Current, 0.001f, "Vida cheia");
            Vector3 flat = player.transform.position - spawn;
            flat.y = 0f;
            Assert.Less(flat.magnitude, 0.5f, "Acordou no spawn");

            Assert.IsTrue(match.IsStarted, "No solo a largada volta sozinha (D-018)");

            // A tela clareia depois do recomeço.
            yield return WaitFor(() => !ui.Active, settings.wipeFadeInSeconds + 2f);
            Assert.IsFalse(ui.Active, "A tela voltou ao normal");
            Assert.AreEqual(0f, ui.FadeAlpha, 0.001f);

            // E a primeira onda sai de novo.
            yield return WaitFor(() => spawner.AliveCount > 0, spawner.Settings.firstWaveDelay + 5f);
            Assert.Greater(spawner.AliveCount, 0, "A onda 1 nasceu de novo");
            Assert.AreEqual(1, spawner.WavesReleased);
        }

        [UnityTest]
        public IEnumerator Solo_SalaDeEspera_QuedaNaoRecomecaNada()
        {
            // Sem largada (sala de espera) a queda do único jogador é só D-022: volta no spawn pelo tempo, sem escurecer.
            Assert.IsFalse(match.IsStarted);
            Kill(life);
            Assert.IsTrue(life.IsDowned);

            yield return new WaitForSeconds(settings.wipeFadeSeconds + settings.wipeHoldBlackSeconds + 0.5f);
            Assert.AreEqual(0, reset.WipesStarted, "Sem partida não há queda total");
            Assert.IsFalse(ui.Active);
            Assert.AreEqual(WipePhase.Idle, reset.Phase);
            Assert.IsFalse(match.IsStarted);
        }

        // ---------- Coop (um jogador extra, declarado pelo gancho de teste) ----------

        [UnityTest]
        public IEnumerator Coop_SoUmCaido_NaoDispara_TodosCaidos_ZeraEEsperaAAlavanca()
        {
            yield return SpawnFakePlayer();
            PlayerLife other = extras[0];
            var otherCards = other.GetComponent<PlayerCards>();

            match.ServerStart();
            yield return GiveCards(cards);
            yield return GiveCards(otherCards);
            Assert.IsFalse(WipeRule.IsSolo(2), "Dois jogadores conectados: coop");

            // Só um caiu: continua D-003, ninguém recomeça.
            Kill(life);
            yield return new WaitForSeconds(settings.wipeFadeSeconds + settings.wipeHoldBlackSeconds + 0.4f);
            Assert.AreEqual(0, reset.WipesStarted, "Um caído só não dispara");
            Assert.IsFalse(ui.Active);
            Assert.IsTrue(match.IsStarted, "A partida segue");
            Assert.Greater(cards.Inventory.Count, 0, "As cartas de quem caiu continuam");

            // O segundo cai: queda total.
            Kill(other);
            yield return WaitFor(() => ui.FadeAlpha > 0.3f, 1.5f);
            Assert.Greater(ui.FadeAlpha, 0.3f, "A tela escurece");
            Assert.AreEqual(1, reset.WipesStarted);

            yield return WaitFor(() => reset.RestartsCompleted == 1, settings.wipeFadeSeconds + settings.wipeHoldBlackSeconds + 2f);
            Assert.AreEqual(1, reset.RestartsCompleted);
            Assert.IsFalse(reset.LastRestartWasSolo);
            yield return null;
            yield return null;

            // No coop todos acordam na sala de espera: partida NÃO iniciada (portões apagados), sem cartas, de pé.
            Assert.IsFalse(match.IsStarted, "Coop: espera o host puxar a alavanca (D-087)");
            Assert.IsFalse(life.IsDowned);
            Assert.IsFalse(other.IsDowned);
            Assert.AreEqual(health.Max, health.Current, 0.001f);
            AssertSemCartas(cards, "Jogador 1");
            AssertSemCartas(otherCards, "Jogador 2");
            Assert.AreEqual(0, spawner.AliveCount);

            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(match.IsStarted, "Nada recomeça sozinho no coop");
            Assert.AreEqual(0, spawner.WavesReleased, "Sem partida não sai onda");

            // O host puxa a alavanca de novo (StartLever.ServerInteract chama o ServerStart).
            match.ServerStart();
            Assert.IsTrue(match.IsStarted, "A alavanca começa a partida de novo");
            yield return WaitFor(() => !ui.Active, settings.wipeFadeInSeconds + 2f);
            Assert.IsFalse(ui.Active);
        }

        // ---------- A tela ----------

        [UnityTest]
        public IEnumerator Tela_EscureceSeguraEClareia_SemReceberMouse()
        {
            Assert.IsFalse(ui.Active);
            Assert.AreEqual(0f, ui.FadeAlpha, 0.001f);

            ui.BeginFadeOut(0.3f, 0f);
            Assert.IsTrue(ui.Active);
            yield return new WaitForSeconds(0.15f);
            Assert.Greater(ui.FadeAlpha, 0f);
            Assert.Less(ui.FadeAlpha, 1f, "No meio do escurecer");

            yield return WaitFor(() => ui.IsBlack, 1f);
            Assert.IsTrue(ui.IsBlack, "Chegou ao preto");
            Assert.AreEqual(1f, ui.FadeAlpha, 0.001f);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(ui.IsBlack, "Segura o preto até o host avisar");

            Assert.IsFalse(ui.BlocksMouse, "O preto não recebe o mouse");
            Canvas canvas = ui.GetComponent<Canvas>();
            Assert.IsTrue(canvas.overrideSorting, "Desenha por cima da HUD");
            Assert.Greater(canvas.sortingOrder, 100);

            ui.BeginFadeIn(0.2f);
            yield return WaitFor(() => !ui.Active, 1f);
            Assert.IsFalse(ui.Active);
            Assert.AreEqual(0f, ui.FadeAlpha, 0.001f);
        }
    }
}
