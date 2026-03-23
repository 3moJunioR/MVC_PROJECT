using System.ComponentModel.DataAnnotations;

namespace finalProj.Models.ViewModels
{
    public class CheckoutVM
    {
        [Required(ErrorMessage = "Country is required")]
        public string Country { get; set; }

        [Required(ErrorMessage = "City is required")]
        public string City { get; set; }

        [Required(ErrorMessage = "Street is required")]
        public string Street { get; set; }

        [Required(ErrorMessage = "Zip Code is required")]
        public string Zip { get; set; }

        public decimal TotalAmount { get; set; }
    }
}