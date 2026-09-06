namespace ReservationManagementApp.Models.Entities
{
    public class Room
    {
        public string RoomId { get; set; }
        public string FacilityId { get; set; }
        public string RoomName { get; set; }
        public int Capacity { get; set; }
        public int HourlyRate { get; set; }
        public string Equipment { get; set; }
        public string Description { get; set; }
    }
}
