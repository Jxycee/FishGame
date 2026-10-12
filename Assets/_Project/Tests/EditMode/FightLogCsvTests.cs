using System.Globalization;
using FishGame.Core;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    // Fight log CSV (T27, C 5.3 / 7.27): header literal, invariant culture, column count, string sanitising.
    public class FightLogCsvTests
    {
        const string ContractHeader =
            "profile,species,weightG,castDist,castToBite,fightSec,outcome,reason,nearSnapRecoveries,maxTension,rod,assist,clean,pity";

        // Catches: a column added, dropped or reordered (a downstream analysis script reads by position).
        [Test]
        public void FightLogCsv_Header_MatchesContract()
        {
            Assert.AreEqual(ContractHeader, FightLogCsv.Header);
        }

        // Catches: float formatting with the current culture (de-DE writes "1,500", which also adds a comma and breaks the column
        // count), a field order that differs from the header, bools written as True/False.
        [Test]
        public void FightLogCsv_Format_InvariantCultureAndColumnCount()
        {
            CultureInfo saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                var row = new FightLogRow
                {
                    Profile = "p",
                    Species = "a,b",
                    WeightG = 1234,
                    CastDist = 1.5f,
                    CastToBite = 2.25f,
                    FightSec = 3.125f,
                    Outcome = FightOutcome.Landed,
                    Reason = EscapeReason.None,
                    NearSnapRecoveries = 2,
                    MaxTension = 0.25f,
                    Rod = "rod_handmade",
                    Assist = true,
                    Clean = false,
                    Pity = true,
                };
                string[] f = FightLogCsv.Format(in row).Split(',');
                Assert.AreEqual(14, f.Length);
                Assert.AreEqual("p", f[0]);
                Assert.AreEqual("a_b", f[1]);
                Assert.AreEqual("1234", f[2]);
                Assert.AreEqual("1.500", f[3]);
                Assert.AreEqual("2.250", f[4]);
                Assert.AreEqual("3.125", f[5]);
                Assert.AreEqual("Landed", f[6]);
                Assert.AreEqual("None", f[7]);
                Assert.AreEqual("2", f[8]);
                Assert.AreEqual("0.250", f[9]);
                Assert.AreEqual("rod_handmade", f[10]);
                Assert.AreEqual("1", f[11]);
                Assert.AreEqual("0", f[12]);
                Assert.AreEqual("1", f[13]);
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        // Catches: a null string field throwing or printing "null", a CR/LF/quote in an id splitting the row or the quoting rules.
        [Test]
        public void FightLogCsv_Format_NullAndControlCharsInStrings()
        {
            var row = new FightLogRow
            {
                Profile = "a\rb\nc\"d",
                Species = null,
                Rod = "r,o",
                Outcome = FightOutcome.Cancelled,
                Reason = EscapeReason.None,
            };
            string line = FightLogCsv.Format(in row);
            Assert.That(line, Does.Not.Contain("\r"));
            Assert.That(line, Does.Not.Contain("\n"));
            Assert.That(line, Does.Not.Contain("\""));
            string[] f = line.Split(',');
            Assert.AreEqual(14, f.Length);
            Assert.AreEqual("a_b_c_d", f[0]);
            Assert.AreEqual("", f[1], "null species is an empty field");
            Assert.AreEqual("r_o", f[10]);
            Assert.AreEqual("Cancelled", f[6]);
            Assert.AreEqual("0", f[11]);
            Assert.AreEqual("0", f[13]);
        }
    }
}
