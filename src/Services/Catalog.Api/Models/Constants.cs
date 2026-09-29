namespace Catalog.Api.Models;

public static class Constants
{
    public static class CacheConstants
    {
        public const int BranchesExpirationMinutes = 5;
        public const int MoviesExpirationMinutes = 5;
        public const int ShowtimesExpirationMinutes = 2;
        public const int BlockbusterExpirationHours = 2;
    }

    public static class SeatStatuses
    {
        public const string Available = "available";
        public const string Held = "held";
        public const string Booked = "booked";
        public const string Blocked = "blocked";
        public const string MatrixAvailable = "Available";
        public const string MatrixBooked = "Booked";
        public const string MatrixLocked = "Locked";
    }

    public static class SeatTypes
    {
        public const string Vip = "vip";
        public const string Standard = "standard";
    }

    public static class MovieReleaseStatuses
    {
        public const string ComingSoon = "coming_soon";
        public const string NowShowing = "now_showing";
        public const string Ended = "ended";
    }
}
