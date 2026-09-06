using System;
using System.Collections.Generic;
using System.Data.SQLite;
using ReservationManagementApp.DataAccess;
using ReservationManagementApp.Models.Entities;
using ReservationManagementApp.Models.ViewModels;

namespace ReservationManagementApp.Services
{
    public class ReservationService
    {
        private const int MaximumTemporaryReservations = 3;
        private static readonly TimeSpan BusinessOpeningTime = new TimeSpan(9, 0, 0);
        private static readonly TimeSpan BusinessClosingTime = new TimeSpan(18, 0, 0);
        private static readonly TimeSpan ReservationUnit = TimeSpan.FromMinutes(15);

        private readonly SQLiteConnectionFactory _connectionFactory;

        public ReservationService(SQLiteConnectionFactory connectionFactory)
        {
            if (connectionFactory == null)
            {
                throw new ArgumentNullException("connectionFactory");
            }

            _connectionFactory = connectionFactory;
        }

        public bool CreateReservation(ReservationInputViewModel model, string userId)
        {
            DateTime startDateTime;
            DateTime endDateTime;

            if (!TryGetReservationRange(model, out startDateTime, out endDateTime))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(model.Title))
            {
                return false;
            }

            using (SQLiteConnection connection = _connectionFactory.OpenConnection())
            using (SQLiteTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    // 予約と参加者を同じTransactionで処理するため、
                    // すべてのDAOへ同じConnectionとTransactionを渡します。
                    RoomDao roomDao = new RoomDao(connection, transaction);
                    ReservationDao reservationDao = new ReservationDao(connection, transaction);
                    ParticipantDao participantDao = new ParticipantDao(connection, transaction);

                    // 検索後に別の予約が登録されている可能性があるため、
                    // 登録直前にも会議室の空きを確認します。
                    if (!reservationDao.IsRoomAvailable(model.RoomId, startDateTime, endDateTime))
                    {
                        transaction.Rollback();
                        return false;
                    }

                    Room room = roomDao.FindById(model.RoomId);
                    if (room == null)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    int totalPrice = CalculateTotalPrice(
                        room.HourlyRate,
                        startDateTime,
                        endDateTime);

                    List<Participant> participants = CopyParticipants(model.Participants);

                    Reservation reservation = new Reservation
                    {
                        UserId = userId,
                        RoomId = room.RoomId,
                        StartDateTime = startDateTime,
                        EndDateTime = endDateTime,
                        Title = model.Title,
                        Remarks = model.Remarks,
                        Status = ReservationStatus.Confirmed,
                        TotalPrice = totalPrice,
                        Participants = participants
                    };

                    int reservationId = reservationDao.Insert(reservation);
                    if (reservationId <= 0)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    participantDao.InsertAll(reservationId, reservation.Participants);
                    model.ReservationId = reservationId;
                    model.TotalPrice = reservation.TotalPrice;

                    transaction.Commit();
                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public bool CreateTemporaryReservation(
            string roomId,
            DateTime date,
            TimeSpan startTime,
            TimeSpan endTime,
            string userId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            if (!IsValidReservationTime(startTime, endTime))
            {
                return false;
            }

            DateTime startDateTime = CombineDateAndTime(date, startTime);
            DateTime endDateTime = CombineDateAndTime(date, endTime);

            using (SQLiteConnection connection = _connectionFactory.OpenConnection())
            using (SQLiteTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    RoomDao roomDao = new RoomDao(connection, transaction);
                    ReservationDao reservationDao = new ReservationDao(connection, transaction);

                    // 検索時だけでなく、登録直前にも仮予約の件数を確認します。
                    int temporaryReservationCount =
                        reservationDao.CountTemporaryReservations(userId);

                    if (temporaryReservationCount >= MaximumTemporaryReservations)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    Room room = roomDao.FindById(roomId);
                    if (room == null)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    if (!reservationDao.IsRoomAvailable(roomId, startDateTime, endDateTime))
                    {
                        transaction.Rollback();
                        return false;
                    }

                    Reservation reservation = new Reservation
                    {
                        UserId = userId,
                        RoomId = roomId,
                        StartDateTime = startDateTime,
                        EndDateTime = endDateTime,
                        Title = null,
                        Remarks = null,
                        Status = ReservationStatus.Tentative,
                        TotalPrice = null
                    };

                    int reservationId = reservationDao.Insert(reservation);
                    if (reservationId <= 0)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    transaction.Commit();
                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public bool ConfirmTemporaryReservation(
            int reservationId,
            ReservationInputViewModel model,
            string userId)
        {
            if (model == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(model.Title))
            {
                return false;
            }

            using (SQLiteConnection connection = _connectionFactory.OpenConnection())
            using (SQLiteTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    RoomDao roomDao = new RoomDao(connection, transaction);
                    ReservationDao reservationDao = new ReservationDao(connection, transaction);
                    ParticipantDao participantDao = new ParticipantDao(connection, transaction);

                    Reservation reservation = reservationDao.FindById(reservationId);
                    if (reservation == null)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    if (reservation.UserId != userId)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    if (reservation.Status != ReservationStatus.Tentative)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    if (reservation.StartDateTime.Date != reservation.EndDateTime.Date)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    bool isValidReservationTime = IsValidReservationTime(
                        reservation.StartDateTime.TimeOfDay,
                        reservation.EndDateTime.TimeOfDay);

                    if (!isValidReservationTime)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    // 変更対象の予約自身は、重複予約の判定から除外します。
                    bool isRoomAvailable = reservationDao.IsRoomAvailable(
                        reservation.RoomId,
                        reservation.StartDateTime,
                        reservation.EndDateTime,
                        reservation.ReservationId);

                    if (!isRoomAvailable)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    Room room = roomDao.FindById(reservation.RoomId);
                    if (room == null)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    reservation.Title = model.Title;
                    reservation.Remarks = model.Remarks;
                    reservation.Status = ReservationStatus.Confirmed;
                    reservation.TotalPrice = CalculateTotalPrice(
                        room.HourlyRate,
                        reservation.StartDateTime,
                        reservation.EndDateTime);
                    reservation.Participants = CopyParticipants(model.Participants);

                    if (!reservationDao.Update(reservation))
                    {
                        transaction.Rollback();
                        return false;
                    }

                    // 参加者の入れ替えも予約更新と同じTransactionで行います。
                    participantDao.DeleteByReservationId(reservation.ReservationId);
                    participantDao.InsertAll(reservation.ReservationId, reservation.Participants);
                    model.ReservationId = reservation.ReservationId;
                    model.TotalPrice = reservation.TotalPrice;

                    transaction.Commit();
                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public bool UpdateReservation(ReservationInputViewModel model, string userId)
        {
            DateTime startDateTime;
            DateTime endDateTime;

            if (!TryGetReservationRange(model, out startDateTime, out endDateTime))
            {
                return false;
            }

            if (!model.ReservationId.HasValue)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(model.Title))
            {
                return false;
            }

            using (SQLiteConnection connection = _connectionFactory.OpenConnection())
            using (SQLiteTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    RoomDao roomDao = new RoomDao(connection, transaction);
                    ReservationDao reservationDao = new ReservationDao(connection, transaction);
                    ParticipantDao participantDao = new ParticipantDao(connection, transaction);

                    Reservation reservation =
                        reservationDao.FindById(model.ReservationId.Value);

                    if (reservation == null)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    if (reservation.UserId != userId)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    if (reservation.Status != ReservationStatus.Confirmed)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    // 変更対象の予約自身は、重複予約の判定から除外します。
                    bool isRoomAvailable = reservationDao.IsRoomAvailable(
                        model.RoomId,
                        startDateTime,
                        endDateTime,
                        reservation.ReservationId);

                    if (!isRoomAvailable)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    Room room = roomDao.FindById(model.RoomId);
                    if (room == null)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    reservation.RoomId = room.RoomId;
                    reservation.StartDateTime = startDateTime;
                    reservation.EndDateTime = endDateTime;
                    reservation.Title = model.Title;
                    reservation.Remarks = model.Remarks;
                    reservation.TotalPrice = CalculateTotalPrice(
                        room.HourlyRate,
                        startDateTime,
                        endDateTime);
                    reservation.Participants = CopyParticipants(model.Participants);

                    if (!reservationDao.Update(reservation))
                    {
                        transaction.Rollback();
                        return false;
                    }

                    // 参加者の入れ替えも予約更新と同じTransactionで行います。
                    participantDao.DeleteByReservationId(reservation.ReservationId);
                    participantDao.InsertAll(reservation.ReservationId, reservation.Participants);
                    model.TotalPrice = reservation.TotalPrice;

                    transaction.Commit();
                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public bool CancelReservation(int reservationId, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            var reservationDao = new ReservationDao(_connectionFactory);
            var reservation = reservationDao.FindById(reservationId);

            return reservation != null
                && reservation.UserId == userId
                && reservationDao.UpdateStatus(reservationId, ReservationStatus.Cancelled);
        }

        private static bool TryGetReservationRange(
            ReservationInputViewModel model,
            out DateTime startDateTime,
            out DateTime endDateTime)
        {
            startDateTime = default(DateTime);
            endDateTime = default(DateTime);

            if (model == null
                || string.IsNullOrWhiteSpace(model.RoomId)
                || !IsValidReservationTime(model.StartTime, model.EndTime))
            {
                return false;
            }

            startDateTime = CombineDateAndTime(model.Date, model.StartTime);
            endDateTime = CombineDateAndTime(model.Date, model.EndTime);
            return true;
        }

        private static bool IsValidReservationTime(TimeSpan startTime, TimeSpan endTime)
        {
            return startTime >= BusinessOpeningTime
                && endTime <= BusinessClosingTime
                && startTime < endTime
                && startTime.Ticks % ReservationUnit.Ticks == 0
                && endTime.Ticks % ReservationUnit.Ticks == 0;
        }

        private static DateTime CombineDateAndTime(DateTime date, TimeSpan time)
        {
            return DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified);
        }

        private static int CalculateTotalPrice(
            int hourlyRate,
            DateTime startDateTime,
            DateTime endDateTime)
        {
            var slotCount = (endDateTime - startDateTime).Ticks / ReservationUnit.Ticks;
            return checked((hourlyRate / 4) * (int)slotCount);
        }

        private static List<Participant> CopyParticipants(IEnumerable<Participant> participants)
        {
            return participants == null
                ? new List<Participant>()
                : new List<Participant>(participants);
        }
    }
}
