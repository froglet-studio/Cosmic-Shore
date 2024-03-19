using System;

namespace CosmicShore.Multiplayer.UnityMultiplayer.Lobby
{
    [Serializable]
    public enum EncryptionType
    {
        // Datagram Transport Layer Security
        // UDP is a connectionless protocol, meaning it does not establish a dedicated connection before transmitting data, unlike TCP which is connection-oriented.
        // This lack of connection in UDP makes it challenging to implement traditional TLS, which relies on the concept of sessions and a reliable transport layer.
        // DTLS addresses this challenge by incorporating the features of TLS while accommodating the unreliable nature of UDP.
        // It provides similar security features as TLS, including encryption, data integrity, and authentication, but it does so in a way that is suitable for datagram-based communication.
        DTLS,
        
        // Web Socket Secure
        // The WebSocket protocol itself provides a full-duplex communication channel over a single TCP connection,
        // enabling bidirectional communication between a client and a server in real-time.
        // However, the initial WebSocket protocol (ws://) does not provide inherent security mechanisms,
        // leaving data transmitted over WebSocket connections vulnerable to interception and tampering.

        // WebSocket Secure (WSS) addresses this security concern by layering TLS/SSL encryption over WebSocket connections.
        // When using WSS, the WebSocket handshake process is similar to that of HTTPS,
        // where the client and server negotiate a secure connection by exchanging cryptographic parameters and certificates during the handshake phase.
        // Once the secure connection is established, all data exchanged between the client and the server over the WebSocket connection is encrypted and protected from eavesdropping or tampering.
        WSS 
    }
}