﻿using System;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using DuckDB.NET.Data;
using static DuckDB.NET.Windows.NativeMethods;
using Dapper;

namespace DuckDB.NET.Samples
{
    class Program
    {
        static void Main(string[] args)
        {
            AdoNetSamples();

            LowLevelBindingsSample();
        }

        private static void AdoNetSamples()
        {
            if (File.Exists("file.db"))
            {
                File.Delete("file.db");
            }

            using var duckDBConnection = new DuckDBConnection("Data Source=file.db");
            duckDBConnection.Open();

            var command = duckDBConnection.CreateCommand();

            command.CommandText = "CREATE TABLE integers(foo INTEGER, bar INTEGER);";
            var executeNonQuery = command.ExecuteNonQuery();

            command.CommandText = "INSERT INTO integers VALUES (3, 4), (5, 6), (7, NULL);";
            executeNonQuery = command.ExecuteNonQuery();

            command.CommandText = "Select count(*) from integers";
            var executeScalar = command.ExecuteScalar();

            command.CommandText = "SELECT foo, bar FROM integers";
            var reader = command.ExecuteReader();
            PrintQueryResults(reader);

            var results = duckDBConnection.Query<FooBar>("SELECT foo, bar FROM integers");

            try
            {
                command.CommandText = "Not a valid Sql statement";
                var causesError = command.ExecuteNonQuery();
            }
            catch (DuckDBException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static void LowLevelBindingsSample()
        {
            var result = DuckDBOpen(null, out var database);

            using (database)
            {
                result = DuckDBConnect(database, out var connection);
                using (connection)
                {
                    var queryResult = new DuckDBResult();
                    result = DuckDBQuery(connection, "CREATE TABLE integers(foo INTEGER, bar INTEGER);", null);
                    result = DuckDBQuery(connection, "INSERT INTO integers VALUES (3, 4), (5, 6), (7, NULL);", null);
                    result = DuckDBQuery(connection, "SELECT foo, bar FROM integers", queryResult);

                    PrintQueryResults(queryResult);

                    // clean up
                    DuckDBDestroyResult(queryResult);

                    result = DuckDBPrepare(connection, "INSERT INTO integers VALUES (?, ?)", out var insertStatement);

                    using (insertStatement)
                    {
                        result = DuckDBBindInt32(insertStatement, 1, 42); // the parameter index starts counting at 1!
                        result = DuckDBBindInt32(insertStatement, 2, 43);

                        result = DuckDBExecutePrepared(insertStatement, null);
                    }


                    result = DuckDBPrepare(connection, "SELECT * FROM integers WHERE foo = ?", out var selectStatement);

                    using (selectStatement)
                    {
                        result = DuckDBBindInt32(selectStatement, 1, 42);

                        result = DuckDBExecutePrepared(selectStatement, queryResult);
                    }

                    PrintQueryResults(queryResult);

                    // clean up
                    DuckDBDestroyResult(queryResult);
                }
            }
        }

        private static void PrintQueryResults(DbDataReader queryResult)
        {
            for (var index = 0; index < queryResult.FieldCount; index++)
            {
                var column = queryResult.GetName(index);
                Console.Write($"{column} ");
            }

            Console.WriteLine();
            
            while (queryResult.Read())
            {
                for (int ordinal = 0; ordinal < queryResult.FieldCount; ordinal++)
                {
                    var val = queryResult.GetValue(ordinal);
                    Console.Write(val);
                    Console.Write(" ");
                }

                Console.WriteLine();
            }
        }

        private static void PrintQueryResults(DuckDBResult queryResult)
        {
            var columnCount = DuckDBColumnCount(queryResult);
            for (var index = 0; index < columnCount; index++)
            {
                var columnName = DuckDBColumnName(queryResult, index).ToManagedString(false);
                Console.Write($"{columnName} ");
            }

            Console.WriteLine();

            var rowCount = DuckDBRowCount(queryResult);
            for (long row = 0; row < rowCount; row++)
            {
                for (long column = 0; column < columnCount; column++)
                {
                    var val = DuckDBValueInt32(queryResult, column, row);
                    Console.Write(val);
                    Console.Write(" ");
                }

                Console.WriteLine();
            }
        }
    }

    class FooBar
    {
        public int Foo { get; set; }
        public int Bar { get; set; }
    }
}
