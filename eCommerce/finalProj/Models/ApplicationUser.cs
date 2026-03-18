using Microsoft.AspNetCore.Identity;

namespace finalProj.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
    }
}
