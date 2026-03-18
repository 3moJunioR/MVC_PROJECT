    using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace finalProj.Models
{
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }
        [Required, StringLength(100)]
        public string Name { get; set; }
        public int? ParentCategoryId { get; set; }
        [ForeignKey("ParentCategoryId")]
        public virtual Category ParentCategory { get; set; } //lazy
    
        public virtual ICollection<Product> Products { get; set; } //lazy
    }
}
