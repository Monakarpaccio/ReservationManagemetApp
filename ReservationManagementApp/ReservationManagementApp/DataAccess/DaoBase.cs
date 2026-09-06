using System;
using System.Data;
using System.Data.SQLite;
using System.Globalization;

namespace ReservationManagementApp.DataAccess
{
    public abstract class DaoBase
    {
        private readonly SQLiteConnectionFactory _connectionFactory;
        private readonly SQLiteConnection _sharedConnection;
        private readonly SQLiteTransaction _sharedTransaction;
        private static readonly TimeZoneInfo JapanTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
        private const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff";

        protected DaoBase(SQLiteConnectionFactory connectionFactory)
        {
            if (connectionFactory == null)
            {
                throw new ArgumentNullException("connectionFactory");
            }

            _connectionFactory = connectionFactory;
        }

        protected DaoBase(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            if (connection == null)
            {
                throw new ArgumentNullException("connection");
            }

            if (transaction == null)
            {
                throw new ArgumentNullException("transaction");
            }

            if (connection.State != ConnectionState.Open)
            {
                throw new ArgumentException("The shared SQLite connection must be open.", "connection");
            }

            _sharedConnection = connection;
            _sharedTransaction = transaction;
        }

        protected T Execute<T>(Func<SQLiteConnection, SQLiteTransaction, T> operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException("operation");
            }

            if (_sharedConnection != null)
            {
                return operation(_sharedConnection, _sharedTransaction);
            }

            using (var connection = _connectionFactory.OpenConnection())
            {
                return operation(connection, null);
            }
        }

        protected static SQLiteCommand CreateCommand(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            string commandText)
        {
            var command = connection.CreateCommand();
            command.CommandText = commandText;
            command.Transaction = transaction;
            return command;
        }

        protected static void AddParameter(
            SQLiteCommand command,
            string name,
            DbType type,
            object value)
        {
            var parameter = command.Parameters.Add(name, type);
            parameter.Value = value ?? DBNull.Value;
        }

        protected static string ToDatabaseDateTime(DateTime value)
        {
            DateTime japanTime;

            if (value.Kind == DateTimeKind.Utc)
            {
                japanTime = TimeZoneInfo.ConvertTimeFromUtc(value, JapanTimeZone);
            }
            else if (value.Kind == DateTimeKind.Local)
            {
                japanTime = TimeZoneInfo.ConvertTime(value, JapanTimeZone);
            }
            else
            {
                japanTime = value;
            }

            return DateTime.SpecifyKind(japanTime, DateTimeKind.Unspecified)
                .ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        }

        protected static DateTime FromDatabaseDateTime(string value)
        {
            DateTime parsed;
            if (!DateTime.TryParseExact(
                value,
                new[] { DateTimeFormat, "yyyy-MM-dd'T'HH:mm:ss" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsed))
            {
                throw new FormatException("The SQLite date-time value is not a supported ISO-8601 format.");
            }

            return DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        }
    }
}
