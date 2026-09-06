using System;
using System.Collections.Generic;

namespace ReservationManagementApp.Models.Entities
{
    public class Reservation
    {
        public Reservation()
        {
            Participants = new List<Participant>();
        }

        public int ReservationId { get; set; }
        public string UserId { get; set; }
        public string RoomId { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string Title { get; set; }
        public string Remarks { get; set; }
        public ReservationStatus Status { get; set; }
        public int? TotalPrice { get; set; }
        public List<Participant> Participants { get; set; }
    }
}
