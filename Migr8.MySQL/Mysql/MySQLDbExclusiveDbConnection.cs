﻿using System;
using System.Collections.Generic;
using System.Data;
using Migr8.Internals;
using MySql.Data.MySqlClient;

namespace Migr8.Mysql.Mysql
{
    class MysqlDbExclusiveDbConnection : IExclusiveDbConnection
    {
        readonly MySqlConnection _connection;
        readonly MySqlTransaction _transaction;

        public MysqlDbExclusiveDbConnection(string connectionString)
        {
            _connection = new MySqlConnection(connectionString);
            _connection.Open();
            _transaction = _connection.BeginTransaction(IsolationLevel.Serializable);
        }

        public void Dispose()
        {
            _transaction.Dispose();
            _connection.Dispose();
        }

        public void Complete()
        {
            _transaction.Commit();
        }

        public HashSet<string> GetTableNames()
        {
            var tableNames = new HashSet<string>();

            using (var command = CreateCommand())
            {
                
                command.CommandText = $@"SELECT * FROM information_schema.tables WHERE table_schema = '{_connection.DataSource}'";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tableNames.Add(reader["table_name"].ToString());
                    }
                }
            }

            return tableNames;
        }

        public void LogMigration(IExecutableSqlMigration migration, string migrationTableName)
        {
            using (var command = CreateCommand())
            {
                command.CommandText = $@"
INSERT INTO `{migrationTableName}` (
    `MigrationId`,
    `Sql`,
    `Description`,
    `Time`,
    `UserName`,
    `UserDomainName`,
    `MachineName`
) VALUES (
    @id,
    @sql,
    @description,
    @time,
    @userName,
    @userDomainName,
    @machineName
)
";                
                command.Parameters.Add("id", MySqlDbType.Text).Value = migration.Id;
                command.Parameters.Add("sql", MySqlDbType.Text).Value = migration.Sql;
                command.Parameters.Add("description", MySqlDbType.Text).Value = migration.Description;
                command.Parameters.Add("time", MySqlDbType.Timestamp).Value = DateTime.Now;
                command.Parameters.Add("userName", MySqlDbType.Text).Value = Environment.GetEnvironmentVariable("USERNAME") ?? "??";
                command.Parameters.Add("userDomainName", MySqlDbType.Text).Value = Environment.GetEnvironmentVariable("USERDOMAIN") ?? "??";
                command.Parameters.Add("machineName", MySqlDbType.Text).Value = Environment.MachineName;

                command.ExecuteNonQuery();
            }
        }

        public void CreateMigrationTable(string migrationTableName)
        {
            using (var command = CreateCommand())
            {
                command.CommandText =
                    $@"
                    CREATE TABLE IF NOT EXISTS `{migrationTableName}` (
                        `Id` BIGINT NOT NULL AUTO_INCREMENT,
                        `MigrationId` VARCHAR(100) NOT NULL,
                        `Sql` TEXT NOT NULL,
                        `Description` TEXT NOT NULL,
                        `Time` TIMESTAMP NOT NULL,
                        `UserName` TEXT NOT NULL,
                        `UserDomainName` TEXT NOT NULL,
                        `MachineName` TEXT NOT NULL,
                        PRIMARY KEY (`Id`),
                        UNIQUE KEY `UNIQUE_{migrationTableName}_MigrationId` (`MigrationId`)                        
                    );
                    ";

                command.ExecuteNonQuery();
            }
            
        }

        public void ExecuteStatement(string sqlStatement)
        {
            using (var command = CreateCommand())
            {
                command.CommandText = sqlStatement;
                command.ExecuteNonQuery();
            }
        }

        public IEnumerable<string> GetExecutedMigrationIds(string migrationTableName)
        {
            var list = new List<string>();
            using (var command = CreateCommand())
            {
                command.CommandText = $@"SELECT `MigrationId` FROM `{migrationTableName}`";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add((string)reader["MigrationId"]);
                    }
                }
            }
            return list;

        }

        MySqlCommand CreateCommand()
        {
            var sqlCommand = _connection.CreateCommand();
            sqlCommand.Transaction = _transaction;
            sqlCommand.CommandTimeout = 60 * 10;//10 minutes
            return sqlCommand;
        }
    }
}