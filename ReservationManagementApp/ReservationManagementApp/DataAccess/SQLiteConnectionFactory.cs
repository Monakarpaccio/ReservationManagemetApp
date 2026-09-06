using System.Configuration;
using System.Data.SQLite;

namespace ReservationManagementApp.DataAccess
{
    public class SQLiteConnectionFactory
    {
        private const string DefaultConnectionStringName = "ReservationDatabase";
        private readonly string _connectionString;

        public SQLiteConnectionFactory()
        {
            var setting = ConfigurationManager.ConnectionStrings[DefaultConnectionStringName];
            if (setting == null || string.IsNullOrWhiteSpace(setting.ConnectionString))
            {
                throw new ConfigurationErrorsException(
                    string.Format(
                        "Connection string '{0}' was not found.",
                        DefaultConnectionStringName));
            }

            _connectionString = setting.ConnectionString;
        }

        public SQLiteConnectionFactory(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ConfigurationErrorsException("SQLite connection string must not be empty.");
            }

            _connectionString = connectionString;
        }

        public SQLiteConnection OpenConnection()
        {
            var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_keys = ON;";
                command.ExecuteNonQuery();
            }

            return connection;
        }

    }
}
