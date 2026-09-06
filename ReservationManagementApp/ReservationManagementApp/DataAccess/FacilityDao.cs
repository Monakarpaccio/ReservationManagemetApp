using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using ReservationManagementApp.Models.Entities;

namespace ReservationManagementApp.DataAccess
{
    public class FacilityDao : DaoBase
    {
        public FacilityDao(SQLiteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        public FacilityDao(SQLiteConnection connection, SQLiteTransaction transaction)
            : base(connection, transaction)
        {
        }

        public List<Facility> FindAll()
        {
            const string sql = @"
SELECT facility_id, facility_name, address, description
FROM facilities
ORDER BY facility_id;";

            return Execute((connection, transaction) =>
            {
                var facilities = new List<Facility>();

                using (var command = CreateCommand(connection, transaction, sql))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        facilities.Add(MapFacility(reader));
                    }
                }

                return facilities;
            });
        }

        public Facility FindById(string facilityId)
        {
            const string sql = @"
SELECT facility_id, facility_name, address, description
FROM facilities
WHERE facility_id = @facilityId;";

            return Execute((connection, transaction) =>
            {
                using (var command = CreateCommand(connection, transaction, sql))
                {
                    AddParameter(command, "@facilityId", DbType.String, facilityId);

                    using (var reader = command.ExecuteReader())
                    {
                        return reader.Read() ? MapFacility(reader) : null;
                    }
                }
            });
        }

        private static Facility MapFacility(SQLiteDataReader reader)
        {
            return new Facility
            {
                FacilityId = reader.GetString(0),
                FacilityName = reader.GetString(1),
                Address = reader.IsDBNull(2) ? null : reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3)
            };
        }
    }
}
