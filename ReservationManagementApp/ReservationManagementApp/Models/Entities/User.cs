namespace ReservationManagementApp.Models.Entities
{
    public class User
    {
        public string UserId { get; set; }
        public string LoginId { get; set; }
        public string PasswordHash { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Department { get; set; }
        public UserRole Role { get; set; }
    }
}
