namespace Agent.Api.Models
{

    public class RoleData
    {
       

        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime DateTime { get; set; } = DateTime.UtcNow;

        public string Token { get; set; }
    }
}
