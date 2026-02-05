
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Credentials.Camunda.Models;
using Npgsql;
using Serilog;

namespace Credentials.Camunda.Services
{
    public class PostgreSQLUserManagementService : IPostgreSQLUserManagementService
    {
        private readonly string _connectionString;

        public PostgreSQLUserManagementService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("TREPostgresConnection");
        }

        public async Task<UserCreationResult> CreateUserAsync(CreateUserRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return UserCreationResult.Error("Username and password are required");
                }

                if (!IsValidUsername(request.Username))
                {
                    return UserCreationResult.Error("Invalid username format");
                }

                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                if (await UserExistsInternalAsync(connection, request.Username))
                {
                    Log.Information("User {Username} already exists, skipping creation", request.Username);
                    return UserCreationResult.Ok(); 
                }

                await CreateUserInternalAsync(connection, request);

                // Grant permissions for each schema
                if (request.SchemaPermissions != null && request.SchemaPermissions.Any())
                {
                    foreach (var schemaPermission in request.SchemaPermissions)
                    {
                        if(!string.IsNullOrWhiteSpace(schemaPermission.SchemaName))
                        {
                            await EnsureSchemaExistsAsync(connection, schemaPermission.SchemaName);
                        }
                        
                        if (schemaPermission.Permissions != DatabasePermissions.None)
                        {
                            await GrantSchemaPermissionsInternalAsync(connection, request.Username,
                                schemaPermission.SchemaName, schemaPermission.Permissions);
                        }
                    }

                    Log.Information("Successfully created user: {Username} with {SchemaCount} schema permissions",
                        request.Username, request.SchemaPermissions.Count);
                }
                else
                {
                    Log.Information("Successfully created user: {Username} with no schema permissions",
                        request.Username);
                }
                return UserCreationResult.Ok();

            }
            catch (PostgresException ex)
            {
                Log.Error(ex, "PostgreSQL error creating user: {Username}", request.Username);
                return UserCreationResult.Error($"Database error: {ex.MessageText}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating user: {Username}", request.Username);
                return UserCreationResult.Error($"Unexpected error: {ex.Message}");
            }
        }

        public async Task<bool> UserExistsAsync(string username)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                return await UserExistsInternalAsync(connection, username);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error checking if user exists: {Username}", username);
                return false;
            }
        }

        public async Task<bool> DropUserAsync(string username)
        {
            try
            {
                if (!IsValidUsername(username))
                {
                    return false;
                }

                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // Determine admin user from connection string
                var builder = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
                var adminUser = builder.Username;
                // Fallback to 'postgres' if admin user is not valid
                if (string.IsNullOrWhiteSpace(adminUser) || !IsValidUsername(adminUser))
                {
                    adminUser = "postgres";
                }

                var reassignCommand = $"REASSIGN OWNED BY \"{username}\" TO \"{adminUser}\"";
                using (var cmd = new NpgsqlCommand(reassignCommand, connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                    Log.Information("Reassigned owned objects for user: {Username}", username);
                }

                var dropOwnedCommand = $"DROP OWNED BY \"{username}\"";
                using (var cmd = new NpgsqlCommand(dropOwnedCommand, connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                    Log.Information("Dropped owned objects for user: {Username}", username);
                }

                var dropUserCommand = $"DROP USER IF EXISTS \"{username}\"";
                using (var cmd = new NpgsqlCommand(dropUserCommand, connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                    Log.Information("Successfully dropped user: {Username}", username);
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error dropping user: {Username}", username);
                return false;
            }
        }

        public async Task<bool> GrantSchemaPermissionsAsync(string username, string schemaName, DatabasePermissions permissions)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                await GrantSchemaPermissionsInternalAsync(connection, username, schemaName, permissions);

                Log.Information("Successfully granted {Permissions} permissions on schema {Schema} to user {Username}",
                    permissions, schemaName, username);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error granting schema permissions to user: {Username}, Schema: {Schema}", username, schemaName);
                return false;
            }
        }

        public async Task<bool> RevokeSchemaPermissionsAsync(string username, string schemaName)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                var commands = new List<string>
                {
                    $"REVOKE ALL ON SCHEMA \"{schemaName}\" FROM \"{username}\"",
                    $"REVOKE ALL ON ALL TABLES IN SCHEMA \"{schemaName}\" FROM \"{username}\"",
                    $"REVOKE ALL ON ALL SEQUENCES IN SCHEMA \"{schemaName}\" FROM \"{username}\"",
                    $"ALTER DEFAULT PRIVILEGES IN SCHEMA \"{schemaName}\" REVOKE ALL ON TABLES FROM \"{username}\""
                };

                foreach (var command in commands)
                {
                    using var cmd = new NpgsqlCommand(command, connection);
                    await cmd.ExecuteNonQueryAsync();
                }

                Log.Information("Successfully revoked permissions on schema {Schema} from user {Username}", schemaName, username);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error revoking schema permissions from user: {Username}, Schema: {Schema}", username, schemaName);
                return false;
            }
        }

        public async Task<List<string>> GetUserSchemasAsync(string username)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT DISTINCT nspname 
                    FROM pg_namespace n
                    JOIN pg_class c ON n.oid = c.relnamespace
                    JOIN pg_tables t ON c.relname = t.tablename AND n.nspname = t.schemaname
                    WHERE has_schema_privilege(@username, n.nspname, 'USAGE')";

                using var cmd = new NpgsqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@username", username);

                var schemas = new List<string>();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    schemas.Add(reader.GetString(0));
                }

                return schemas;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting user schemas: {Username}", username);
                return new List<string>();
            }
        }

        private async Task<bool> UserExistsInternalAsync(NpgsqlConnection connection, string username)
        {
            var checkUserQuery = "SELECT 1 FROM pg_user WHERE usename = @username";
            using var checkCmd = new NpgsqlCommand(checkUserQuery, connection);
            checkCmd.Parameters.AddWithValue("@username", username);
            var result = await checkCmd.ExecuteScalarAsync();
            return result != null;
        }

        private async Task CreateUserInternalAsync(NpgsqlConnection connection, CreateUserRequest request)
        {
            var privileges = new List<string>();

            if (request.CanLogin) privileges.Add("LOGIN");
            if (request.CanCreateDb) privileges.Add("CREATEDB");
            if (request.CanCreateRole) privileges.Add("CREATEROLE");

            var privilegeString = privileges.Count > 0 ? string.Join(" ", privileges) : "LOGIN";

            // Escape the password properly to prevent SQL injection
            var escapedPassword = request.Password.Replace("'", "''");

            var createUserCommand = $"CREATE USER \"{request.Username}\" WITH PASSWORD '{escapedPassword}' {privilegeString}";
            using var cmd = new NpgsqlCommand(createUserCommand, connection);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task GrantSchemaPermissionsInternalAsync(NpgsqlConnection connection, string username,
            string schemaName, DatabasePermissions permissions)
        {
            var commands = new List<string>();

            // Always grant USAGE on schema first
            commands.Add($"GRANT USAGE ON SCHEMA \"{schemaName}\" TO \"{username}\"");

            if (permissions.HasFlag(DatabasePermissions.Read))
            {
                // Grant SELECT on all existing tables
                commands.Add($"GRANT SELECT ON ALL TABLES IN SCHEMA \"{schemaName}\" TO \"{username}\"");
                commands.Add($"GRANT SELECT ON ALL SEQUENCES IN SCHEMA \"{schemaName}\" TO \"{username}\"");

                // Ensure future tables in schema are also readable
                commands.Add($"ALTER DEFAULT PRIVILEGES IN SCHEMA \"{schemaName}\" GRANT SELECT ON TABLES TO \"{username}\"");
                commands.Add($"ALTER DEFAULT PRIVILEGES IN SCHEMA \"{schemaName}\" GRANT SELECT ON SEQUENCES TO \"{username}\"");
            }

            if (permissions.HasFlag(DatabasePermissions.Write))
            {
                // Grant read-write on all existing tables
                commands.Add($"GRANT INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA \"{schemaName}\" TO \"{username}\"");
                commands.Add($"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA \"{schemaName}\" TO \"{username}\"");

                // Ensure future tables in schema are also read-write
                commands.Add($"ALTER DEFAULT PRIVILEGES IN SCHEMA \"{schemaName}\" GRANT INSERT, UPDATE, DELETE ON TABLES TO \"{username}\"");
                commands.Add($"ALTER DEFAULT PRIVILEGES IN SCHEMA \"{schemaName}\" GRANT USAGE, SELECT ON SEQUENCES TO \"{username}\"");
            }

            if (permissions.HasFlag(DatabasePermissions.CreateTables))
            {
                commands.Add($"GRANT CREATE ON SCHEMA \"{schemaName}\" TO \"{username}\"");
            }
            

            foreach (var command in commands)
            {
                using var cmd = new NpgsqlCommand(command, connection);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private bool IsValidUsername(string username)
        {
            // Basic validation to prevent SQL injection
            if (string.IsNullOrWhiteSpace(username)) return false;
            if (username.Length > 63) return false; // PostgreSQL username limit

            // Allow alphanumeric, underscore, and hyphen
            return username.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');
        }

        private bool IsValidSchemaName(string schemaName)
        {
            // Similar validation for schema names
            if (string.IsNullOrWhiteSpace(schemaName)) return false;
            if (schemaName.Length > 63) return false; // PostgreSQL identifier limit

            // Allow alphanumeric, underscore, and hyphen
            return schemaName.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');
        }
        
        private async Task EnsureSchemaExistsAsync(NpgsqlConnection connection, string schemaName)
        {
            if (string.IsNullOrWhiteSpace(schemaName))
            {
                Log.Warning("Schema name is empty, skipping EnsureSchemaExistsAsync");
                return;
            }

            if (!IsValidSchemaName(schemaName))
            {
                Log.Warning("Invalid schema name: {SchemaName}, skipping creation", schemaName);
                return;
            }

            // Use double quotes to preserve case-sensitivity and allow names starting with digits
            var commandText = $"CREATE SCHEMA IF NOT EXISTS \"{schemaName}\"";

            Log.Information("Executing SQL: {CommandText}", commandText);

            using var cmd = new NpgsqlCommand(commandText, connection);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
