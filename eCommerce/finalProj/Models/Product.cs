using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace finalProj.Models
{
    public class Product
    {

            [Key]
            public int ProductId { get; set; }

            [Required]
            public string Name { get; set; }

            [Required]
            public string SKU { get; set; }

            [Column(TypeName = "decimal(18,2)")]
            public decimal Price { get; set; }

            public int StockQuantity { get; set; }

            public bool IsActive { get; set; }

            public DateTime CreatedAt { get; set; } = DateTime.Now; 

            public int CategoryId { get; set; }
            [ForeignKey("CategoryId")]
            public virtual Category Category { get; set; }
    }
}

