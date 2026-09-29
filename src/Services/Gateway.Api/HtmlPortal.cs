namespace Gateway.Api;

public static class HtmlPortal
{
    public const string Content = """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Cinema POS & Digital Ticketing — Gateway & Public Web Portal</title>
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css" rel="stylesheet">
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css">
    <style>
        :root {
            --bg-color: #0b0f19;
            --card-bg: #1e293b;
            --accent: #38bdf8;
            --border: #334155;
        }
        body {
            background: var(--bg-color);
            color: #f1f5f9;
            font-family: system-ui, -apple-system, sans-serif;
            padding-bottom: 3rem;
        }
        .hero {
            background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%);
            border-bottom: 1px solid var(--border);
            padding: 2.5rem 0;
            margin-bottom: 2rem;
        }
        .card-custom {
            background: var(--card-bg);
            border: 1px solid var(--border);
            border-radius: 12px;
            transition: transform 0.2s, border-color 0.2s;
            height: 100%;
        }
        .card-custom:hover {
            transform: translateY(-4px);
            border-color: var(--accent);
        }
        .badge-net {
            background: #6366f1;
            color: white;
            font-weight: 600;
        }
        .badge-phase3 {
            background: #e11d48;
            color: white;
            font-weight: 600;
        }
        .badge-spring {
            background: #10b981;
            color: white;
            font-weight: 600;
        }
        .table-dark-custom {
            background: var(--card-bg);
            border-radius: 10px;
            overflow: hidden;
            border: 1px solid var(--border);
        }
        .table-dark-custom th {
            background: #0f172a;
            color: var(--accent);
            border-color: var(--border);
        }
        .table-dark-custom td {
            background: var(--card-bg);
            color: #e2e8f0;
            border-color: var(--border);
            vertical-align: middle;
        }
        a {
            text-decoration: none;
        }
    </style>
</head>
<body>
    <div class="hero">
        <div class="container">
            <div class="d-flex align-items-center justify-content-between flex-wrap gap-3">
                <div>
                    <span class="badge badge-net px-3 py-2 mb-2 rounded-pill me-1">.NET 8 Microservices</span>
                    <span class="badge badge-phase3 px-3 py-2 mb-2 rounded-pill">Phase 3: Public Web &amp; Guest Checkout</span>
                    <h1 class="display-6 fw-bold text-white mb-1">🎬 Multi-Branch Cinema POS &amp; Public Web Portal</h1>
                    <p class="text-secondary mb-0">High-Performance YARP Gateway &bull; Pluggable Email Notifications &bull; HMAC Signed E-Tickets</p>
                </div>
                <div class="d-flex gap-2">
                    <a href="/swagger" class="btn btn-primary btn-lg shadow px-4">
                        <i class="bi bi-journal-code me-2"></i> Unified Swagger
                    </a>
                    <a href="http://localhost:8025" target="_blank" class="btn btn-danger btn-lg shadow px-4">
                        <i class="bi bi-envelope-open-fill me-2"></i> Mailpit Inbox
                    </a>
                </div>
            </div>
        </div>
    </div>

    <div class="container">

        <!-- Developer Tools & Web UIs -->
        <h4 class="text-white mb-3"><i class="bi bi-tools text-info me-2"></i> Infrastructure &amp; Monitoring UIs</h4>
        <div class="row g-3 mb-5">
            <div class="col-lg-4 col-md-6">
                <div class="card-custom p-4">
                    <div class="d-flex align-items-center justify-content-between mb-3">
                        <h5 class="text-white mb-0"><i class="bi bi-envelope-paper-heart-fill text-danger me-2"></i> Mailpit Web Inbox</h5>
                        <span class="badge bg-danger">Port 8025</span>
                    </div>
                    <p class="text-secondary small">Visual email inbox for Phase 3 transactional e-tickets. Intercepts SMTP traffic on port 1025 with zero external internet dependencies or spam risk.</p>
                    <a href="http://localhost:8025" target="_blank" class="btn btn-outline-danger btn-sm w-100">
                        <i class="bi bi-box-arrow-up-right me-1"></i> Open Mailpit Web UI
                    </a>
                </div>
            </div>

            <div class="col-lg-4 col-md-6">
                <div class="card-custom p-4">
                    <div class="d-flex align-items-center justify-content-between mb-3">
                        <h5 class="text-white mb-0"><i class="bi bi-database text-primary me-2"></i> PostgreSQL UI</h5>
                        <span class="badge bg-success">Port 8086</span>
                    </div>
                    <p class="text-secondary small">Web GUI (pgweb) connected to PostgreSQL 16 (Port 5433) and Read Replica (Port 5434). Inspect 5 schemas, ACID tables, and run SQL queries.</p>
                    <a href="http://localhost:8086" target="_blank" class="btn btn-outline-primary btn-sm w-100">
                        <i class="bi bi-box-arrow-up-right me-1"></i> Open PostgreSQL Web UI
                    </a>
                </div>
            </div>

            <div class="col-lg-4 col-md-6">
                <div class="card-custom p-4">
                    <div class="d-flex align-items-center justify-content-between mb-3">
                        <h5 class="text-white mb-0"><i class="bi bi-lightning-charge text-danger me-2"></i> Redis UI</h5>
                        <span class="badge bg-danger">Port 8087</span>
                    </div>
                    <p class="text-secondary small">Web GUI (redis-commander) connected to Redis 7 (Port 6379). Inspect seat locks (<code>seat-hold:*</code>), 8-min guest TTL countdowns, and caches.</p>
                    <a href="http://localhost:8087" target="_blank" class="btn btn-outline-danger btn-sm w-100">
                        <i class="bi bi-box-arrow-up-right me-1"></i> Open Redis Web UI
                    </a>
                </div>
            </div>

            <div class="col-lg-4 col-md-6">
                <div class="card-custom p-4">
                    <div class="d-flex align-items-center justify-content-between mb-3">
                        <h5 class="text-white mb-0"><i class="bi bi-envelope-paper-fill text-warning me-2"></i> RabbitMQ UI</h5>
                        <span class="badge bg-warning text-dark">Port 15672</span>
                    </div>
                    <p class="text-secondary small">Web GUI for RabbitMQ 3. Inspect AMQP topic exchange (<code>cinema.events</code>), consumer queues, and <code>ticket.issued</code> events.<br><span class="text-muted" style="font-size: 0.75rem;">cinema_app / change-this-development-password</span></p>
                    <a href="http://localhost:15672" target="_blank" class="btn btn-outline-warning btn-sm w-100">
                        <i class="bi bi-box-arrow-up-right me-1"></i> Open RabbitMQ Web UI
                    </a>
                </div>
            </div>

            <div class="col-lg-4 col-md-6">
                <div class="card-custom p-4">
                    <div class="d-flex align-items-center justify-content-between mb-3">
                        <h5 class="text-white mb-0"><i class="bi bi-journal-text text-info me-2"></i> Seq Logging</h5>
                        <span class="badge bg-info text-dark">Port 5341</span>
                    </div>
                    <p class="text-secondary small">Dedicated real-time structured logging server for .NET 8. Ingests CLEF JSON streams, trace correlation IDs, and Serilog events across microservices.</p>
                    <a href="http://localhost:5341" target="_blank" class="btn btn-outline-info btn-sm w-100">
                        <i class="bi bi-box-arrow-up-right me-1"></i> Open Seq Logging Web UI
                    </a>
                </div>
            </div>

            <div class="col-lg-4 col-md-6">
                <div class="card-custom p-4">
                    <div class="d-flex align-items-center justify-content-between mb-3">
                        <h5 class="text-white mb-0"><i class="bi bi-diagram-2 text-primary me-2"></i> Distributed Tracing: Jaeger</h5>
                        <span class="badge bg-primary">Port 16186</span>
                    </div>
                    <p class="text-secondary small">OpenTelemetry distributed trace visualizer. Trace end-to-end request latencies, span waterfalls, and microservice hops across Gateway and backend services.</p>
                    <a href="http://localhost:16186" target="_blank" class="btn btn-outline-primary btn-sm w-100">
                        <i class="bi bi-box-arrow-up-right me-1"></i> Open Jaeger Web UI
                    </a>
                </div>
            </div>

            <div class="col-lg-4 col-md-6">
                <div class="card-custom p-4">
                    <div class="d-flex align-items-center justify-content-between mb-3">
                        <h5 class="text-white mb-0"><i class="bi bi-hdd-network-fill text-warning me-2"></i> MinIO S3 Console</h5>
                        <span class="badge bg-warning text-dark">Port 8080 / 9001</span>
                    </div>
                    <p class="text-secondary small">S3-compatible object storage console. Inspect buckets (<code>cinema-media</code>), upload movie posters/trailers, and test S3 presigned URLs.<br><span class="text-muted" style="font-size: 0.75rem;">minioadmin / change-this-development-password</span></p>
                    <div class="d-flex gap-2">
                        <a href="/minio-console" target="_blank" class="btn btn-outline-warning btn-sm flex-fill">
                            <i class="bi bi-box-arrow-up-right me-1"></i> Gateway /minio-console
                        </a>
                        <a href="http://localhost:9001" target="_blank" class="btn btn-outline-secondary btn-sm flex-fill">
                            <i class="bi bi-box-arrow-up-right me-1"></i> Direct :9001
                        </a>
                    </div>
                </div>
            </div>
        </div>

        <!-- Microservices Fleet Ports -->
        <h4 class="text-white mb-3"><i class="bi bi-diagram-3 text-warning me-2"></i> Microservices Fleet (Direct Ports)</h4>
        <div class="row g-3 mb-5">
            <div class="col-lg col-md-4 col-sm-6">
                <div class="card-custom p-3 text-center d-flex flex-column justify-content-between">
                    <div>
                        <div class="fw-bold text-white mb-1">Catalog API</div>
                        <div class="badge bg-primary mb-2">Port 8081</div>
                        <p class="text-secondary small mb-3">Movies, showtimes, branches, visual seat map</p>
                    </div>
                    <div class="d-flex flex-column gap-1">
                        <a href="http://localhost:8081/swagger" target="_blank" class="btn btn-sm btn-dark w-100"><i class="bi bi-journal-code me-1"></i> Swagger 8081</a>
                        <a href="http://localhost:8081/health" target="_blank" class="btn btn-sm btn-outline-secondary w-100"><i class="bi bi-heart-pulse me-1"></i> Health Check</a>
                    </div>
                </div>
            </div>
            <div class="col-lg col-md-4 col-sm-6">
                <div class="card-custom p-3 text-center d-flex flex-column justify-content-between">
                    <div>
                        <div class="fw-bold text-white mb-1">Reservation API</div>
                        <div class="badge bg-success mb-2">Port 8082</div>
                        <p class="text-secondary small mb-3">Redis Lua hold, 8-min guest TTL, idempotency</p>
                    </div>
                    <div class="d-flex flex-column gap-1">
                        <a href="http://localhost:8082/swagger" target="_blank" class="btn btn-sm btn-dark w-100"><i class="bi bi-journal-code me-1"></i> Swagger 8082</a>
                        <a href="http://localhost:8082/health" target="_blank" class="btn btn-sm btn-outline-secondary w-100"><i class="bi bi-heart-pulse me-1"></i> Health Check</a>
                    </div>
                </div>
            </div>
            <div class="col-lg col-md-4 col-sm-6">
                <div class="card-custom p-3 text-center d-flex flex-column justify-content-between">
                    <div>
                        <div class="fw-bold text-white mb-1">POS API</div>
                        <div class="badge bg-danger mb-2">Port 8083</div>
                        <p class="text-secondary small mb-3">Guest checkout, payments, till shifts, drawer kick</p>
                    </div>
                    <div class="d-flex flex-column gap-1">
                        <a href="http://localhost:8083/swagger" target="_blank" class="btn btn-sm btn-dark w-100"><i class="bi bi-journal-code me-1"></i> Swagger 8083</a>
                        <a href="http://localhost:8083/health" target="_blank" class="btn btn-sm btn-outline-secondary w-100"><i class="bi bi-heart-pulse me-1"></i> Health Check</a>
                    </div>
                </div>
            </div>
            <div class="col-lg col-md-4 col-sm-6">
                <div class="card-custom p-3 text-center d-flex flex-column justify-content-between">
                    <div>
                        <div class="fw-bold text-white mb-1">Ticket API</div>
                        <div class="badge bg-warning text-dark mb-2">Port 8084</div>
                        <p class="text-secondary small mb-3">Notification worker, HMAC pass, gate scanner</p>
                    </div>
                    <div class="d-flex flex-column gap-1">
                        <a href="http://localhost:8084/swagger" target="_blank" class="btn btn-sm btn-dark w-100"><i class="bi bi-journal-code me-1"></i> Swagger 8084</a>
                        <a href="http://localhost:8084/health" target="_blank" class="btn btn-sm btn-outline-secondary w-100"><i class="bi bi-heart-pulse me-1"></i> Health Check</a>
                    </div>
                </div>
            </div>
            <div class="col-lg col-md-4 col-sm-6">
                <div class="card-custom p-3 text-center d-flex flex-column justify-content-between">
                    <div>
                        <div class="fw-bold text-white mb-1">Identity API</div>
                        <div class="badge bg-info text-dark mb-2">Port 8085</div>
                        <p class="text-secondary small mb-3">Cashier PIN terminal login, RBAC roles, staff list</p>
                    </div>
                    <div class="d-flex flex-column gap-1">
                        <a href="http://localhost:8085/swagger" target="_blank" class="btn btn-sm btn-dark w-100"><i class="bi bi-journal-code me-1"></i> Swagger 8085</a>
                        <a href="http://localhost:8085/health" target="_blank" class="btn btn-sm btn-outline-secondary w-100"><i class="bi bi-heart-pulse me-1"></i> Health Check</a>
                    </div>
                </div>
            </div>
        </div>
    </div>
</body>
</html>
""";
}
