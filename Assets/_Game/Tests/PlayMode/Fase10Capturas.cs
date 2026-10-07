using System.Collections;
using Game.Cameras;
using Game.Cards;
using Game.Core.Cards;
using Game.Net;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Capturas da Fase 10 em Play: o visual das skills novas (Giro, Chicote, Martelo, Carro, Ceifadora, Estrela, Tempestade).
    /// Só tira fotos; roda só quando pedido:
    /// -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.Fase10Capturas (pasta: variável JOIIGO_CAPTURAS).
    /// </summary>
    [Explicit("Só tira capturas; rodar quando for conferir o visual.")]
    public class Fase10Capturas
    {
        private string dir;
        private GameObject player;
        private PlayerCards cards;
        private CameraSettings cameraSettings;
        private float cameraDistance;

        [UnitySetUp]
        public IEnumerator SobeHost()
        {
            dir = PlayCapture.Folder("fase10");
            yield return ArenaTestScene.Load();
            Assert.IsTrue(Object.FindFirstObjectByType<NetSession>().Host(), "Host iniciou");
            float timeout = 5f;
            while (NetworkManager.Singleton.LocalClient?.PlayerObject == null && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            player = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
            cards = player.GetComponent<PlayerCards>();
            cameraSettings = Camera.main.GetComponent<CameraFollow>().Settings;
            cameraDistance = cameraSettings.distance;
            cameraSettings.distance = 9f;
        }

        [UnityTearDown]
        public IEnumerator Encerra()
        {
            cameraSettings.distance = cameraDistance;
            yield return ArenaTestScene.Cleanup();
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
                yield return null;
        }

        private int nextSlot;

        private IEnumerator UseAndShoot(string cardId, string shotName, float wait)
        {
            int slot = nextSlot++ % 4; // um espaço de skill por carta, em rodízio
            int id = cards.FindCardId(cardId);
            Assert.GreaterOrEqual(id, 0, $"Carta {cardId} existe");
            cards.ServerGiveCard(id);
            cards.RequestEquip(id, SlotType.Skill, slot);
            Assert.AreEqual(id, cards.GetSlot(SlotType.Skill, slot), $"{cardId} equipada");
            float timeout = 30f; // espera o espaço sair da recarga da skill anterior
            while (cards.SkillCooldownFraction(slot) > 0f && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            cards.AddEnergy(200f);
            yield return Wait(0.3f);
            Vector3 aim = player.transform.position + player.transform.forward * 6f;
            Assert.IsTrue(cards.ServerUseSkill(slot, aim), $"{cardId} usada");
            yield return Wait(wait);
            yield return PlayCapture.Shot(dir, shotName);
            yield return Wait(0.5f);
        }

        [UnityTest]
        public IEnumerator CapturaAsSkillsNovas()
        {
            player.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            yield return Wait(0.5f);
            yield return UseAndShoot("giro_engrenagem", "f10_01_giro", 0.12f);
            yield return UseAndShoot("chicote_corrente", "f10_02_chicote", 0.12f);
            yield return UseAndShoot("martelo_vapor", "f10_03_martelo", 0.12f);
            yield return UseAndShoot("carro_vapor", "f10_04_carro", 0.15f);
            yield return UseAndShoot("ceifadora_engrenagens", "f10_05_ceifadora", 0.12f);
            yield return UseAndShoot("estrela_cristal", "f10_06_estrela", 0.12f);
            yield return UseAndShoot("tempestade_faiscas", "f10_07_tempestade", 0.12f);
        }
    }
}
