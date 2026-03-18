namespace finalProj.Models
{
    public class Address
    {
        public int AddressId { get; set; }
        public string UserId { get; set; }
        public virtual ApplicationUser User { get; set; }  //lazy
        public string Country { get; set; }
        public string City { get; set; }
        public string Street { get; set; }
        public string Zip { get; set; }
        public bool IsDefault { get; set; }
    }
}
