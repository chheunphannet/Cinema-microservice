using System;
using System.IO;
using System.Linq;
using DbUp;

namespace Cinema.DbMigrator
{
    class Program
    {
        static int Main(string[] args)
        {
            var connectionString =
                Environment.GetEnvironmentVariable("ConnectionStrings__WriteDb")
                ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
                ?? args.FirstOrDefault();

            var scriptPath = Environment.GetEnvironmentVariable("MIGRATIONS_PATH");
            if (string.IsNullOrWhiteSpace(scriptPath) || !Directory.Exists(scriptPath))
            {
                if (Directory.Exists("/app/infra/postgres/migrations"))
                {
                    scriptPath = "/app/infra/postgres/migrations";
                }
                else
                {
                    var dir = AppContext.BaseDirectory;
                    while (!string.IsNullOrEmpty(dir))
                    {
                        var candidate = Path.Combine(dir, "infra", "postgres", "migrations");
                        if (Directory.Exists(candidate))
                        {
                            scriptPath = candidate;
                            break;
                        }
                        var parent = Directory.GetParent(dir);
                        dir = parent?.FullName;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(scriptPath) || !Directory.Exists(scriptPath))
            {
                Console.WriteLine("Could not locate migrations directory.");
                return -1;
            }

            var upgrader = DeployChanges.To
                .PostgresqlDatabase(connectionString)
                .WithScriptsFromFileSystem(scriptPath)
                .JournalToPostgresqlTable("public", "SchemaVersions")
                .LogToConsole()
                .Build();

            var result = upgrader.PerformUpgrade();
            if (!result.Successful)
            {
                Console.WriteLine(result.Error);
                return -1;
            }

            return 0;
        }
    }
}
