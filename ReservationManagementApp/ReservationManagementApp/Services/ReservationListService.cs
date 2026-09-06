using System;
using System.Collections.Generic;
using ReservationManagementApp.DataAccess;
using ReservationManagementApp.Models.Entities;
using ReservationManagementApp.Models.ViewModels;

namespace ReservationManagementApp.Services
{
    public class ReservationListService
    {
        private readonly FacilityDao _facilityDao;
        private readonly RoomDao _roomDao;
        private readonly ReservationDao _reservationDao;
        private readonly ParticipantDao _participantDao;

        public ReservationListService(SQLiteConnectionFactory connectionFactory)
        {
            if (connectionFactory == null)
            {
                throw new ArgumentNullException("connectionFactory");
            }

            _facilityDao = new FacilityDao(connectionFactory);
            _roomDao = new RoomDao(connectionFactory);
            _reservationDao = new ReservationDao(connectionFactory);
            _participantDao = new ParticipantDao(connectionFactory);
        }

        public List<ReservationDetailViewModel> GetReservationList(
            string userId,
            ReservationStatus status)
        {
            var details = new List<ReservationDetailViewModel>();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return details;
            }

            var reservations = _reservationDao.FindByUserAndStatus(userId, status);
            foreach (var reservation in reservations)
            {
                var room = _roomDao.FindById(reservation.RoomId);
                if (room == null)
                {
                    continue;
                }

                var facility = _facilityDao.FindById(room.FacilityId);
                if (facility == null)
                {
                    continue;
                }

                var participants = _participantDao.FindByReservationId(
                    reservation.ReservationId);
                reservation.Participants = participants;

                details.Add(new ReservationDetailViewModel
                {
                    ReservationId = reservation.ReservationId,
                    FacilityName = facility.FacilityName,
                    RoomId = room.RoomId,
                    RoomName = room.RoomName,
                    StartDateTime = reservation.StartDateTime,
                    EndDateTime = reservation.EndDateTime,
                    Title = reservation.Title,
                    Participants = participants,
                    Remarks = reservation.Remarks,
                    Status = reservation.Status,
                    TotalPrice = reservation.TotalPrice
                });
            }

            return details;
        }

        public List<Facility> GetFacilities()
        {
            return _facilityDao.FindAll();
        }

        public List<ReservationDetailViewModel> GetCurrentConfirmedReservations(
            string userId)
        {
            var currentDateTime = DateTime.Now;
            var confirmedReservations = GetReservationList(
                userId,
                ReservationStatus.Confirmed);
            var currentReservations = new List<ReservationDetailViewModel>();

            foreach (var reservation in confirmedReservations)
            {
                if (reservation.EndDateTime >= currentDateTime)
                {
                    currentReservations.Add(reservation);
                }
            }

            return currentReservations;
        }

        public List<ReservationDetailViewModel> GetCompletedReservations(
            string userId)
        {
            var currentDateTime = DateTime.Now;
            var confirmedReservations = GetReservationList(
                userId,
                ReservationStatus.Confirmed);
            var completedReservations = new List<ReservationDetailViewModel>();

            foreach (var reservation in confirmedReservations)
            {
                if (reservation.EndDateTime < currentDateTime)
                {
                    completedReservations.Add(reservation);
                }
            }

            return completedReservations;
        }
    }
}
