using System.Collections;
using Game.Aura;
using Game.Cameras;
using Game.Cards;
using Game.Combat;
using Game.Core.Aura;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Net;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Capturas da Fase 7 em Play: a aura em cada situação (D-060 a D-066). Só tira fotos; roda só quando pedido:
    /// -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.Fase7Capturas (pasta: variável JOIIGO_CAPTURAS).
    /// </summary>
    [Explicit("Só tira capturas; rodar quando for conferir o visual.")]
    public class Fase7Capturas
    {
        private string dir;
        private GameObject player;
        private PlayerCards cards;
        private NetworkHealth health;
        private CameraSettings cameraSettings;
        private float cameraDistance;

        [UnitySetUp]
        public IEnumerator SobeHost()
        {
            dir = PlayCapture.Folder("fase7");
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
            health = player.GetComponent<NetworkHealth>();

            // Câmera mais perto só para as fotos (o asset é devolvido no fim).
            cameraSettings = Camera.main.GetComponent<CameraFollow>().Settings;
            cameraDistance = cameraSettings.distance;
            cameraSettings.distance = 8f;
        }

        [UnityTearDown]
        public IEnumerator Encerra()
        {
            if (cameraSettings != null)
                cameraSettings.distance = cameraDistance;
            AuraPaletteSwitch.Set(AuraPalette.Normal);
            yield return ArenaTestScene.Cleanup();
        }

        private int CardId(string id)
        {
            for (int i = 0; i < cards.Database.Count; i++)
                if (cards.Database.Get(i).id == id)
                    return i;
            Assert.Fail($"Carta {id} não existe");
            return -1;
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
                yield return null;
        }

        [UnityTest]
        public IEnumerator CapturaAAuraEmCadaSituacao()
        {
            yield return Wait(1.5f);
            yield return PlayCapture.Shot(dir, "f7_01_hp_cheio_sem_energia");

            cards.AddEnergy(10000f);
            yield return Wait(1.5f);
            yield return PlayCapture.Shot(dir, "f7_02_energia_cheia");

            health.ServerApplyDamage(new DamagePacket(health.Max * 0.85f, 0f), 0);
            yield return Wait(1.5f);
            yield return PlayCapture.Shot(dir, "f7_03_hp_baixo");
            yield return Wait(0.13f);
            yield return PlayCapture.Shot(dir, "f7_03b_hp_baixo_falha");

            health.ServerRestore();
            var shield = player.GetComponent<PlayerShield>();
            if (shield == null)
                shield = player.AddComponent<PlayerShield>();
            shield.Activate(cards, 5f, 0f, 0f, 1f, 1f, 60f);
            yield return Wait(1f);
            yield return PlayCapture.Shot(dir, "f7_04_escudo");

            int blade = CardId("lamina_sedenta");
            cards.ServerGiveCard(blade);
            cards.RequestEquip(blade, SlotType.Skill, 0);
            yield return Wait(4f); // a revelação da carta nova passa pela tela antes
            yield return PlayCapture.Shot(dir, "f7_05_maldicao");

            int spring = CardId("mola_recuo");
            cards.ServerGiveCard(spring);
            cards.RequestEquip(spring, SlotType.Passive, 0);
            yield return Wait(4f);
            health.ServerApplyDamage(new DamagePacket(5f, 0f), 0);
            yield return Wait(0.6f);
            yield return PlayCapture.Shot(dir, "f7_06_bonus_de_dano");

            AuraPaletteSwitch.Set(AuraPalette.Alternative);
            yield return Wait(0.5f);
            yield return PlayCapture.Shot(dir, "f7_07_paleta_alternativa");
            AuraPaletteSwitch.Set(AuraPalette.Normal);

            health.ServerApplyDamage(new DamagePacket(health.Max * 10f, 0f), 0);
            yield return Wait(1.5f);
            yield return PlayCapture.Shot(dir, "f7_08_caido");
        }
    }
}
