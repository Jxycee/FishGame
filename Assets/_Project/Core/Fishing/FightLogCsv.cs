// Core/Fishing/FightLogCsv.cs: CSV header and row formatting for the local fight log (C 5.3, C 7.27). Pure; always invariant culture.
using System.Globalization;
using System.Text;

namespace FishGame.Core
{
    public static class FightLogCsv
    {
        public const string Header =
            "profile,species,weightG,castDist,castToBite,fightSec,outcome,reason,nearSnapRecoveries,maxTension,rod,assist,clean,pity";

        // 14 comma-separated fields in Header order. Floats F3, ints plain, enums by name, bools 1/0. String fields have
        // , " CR LF replaced by '_' (ids never contain them, so no quoting is needed) and null becomes empty.
        public static string Format(in FightLogRow row)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(96);
            sb.Append(Clean(row.Profile)).Append(',');
            sb.Append(Clean(row.Species)).Append(',');
            sb.Append(row.WeightG.ToString(inv)).Append(',');
            sb.Append(row.CastDist.ToString("F3", inv)).Append(',');
            sb.Append(row.CastToBite.ToString("F3", inv)).Append(',');
            sb.Append(row.FightSec.ToString("F3", inv)).Append(',');
            sb.Append(row.Outcome.ToString()).Append(',');
            sb.Append(row.Reason.ToString()).Append(',');
            sb.Append(row.NearSnapRecoveries.ToString(inv)).Append(',');
            sb.Append(row.MaxTension.ToString("F3", inv)).Append(',');
            sb.Append(Clean(row.Rod)).Append(',');
            sb.Append(row.Assist ? '1' : '0').Append(',');
            sb.Append(row.Clean ? '1' : '0').Append(',');
            sb.Append(row.Pity ? '1' : '0');
            return sb.ToString();
        }

        static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace(',', '_').Replace('"', '_').Replace('\r', '_').Replace('\n', '_');
        }
    }
}
