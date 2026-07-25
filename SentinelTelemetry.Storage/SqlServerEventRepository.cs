using Microsoft.Data.SqlClient;
using SentinelTelemetry.Core.Interfaces;
using SentinelTelemetry.Core.Models;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace SentinelTelemetry.Persistence
{
    public class SqlServerEventRepository : IEventRepository
    {
        private readonly string _connectionString;

        public SqlServerEventRepository(string conecctionString)
        {
            _connectionString = conecctionString;
            EnsureDatabaseAndTableCreated();
        }

        private void EnsureDatabaseAndTableCreated()
        {
            var masterConnectionString = new SqlConnectionStringBuilder(_connectionString)
            {
                InitialCatalog = "master"
            }.ConnectionString;

            var databaseName = new SqlConnectionStringBuilder(_connectionString).InitialCatalog;

            using (var masterConnection = new SqlConnection(masterConnectionString))
            {
                masterConnection.Open();
                var createDbCommand = masterConnection.CreateCommand();
                createDbCommand.CommandText = $@"
                    IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = '{databaseName}')
                    CREATE DATABASE [{databaseName}];";
                createDbCommand.ExecuteNonQuery();
            }

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            var createTablesCommand = connection.CreateCommand();
            createTablesCommand.CommandText = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Source' AND xtype='U')
                CREATE TABLE dbo.Source (
                    id_source INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    source_name NVARCHAR(100) NOT NULL UNIQUE,
                    description NVARCHAR(255) NULL
                );

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Category' AND xtype='U')
                CREATE TABLE dbo.Category (
                    id_category INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    category_name NVARCHAR(100) NOT NULL UNIQUE,
                    description NVARCHAR(255) NULL
                );

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Version' AND xtype='U')
                CREATE TABLE dbo.Version (
                    id_version INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    numero_version INT NOT NULL UNIQUE,
                    description NVARCHAR(255) NULL
                );

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Events' AND xtype='U')
                CREATE TABLE dbo.Events (
                    id_event UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    timestamp DATETIME2(7) NOT NULL,
                    id_source INT NOT NULL,
                    id_category INT NOT NULL,
                    event_type NVARCHAR(100) NOT NULL,
                    id_version INT NOT NULL,
                    data_json NVARCHAR(MAX) NOT NULL,
                    CONSTRAINT fk_events_source FOREIGN KEY (id_source) REFERENCES dbo.Source(id_source),
                    CONSTRAINT fk_events_category FOREIGN KEY (id_category) REFERENCES dbo.Category(id_category),
                    CONSTRAINT fk_events_version FOREIGN KEY (id_version) REFERENCES dbo.Version(id_version)
                );";
            createTablesCommand.ExecuteNonQuery();
        }

        // Busca el Id de una fila de catálogo por su nombre; si no existe, la crea y devuelve el nuevo Id.
        // Reutilizable para Source y Category porque las dos siguen el mismo patrón (nombre único -> Id).
        private async Task<int> GetOrCreateCatalogIdAsync(
            SqlConnection connection,
            string tableName,
            string idColumn,
            string nameColumn,
            string nameValue)
        {
            var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = $"SELECT {idColumn} FROM dbo.{tableName} WHERE {nameColumn} = @name;";
            selectCommand.Parameters.AddWithValue("@name", nameValue);

            var existingId = await selectCommand.ExecuteScalarAsync();
            if (existingId != null)
            {
                return (int)existingId;
            }

            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = $@"
                INSERT INTO dbo.{tableName} ({nameColumn})
                OUTPUT INSERTED.{idColumn}
                VALUES (@name);";
            insertCommand.Parameters.AddWithValue("@name", nameValue);

            var newId = await insertCommand.ExecuteScalarAsync();
            return (int)newId!;
        }

        // Version se resuelve aparte porque busca por número (int), no por nombre de texto
        private async Task<int> GetOrCreateVersionIdAsync(SqlConnection connection, int numeroVersion)
        {
            var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = "SELECT id_version FROM dbo.Version WHERE numero_version = @numero;";
            selectCommand.Parameters.AddWithValue("@numero", numeroVersion);

            var existingId = await selectCommand.ExecuteScalarAsync();
            if (existingId != null)
            {
                return (int)existingId;
            }

            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT INTO dbo.Version (numero_version)
                OUTPUT INSERTED.id_version
                VALUES (@numero);";
            insertCommand.Parameters.AddWithValue("@numero", numeroVersion);

            var newId = await insertCommand.ExecuteScalarAsync();
            return (int)newId!;
        }

        public async Task SaveEventAsync(EventBase evento)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            int idSource = await GetOrCreateCatalogIdAsync(connection, "Source", "id_source", "source_name", evento.Source);
            int idCategory = await GetOrCreateCatalogIdAsync(connection, "Category", "id_category", "category_name", evento.Category);
            int idVersion = await GetOrCreateVersionIdAsync(connection, evento.Version);

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO dbo.Events (id_event, timestamp, id_source, id_category, event_type, id_version, data_json)
                VALUES (@id_event, @timestamp, @id_source, @id_category, @event_type, @id_version, @data_json);";

            command.Parameters.AddWithValue("@id_event", evento.Id);
            command.Parameters.AddWithValue("@timestamp", evento.Timestamp);
            command.Parameters.AddWithValue("@id_source", idSource);
            command.Parameters.AddWithValue("@id_category", idCategory);
            command.Parameters.AddWithValue("@event_type", evento.EventType);
            command.Parameters.AddWithValue("@id_version", idVersion);
            command.Parameters.AddWithValue("@data_json", JsonSerializer.Serialize(evento, evento.GetType()));

            await command.ExecuteNonQueryAsync();
        }
    }
}