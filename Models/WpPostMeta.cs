using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace track_api.Models
{
    [Table("wp_postmeta")]
    public class WpPostMeta
    {
        [Key]
        [Column("meta_id")]
        public long MetaId { get; set; }

        [Column("post_id")]
        public long PostId { get; set; }

        [Column("meta_key")]
        public string MetaKey { get; set; }

        [Column("meta_value")]
        public string MetaValue { get; set; }

        [ForeignKey("PostId")]
        public WpPost Post { get; set; }
    }
}
