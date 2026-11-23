using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ProductionApp
{
    internal static class DatabaseStartup
    {
        public static async Task InitializeAsync(AppDbContext db)
        {
            var settings = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
            if (!settings.DataSource.Equals(@"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase)
                || !settings.InitialCatalog.Equals("ProductionDB", StringComparison.OrdinalIgnoreCase)
                || settings.AttachDBFilename.Length > 0)
            {
                await db.Database.MigrateAsync();
                return;
            }

            // Один запуск проверяет файлы и применяет миграции. Закрытие сеанса освобождает блокировку.
            settings.InitialCatalog = "master";
            settings.Pooling = false;
            await using var master = new SqlConnection(settings.ConnectionString);
            await master.OpenAsync();
            using var command = master.CreateCommand();
            command.CommandText = """
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource = N'ProductionApp.DatabaseStartup',
                    @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 15000;
                SELECT @result;
                """;
            if (await command.ExecuteScalarAsync() is not int lockResult || lockResult < 0)
            {
                throw new InvalidOperationException("Базу сейчас подготавливает другой экземпляр приложения.");
            }

            await AttachExistingFilesAsync(master);
            await db.Database.MigrateAsync();
        }

        private static async Task AttachExistingFilesAsync(SqlConnection master)
        {
            using var command = master.CreateCommand();
            command.CommandText = """
                SELECT DB_ID(N'ProductionDB'),
                       CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultDataPath')),
                       CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultLogPath'));
                """;
            string dataPath;
            string logPath;
            await using (var reader = await command.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync() || !reader.IsDBNull(0))
                {
                    return;
                }

                string dataFolder = reader.IsDBNull(1)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    : reader.GetString(1);
                string logFolder = reader.IsDBNull(2) ? dataFolder : reader.GetString(2);
                dataPath = Path.Combine(dataFolder, "ProductionDB.mdf");
                logPath = Path.Combine(logFolder, "ProductionDB_log.ldf");
            }

            bool hasData = File.Exists(dataPath);
            bool hasLog = File.Exists(logPath);
            if (!hasData && !hasLog)
            {
                return;
            }

            if (!hasData || !hasLog)
            {
                string missing = hasData ? logPath : dataPath;
                throw new FileNotFoundException("Для подключения существующей базы не найден файл " + missing);
            }

            string dataFile = dataPath.Replace("'", "''");
            string logFile = logPath.Replace("'", "''");
            command.CommandText = $"""
                CREATE DATABASE [ProductionDB] ON
                    (FILENAME = N'{dataFile}'),
                    (FILENAME = N'{logFile}')
                FOR ATTACH;
                """;
            await command.ExecuteNonQueryAsync();
        }
    }
}
