using Dapper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace mssqlmigrator
{
    class Program
    {
        private static string env;
        
        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintOptions();
                return;
            }

            (string option, string migrationId) = GetOptions(args);

            switch (option)
            {
                case "new":
                    CreateMigrationFile();
                    break;
                case "up":
                    await MigrateUp();
                    break;
                case "down":
                    await MigrateDown(migrationId);
                    break;
                default:
                    Console.WriteLine("Invalid command");
                    PrintOptions();
                    break;
            }
        }

        private static (string, string) GetOptions(string[] args)
        {
            var option = args[0].ToLowerInvariant();
            env = args.ElementAtOrDefault(1) ?? "dev";
            var migrationId = args.ElementAtOrDefault(2) ?? string.Empty;
            return (option, migrationId);
        }

        private static async Task MigrateDown(string migrationId)
        {
            bool up = false;
            await ExecuteMigrationUpOrDown(up, migrationId);
        }

        private static async Task MigrateUp()
        {
            bool up = true;
            await ExecuteMigrationUpOrDown(up);

        }

        private static async Task ExecuteMigrationUpOrDown(bool up, string migrationId = "")
        {
            string sqlConnectionString = GetConnectionString();

            var files = Directory.GetFileSystemEntries("migration", "*.sql").Select(x => Path.GetFileName(x)).OrderBy(x=> x);

            using SqlConnection conn = new SqlConnection(sqlConnectionString);
            await conn.OpenAsync();
            var tran = await conn.BeginTransactionAsync();
            var fileIds = files.ToDictionary(x =>x.Split("_").First(), x => x);
            var migrations = await conn.QueryAsync<string>("SELECT id FROM migration order by id", null, tran);

            if (up)
            {
                var newMigrations = fileIds.Where(x => !migrations.Contains(x.Key));
               
                foreach (var file in newMigrations)
                {
                    Console.WriteLine($"Applying {file.Key}");
                    string fullScript = File.ReadAllText(Path.Combine("migration", file.Value));
                    var scripts = fullScript.Split("--down");
                    var script = scripts.First();
                    await conn.ExecuteAsync(script, transaction:tran);
                }
                var migrationsToInsert = newMigrations.Select(x => new { id = x.Key });
                
                if (migrationsToInsert?.Any() == true)
                {
                    await conn.ExecuteAsync("INSERT INTO migration (id) VALUES (@id)", migrationsToInsert, tran);
                }
                
            }
            else
            {
                var migrationToUndo = migrations.FirstOrDefault(x => x == migrationId) ?? migrations.Last();
                var migrationFileToUndo = fileIds.FirstOrDefault(x => x.Key == migrationToUndo);
                if (!migrationFileToUndo.Equals(default(KeyValuePair<string, string>)))
                {
                    Console.WriteLine($"Rolling back {migrationFileToUndo.Key}");
                    string fullScript = File.ReadAllText(Path.Combine("migration", migrationFileToUndo.Value));
                    var scripts = fullScript.Split("--down");
                    var script = scripts.Last();
                    await conn.ExecuteAsync(script, transaction:tran);
                    await conn.ExecuteAsync("DELETE FROM migration WHERE id = @id", new { id = migrationFileToUndo.Key }, tran);
                }
            }

            await tran.CommitAsync();
            await conn.CloseAsync();


        }

        private static string GetConnectionString()
        {
            var configStr = File.ReadAllText("config.json");
            var configObj = JsonConvert.DeserializeObject<Dictionary<string, string>>(configStr);

            if (!configObj.Keys.Contains(env))
            {
                Console.WriteLine("Invalid environment");
                throw new ArgumentException("env");
            }

            string sqlConnectionString = configObj[env];
            return sqlConnectionString;
        }

        private static void CreateMigrationFile()
        {
            var dateNow = DateTime.UtcNow;
            var asm = Assembly.GetExecutingAssembly();
            //using Stream stream = asm.GetManifestResourceStream("mssqlmigrator.template_migration.sql");
            var dir = Directory.CreateDirectory("migration");
            //using StreamReader streamReader = new StreamReader(stream);
            var fileContent = File.ReadAllText("template_migration.sql");
            //string fileContent =  streamReader.ReadToEnd();
            File.WriteAllText(Path.Combine(dir.FullName, $"{dateNow:yyyyMMddHHmmssff}_migration.sql"), fileContent);

        }

        private static void PrintOptions()
        {
            Console.WriteLine("Please enter one of the following options:");
            Console.WriteLine("- 'new' to create a new migration file");
            Console.WriteLine("- 'up' to apply new migrations");
            Console.WriteLine("- 'down' to rollback latest migration");
        }
    }
}
