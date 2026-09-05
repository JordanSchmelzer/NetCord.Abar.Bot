using System;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;


namespace NetCord.Abar.Bot.Database
{
    public static class DbUtil
    {
        public static bool InitializeDatabase(string connStr)
        {
            using var conn = new SqliteConnection(connStr);
            conn.Open();

            // Define and add queries here.
            List<string> cmdSqls = new();
            string createTableSql = @"
                CREATE TABLE IF NOT EXISTS Sounds (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    searchTerm STRING NOT NULL,
                    resourcePath STRING
                );";
            cmdSqls.Add(createTableSql);

            // Execute each query in the list
            foreach (string sqlStr in cmdSqls)
            {
                using var cmd = new SqliteCommand(sqlStr, conn);
                cmd.ExecuteNonQuery();
            }

            return true;
        }
    }
}
