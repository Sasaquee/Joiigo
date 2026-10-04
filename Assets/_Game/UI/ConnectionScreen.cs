using Game.Net;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Tela simples de Hospedar / Entrar com IP. Some ao conectar e volta quando a sessão termina,
    /// com o motivo em uma linha. O host vê o próprio IP num canto (D-012).
    /// </summary>
    public class ConnectionScreen : MonoBehaviour
    {
        [SerializeField] private NetSession session;
        [SerializeField] private GameObject panel;
        [SerializeField] private InputField ipField;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Text hostIpText;

        public void Configure(NetSession newSession, GameObject newPanel, InputField ip, Button host, Button join, Text status, Text hostIp)
        {
            session = newSession;
            panel = newPanel;
            ipField = ip;
            hostButton = host;
            joinButton = join;
            statusText = status;
            hostIpText = hostIp;
        }

        private void OnEnable()
        {
            hostButton.onClick.AddListener(OnHost);
            joinButton.onClick.AddListener(OnJoin);
            session.Connected += OnConnected;
            session.Ended += OnEnded;
        }

        private void OnDisable()
        {
            hostButton.onClick.RemoveListener(OnHost);
            joinButton.onClick.RemoveListener(OnJoin);
            session.Connected -= OnConnected;
            session.Ended -= OnEnded;
        }

        private void Start()
        {
            if (string.IsNullOrEmpty(ipField.text))
                ipField.text = "127.0.0.1";
            if (!session.IsRunning)
                ShowPanel(string.Empty);
        }

        private void OnHost()
        {
            SetButtons(false);
            if (!session.Host())
            {
                ShowPanel("Não foi possível hospedar.");
                return;
            }

            var ips = LocalAddress.LanIPv4();
            hostIpText.text = ips.Count > 0 ? "IP: " + string.Join("  ·  ", ips) : string.Empty;
            hostIpText.gameObject.SetActive(ips.Count > 0);
        }

        private void OnJoin()
        {
            SetButtons(false);
            statusText.text = "Conectando…";
            if (!session.Join(ipField.text))
                ShowPanel("Não foi possível conectar.");
        }

        private void OnConnected()
        {
            if (panel == null)
                return;
            panel.SetActive(false);
        }

        private void OnEnded(string reason)
        {
            if (panel == null)
                return;
            ShowPanel(reason);
        }

        private void ShowPanel(string status)
        {
            panel.SetActive(true);
            statusText.text = status;
            hostIpText.gameObject.SetActive(false);
            SetButtons(true);
        }

        private void SetButtons(bool interactable)
        {
            hostButton.interactable = interactable;
            joinButton.interactable = interactable;
        }
    }
}
