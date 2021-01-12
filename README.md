# mssqlmigrator

.NET Console App tool to automatically run migrations in SQL Server Database. This can be used in DevOps pipelines to automatically apply migration scripts to target environments.

Inspired by: [dbmate](https://github.com/amacneil/dbmate)

### Usage

- Use Visual Studio to Publish the solution to a [single file](https://docs.microsoft.com/pt-br/dotnet/core/deploying/single-file)
- run mssqlmigrator.exe new to create a new migration file
- Fill the generated file's up and down section. The up section will be executed upon the migration's application and the down section will be executed to revert the migration. The file needs to keep the name pattern "yyyyMMddHHmmssff_migration" where "migration" can be replaced by a more suitable name. The date id and the underscore need to be maintained.
- Fill the config.json with the connectionStrings of each environment to which you would like to apply the migrations
- Create a migration table in the target database, containing only one column named id, of type varchar(100)
- run "mssqlmigrator.exe up [env]" to apply all the migrations that are on the migration folder that have not yet been applied on the target database. If no env is specified, this option defaults to "dev".
- run "mssqlmigrator.exe down [env] [migrationId]" to revert a single migration. If env is not specified, it defaults to "dev". If migrationId is not specified, it defaults to the last migration applied to the database.
  
### Dependencies: 
- .NET Core 3
- Dapper
- NewtonSoft.Json
- System.Data.SqlClient