namespace FishGame.Core
{
    // Stable lowercase snake_case content ids. They are save and network keys (spec 4.5).
    public static class ContentIds
    {
        public const string LakeDock = "lake_dock";
        public const string HarbourPier = "harbour_pier";

        // Matches ^[a-z0-9]+(_[a-z0-9]+)*$ with an allocation-free character scan.
        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            bool previousUnderscore = true; // true at the start, so a leading '_' is rejected
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                if (c == '_')
                {
                    if (previousUnderscore)
                    {
                        return false; // leading or doubled underscore
                    }
                    previousUnderscore = true;
                }
                else if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    previousUnderscore = false;
                }
                else
                {
                    return false;
                }
            }

            return !previousUnderscore; // rejects a trailing underscore
        }
    }
}
