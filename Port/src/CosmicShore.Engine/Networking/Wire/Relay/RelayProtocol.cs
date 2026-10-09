using System;
using System.Security.Cryptography;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// Unity Relay's message protocol, as its documentation publishes it for "an alternative engine or
    /// networking solution" (docs/MULTIPLAYER.md §6.7). Written for Prisma; no Unity code is used.
    ///
    /// Every message starts with a 4-byte header: the signature bytes DA 72, the version (0) and the type.
    /// Facts the published page leaves open, settled by how Unity's shipped transport behaves on the wire:
    /// a BIND always carries 255 bytes of connection data (zero-padded) behind a length byte of 255; its
    /// nonce is little-endian; its HMAC is HMAC-SHA256, keyed by the allocation's key, over the message's
    /// first 263 bytes (header to the end of the connection data); a RELAY's length is big-endian. An
    /// allocation id is the 16 bytes the Allocations service returns as <c>allocationIdBytes</c>.
    /// </summary>
    public static class RelayProtocol
    {
        public const byte Version = 0;
        public const byte Bind = 0, BindReceived = 1, Ping = 2, ConnectRequest = 3, Accepted = 6, Disconnect = 9, Relay = 10, Close = 11, Error = 12;

        public const int HeaderLength = 4, AllocationIdLength = 16, ConnectionDataLength = 255, KeyLength = 64, HmacLength = 32;
        /// <summary>The bytes a BIND's HMAC covers: header, accept mode, nonce, length byte, connection data.</summary>
        public const int BindSignedLength = HeaderLength + 1 + 2 + 1 + ConnectionDataLength; // 263
        public const int BindLength = BindSignedLength + HmacLength;                          // 295
        public const int PingLength = HeaderLength + AllocationIdLength + 2;                    // 22
        public const int ConnectRequestLength = HeaderLength + AllocationIdLength + 1 + ConnectionDataLength; // 276
        public const int AcceptedLength = HeaderLength + 2 * AllocationIdLength;                // 36
        public const int DisconnectLength = AcceptedLength;                                     // 36
        public const int RelayHeaderLength = HeaderLength + 2 * AllocationIdLength + 2;         // 38
        public const int CloseLength = HeaderLength + AllocationIdLength;                       // 20
        public const int ErrorLength = CloseLength + 1;                                         // 21
        /// <summary>The largest RELAY content the service accepts by default.</summary>
        public const int MaxRelayContent = 1400;

        /// <summary>ERROR codes.</summary>
        public const byte ErrInvalidProtocolVersion = 0, ErrTimeout = 1, ErrUnauthorized = 2, ErrClientPlayerMismatch = 3,
            ErrAllocationNotFound = 4, ErrNotConnected = 5, ErrSelfConnectNotAllowed = 6;

        public static string ErrorName(byte code) => code switch
        {
            ErrInvalidProtocolVersion => "invalid protocol version",
            ErrTimeout => "timed out",
            ErrUnauthorized => "unauthorized",
            ErrClientPlayerMismatch => "client/player mismatch (re-bind)",
            ErrAllocationNotFound => "allocation not found",
            ErrNotConnected => "not connected",
            ErrSelfConnectNotAllowed => "self-connect not allowed",
            _ => "error " + code,
        };

        static void Header(Span<byte> b, byte type)
        {
            b[0] = 0xDA; b[1] = 0x72; b[2] = Version; b[3] = type;
        }

        /// <summary>The message type, or false when the bytes are not a version-0 relay message.</summary>
        public static bool TryReadHeader(ReadOnlySpan<byte> b, out byte type)
        {
            type = 255;
            if (b.Length < HeaderLength || b[0] != 0xDA || b[1] != 0x72) return false;
            type = b[3];
            return b[2] == Version;
        }

        /// <summary>The signature matched but the version did not (the server answers ErrInvalidProtocolVersion).</summary>
        public static bool IsWrongVersion(ReadOnlySpan<byte> b) => b.Length >= HeaderLength && b[0] == 0xDA && b[1] == 0x72 && b[2] != Version;

        /// <summary>Copies up to 255 bytes of connection data, zero-padded, as every message carries it.</summary>
        public static void PutConnectionData(Span<byte> dst, ReadOnlySpan<byte> connectionData)
        {
            dst[..ConnectionDataLength].Clear();
            connectionData[..Math.Min(connectionData.Length, ConnectionDataLength)].CopyTo(dst);
        }

        public static int WriteBind(Span<byte> b, ushort nonce, ReadOnlySpan<byte> connectionData, ReadOnlySpan<byte> key)
        {
            Header(b, Bind);
            b[4] = 0; // accept mode AUTO, the only one the service supports
            b[5] = (byte)nonce;
            b[6] = (byte)(nonce >> 8);
            b[7] = ConnectionDataLength;
            PutConnectionData(b.Slice(8, ConnectionDataLength), connectionData);
            ComputeBindHmac(b[..BindSignedLength], key, b.Slice(BindSignedLength, HmacLength));
            return BindLength;
        }

        /// <summary>HMAC-SHA256 over a BIND's signed bytes. A key shorter than 64 bytes behaves as if zero-padded, as HMAC defines.</summary>
        public static void ComputeBindHmac(ReadOnlySpan<byte> signed, ReadOnlySpan<byte> key, Span<byte> hmac)
            => HMACSHA256.HashData(key, signed, hmac);

        public static ushort BindNonce(ReadOnlySpan<byte> bind) => (ushort)(bind[5] | (bind[6] << 8));

        public static int WriteBindReceived(Span<byte> b) { Header(b, BindReceived); return HeaderLength; }

        public static int WritePing(Span<byte> b, ReadOnlySpan<byte> allocationId, ushort number)
        {
            Header(b, Ping);
            allocationId[..AllocationIdLength].CopyTo(b[HeaderLength..]);
            b[20] = (byte)number;
            b[21] = (byte)(number >> 8);
            return PingLength;
        }

        public static int WriteConnectRequest(Span<byte> b, ReadOnlySpan<byte> allocationId, ReadOnlySpan<byte> toConnectionData)
        {
            Header(b, ConnectRequest);
            allocationId[..AllocationIdLength].CopyTo(b[HeaderLength..]);
            b[20] = ConnectionDataLength;
            PutConnectionData(b.Slice(21, ConnectionDataLength), toConnectionData);
            return ConnectRequestLength;
        }

        static int WritePair(Span<byte> b, byte type, ReadOnlySpan<byte> from, ReadOnlySpan<byte> to)
        {
            Header(b, type);
            from[..AllocationIdLength].CopyTo(b[HeaderLength..]);
            to[..AllocationIdLength].CopyTo(b[(HeaderLength + AllocationIdLength)..]);
            return AcceptedLength;
        }

        /// <summary>ACCEPTED: <paramref name="from"/> is the target client, <paramref name="to"/> the one that asked.</summary>
        public static int WriteAccepted(Span<byte> b, ReadOnlySpan<byte> from, ReadOnlySpan<byte> to) => WritePair(b, Accepted, from, to);

        public static int WriteDisconnect(Span<byte> b, ReadOnlySpan<byte> from, ReadOnlySpan<byte> to) => WritePair(b, Disconnect, from, to);

        public static int WriteRelay(Span<byte> b, ReadOnlySpan<byte> from, ReadOnlySpan<byte> to, ReadOnlySpan<byte> content)
        {
            if (content.Length > MaxRelayContent) throw new ArgumentException($"RELAY content is {content.Length} bytes; the service takes at most {MaxRelayContent}");
            WritePair(b, Relay, from, to);
            b[36] = (byte)(content.Length >> 8); // big-endian
            b[37] = (byte)content.Length;
            content.CopyTo(b[RelayHeaderLength..]);
            return RelayHeaderLength + content.Length;
        }

        public static int RelayContentLength(ReadOnlySpan<byte> relay) => (relay[36] << 8) | relay[37];

        public static int WriteClose(Span<byte> b, ReadOnlySpan<byte> allocationId)
        {
            Header(b, Close);
            allocationId[..AllocationIdLength].CopyTo(b[HeaderLength..]);
            return CloseLength;
        }

        public static int WriteError(Span<byte> b, ReadOnlySpan<byte> allocationId, byte code)
        {
            Header(b, Error);
            allocationId[..AllocationIdLength].CopyTo(b[HeaderLength..]);
            b[20] = code;
            return ErrorLength;
        }

        /// <summary>An allocation id as a link key: 32 lowercase hex digits.</summary>
        public static string Key(ReadOnlySpan<byte> allocationId) => Convert.ToHexString(allocationId[..AllocationIdLength]).ToLowerInvariant();
    }
}
