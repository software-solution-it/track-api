using System.ComponentModel.DataAnnotations;

namespace track_api.Models
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public string TrackingCode { get; set; }
        public string ItemName { get; set; }
        public string UserEmail { get; set; }
        public string UserName { get; set; }
    }
}
