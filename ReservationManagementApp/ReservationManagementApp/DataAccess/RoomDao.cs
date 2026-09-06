using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using ReservationManagementApp.Models.Entities;

namespace ReservationManagementApp.DataAccess
{
    public class RoomDao : DaoBase
    {
        public RoomDao(SQLiteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        public RoomDao(SQLiteConnection connection, SQLiteTransaction transaction)
            : base(connection, transaction)
        {
        }

        public List<Room> FindByFacilityId(string facilityId, int? capacity)
        {
            const string sql = @"
SELECT room_id, facility_id, room_name, capacity, hourly_rate, equipment, description
FROM rooms
WHERE facility_id = @facilityId
  AND (@capacity IS NULL OR capacity >= @capacity)
ORDER BY room_id;";

            return Execute((connection, transaction) =>
            {
                var rooms = new List<Room>();

                using (var command = CreateCommand(connection, transaction, sql))
                {
                    AddParameter(command, "@facilityId", DbType.String, facilityId);
                    AddParameter(command, "@capacity", DbType.Int32, capacity);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            rooms.Add(MapRoom(reader));
                        }
                    }
                }

                return rooms;
            });
        }

        public Room FindById(string roomId)
        {
            const string sql = @"
SELECT room_id, facility_id, room_name, capacity, hourly_rate, equipment, description
FROM rooms
WHERE room_id = @roomId;";

            return Execute((connection, transaction) =>
            {
                using (var command = CreateCommand(connection, transaction, sql))
                {
                    AddParameter(command, "@roomId", DbType.String, roomId);

                    using (var reader = command.ExecuteReader())
                    {
                        return reader.Read() ? MapRoom(reader) : null;
                    }
                }
            });
        }

        private static Room MapRoom(SQLiteDataReader reader)
        {
            return new Room
            {
                RoomId = reader.GetString(0),
                FacilityId = reader.GetString(1),
                RoomName = reader.GetString(2),
                Capacity = reader.GetInt32(3),
                HourlyRate = reader.GetInt32(4),
                Equipment = reader.IsDBNull(5) ? null : reader.GetString(5),
                Description = reader.IsDBNull(6) ? null : reader.GetString(6)
            };
        }
    }
}
