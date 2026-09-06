using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using ReservationManagementApp.Models.Entities;

namespace ReservationManagementApp.DataAccess
{
    public class ReservationDao : DaoBase
    {
        public ReservationDao(SQLiteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        public ReservationDao(SQLiteConnection connection, SQLiteTransaction transaction)
            : base(connection, transaction)
        {
        }

        public Reservation FindById(int reservationId)
        {
            const string sql = @"
SELECT reservation_id, user_id, room_id, start_datetime, end_datetime,
       title, remarks, status, total_price
FROM reservations
WHERE reservation_id = @reservationId;";

            return Execute((connection, transaction) =>
            {
                using (var command = CreateCommand(connection, transaction, sql))
                {
                    AddParameter(command, "@reservationId", DbType.Int32, reservationId);

                    using (var reader = command.ExecuteReader())
                    {
                        return reader.Read() ? MapReservation(reader) : null;
                    }
                }
            });
        }

        public List<Reservation> FindByRoomAndDate(string roomId, DateTime date)
        {
            const string sql = @"
SELECT reservation_id, user_id, room_id, start_datetime, end_datetime,
       title, remarks, status, total_price
FROM reservations
WHERE room_id = @roomId
  AND start_datetime < @dayEnd
  AND end_datetime > @dayStart
  AND status <> @cancelledStatus
ORDER BY start_datetime, reservation_id;";

            var dayStart = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
            var dayEnd = dayStart.AddDays(1);

            return Execute((connection, transaction) =>
            {
                var reservations = new List<Reservation>();

                using (var command = CreateCommand(connection, transaction, sql))
                {
                    AddParameter(command, "@roomId", DbType.String, roomId);
                    AddParameter(command, "@dayStart", DbType.String, ToDatabaseDateTime(dayStart));
                    AddParameter(command, "@dayEnd", DbType.String, ToDatabaseDateTime(dayEnd));
                    AddParameter(command, "@cancelledStatus", DbType.Int32, (int)ReservationStatus.Cancelled);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            reservations.Add(MapReservation(reader));
                        }
                    }
                }

                return reservations;
            });
        }

