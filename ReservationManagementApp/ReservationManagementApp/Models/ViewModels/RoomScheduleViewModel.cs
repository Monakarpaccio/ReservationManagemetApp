using System.Collections.Generic;
using ReservationManagementApp.Models.Entities;

namespace ReservationManagementApp.Models.ViewModels
{
    public class RoomScheduleViewModel
    {
        public RoomScheduleViewModel()
        {
            Reservations = new List<Reservation>();
        }

        public string RoomId { get; set; }
        public string RoomName { get; set; }
        public int Capacity { get; set; }
        public int HourlyRate { get; set; }
        public List<Reservation> Reservations { get; set; }
    }
}
