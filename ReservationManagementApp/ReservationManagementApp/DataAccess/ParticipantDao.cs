using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using ReservationManagementApp.Models.Entities;

namespace ReservationManagementApp.DataAccess
{
    public class ParticipantDao : DaoBase
    {
        public ParticipantDao(SQLiteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        public ParticipantDao(SQLiteConnection connection, SQLiteTransaction transaction)
            : base(connection, transaction)
        {
        }

        public List<Participant> FindByReservationId(int reservationId)
        {
            const string sql = @"
SELECT participant_name, company_name, is_private
FROM participants
WHERE reservation_id = @reservationId
ORDER BY participant_name, company_name;";

            return Execute((connection, transaction) =>
            {
                var participants = new List<Participant>();

                using (var command = CreateCommand(connection, transaction, sql))
                {
                    AddParameter(command, "@reservationId", DbType.Int32, reservationId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            participants.Add(new Participant
                            {
                                Name = reader.GetString(0),
                                CompanyName = reader.GetString(1),
                                IsPrivate = reader.GetInt32(2) == 1
                            });
                        }
                    }
                }

                return participants;
            });
        }

        public bool InsertAll(int reservationId, List<Participant> participants)
        {
            if (participants == null)
            {
                throw new ArgumentNullException("participants");
            }

            return Execute((connection, transaction) =>
            {
                if (transaction != null)
                {
                    InsertParticipants(connection, transaction, reservationId, participants);
                    return true;
                }

                using (var localTransaction = connection.BeginTransaction())
                {
                    try
                    {
                        InsertParticipants(connection, localTransaction, reservationId, participants);
                        localTransaction.Commit();
                        return true;
                    }
                    catch
                    {
                        localTransaction.Rollback();
                        throw;
                    }
                }
            });
        }

        public int DeleteByReservationId(int reservationId)
        {
            const string sql = @"
DELETE FROM participants
WHERE reservation_id = @reservationId;";

            return Execute((connection, transaction) =>
            {
                using (var command = CreateCommand(connection, transaction, sql))
                {
                    AddParameter(command, "@reservationId", DbType.Int32, reservationId);
                    return command.ExecuteNonQuery();
                }
            });
        }

        private static void InsertParticipants(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            int reservationId,
            IEnumerable<Participant> participants)
        {
            const string sql = @"
INSERT INTO participants
    (reservation_id, participant_name, company_name, is_private)
VALUES
    (@reservationId, @participantName, @companyName, @isPrivate);";

            foreach (var participant in participants)
            {
                if (participant == null)
                {
                    throw new ArgumentException("The participant list must not contain null.", "participants");
                }

                using (var command = CreateCommand(connection, transaction, sql))
                {
                    AddParameter(command, "@reservationId", DbType.Int32, reservationId);
                    AddParameter(command, "@participantName", DbType.String, participant.Name);
                    AddParameter(command, "@companyName", DbType.String, participant.CompanyName ?? string.Empty);
                    AddParameter(command, "@isPrivate", DbType.Int32, participant.IsPrivate ? 1 : 0);
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
