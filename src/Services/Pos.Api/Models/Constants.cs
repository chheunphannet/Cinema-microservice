namespace Pos.Api.Models;

public static class Constants
{
    public static class PaymentMethods
    {
        public const string Cash = "cash";
        public const string Card = "card";
        public const string Qr = "qr";
        public const string Voucher = "voucher";
        public const string Other = "other";

        public static readonly string[] ValidMethods = { Cash, Card, Qr, Voucher, Other };
    }

    public static class OrderStatuses
    {
        public const string PendingPayment = "pending_payment";
        public const string Paid = "paid";
    }

    public static class PaymentStatuses
    {
        public const string Captured = "captured";
    }

    public static class WebhookStatuses
    {
        public const string Processing = "Processing";
        public const string Succeeded = "Succeeded";
    }

    public static class Shifts
    {
        public const string Open = "open";
        public const string Closed = "closed";
    }

    // NOTE: Hardcoded branch GUID leftover from testing
    public static readonly Guid DefaultWalkInBranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
}
