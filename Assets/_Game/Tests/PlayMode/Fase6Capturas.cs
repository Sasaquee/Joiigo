using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Cards;
using Game.Core.Cards;
using Game.Dice;
using Game.Net;
using Game.UI;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Capturas da Fase 6 em Play (carta no chão, dado rolando e parado, revelação, emboscada, tiragem com qualidade).
    /// Não é teste de regra: só tira fotos para conferir o visual. Roda só quando pedido:
    /// -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.Fase6Capturas (pasta: variável JOIIGO_CAPTURAS).
    /// </summary>
    [Explicit("Só tira capturas; rodar quando for conferir o visual.")]
    public class Fase6Capturas
    {
        private string dir;
        private PlayerCards cards;
        private CardDropService service;

        [UnitySetUp]
        public IEnumerator SobeHost()
        {
            dir = System.Environment.GetEnvironmentVariable("JOIIGO_CAPTURAS");
            if (string.IsNullOrEmpty(dir))
                dir = Path.Combine(Application.dataPath, "..", "Logs", "Capturas");
            Directory.CreateDirectory(dir);

            yield return ArenaTestScene.Load();
            Assert.IsTrue(Object.FindFirstObjectByType<NetSession>().Host(), "Host iniciou");
            float timeout = 5f;
            while (NetworkManager.Singleton.LocalClient?.PlayerObject == null && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            cards = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerCards>();
            service = Object.FindFirstObjectByType<CardDropService>();
        }

        [UnityTearDown]
        public IEnumerator Encerra()
        {
            CardDropService.DebugForceNextRoll(null);
            yield return ArenaTestScene.Cleanup();
        }

        [UnityTest]
        public IEnumerator CapturaOFluxoDaCartaNoChao()
        {
            yield return new WaitForSeconds(1.5f); // shaders e câmera assentam
            yield return Shot("f6_00_jogo");

            Vector3 p = cards.transform.position;
            FloorCard card = service.ServerSpawnFloorCard(new Vector3(p.x + 1.8f, 0f, p.z + 1.2f));
            yield return new WaitForSeconds(1.2f);
            yield return Shot("f6_01_carta_no_chao");

            yield return Roll(card, 20, "20");

            card = service.ServerSpawnFloorCard(new Vector3(p.x - 1.8f, 0f, p.z + 1.2f));
            yield return new WaitForSeconds(0.5f);
            yield return Roll(card, 1, "01");
            yield return new WaitForSeconds(1.5f);
            yield return Shot("f6_09_emboscada");

            // Uma carta de cada qualidade para ver as molduras na tiragem.
            var grants = new List<CardGrant>();
            CardQuality[] qualities = { CardQuality.Worn, CardQuality.Good, CardQuality.Perfect };
            int q = 0;
            for (int id = 0; id < cards.Database.Count && q < qualities.Length; id++)
            {
                var data = cards.Database.Get(id);
                if (data.cursed || data.arcana != Arcana.Minor || cards.Owns(id))
                    continue;
                grants.Add(new CardGrant(id, GrantKind.New, qualities[q++]));
            }
            cards.ServerApplyGrants(grants);
            yield return new WaitForSeconds(2.5f); // revelações das cartas novas
            var loadout = Object.FindFirstObjectByType<LoadoutScreen>();
            if (loadout != null)
            {
                loadout.Open();
                yield return new WaitForSeconds(0.5f);
                yield return Shot("f6_10_tiragem_qualidade");
                loadout.Toggle();
            }
            yield return Shot("f6_11_barra_qualidade");
        }

        private IEnumerator Roll(FloorCard card, int value, string tag)
        {
            CardDropService.DebugForceNextRoll(value);
            card.ServerInteract(NetworkManager.Singleton.LocalClientId);
            float roll = service.Settings.rollDuration;
            yield return new WaitForSeconds(roll * 0.3f);
            yield return Shot($"f6_{tag}_a_dado_rolando");
            yield return new WaitForSeconds(roll * 0.7f + 0.15f);
            yield return Shot($"f6_{tag}_b_dado_parado");
            yield return new WaitForSeconds(service.Settings.grantDelay + 0.35f);
            yield return Shot($"f6_{tag}_c_revelacao");
            yield return new WaitForSeconds(1.2f);
            yield return Shot($"f6_{tag}_d_depois");
        }

        private IEnumerator Shot(string name) => PlayCapture.Shot(dir, name);
    }
}
