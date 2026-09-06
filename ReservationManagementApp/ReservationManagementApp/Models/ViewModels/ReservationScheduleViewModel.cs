using System;
using System.Collections.Generic;

namespace ReservationManagementApp.Models.ViewModels
{
    public class ReservationScheduleViewModel
    {
        public ReservationScheduleViewModel()
        {
            Rooms = new List<RoomScheduleViewModel>();
        }

        public string FacilityId { get; set; }
        public string FacilityName { get; set; }
        public DateTime Date { get; set; }
        public int? Capacity { get; set; }
        public List<RoomScheduleViewModel> Rooms { get; set; }
    }
}
