using System;
using System.Collections.Generic;
using ReservationManagementApp.Models.Entities;

namespace ReservationManagementApp.Models.ViewModels
{
    public class ReservationDetailViewModel
    {
        public ReservationDetailViewModel()
        {
            Participants = new List<Participant>();
        }

        public int ReservationId { get; set; }
        public string FacilityName { get; set; }
        public string RoomId { get; set; }
        public string RoomName { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string Title { get; set; }
        public List<Participant> Participants { get; set; }
        public string Remarks { get; set; }
        public ReservationStatus Status { get; set; }
        public int? TotalPrice { get; set; }
    }
}
