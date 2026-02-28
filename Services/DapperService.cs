using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace EmissiView.Services
{
    public class DapperService
    {
        private readonly IConfiguration _configuration;

        public DapperService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private IDbConnection CreateConnection(string dbCharacter)
        {
            if (dbCharacter == "B")
            {
                return new SqlConnection(_configuration["ConnectionStrings:BLAZING"]);
            }
            else
            {
                return new SqlConnection(_configuration["ConnectionStrings:UICT"]);
            }
        }

        public async Task Execute(string dbCharacter, string query, object param = null)
        {
            using var connection = CreateConnection(dbCharacter);
            await connection.ExecuteAsync(query, param);
        }
        public async Task<dynamic> ExecuteScalar(string dbCharacter, string query, object param = null)
        {
            using var connection = CreateConnection(dbCharacter);
            return await connection.ExecuteScalarAsync(query, param);
        }
        public async Task<T> ExecuteScalar<T>(string dbCharacter, string query, object param = null)
        {
            using var connection = CreateConnection(dbCharacter);
            return await connection.ExecuteScalarAsync<T>(query, param);
        }
        public async Task<IEnumerable<dynamic>> Query(string dbCharacter, string query, object param = null)
        {
            using var connection = CreateConnection(dbCharacter);
            return await connection.QueryAsync<dynamic>(query, param);
        }
        public async Task<IEnumerable<T>> Query<T>(string dbCharacter, string query, object param = null)
        {
            using var connection = CreateConnection(dbCharacter);
            return await connection.QueryAsync<T>(query, param);
        }
        public async Task<dynamic> QueryFirst(string dbCharacter, string query, object param = null)
        {
            using var connection = CreateConnection(dbCharacter);
            return await connection.QueryFirstOrDefaultAsync<dynamic>(query, param);
        }
        public async Task<T> QueryFirst<T>(string dbCharacter, string query, object param = null)
        {
            using var connection = CreateConnection(dbCharacter);
            return await connection.QueryFirstOrDefaultAsync<T>(query, param);
        }
        public T QueryFirstSync<T>(string dbCharacter, string query, object param = null)
        {
            using var connection = CreateConnection(dbCharacter);
            return connection.QueryFirstOrDefault<T>(query, param);
        }
        // ADD BY MODEL
        public async Task<bool> Insert<T>(string dbCharacter, T model) where T : class
        {
            using var connection = CreateConnection(dbCharacter);
            await connection.InsertAsync(model);
            return true;
        }

        public async Task<bool> Update<T>(string dbCharacter, T model) where T : class
        {
            using var connection = CreateConnection(dbCharacter);
            await connection.UpdateAsync(model);
            return true;
        }
    }
}
