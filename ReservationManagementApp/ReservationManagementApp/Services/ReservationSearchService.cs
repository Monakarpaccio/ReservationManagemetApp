using System;
using System.Collections.Generic;
using ReservationManagementApp.DataAccess;
using ReservationManagementApp.Models.Entities;
using ReservationManagementApp.Models.ViewModels;

namespace ReservationManagementApp.Services
{
    public class ReservationSearchService
    {
        private const int MaximumTemporaryReservations = 3;
        private static readonly TimeSpan BusinessOpeningTime = new TimeSpan(9, 0, 0);
        private static readonly TimeSpan BusinessClosingTime = new TimeSpan(18, 0, 0);
        private static readonly TimeSpan ReservationUnit = TimeSpan.FromMinutes(15);

        private readonly FacilityDao _facilityDao;
        private readonly RoomDao _roomDao;
        private readonly ReservationDao _reservationDao;

        public ReservationSearchService(SQLiteConnectionFactory connectionFactory)
        {
            if (connectionFactory == null)
            {
                throw new ArgumentNullException("connectionFactory");
            }

            _facilityDao = new FacilityDao(connectionFactory);
            _roomDao = new RoomDao(connectionFactory);
            _reservationDao = new ReservationDao(connectionFactory);
        }

        public bool CanCreateTemporaryReservation(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            return _reservationDao.CountTemporaryReservations(userId)
                < MaximumTemporaryReservations;
        }

        public ReservationScheduleViewModel GetReservationSchedule(
            string facilityId,
            DateTime date,
            int? capacity)
        {
            if (string.IsNullOrWhiteSpace(facilityId)
                || (capacity.HasValue && capacity.Value < 1))
            {
                return null;
            }

            var facility = _facilityDao.FindById(facilityId);
            if (facility == null)
            {
                return null;
            }

            var schedule = new ReservationScheduleViewModel
            {
                FacilityId = facility.FacilityId,
                FacilityName = facility.FacilityName,
                Date = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified),
                Capacity = capacity
            };

            var rooms = _roomDao.FindByFacilityId(facilityId, capacity);
            foreach (var room in rooms)
            {
                schedule.Rooms.Add(new RoomScheduleViewModel
                {
                    RoomId = room.RoomId,
                    RoomName = room.RoomName,
                    Capacity = room.Capacity,
                    HourlyRate = room.HourlyRate,
                    Reservations = _reservationDao.FindByRoomAndDate(room.RoomId, date)
                });
            }

            return schedule;
        }

        public List<Room> SearchAvailableRoomsForTemporary(
            string facilityId,
            DateTime date,
            TimeSpan startTime,
            TimeSpan endTime,
            int? capacity,
            string userId)
        {
            var availableRooms = new List<Room>();
            if (string.IsNullOrWhiteSpace(facilityId)
                || (capacity.HasValue && capacity.Value < 1)
                || !CanCreateTemporaryReservation(userId)
                || !IsValidReservationTime(startTime, endTime))
            {
                return availableRooms;
            }

            var startDateTime = CombineDateAndTime(date, startTime);
            var endDateTime = CombineDateAndTime(date, endTime);
            var rooms = _roomDao.FindByFacilityId(facilityId, capacity);

            foreach (var room in rooms)
            {
                if (_reservationDao.IsRoomAvailable(
                    room.RoomId,
                    startDateTime,
                    endDateTime))
                {
                    availableRooms.Add(room);
                }
            }

            return availableRooms;
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
    }
}