        public List<Reservation> FindByUserAndStatus(string userId, ReservationStatus status)
        {
            const string sql = @"
SELECT reservation_id, user_id, room_id, start_datetime, end_datetime,
       title, remarks, status, total_price
FROM reservations
WHERE user_id = @userId
  AND status = @status
ORDER BY start_datetime, reservation_id;";

            return Execute((connection, transaction) =>
            {
                var reservations = new List<Reservation>();

                using (var command = CreateCommand(connection, transaction, sql))
                {
                    AddParameter(command, "@userId", DbType.String, userId);
                    AddParameter(command, "@status", DbType.Int32, (int)status);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            reservations.Add(MapReservation(reader));
                        }
                    }
                }

                return reservations;
            });
        }

        public int CountTemporaryReservations(string userId)
        {
            const string sql = @"
SELECT COUNT(*)
FROM reservations
WHERE user_id = @userId
  AND status = @tentativeStatus;";

            return Execute((connection, transaction) =>
            {
                using (var command = CreateCommand(connection, transaction, sql))
                {
                    AddParameter(command, "@userId", DbType.String, userId);
                    AddParameter(command, "@tentativeStatus", DbType.Int32, (int)ReservationStatus.Tentative);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            });
        }

        public bool IsRoomAvailable(
            string roomId,
            DateTime start,
            DateTime end,
            int? excludeReservationId = null)
        {
            var sql = @"
SELECT COUNT(*)
FROM reservations
WHERE room_id = @roomId
  AND start_datetime < @requestedEnd
  AND end_datetime > @requestedStart
  AND status <> @cancelledStatus";

            if (excludeReservationId.HasValue)
            {
                sql += @"
  AND reservation_id <> @excludeReservationId";
            }

            sql += ";";

            return Execute((connection, transaction) =>
            {
                using (var command = CreateCommand(connection, transaction, sql))
                {
                    AddParameter(command, "@roomId", DbType.String, roomId);
                    AddParameter(command, "@requestedStart", DbType.String, ToDatabaseDateTime(start));
                    AddParameter(command, "@requestedEnd", DbType.String, ToDatabaseDateTime(end));
                    AddParameter(command, "@cancelledStatus", DbType.Int32, (int)ReservationStatus.Cancelled);

                    if (excludeReservationId.HasValue)
                    {
                        AddParameter(
                            command,
                            "@excludeReservationId",
                            DbType.Int32,
                            excludeReservationId.Value);
                    }

                    return Convert.ToInt32(command.ExecuteScalar()) == 0;
                }
            });
        }

        public int Insert(Reservation reservation)
        {
            if (reservation == null)
            {
                throw new ArgumentNullException("reservation");
            }

            const string sql = @"
INSERT INTO reservations
    (user_id, room_id, start_datetime, end_datetime, title, remarks, status, total_price)
VALUES
    (@userId, @roomId, @startDateTime, @endDateTime, @title, @remarks, @status, @totalPrice);
SELECT last_insert_rowid();";

            return Execute((connection, transaction) =>
            {
                using (var command = CreateCommand(connection, transaction, sql))
                {
                    AddReservationParameters(command, reservation);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            });
        }

        public bool Update(Reservation reservation)
        {
            if (reservation == null)
            {
                throw new ArgumentNullException("reservation");
            }

            const string sql = @"
UPDATE reservations
SET user_id = @userId,
    room_id = @roomId,
    start_datetime = @startDateTime,
    end_datetime = @endDateTime,
    title = @title,
    remarks = @remarks,
    status = @status,
    total_price = @totalPrice
WHERE reservation_id = @reservationId;";

            return Execute((connection, transaction) =>
            {
                using (var command = CreateCommand(connection, transaction, sql))
                {
                    AddReservationParameters(command, reservation);
                    AddParameter(command, "@reservationId", DbType.Int32, reservation.ReservationId);
                    return command.ExecuteNonQuery() > 0;
                }
            });
        }

        public bool UpdateStatus(int reservationId, ReservationStatus status)
        {
            const string sql = @"
UPDATE reservations
SET status = @status
WHERE reservation_id = @reservationId;";

            return Execute((connection, transaction) =>
            {
                using (var command = CreateCommand(connection, transaction, sql))
                {
                    AddParameter(command, "@status", DbType.Int32, (int)status);
                    AddParameter(command, "@reservationId", DbType.Int32, reservationId);
                    return command.ExecuteNonQuery() > 0;
                }
            });
        }

        private static void AddReservationParameters(SQLiteCommand command, Reservation reservation)
        {
            AddParameter(command, "@userId", DbType.String, reservation.UserId);
            AddParameter(command, "@roomId", DbType.String, reservation.RoomId);
            AddParameter(
                command,
                "@startDateTime",
                DbType.String,
                ToDatabaseDateTime(reservation.StartDateTime));
            AddParameter(
                command,
                "@endDateTime",
                DbType.String,
                ToDatabaseDateTime(reservation.EndDateTime));
            AddParameter(command, "@title", DbType.String, reservation.Title);
            AddParameter(command, "@remarks", DbType.String, reservation.Remarks);
            AddParameter(command, "@status", DbType.Int32, (int)reservation.Status);
            AddParameter(command, "@totalPrice", DbType.Int32, reservation.TotalPrice);
        }

        private static Reservation MapReservation(SQLiteDataReader reader)
        {
            return new Reservation
            {
                ReservationId = reader.GetInt32(0),
                UserId = reader.GetString(1),
                RoomId = reader.GetString(2),
                StartDateTime = FromDatabaseDateTime(reader.GetString(3)),
                EndDateTime = FromDatabaseDateTime(reader.GetString(4)),
                Title = reader.IsDBNull(5) ? null : reader.GetString(5),
                Remarks = reader.IsDBNull(6) ? null : reader.GetString(6),
                Status = (ReservationStatus)reader.GetInt32(7),
                TotalPrice = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8)
            };
        }
    }
}
