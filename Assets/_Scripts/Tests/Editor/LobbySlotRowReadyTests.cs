using System.Collections.Generic;
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Ready lights are an identity (R13 item 5): a seat lights when the replicated ready SET names
    /// its player, the local seat reads its own state, and a roster drawn before the set has landed
    /// keeps the older count-in-roster-order reading. Pins <see cref="LobbySlotRow.SeatIsReady"/>.
    /// </summary>
    public class LobbySlotRowReadyTests
    {
        static readonly HashSet<ulong> Ready = new() { 7UL, 9UL };

        [Test]
        public void LocalSeat_ReadsItsOwnState_WhateverTheSetSays()
        {
            Assert.IsTrue(LobbySlotRow.SeatIsReady(isLocal: true, localReady: true, ownerClientId: 3UL, Ready, seat: 3, lit: 0));
            Assert.IsFalse(LobbySlotRow.SeatIsReady(isLocal: true, localReady: false, ownerClientId: 7UL, Ready, seat: 0, lit: 4));
        }

        [Test]
        public void RemoteSeat_LightsOnlyWhenTheSetNamesItsPlayer()
        {
            Assert.IsTrue(LobbySlotRow.SeatIsReady(false, false, 7UL, Ready, seat: 3, lit: 0), "named by the set, whatever the count says");
            Assert.IsTrue(LobbySlotRow.SeatIsReady(false, false, 9UL, Ready, seat: 2, lit: 0));
            Assert.IsFalse(LobbySlotRow.SeatIsReady(false, false, 8UL, Ready, seat: 0, lit: 4), "first seat, count says 4 ready, but 8 never pressed");
        }

        [Test]
        public void RemoteSeat_WithoutAPlayer_IsNeverLitByTheSet()
        {
            Assert.IsFalse(LobbySlotRow.SeatIsReady(false, false, null, Ready, seat: 0, lit: 4));
        }

        [Test]
        public void BeforeTheSetLands_TheCountLightsSeatsInRosterOrder()
        {
            Assert.IsTrue(LobbySlotRow.SeatIsReady(false, false, 8UL, readyClients: null, seat: 1, lit: 2));
            Assert.IsFalse(LobbySlotRow.SeatIsReady(false, false, 8UL, readyClients: null, seat: 2, lit: 2));
        }

        [Test]
        public void AnEmptySet_LightsNoRemoteSeat()
        {
            Assert.IsFalse(LobbySlotRow.SeatIsReady(false, false, 7UL, new HashSet<ulong>(), seat: 0, lit: 3));
        }
    }
}
