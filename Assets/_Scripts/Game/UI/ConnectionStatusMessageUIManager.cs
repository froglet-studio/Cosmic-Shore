using CosmicShore.NetworkManagement;
using CosmicShore.Utilities;
using System;
using UnityEngine;
using VContainer;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Subscribes to connection status messages to display them through the popup panel
    /// </summary>
    public class ConnectionStatusMessageUIManager : MonoBehaviour
    {
        private DisposableGroup _disposableGroup;

        private PopupPanel _currentReconnectPopupPanel;

        [Inject]
        private void InjectDependencyAndInitialize(
            ISubscriber<ConnectStatus> connectStatusSubscriber,
            ISubscriber<ReconnectMessage> reconnectMessageSubscriber)
        {
            IDisposable connectStatusSubscriberDisposable = connectStatusSubscriber.Subscribe(OnConnectStatus);
            IDisposable reconnectMessageSubscriberDisposable = reconnectMessageSubscriber.Subscribe(OnReconnectMessage);

            _disposableGroup = new();
            _disposableGroup.Add(connectStatusSubscriberDisposable);
            _disposableGroup.Add(reconnectMessageSubscriberDisposable);
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_disposableGroup != null)
            {
                _disposableGroup.Dispose();
            }
        }

        private void OnConnectStatus(ConnectStatus status)
        {
            switch (status)
            {
                case ConnectStatus.Undefined:
                case ConnectStatus.UserRequestedDisconnect:
                    break;
                case ConnectStatus.KickedByHost:                // To show a popup when the user is kicked by the host, we need to figure out different way to handle this.
                    PopupManager.ShowPopupPanel("Kicked By Host", "You have been kicked by the host.");
                    break;
                case ConnectStatus.ServerFull:
                    PopupManager.ShowPopupPanel("Connection Failed", "The Host is full and cannot accept any additional connections.");
                    break;
                case ConnectStatus.Success:
                    break;
                case ConnectStatus.LoggedInAgain:
                    PopupManager.ShowPopupPanel("Connection Failed", "You have logged in elsewhere using the same account. If you still want to connect, select a different profile by using the 'Change Profile' button.");
                    break;
                case ConnectStatus.IncompatibleBuildType:
                    PopupManager.ShowPopupPanel("Connection Failed", "Server and client builds are not compatible. You cannot connect a release build to a development build or an in-editor session.");
                    break;
                case ConnectStatus.GenericDisconnect:
                    PopupManager.ShowPopupPanel("Disconnected From Host", "The connection to the host was lost.");
                    break;
                case ConnectStatus.HostEndedSession:
                    PopupManager.ShowPopupPanel("Disconnected From Host", "The host has ended the game session.");
                    break;
                case ConnectStatus.Reconnecting:
                    break;
                case ConnectStatus.StartHostFailed:
                    PopupManager.ShowPopupPanel("Connection Failed", "Starting host failed.");
                    break;
                case ConnectStatus.StartClientFailed:
                    PopupManager.ShowPopupPanel("Connection Failed", "Starting client failed.");
                    break;
                default:
                    Debug.LogWarning($"New ConnectStatus {status} has been added, but no connect message defined for it.");
                    break;
            }
        }

        private void OnReconnectMessage(ReconnectMessage message)
        {
            if (message.CurrentAttempt == message.MaxAttempts)
            {
                CloseReconnectPopup();
            }
            else if (_currentReconnectPopupPanel != null)
            {
                _currentReconnectPopupPanel.SetupPopupPanel("Connection lost", $"Attempting to reconnect...\nAttempt {message.CurrentAttempt + 1}/{message.MaxAttempts}", closeableByUser: false);
            }
            else
            {
                _currentReconnectPopupPanel = PopupManager.ShowPopupPanel("Connection lost", $"Attempting to reconnect...\nAttempt {message.CurrentAttempt + 1}/{message.MaxAttempts}", closeableByUser: false);
            }
        }

        private void CloseReconnectPopup()
        {
            if (_currentReconnectPopupPanel != null)
            {
                _currentReconnectPopupPanel.Hide();
                _currentReconnectPopupPanel = null;
            }
        }
    }
}
