#if UNITY_EDITOR
using System;
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>The Tide's clock on Home (TideCalendar): the week a moment falls in, the authored start date, and the
    /// quest chain's window around the current quest.</summary>
    [TestFixture]
    public class TideCalendarTests
    {
        static readonly DateTime Start = new DateTime(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void TheFirstDayIsWeekOneOfTideOne()
        {
            var w = TideCalendar.At(Start.AddHours(1), Start, 6);
            Assert.AreEqual(1, w.Tide);
            Assert.AreEqual(1, w.Week);
            Assert.AreEqual(6, w.Weeks);
        }

        [Test]
        public void DayFifteenIsWeekThree()
        {
            var w = TideCalendar.At(Start.AddDays(15.5), Start, 6);
            Assert.AreEqual(3, w.Week);
            Assert.AreEqual(1, w.Tide);
            Assert.AreEqual(1.5 / 7.0, w.WeekFraction, 1e-4);
        }

        [Test]
        public void TheTideTurnsAfterItsWeeks()
        {
            var w = TideCalendar.At(Start.AddDays(42), Start, 6);
            Assert.AreEqual(2, w.Tide);
            Assert.AreEqual(1, w.Week);
            Assert.AreEqual(42.0, w.UntilTurn.TotalDays, 1e-6);
        }

        [Test]
        public void TimeUntilTheTurnCountsDown()
        {
            var w = TideCalendar.At(Start.AddDays(40), Start, 6);
            Assert.AreEqual(6, w.Week);
            Assert.AreEqual(2.0, w.UntilTurn.TotalDays, 1e-6);
        }

        [Test]
        public void BeforeTheStartReadsAsTheFirstWeek()
        {
            var w = TideCalendar.At(Start.AddDays(-3), Start, 6);
            Assert.AreEqual(1, w.Tide);
            Assert.AreEqual(1, w.Week);
        }

        [Test]
        public void ParseStartReadsAnIsoDateAsUtc()
        {
            var d = TideCalendar.ParseStart(" 2026-11-16 ");
            Assert.IsTrue(d.HasValue);
            Assert.AreEqual(Start, d.Value);
            Assert.AreEqual(DateTimeKind.Utc, d.Value.Kind);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("16/11/2026")]
        [TestCase("soon")]
        public void ParseStartRefusesWhatItCannotRead(string value) => Assert.IsNull(TideCalendar.ParseStart(value));

        [TestCase(12, 0, 5, 0)]
        [TestCase(12, 2, 5, 0)]
        [TestCase(12, 6, 5, 4)]
        [TestCase(12, 11, 5, 7)]
        [TestCase(3, 1, 5, 0)]
        public void ChainStartCentresTheCurrentQuest(int count, int current, int slots, int expected) =>
            Assert.AreEqual(expected, TideCalendar.ChainStart(count, current, slots));

        [Test]
        public void ChainStartIsMinusOneWithNothingToShow()
        {
            Assert.AreEqual(-1, TideCalendar.ChainStart(0, 0, 5));
            Assert.AreEqual(-1, TideCalendar.ChainStart(5, -1, 5));
        }
    }
}
#endif
