using System.Data;
using System.Diagnostics;
using AIAgentService.Core.Models;
using AIAgentService.Core.Models.Enums;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Serilog.Context;

namespace AIAgentService.Core.Library;

public sealed class MySqlClient
{
    public readonly DataBaseEnum DataBase;

    private readonly string _connectionString;

    private readonly IEnumerable<string> _backupConnectionStringList;

    private readonly ILogger<MySqlClient> _logger;

    public MySqlClient(IServiceProvider provider, DataBaseEnum dataBase, string connectionString)
    {
        DataBase = dataBase;
        _logger = provider.GetRequiredService<ILogger<MySqlClient>>();
        _connectionString = connectionString;
        _backupConnectionStringList = null;
    }

    public MySqlClient(IServiceProvider provider, DataBaseEnum dataBase, string connectionString, IEnumerable<string> backupConnectionStringList)
    {
        DataBase = dataBase;
        _logger = provider.GetRequiredService<ILogger<MySqlClient>>();
        _connectionString = connectionString;
        _backupConnectionStringList = backupConnectionStringList;
    }

    private string HealthCheck()
    {            
        if (_backupConnectionStringList == null)
        {
            return _connectionString;
        }

        foreach (var backupConn in _backupConnectionStringList)
        {
            try
            {
                using var connection = new MySqlConnection(backupConn);
                var dbSlaveStatus = connection.QueryFirstOrDefault<DBSlaveStatus>("Service_HealthCheck_V2", commandType: CommandType.StoredProcedure);

                // 拿不到Status或延遲太久都換下一個連線
                if (dbSlaveStatus == null || dbSlaveStatus.Seconds_Behind_Master > 180)
                {
                    _logger.LogWarning($"{ReplaceDBString(backupConn)} | DelayTime = {dbSlaveStatus?.Seconds_Behind_Master}");
                    continue;
                }

                return backupConn;
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"{ReplaceDBString(backupConn)} | HealthCheck Exception");
            }           
        }

