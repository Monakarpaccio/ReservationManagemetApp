using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ReservationManagementApp.Models.Entities;

namespace ReservationManagementApp.Models.ViewModels
{
    public class ReservationInputViewModel
    {
        public ReservationInputViewModel()
        {
            Participants = new List<Participant>();
        }

        public int? ReservationId { get; set; }

        public string FacilityName { get; set; }

        [Required]
        public string RoomId { get; set; }

        public string RoomName { get; set; }

        public DateTime Date { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        [Required]
        public string Title { get; set; }

        public List<Participant> Participants { get; set; }

        public string Remarks { get; set; }

        public int? TotalPrice { get; set; }
    }
}
