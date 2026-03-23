using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;

namespace finalProj.Models
{
    public class Order
    {
        public enum OrderStatus
        {
            Pending = 0,
            Shipped = 1,
            Delivered = 2,
            Cancelled = 3
        }
        [Key]
        public int OrderId { get; set; }
        public string UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }
        public int ShippingAddressId { get; set; }
        [ForeignKey("ShippingAddressId")]
        public virtual Address ShippingAddress { get; set; }
        public string OrderNumber { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime OrderDate { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }
        public virtual ICollection<OrderItem> OrderItems { get; set; }
    }
}