        // 前面連線檢查都有問題，用最後一個連線
        return _backupConnectionStringList.Last();
    }

    public MySqlConnection GetMySqlConnection()
    {
        var conn = HealthCheck();
        return new MySqlConnection(conn);
    }

    #region Query

    public async Task<T> QueryFirstAsync<T>(string name, object parameters)
    {
        T result;
        var sw = Stopwatch.StartNew();

        try
        {
            var conn = HealthCheck();
            await using var connection = new MySqlConnection(conn);
            var command = new CommandDefinition(name, parameters, // 使用 CommandDefinition 不讓 Dapper 用預設值建立
                commandType: CommandType.StoredProcedure,
                flags: CommandFlags.Buffered);

            result = await connection.QueryFirstOrDefaultAsync<T>(command);
            await connection.CloseAsync();
            await connection.DisposeAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, name);
            throw;
        }

        LogContext.PushProperty("ElapsedMilliseconds", sw.Elapsed.TotalMilliseconds);
        _logger.LogDebug($"{name} {sw.Elapsed:c}");
        return result;
    }

    public async Task<T> QueryFirstWithNoCacheAsync<T>(string name, object parameters)
    {
        T result;
        var sw = Stopwatch.StartNew();

        try
        {
            var conn = HealthCheck();
            await using var connection = new MySqlConnection(conn);
            var command = new CommandDefinition(name, parameters, // 使用 CommandDefinition 不讓 Dapper 用預設值建立
                commandType: CommandType.StoredProcedure,
                flags: CommandFlags.NoCache);

            result = await connection.QueryFirstOrDefaultAsync<T>(command);
            await connection.CloseAsync();
            await connection.DisposeAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, name);
            throw;
        }

        LogContext.PushProperty("ElapsedMilliseconds", sw.Elapsed.TotalMilliseconds);
        _logger.LogDebug($"{name} {sw.Elapsed:c}");
        return result;
    }

    public T QueryFirst<T>(string name, object parameters, int? commandTimeout = null)
    {
        T result;
        var sw = Stopwatch.StartNew();
        try
        {
            var conn = HealthCheck();
            using var connection = new MySqlConnection(conn);
            var command = new CommandDefinition(name, parameters, // 使用 CommandDefinition 不讓 Dapper 用預設值建立
                commandType: CommandType.StoredProcedure,
                commandTimeout: commandTimeout,
                flags: CommandFlags.Buffered);

            result = connection.QueryFirstOrDefault<T>(command);
            connection.Close();
            connection.Dispose();
        }
        catch (Exception e)
        {
            _logger.LogError(e, name);
            throw;
        }

        LogContext.PushProperty("ElapsedMilliseconds", sw.Elapsed.TotalMilliseconds);
        _logger.LogDebug($"{name} {sw.Elapsed:c}");
        return result;
    }

    public IEnumerable<T> Query<T>(string sql, object parameters = null)
    {
        IEnumerable<T> result;
        var sw = Stopwatch.StartNew();

        try
        {
            var conn = HealthCheck();
            using var connection = new MySqlConnection(conn);
            var command = new CommandDefinition(sql, parameters, // 使用 CommandDefinition 不讓 Dapper 用預設值建立
                commandType: CommandType.StoredProcedure,
                flags: CommandFlags.Buffered);

            result = connection.Query<T>(command);
            connection.Close();
            connection.Dispose();
        }
        catch (Exception e)
        {
            _logger.LogError(e, sql);
            throw;
        }

        LogContext.PushProperty("ElapsedMilliseconds", sw.Elapsed.TotalMilliseconds);
        _logger.LogDebug($"{sql} {sw.Elapsed:c}");
        return result.ToList();
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string name, object parameters = null)
    {
        IEnumerable<T> result;
        var sw = Stopwatch.StartNew();

        try
        {
            var conn = HealthCheck();
            await using var connection = new MySqlConnection(conn);
            result = await connection.QueryAsync<T>(name, parameters, commandType: CommandType.StoredProcedure);
            await connection.CloseAsync();
            await connection.DisposeAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, name);
            throw;
        }

        LogContext.PushProperty("ElapsedMilliseconds", sw.Elapsed.TotalMilliseconds);
        _logger.LogDebug($"{name} {sw.Elapsed:c}");
        return result;
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string name, object parameters, int? commandTimeout = null)
    {
        IEnumerable<T> result;
        var sw = Stopwatch.StartNew();
        try
        {
            var conn = HealthCheck();
            await using var connection = new MySqlConnection(conn);
            result = await connection.QueryAsync<T>(name, parameters, commandType: CommandType.StoredProcedure, commandTimeout: commandTimeout);
        }
        catch (Exception e)
        {
            _logger.LogError(e, name);
            throw;
        }

        LogContext.PushProperty("ElapsedMilliseconds", sw.Elapsed.TotalMilliseconds);
        _logger.LogDebug($"{name} {sw.Elapsed:c}");
        return result;
    }

    #endregion

    #region RetryQuery

    public async Task<T> RetryQueryFirstAsync<T>(string name, DynamicParameters parameters)
    {
        T result;
        var sw = Stopwatch.StartNew();

        try
        {
            var conn = HealthCheck();
            _logger.LogInformation($"DB {ReplaceDBString(conn)} | {name} {sw.Elapsed:c}| {SerializeDynamicParameters(parameters)}");
            using var connection = new MySqlConnection(conn);
            result = await connection.QueryFirstOrDefaultAsync<T>(name, param: parameters, commandType: CommandType.StoredProcedure);
        }
        catch (MySqlException sqlEx)
        {
            var conn = HealthCheck();
            _logger.LogError(sqlEx, $"DBRetry {ReplaceDBString(conn)} | {name} {sw.Elapsed:c}| {SerializeDynamicParameters(parameters)}");
            using var connection = new MySqlConnection(conn);
            result = await connection.QueryFirstOrDefaultAsync<T>(name, param: parameters, commandType: CommandType.StoredProcedure);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"{name} {sw.Elapsed:c}| {SerializeDynamicParameters(parameters)}");
            throw;
        }

        LogContext.PushProperty("ElapsedMilliseconds", sw.Elapsed.TotalMilliseconds);
        _logger.LogDebug($"{name} {sw.Elapsed:c}");
        return result;
    }

    public IEnumerable<T> RetryQuery<T>(string name, DynamicParameters parameters)
    {
        IEnumerable<T> result;
        var sw = Stopwatch.StartNew();

        try
        {
            var conn = HealthCheck();
            _logger.LogInformation($"DB {ReplaceDBString(conn)} | {name} {sw.Elapsed:c}| {SerializeDynamicParameters(parameters)}");
            using var connection = new MySqlConnection(conn);
            result = connection.Query<T>(name, param: parameters, commandType: CommandType.StoredProcedure);
        }
        catch (MySqlException sqlEx)
        {
            var conn = HealthCheck();
            _logger.LogError(sqlEx, $"DBRetry {ReplaceDBString(conn)} | {name} {sw.Elapsed:c}| {SerializeDynamicParameters(parameters)}");
            using var connection = new MySqlConnection(conn);
            result = connection.Query<T>(name, param: parameters, commandType: CommandType.StoredProcedure);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"{name} {sw.Elapsed:c}| {SerializeDynamicParameters(parameters)}");
            throw;
        }

        LogContext.PushProperty("ElapsedMilliseconds", sw.Elapsed.TotalMilliseconds);
        _logger.LogDebug($"{name} {sw.Elapsed:c}");
        return result;
    }

    public async Task<IEnumerable<T>> RetryQueryAsync<T>(string name, DynamicParameters parameters)
    {
        IEnumerable<T> result;
        var sw = Stopwatch.StartNew();
        try
        {
            var conn = HealthCheck();
            _logger.LogInformation($"DB {ReplaceDBString(conn)} | {name} {sw.Elapsed:c}| {SerializeDynamicParameters(parameters)}");
            await using var connection = new MySqlConnection(conn);
            result = await connection.QueryAsync<T>(name, parameters, commandType: CommandType.StoredProcedure);
        }
        catch (MySqlException sqlEx)
        {
            var conn = HealthCheck();
            _logger.LogError(sqlEx, $"DBRetry {ReplaceDBString(conn)} | {name} {sw.Elapsed:c}| {SerializeDynamicParameters(parameters)}");
            using var connection = new MySqlConnection(conn);
            result = await connection.QueryAsync<T>(name, param: parameters, commandType: CommandType.StoredProcedure);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"{name} {sw.Elapsed:c}| {SerializeDynamicParameters(parameters)}");
            throw;
        }

        LogContext.PushProperty("ElapsedMilliseconds", sw.Elapsed.TotalMilliseconds);
        _logger.LogDebug($"{name} {sw.Elapsed:c}");
        return result;
    }

    #endregion

    #region Execute

    public async Task<int> ExecuteAsync(string name, IDbTransaction transaction, object parameters = null)
    {
        var sw = Stopwatch.StartNew();
        int result;
        try
        {
            result = await transaction.Connection.ExecuteAsync(name, parameters, commandType: CommandType.StoredProcedure);
        }
        catch (Exception e)
        {
            _logger.LogError(e, name);
            throw;
        }

        LogContext.PushProperty("ElapsedMilliseconds", sw.Elapsed.TotalMilliseconds);
        _logger.LogDebug($"{name} {sw.Elapsed:c}");
        return result;
    }

    public async Task<int> ExecuteAsync(string name, object parameters)
    {
        int result;
        var sw = Stopwatch.StartNew();
        try
        {
            var conn = HealthCheck();
            await using var connection = new MySqlConnection(conn);
            result = await connection.ExecuteAsync(name, parameters, commandType: CommandType.StoredProcedure);
            await connection.CloseAsync();
            await connection.DisposeAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, name);
            throw;
        }

        LogContext.PushProperty("ElapsedMilliseconds", sw.Elapsed.TotalMilliseconds);
        _logger.LogDebug($"{name} {sw.Elapsed:c}");
        return result;
    }

    public T ExecuteScalar<T>(string name, object parameters)
    {
        T result;
        var sw = Stopwatch.StartNew();
        try
        {
            var conn = HealthCheck();
            using var connection = new MySqlConnection(conn);
            result = connection.ExecuteScalar<T>(name, parameters, commandType: CommandType.StoredProcedure);
            connection.Close();
            connection.Dispose();
        }
        catch (Exception e)
        {
            _logger.LogError(e, name);
            throw;
        }

        LogContext.PushProperty("ElapsedMilliseconds", sw.Elapsed.TotalMilliseconds);
        _logger.LogDebug($"{name} {sw.Elapsed:c}");
        return result;
    }

    public int Execute(string name, object parameters, int? commandTimeout = null)
    {
        var result = 0;
        var sw = Stopwatch.StartNew();
        try
        {
            var conn = HealthCheck();
            using var connection = new MySqlConnection(conn);
            result = connection.Execute(name, parameters, commandType: CommandType.StoredProcedure, commandTimeout: commandTimeout);
        }
        catch (Exception e)
        {
            _logger.LogError(e, name);
            throw;
        }

        LogContext.PushProperty("ElapsedMilliseconds", sw.Elapsed.TotalMilliseconds);
        _logger.LogDebug($"{name} {sw.Elapsed:c}");
        return result;
    }

    public int Execute(string name, IDbTransaction transaction, object parameters = null)
    {
        var sw = Stopwatch.StartNew();
        int result;
        try
        {
            result = transaction.Connection.Execute(name, parameters, commandType: CommandType.StoredProcedure);
        }
        catch (Exception e)
        {
            _logger.LogError(e, name);
            throw;
        }

        LogContext.PushProperty("elapsed", sw.Elapsed.TotalMilliseconds);
        _logger.LogDebug($"{name} {sw.Elapsed:c}");
        return result;
    }

    public async Task<T> ExecuteScalarAsync<T>(string name, object parameters)
    {
        T result;
        var sw = Stopwatch.StartNew();
        try
        {
            var conn = HealthCheck();
            await using var connection = new MySqlConnection(conn);
            result = await connection.ExecuteScalarAsync<T>(name, parameters, commandType: CommandType.StoredProcedure);
            await connection.CloseAsync();
            await connection.DisposeAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, name);
            throw;
        }

        LogContext.PushProperty("ElapsedMilliseconds", sw.Elapsed.TotalMilliseconds);
        _logger.LogDebug($"{name} {sw.Elapsed:c}");
        return result;
    }

    #endregion

    private string SerializeDynamicParameters(DynamicParameters parameters)
    {
        var dictionary = new Dictionary<string, object>();
        foreach (var paramName in parameters.ParameterNames)
        {
            var value = parameters.Get<dynamic>(paramName);
            dictionary.Add(paramName, value);
        }

        return JsonConvert.SerializeObject(dictionary);
    }

    private string ReplaceDBString(string conn)
    {
        return conn.Substring(0, 28);
    }
}