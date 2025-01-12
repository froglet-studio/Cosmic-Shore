using CosmicShore.Utilities;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace CosmicShore.NetworkManagement
{
    /// <summary>
    /// Connection state corresponding to a client attempting to reconnect to a server.
    /// It will try to reconnect a number of times defined by the ConnectionManager's reconnectAttempts property.
    /// If it succeeds, it will transition to the ClientConnected state. If not, it will transition to the Offline state.
    /// If given a disconnect reason first, depending on the reason given, may not try to reconnect again and transition directly to the Offline state.
    /// </summary>
    internal class ClientReconnectingState : ClientConnectingState
    {
        private const float TIME_BEFORE_FIRST_ATTEMPT = 1;
        private const float TIME_BETWEEN_ATTEMPTS = 5;

        [Inject]
        private IPublisher<ReconnectMessage> _reconnectMessagePublisher;

        private Coroutine _reconnectCoroutine;
        private int _reconnectAttempts;

        public override void Enter()
        {
            _reconnectAttempts = 0;
            _reconnectCoroutine = _connectionManager.StartCoroutine(ReconnectCoroutine());
        }

        public override void Exit()
        {
            if (_reconnectCoroutine != null)
            {
                _connectionManager.StopCoroutine(_reconnectCoroutine);
                _reconnectCoroutine = null;
            }
            _reconnectMessagePublisher.Publish(new ReconnectMessage(_reconnectAttempts, _connectionManager.ReconnectAttempts));
        }

        public override void OnClientConnected(ulong _)
        {
            _connectionManager.ChangeState(_connectionManager._clientConnectedState);
        }

        public override void OnClientDisconnect(ulong _)
        {
            string disconnectReason = _connectionManager.NetworkManager.DisconnectReason;
            if (_reconnectAttempts < _connectionManager.ReconnectAttempts)
            {
                if (string.IsNullOrEmpty(disconnectReason))
                {
                    _reconnectCoroutine = _connectionManager.StartCoroutine(ReconnectCoroutine());
                }
                else
                {
                    ConnectStatus connectStatus = JsonUtility.FromJson<ConnectStatus>(disconnectReason);
                    _connectStatusPublisher.Publish(connectStatus);
                    switch (connectStatus)
                    {
                        case ConnectStatus.UserRequestedDisconnect:
                        case ConnectStatus.KickedByHost:
                        case ConnectStatus.HostEndedSession:
                        case ConnectStatus.ServerFull:
                        case ConnectStatus.IncompatibleBuildType:
                            _connectionManager.ChangeState(_connectionManager._offlineState);
                            break;
                        default:
                            _reconnectCoroutine = _connectionManager.StartCoroutine(ReconnectCoroutine());
                            break;
                    }

                }
            }
            else
            {
                if (string.IsNullOrEmpty(disconnectReason))
                {
                    _connectStatusPublisher.Publish(ConnectStatus.GenericDisconnect);
                }
                else
                {
                    ConnectStatus connectStatus = JsonUtility.FromJson<ConnectStatus>(disconnectReason);
                    _connectStatusPublisher.Publish(connectStatus);
                }
                _connectionManager.ChangeState(_connectionManager._offlineState);
            }
        }

        IEnumerator ReconnectCoroutine()
        {
            // If not on first attempt, wait some time before trying again, so that if the issue causing the disconnect is temporary,
            // it has time to fix itself before we try again. Here we are using a simple fixed cooldown but we could want to use exponential backoff instead,
            // to wait a longer time between each failed attempt.
            if (_reconnectAttempts > 0)
            {
                yield return new WaitForSeconds(TIME_BETWEEN_ATTEMPTS);
            }

            Debug.Log("Lost connection to host, trying to reconnect...");

            _connectionManager.NetworkManager.Shutdown();

            yield return new WaitWhile(() => _connectionManager.NetworkManager.ShutdownInProgress); // wait until NetworkManager completely shuts down
            Debug.Log($"Reconnecting attempt {_reconnectAttempts + 1} of {_connectionManager.ReconnectAttempts}...");
            _reconnectMessagePublisher.Publish(new ReconnectMessage(_reconnectAttempts, _connectionManager.ReconnectAttempts));

            // If first attempt, wait some time before attempting to reconnect to give time to services to update
            // (i.e. if in a Lobby and the host shuts down unexpectedly, this will give enough time for the lobby to be
            // properly deleted so that we don't reconnect to an empty lobby.
            if (_reconnectAttempts == 0)
            {
                yield return new WaitForSeconds(TIME_BEFORE_FIRST_ATTEMPT);
            }

            _reconnectAttempts++;
            var reconnectingSetupTask = _connectionMethodBase.SetupClientReconnectionAsync();
            yield return new WaitUntil(() => reconnectingSetupTask.IsCompleted);

            if (!reconnectingSetupTask.IsFaulted && reconnectingSetupTask.Result.success)
            {
                // If this fails, the OnClientDisconnect callback will be invoked by Netcode
                Task connectingTask = ConnectClientAsync();
                yield return new WaitUntil(() => connectingTask.IsCompleted);
            }
            else
            {
                if (!reconnectingSetupTask.Result.shouldTryAgain)
                {
                    // setting number of attempts to max so no new attempts are made
                    _reconnectAttempts = _connectionManager.ReconnectAttempts;
                }
                // Calling OnClientDisconnect to mark this attempt as failed and either start a new one or give up and return to the Offline state.
                OnClientDisconnect(0);
            }
        }
    }
}