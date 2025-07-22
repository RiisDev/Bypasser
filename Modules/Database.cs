using Microsoft.Data.Sqlite;

namespace Bypasser.Modules
{
    public class Database
    {
        private const string DbPath = "urlMappings.db";

        public Database()
        {
            using SqliteConnection connection = new($"Data Source={DbPath}");
            connection.Open();

            SqliteCommand tableCmd = connection.CreateCommand();
            tableCmd.CommandText =
                """
                    CREATE TABLE IF NOT EXISTS UrlMappings (
                        InputUrl TEXT PRIMARY KEY,
                        MappedUrl TEXT NOT NULL
                    );
                """;
            tableCmd.ExecuteNonQuery();
        }

        public void InsertOrUpdate(string inputUrl, string mappedUrl)
        {
            using SqliteConnection connection = new($"Data Source={DbPath}");
            connection.Open();

            SqliteCommand cmd = connection.CreateCommand();
            cmd.CommandText = """
                                  INSERT INTO UrlMappings (InputUrl, MappedUrl) VALUES ($inputUrl, $mappedUrl)
                                  ON CONFLICT(InputUrl) DO UPDATE SET MappedUrl = excluded.MappedUrl;
                              """;
            cmd.Parameters.AddWithValue("$inputUrl", inputUrl);
            cmd.Parameters.AddWithValue("$mappedUrl", mappedUrl);

            cmd.ExecuteNonQuery();
        }

        public string? Fetch(string inputUrl)
        {
            using SqliteConnection connection = new($"Data Source={DbPath}");
            connection.Open();

            SqliteCommand cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MappedUrl FROM UrlMappings WHERE InputUrl = $inputUrl";
            cmd.Parameters.AddWithValue("$inputUrl", inputUrl);

            using SqliteDataReader reader = cmd.ExecuteReader();
            return reader.Read() ? reader.GetString(0) : null;
        }
    }
}
