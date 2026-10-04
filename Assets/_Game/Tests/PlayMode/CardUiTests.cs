using System.Collections;
using Game.Cards;
using Game.Core.Cards;
using Game.Net;
using Game.Player;
using Game.UI;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Interface das cartas (D-027, D-028): sobe a arena como host e confere a tela de tiragem e a barra de skills.
    /// O Tab é simulado chamando LoadoutScreen.Toggle(), e o arrastar e soltar chamando TryDropOnSlot / TryUnequip,
    /// que são as mesmas rotas que os handlers de arrasto usam.
    /// </summary>
    public class CardUiTests
    {
        private NetSession session;
        private PlayerCards cards;
        private PlayerInputReader reader;
        private LoadoutScreen screen;
        private SkillBar bar;

        [UnitySetUp]
        public IEnumerator SobeHost()
        {
            yield return ArenaTestScene.Load();
            session = Object.FindFirstObjectByType<NetSession>();
            Assert.IsTrue(session.Host(), "Host iniciou");

            float timeout = 5f;
            while (NetworkManager.Singleton.LocalClient?.PlayerObject == null && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(NetworkManager.Singleton.LocalClient?.PlayerObject, "Jogador nasceu");

            NetworkObject player = NetworkManager.Singleton.LocalClient.PlayerObject;
            cards = player.GetComponent<PlayerCards>();
            reader = player.GetComponent<PlayerInputReader>();
            Assert.IsNotNull(cards, "Prefab do jogador tem PlayerCards");
            Assert.IsNotNull(reader, "Prefab do jogador tem PlayerInputReader");

            screen = Object.FindFirstObjectByType<LoadoutScreen>();
            bar = Object.FindFirstObjectByType<SkillBar>();
            Assert.IsNotNull(screen, "Cena tem a tela de tiragem (reconstruir: Game > Setup > Construir Arena)");
            Assert.IsNotNull(bar, "Cena tem a barra de cartas");
            yield return WaitUntil(() => LocalPlayerCards.Get() != null);
        }

        [UnityTearDown]
        public IEnumerator Encerra()
        {
            yield return ArenaTestScene.Cleanup();
        }

        [UnityTest]
        public IEnumerator Tiragem_NasceFechadaAbreBloqueiaAcoesEFecha()
        {
            Assert.IsFalse(screen.IsOpen, "Nasce fechada");
            Assert.IsFalse(reader.UiBlocksActions, "Fechada não bloqueia nada");

            screen.Toggle();
            yield return null;
            Assert.IsTrue(screen.IsOpen, "Tab abre a tiragem");
            Assert.IsTrue(reader.UiBlocksActions, "Aberta, ataque, skills e cinto ficam bloqueados");
            Assert.IsTrue(reader.InputEnabled, "O movimento continua: o mapa de input segue ligado");
            Assert.AreEqual(1f, Time.timeScale, "O jogo segue rodando por trás (D-027)");

            screen.Toggle();
            yield return null;
            Assert.IsFalse(screen.IsOpen, "Tab de novo fecha");
            Assert.IsFalse(reader.UiBlocksActions, "Fechada, o input de combate volta");
        }

        [UnityTest]
        public IEnumerator Inventario_MostraAsCartasDadas()
        {
            screen.Toggle();
            yield return WaitUntil(() => screen.IsOpen);
            Assert.AreEqual(0, screen.InventoryViewCount, "Começa sem cartas (D-033)");

            cards.ServerGiveCard(FirstCardOfKind(CardKind.Skill));
            yield return WaitUntil(() => screen.InventoryViewCount == 1);
            Assert.AreEqual(1, screen.InventoryViewCount, "Uma carta no leque depois do Changed");

            cards.ServerGiveCard(FirstCardOfKind(CardKind.Item));
            yield return WaitUntil(() => screen.InventoryViewCount == 2);
            Assert.AreEqual(2, screen.InventoryViewCount, "Duas cartas no leque");
        }

        [UnityTest]
        public IEnumerator Soltar_EquipaNoEspacoCompativelEDesequipaDeVolta()
        {
            int skill = FirstCardOfKind(CardKind.Skill);
            cards.ServerGiveCard(skill);
            screen.Toggle();
            yield return WaitUntil(() => screen.InventoryViewCount == 1);

            Assert.IsFalse(screen.TryDropOnSlot(skill, SlotType.Passive, 0), "Skill não cabe no espaço de passiva");
            Assert.IsFalse(screen.TryDropOnSlot(skill, SlotType.Skill, CardRules.SkillSlots), "Índice fora do tipo");
            Assert.IsTrue(screen.TryDropOnSlot(skill, SlotType.Skill, 1), "Skill cabe no espaço de skill");

            yield return WaitUntil(() => cards.GetSlot(SlotType.Skill, 1) == skill);
            Assert.AreEqual(skill, cards.GetSlot(SlotType.Skill, 1), "O host equipou a carta");
            Assert.AreEqual(-1, cards.GetSlot(SlotType.Skill, 0), "Os outros espaços seguem vazios");

            yield return WaitUntil(() => screen.InventoryViewCount == 0 && bar.SkillCardId(1) == skill);
            Assert.AreEqual(0, screen.InventoryViewCount, "A carta saiu do leque");
            Assert.AreEqual(skill, bar.SkillCardId(1), "A barra de skills mostra a carta equipada");
            Assert.AreEqual(-1, bar.SkillCardId(0), "Espaço vazio na barra");

            Assert.IsFalse(screen.TryUnequip(SlotType.Skill, 0), "Não há o que tirar de um espaço vazio");
            Assert.IsTrue(screen.TryUnequip(SlotType.Skill, 1), "Soltar no leque devolve a carta");
            yield return WaitUntil(() => cards.GetSlot(SlotType.Skill, 1) == -1 && screen.InventoryViewCount == 1);
            Assert.AreEqual(-1, cards.GetSlot(SlotType.Skill, 1), "O espaço esvaziou");
            Assert.AreEqual(1, screen.InventoryViewCount, "A carta voltou ao leque");
            yield return WaitUntil(() => bar.SkillCardId(1) == -1);
            Assert.AreEqual(-1, bar.SkillCardId(1), "A barra esvaziou");
        }

        [UnityTest]
        public IEnumerator Cinto_EmpilhaConsumiveisIguais()
        {
            int item = FirstCardOfKind(CardKind.Item);
            cards.ServerGiveCard(item);
            cards.ServerGiveCard(item);
            screen.Toggle();
            yield return WaitUntil(() => screen.InventoryViewCount == 2);

            Assert.IsTrue(screen.TryDropOnSlot(item, SlotType.Belt, 0));
            Assert.IsTrue(screen.TryDropOnSlot(item, SlotType.Belt, 0));
            yield return WaitUntil(() => cards.BeltCount(0) == 2 && screen.InventoryViewCount == 0);

            Assert.AreEqual(item, cards.GetSlot(SlotType.Belt, 0));
            Assert.AreEqual(2, cards.BeltCount(0), "As duas cópias empilham no mesmo espaço do cinto");
            Assert.AreEqual(0, screen.InventoryViewCount);
            yield return WaitUntil(() => bar.BeltCardId(0) == item);
            Assert.AreEqual(item, bar.BeltCardId(0), "A barra mostra o consumível do cinto");
        }

        private int FirstCardOfKind(CardKind kind)
        {
            CardDatabase database = cards.Database;
            Assert.IsNotNull(database, "PlayerCards tem o banco de cartas");
            for (int i = 0; i < database.Count; i++)
            {
                if (database.Get(i) != null && database.KindOf(i) == kind)
                    return i;
            }

            Assert.Fail($"O banco de cartas não tem carta do tipo {kind}");
            return -1;
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, float timeout = 3f)
        {
            while (!condition() && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
