using System.Collections;
using Game.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class PlayerMotorTests
    {
        private GameObject ground;
        private GameObject player;
        private MovementSettings settings;

        [SetUp]
        public void SetUp()
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.localScale = new Vector3(100f, 1f, 100f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);

            settings = ScriptableObject.CreateInstance<MovementSettings>();
            player = new GameObject("PlayerTeste");
            var controller = player.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 1f, 0f);
            player.AddComponent<PlayerMotor>().Settings = settings;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(player);
            Object.Destroy(ground);
            Object.Destroy(settings);
        }

        [UnityTest]
        public IEnumerator Motor_AndaNaDirecaoDaIntencao()
        {
            var motor = player.GetComponent<PlayerMotor>();
            motor.SetIntent(Vector3.forward, null);

            yield return new WaitForSeconds(0.5f);

            Assert.Greater(player.transform.position.z, 1f);
            Assert.AreEqual(0f, player.transform.position.x, 0.05f);
        }

        [UnityTest]
        public IEnumerator Motor_OlhaParaAMiraEnquantoAndaParaOutroLado()
        {
            var motor = player.GetComponent<PlayerMotor>();
            motor.SetIntent(Vector3.forward, new Vector3(100f, 0f, 0f));

            yield return new WaitForSeconds(0.5f);

            Assert.Greater(player.transform.position.z, 1f, "Continua andando para frente.");
            Assert.AreEqual(90f, player.transform.eulerAngles.y, 5f, "O corpo olha para a mira (D-005).");
        }

        /// <summary>
        /// Regressão (Fase 3): o Netcode move o transform depois de instanciar. Sem ressincronizar o
        /// CharacterController, o primeiro Move puxava o jogador de volta para a posição antiga.
        /// </summary>
        [UnityTest]
        public IEnumerator Motor_RespeitaTransformMovidoPorFora()
        {
            yield return null; // motor já rodou com o controller na origem
            player.transform.position = new Vector3(10f, 0f, 10f);

            yield return null;
            yield return null;

            Assert.AreEqual(10f, player.transform.position.x, 0.05f);
            Assert.AreEqual(10f, player.transform.position.z, 0.05f);
        }

        [UnityTest]
        public IEnumerator Motor_RespeitaTransformMovidoAntesDoPrimeiroFrame()
        {
            var fresh = new GameObject("PlayerNovo");
            var controller = fresh.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 1f, 0f);
            fresh.AddComponent<PlayerMotor>().Settings = settings;
            fresh.transform.position = new Vector3(-8f, 0f, 5f); // como o spawn do Netcode

            yield return null;
            yield return null;

            Assert.AreEqual(-8f, fresh.transform.position.x, 0.05f);
            Assert.AreEqual(5f, fresh.transform.position.z, 0.05f);
            Object.Destroy(fresh);
        }

        [UnityTest]
        public IEnumerator Motor_ParaQuaseNaHoraAoSoltar()
        {
            var motor = player.GetComponent<PlayerMotor>();
            motor.SetIntent(Vector3.forward, null);
            yield return new WaitForSeconds(0.4f);

            motor.SetIntent(Vector3.zero, null);
            yield return new WaitForSeconds(0.2f);

            Assert.Less(motor.Velocity.magnitude, 0.01f);
        }
    }
}
