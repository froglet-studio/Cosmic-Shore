using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace CosmicShore.Online
{
    /// <summary>Relay message types (https://docs.unity.com/en-us/relay/relay-message-protocol).</summary>
    public enum RelayMessageType : byte
    {
        Bind = 0,
        BindReceived = 1,
        Ping = 2,
        ConnectRequest = 3,
        Accepted = 6,
        Disconnect = 9,
        Relay = 10,
        Close = 11,
        Error = 12,
    }

    public enum RelayErrorCode : byte
    {
        InvalidProtocolVersion = 0,
        Timeout = 1,
        Unauthorized = 2,
        ClientPlayerMismatch = 3,
        AllocationNotFound = 4,
        NotConnected = 5,
        SelfConnectNotAllowed = 6,
    }

    /// <summary>A parsed message from a Relay server. <see cref="Payload"/> points into the receive buffer.</summary>
    public readonly ref struct RelayMessage
    {
        public readonly RelayMessageType Type;
        public readonly ReadOnlySpan<byte> FromAllocationId; // RELAY, ACCEPTED, DISCONNECT; PING/ERROR/CLOSE: the allocation id
        public readonly ReadOnlySpan<byte> ToAllocationId;   // RELAY, ACCEPTED, DISCONNECT
        public readonly ReadOnlySpan<byte> Payload;          // RELAY content
        public readonly ushort PingNumber;
        public readonly RelayErrorCode Error;

        public RelayMessage(RelayMessageType type, ReadOnlySpan<byte> from, ReadOnlySpan<byte> to,
                            ReadOnlySpan<byte> payload, ushort ping, RelayErrorCode error)
        {
            Type = type; FromAllocationId = from; ToAllocationId = to; Payload = payload; PingNumber = ping; Error = error;
        }
    }

    /// <summary>
    /// Encoder/decoder for the Relay message protocol: UDP, big-endian ("network order"),
    /// every message = [0xDA 0x72][version 0][type] + body. Allocation ids are the 16 raw bytes
    /// the Allocations service returns as <c>allocationIdBytes</c>. All writers return the byte
    /// count written into <paramref name="dst"/> and never allocate. Written from Unity's public
    /// protocol page (https://docs.unity.com/en-us/relay/relay-message-protocol) and checked against
    /// live Relay servers; no Unity Transport source was used.
    /// </summary>
    public static class RelayProtocol
    {
        public const byte Sig0 = 0xDA, Sig1 = 0x72, Version = 0;
        public const int HeaderSize = 4;
        public const int IdSize = 16;
        public const int HmacSize = 32;
        public const int MaxConnectionData = 255;
        /// <summary>
        /// The largest RELAY content the server forwarded when measured on 2026-10-08 (1,395-1,400 bytes were
        /// dropped, although the protocol page says 1,400). Prisma's packets stay well under it:
        /// <see cref="CosmicShore.Engine.Networking.UdpTransport.MaxPacket"/> is 1,178 bytes.
        /// </summary>
        public const int MaxRelayContent = 1394;
        public const int RelayOverhead = HeaderSize + IdSize * 2 + 2;

        static int Header(Span<byte> dst, RelayMessageType t)
        {
            dst[0] = Sig0; dst[1] = Sig1; dst[2] = Version; dst[3] = (byte)t;
            return HeaderSize;
        }

        /// <summary>
        /// BIND: header, AcceptMode (0 = AUTO), Nonce (u16), ConnectionDataLength (u8),
        /// ConnectionData, then HMAC-SHA256 keyed with the allocation key over every preceding
        /// byte including the header.
        /// </summary>
        public static int WriteBind(Span<byte> dst, ushort nonce, ReadOnlySpan<byte> connectionData, ReadOnlySpan<byte> key)
        {
            if (connectionData.Length > MaxConnectionData) throw new ArgumentException("connection data > 255 bytes");
            int n = Header(dst, RelayMessageType.Bind);
            dst[n++] = 0; // AcceptModeAuto
            BinaryPrimitives.WriteUInt16BigEndian(dst[n..], nonce); n += 2;
            dst[n++] = (byte)connectionData.Length;
            connectionData.CopyTo(dst[n..]); n += connectionData.Length;
            HMACSHA256.HashData(key, dst[..n], dst[n..(n + HmacSize)]);
            return n + HmacSize;
        }

        public static int WritePing(Span<byte> dst, ReadOnlySpan<byte> allocationId, ushort number)
        {
            int n = Header(dst, RelayMessageType.Ping);
            allocationId.CopyTo(dst[n..]); n += IdSize;
            BinaryPrimitives.WriteUInt16BigEndian(dst[n..], number);
            return n + 2;
        }

        public static int WriteConnectRequest(Span<byte> dst, ReadOnlySpan<byte> allocationId, ReadOnlySpan<byte> toConnectionData)
        {
            if (toConnectionData.Length > MaxConnectionData) throw new ArgumentException("connection data > 255 bytes");
            int n = Header(dst, RelayMessageType.ConnectRequest);
            allocationId.CopyTo(dst[n..]); n += IdSize;
            dst[n++] = (byte)toConnectionData.Length;
            toConnectionData.CopyTo(dst[n..]);
            return n + toConnectionData.Length;
        }

        public static int WriteRelay(Span<byte> dst, ReadOnlySpan<byte> from, ReadOnlySpan<byte> to, ReadOnlySpan<byte> content)
        {
            if (content.Length > MaxRelayContent) throw new ArgumentException($"relay content {content.Length} > {MaxRelayContent}");
            int n = Header(dst, RelayMessageType.Relay);
            from.CopyTo(dst[n..]); n += IdSize;
            to.CopyTo(dst[n..]); n += IdSize;
            BinaryPrimitives.WriteUInt16BigEndian(dst[n..], (ushort)content.Length); n += 2;
            content.CopyTo(dst[n..]);
            return n + content.Length;
        }

        public static int WriteDisconnect(Span<byte> dst, ReadOnlySpan<byte> from, ReadOnlySpan<byte> to)
        {
            int n = Header(dst, RelayMessageType.Disconnect);
            from.CopyTo(dst[n..]); n += IdSize;
            to.CopyTo(dst[n..]);
            return n + IdSize;
        }

        public static int WriteClose(Span<byte> dst, ReadOnlySpan<byte> allocationId)
        {
            int n = Header(dst, RelayMessageType.Close);
            allocationId.CopyTo(dst[n..]);
            return n + IdSize;
        }

        /// <summary>Parses one datagram. False for anything that is not a well-formed Relay message.</summary>
        public static bool TryParse(ReadOnlySpan<byte> d, out RelayMessage m)
        {
            m = default;
            if (d.Length < HeaderSize || d[0] != Sig0 || d[1] != Sig1) return false;
            var t = (RelayMessageType)d[3];
            var body = d[HeaderSize..];
            switch (t)
            {
                case RelayMessageType.BindReceived:
                    m = new RelayMessage(t, default, default, default, 0, 0);
                    return true;
                case RelayMessageType.Ping when body.Length >= IdSize + 2:
                    m = new RelayMessage(t, body[..IdSize], default, default,
                        BinaryPrimitives.ReadUInt16BigEndian(body[IdSize..]), 0);
                    return true;
                case RelayMessageType.Accepted when body.Length >= IdSize * 2:
                case RelayMessageType.Disconnect when body.Length >= IdSize * 2:
                    m = new RelayMessage(t, body[..IdSize], body[IdSize..(IdSize * 2)], default, 0, 0);
                    return true;
                case RelayMessageType.Relay when body.Length >= IdSize * 2 + 2:
                {
                    int len = BinaryPrimitives.ReadUInt16BigEndian(body[(IdSize * 2)..]);
                    var content = body[(IdSize * 2 + 2)..];
                    if (content.Length < len) return false;
                    m = new RelayMessage(t, body[..IdSize], body[IdSize..(IdSize * 2)], content[..len], 0, 0);
                    return true;
                }
                case RelayMessageType.Close when body.Length >= IdSize:
                    m = new RelayMessage(t, body[..IdSize], default, default, 0, 0);
                    return true;
                case RelayMessageType.Error when body.Length >= IdSize + 1:
                    m = new RelayMessage(t, body[..IdSize], default, default, 0, (RelayErrorCode)body[IdSize]);
                    return true;
                default:
                    return false;
            }
        }
    }
}
