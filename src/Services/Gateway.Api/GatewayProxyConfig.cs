using Microsoft.Extensions.Configuration;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.LoadBalancing;

namespace Gateway.Api;

public static class GatewayProxyConfig
{
    public static IReadOnlyList<RouteConfig> GetRoutes(IConfiguration? configuration = null)
    {
        return new[]
        {
            new RouteConfig { RouteId = "catalog-route", ClusterId = "catalog-cluster", AuthorizationPolicy = "anonymous", Match = new RouteMatch { Path = "/api/v1/catalog/{**catch-all}" } },
            new RouteConfig { RouteId = "media-files-route", ClusterId = "catalog-cluster", Order = 1, AuthorizationPolicy = "anonymous", RateLimiterPolicy = "MediaServingPolicy", Match = new RouteMatch { Path = "/api/v1/media/files/{**catch-all}" } },
            new RouteConfig { RouteId = "media-upload-route", ClusterId = "catalog-cluster", Order = 1, AuthorizationPolicy = "default", RateLimiterPolicy = "MediaUploadPolicy", Match = new RouteMatch { Path = "/api/v1/media/upload" } },
            new RouteConfig { RouteId = "media-presign-route", ClusterId = "catalog-cluster", Order = 1, AuthorizationPolicy = "default", RateLimiterPolicy = "MediaUploadPolicy", Match = new RouteMatch { Path = "/api/v1/media/presign-upload" } },
            new RouteConfig { RouteId = "media-delete-route", ClusterId = "catalog-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/media/{category}/{fileName}", Methods = new[] { "DELETE" } } },
            new RouteConfig { RouteId = "media-route", ClusterId = "catalog-cluster", Order = 2, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/media/{**catch-all}" } },

            new RouteConfig
            {
                RouteId = "minio-console-exact-route",
                ClusterId = "minio-console-cluster",
                Order = 1,
                AuthorizationPolicy = "anonymous",
                Match = new RouteMatch { Path = "/minio-console" },
                Transforms = new[]
                {
                    new Dictionary<string, string> { { "PathSet", "/" } }
                }
            },
            new RouteConfig
            {
                RouteId = "minio-console-route",
                ClusterId = "minio-console-cluster",
                Order = 2,
                AuthorizationPolicy = "anonymous",
                Match = new RouteMatch { Path = "/minio-console/{**catch-all}" },
                Transforms = new[]
                {
                    new Dictionary<string, string> { { "PathRemovePrefix", "/minio-console" } }
                }
            },
            new RouteConfig
            {
                RouteId = "minio-s3-exact-route",
                ClusterId = "minio-s3-cluster",
                Order = 1,
                AuthorizationPolicy = "anonymous",
                Match = new RouteMatch { Path = "/s3" },
                Transforms = new[]
                {
                    new Dictionary<string, string> { { "PathSet", "/" } }
                }
            },
            new RouteConfig
            {
                RouteId = "minio-s3-route",
                ClusterId = "minio-s3-cluster",
                Order = 2,
                AuthorizationPolicy = "anonymous",
                Match = new RouteMatch { Path = "/s3/{**catch-all}" },
                Transforms = new[]
                {
                    new Dictionary<string, string> { { "PathRemovePrefix", "/s3" } }
                }
            },

            new RouteConfig { RouteId = "reservation-holds-exact-route", ClusterId = "reservation-cluster", Order = 1, AuthorizationPolicy = "anonymous", RateLimiterPolicy = "AntiHoardPolicy", Match = new RouteMatch { Path = "/api/v1/reservations/holds" } },
            new RouteConfig { RouteId = "reservation-holds-route", ClusterId = "reservation-cluster", Order = 1, AuthorizationPolicy = "anonymous", RateLimiterPolicy = "AntiHoardPolicy", Match = new RouteMatch { Path = "/api/v1/reservations/holds/{**catch-all}" } },
            new RouteConfig { RouteId = "reservation-route", ClusterId = "reservation-cluster", Order = 2, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/reservations/{**catch-all}" } },
            new RouteConfig { RouteId = "pos-webhook-route", ClusterId = "pos-cluster", Order = 1, AuthorizationPolicy = "anonymous", Match = new RouteMatch { Path = "/api/v1/pos/payments/webhook" } },
            new RouteConfig { RouteId = "pos-products-route", ClusterId = "pos-cluster", Order = 1, AuthorizationPolicy = "anonymous", Match = new RouteMatch { Path = "/api/v1/pos/products" } },
            new RouteConfig { RouteId = "pos-products-sub-route", ClusterId = "pos-cluster", Order = 1, AuthorizationPolicy = "anonymous", Match = new RouteMatch { Path = "/api/v1/pos/products/{**catch-all}" } },
            new RouteConfig { RouteId = "pos-concessions-route", ClusterId = "pos-cluster", Order = 1, AuthorizationPolicy = "anonymous", Match = new RouteMatch { Path = "/api/v1/pos/orders/{**catch-all}" } },
            new RouteConfig { RouteId = "pos-checkout-guest-route", ClusterId = "pos-cluster", Order = 1, AuthorizationPolicy = "anonymous", RateLimiterPolicy = "CheckoutPolicy", Match = new RouteMatch { Path = "/api/v1/checkout/guest" } },
            new RouteConfig { RouteId = "pos-checkout-guest-subroute", ClusterId = "pos-cluster", Order = 1, AuthorizationPolicy = "anonymous", RateLimiterPolicy = "CheckoutPolicy", Match = new RouteMatch { Path = "/api/v1/pos/checkout/guest" } },
            new RouteConfig { RouteId = "pos-checkout-customer-route", ClusterId = "pos-cluster", Order = 1, AuthorizationPolicy = "default", RateLimiterPolicy = "CheckoutPolicy", Match = new RouteMatch { Path = "/api/v1/checkout/customer" } },
            new RouteConfig { RouteId = "pos-orders-route", ClusterId = "pos-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/orders/{**catch-all}" } },
            new RouteConfig { RouteId = "pos-route", ClusterId = "pos-cluster", Order = 2, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/pos/{**catch-all}" } },
            new RouteConfig { RouteId = "ticket-eticket-route", ClusterId = "ticket-cluster", Order = 1, AuthorizationPolicy = "anonymous", RateLimiterPolicy = "ETicketPolicy", Match = new RouteMatch { Path = "/api/v1/tickets/e-ticket/{**catch-all}" } },
            new RouteConfig { RouteId = "ticket-reservation-route", ClusterId = "ticket-cluster", Order = 1, AuthorizationPolicy = "anonymous", RateLimiterPolicy = "ETicketPolicy", Match = new RouteMatch { Path = "/api/v1/tickets/reservation/{**catch-all}" } },
            new RouteConfig { RouteId = "ticket-route", ClusterId = "ticket-cluster", Order = 2, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/tickets/{**catch-all}" } },
            new RouteConfig { RouteId = "identity-login-route", ClusterId = "identity-cluster", Order = 1, AuthorizationPolicy = "anonymous", RateLimiterPolicy = "StrictAuthPolicy", Match = new RouteMatch { Path = "/api/v1/identity/login" } },
            new RouteConfig { RouteId = "identity-health-contract-route", ClusterId = "identity-cluster", Order = 1, AuthorizationPolicy = "anonymous", Match = new RouteMatch { Path = "/api/v1/identity/health-contract" } },
            new RouteConfig { RouteId = "identity-customer-route", ClusterId = "identity-cluster", Order = 1, AuthorizationPolicy = "anonymous", RateLimiterPolicy = "StrictAuthPolicy", Match = new RouteMatch { Path = "/api/v1/identity/customers/{**catch-all}" } },
            new RouteConfig { RouteId = "identity-route", ClusterId = "identity-cluster", Order = 2, Match = new RouteMatch { Path = "/api/v1/identity/{**catch-all}" } },
            new RouteConfig { RouteId = "admin-staff-subroute", ClusterId = "identity-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/staff/{**catch-all}" } },
            new RouteConfig { RouteId = "admin-staff-route", ClusterId = "identity-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/staff" } },
            new RouteConfig { RouteId = "admin-shifts-subroute", ClusterId = "identity-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/shifts/{**catch-all}" } },
            new RouteConfig { RouteId = "admin-shifts-route", ClusterId = "identity-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/shifts" } },
            new RouteConfig { RouteId = "admin-customers-subroute", ClusterId = "identity-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/customers/{**catch-all}" } },
            new RouteConfig { RouteId = "admin-customers-route", ClusterId = "identity-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/customers" } },
                        new RouteConfig { RouteId = "admin-auditoriums-subroute", ClusterId = "catalog-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/auditoriums/{**catch-all}" } },
            new RouteConfig { RouteId = "admin-auditoriums-route", ClusterId = "catalog-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/auditoriums" } },
            new RouteConfig { RouteId = "admin-movies-subroute", ClusterId = "catalog-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/movies/{**catch-all}" } },
            new RouteConfig { RouteId = "admin-movies-route", ClusterId = "catalog-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/movies" } },
            new RouteConfig { RouteId = "admin-showtimes-subroute", ClusterId = "catalog-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/showtimes/{**catch-all}" } },
            new RouteConfig { RouteId = "admin-showtimes-route", ClusterId = "catalog-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/showtimes" } },
            new RouteConfig { RouteId = "admin-pricing-subroute", ClusterId = "catalog-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/pricing/{**catch-all}" } },
            new RouteConfig { RouteId = "admin-pricing-route", ClusterId = "catalog-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/pricing" } },
            new RouteConfig { RouteId = "admin-inventory-subroute", ClusterId = "pos-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/inventory/{**catch-all}" } },
            new RouteConfig { RouteId = "admin-inventory-route", ClusterId = "pos-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/inventory" } },
            new RouteConfig { RouteId = "admin-bookings-subroute", ClusterId = "reservation-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/bookings/{**catch-all}" } },
            new RouteConfig { RouteId = "admin-bookings-route", ClusterId = "reservation-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/bookings" } },
            new RouteConfig { RouteId = "admin-loyalty-subroute", ClusterId = "loyalty-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/loyalty/{**catch-all}" } },
            new RouteConfig { RouteId = "admin-loyalty-route", ClusterId = "loyalty-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/loyalty" } },
            new RouteConfig { RouteId = "admin-marketing-subroute", ClusterId = "loyalty-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/marketing/{**catch-all}" } },
            new RouteConfig { RouteId = "admin-marketing-route", ClusterId = "loyalty-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/marketing" } },
            new RouteConfig { RouteId = "admin-system-subroute", ClusterId = "identity-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/system/{**catch-all}" } },
            new RouteConfig { RouteId = "admin-system-route", ClusterId = "identity-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/system" } },
            new RouteConfig { RouteId = "admin-dashboards-subroute", ClusterId = "identity-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/dashboards/{**catch-all}" } },
            new RouteConfig { RouteId = "admin-dashboards-route", ClusterId = "identity-cluster", Order = 1, AuthorizationPolicy = "default", Match = new RouteMatch { Path = "/api/v1/admin/dashboards" } },
            new RouteConfig { RouteId = "loyalty-voucher-route", ClusterId = "loyalty-cluster", Order = 1, AuthorizationPolicy = "anonymous", Match = new RouteMatch { Path = "/api/v1/loyalty/vouchers/validate" } },
            new RouteConfig { RouteId = "loyalty-route", ClusterId = "loyalty-cluster", Order = 2, Match = new RouteMatch { Path = "/api/v1/loyalty/{**catch-all}" } },
            new RouteConfig { RouteId = "oidc-route", ClusterId = "identity-cluster", AuthorizationPolicy = "anonymous", Match = new RouteMatch { Path = "/.well-known/{**catch-all}" } }
        };
    }

    public static IReadOnlyList<ClusterConfig> GetClusters(IConfiguration? configuration = null)
    {
        var catalogUrl = configuration?["Services:CatalogUrl"] ?? "http://catalog-api:8080";
        var reservationUrl = configuration?["Services:ReservationUrl"] ?? "http://reservation-api:8080";
        var posUrl = configuration?["Services:PosUrl"] ?? "http://pos-api:8080";
        var ticketUrl = configuration?["Services:TicketUrl"] ?? "http://ticket-api:8080";
        var identityUrl = configuration?["Services:IdentityUrl"] ?? "http://identity-api:8080";
        var loyaltyUrl = configuration?["Services:LoyaltyUrl"] ?? "http://loyalty-api:8080";
        var minioConsoleUrl = configuration?["Services:MinioConsoleUrl"] ?? "http://minio:9001";
        var minioS3Url = configuration?["Services:MinioS3Url"] ?? "http://minio:9000";

        return new[]
        {
            new ClusterConfig { ClusterId = "catalog-cluster", LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin, Destinations = new Dictionary<string, DestinationConfig> { { "d1", new DestinationConfig { Address = catalogUrl } } } },
            new ClusterConfig { ClusterId = "reservation-cluster", LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin, Destinations = new Dictionary<string, DestinationConfig> { { "d1", new DestinationConfig { Address = reservationUrl } } } },
            new ClusterConfig { ClusterId = "pos-cluster", LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin, Destinations = new Dictionary<string, DestinationConfig> { { "d1", new DestinationConfig { Address = posUrl } } } },
            new ClusterConfig { ClusterId = "ticket-cluster", LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin, Destinations = new Dictionary<string, DestinationConfig> { { "d1", new DestinationConfig { Address = ticketUrl } } } },
            new ClusterConfig { ClusterId = "identity-cluster", LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin, Destinations = new Dictionary<string, DestinationConfig> { { "d1", new DestinationConfig { Address = identityUrl } } } },
            new ClusterConfig { ClusterId = "loyalty-cluster", LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin, Destinations = new Dictionary<string, DestinationConfig> { { "d1", new DestinationConfig { Address = loyaltyUrl } } } },
            new ClusterConfig { ClusterId = "minio-console-cluster", LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin, Destinations = new Dictionary<string, DestinationConfig> { { "d1", new DestinationConfig { Address = minioConsoleUrl } } } },
            new ClusterConfig { ClusterId = "minio-s3-cluster", LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin, Destinations = new Dictionary<string, DestinationConfig> { { "d1", new DestinationConfig { Address = minioS3Url } } } }
        };
    }
}

