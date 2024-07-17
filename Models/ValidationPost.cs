using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("wp_validation_post")]
public class ValidationPost
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("order_id")]
    public int OrderId { get; set; }

    [Column("send_count")]
    public int SendCount { get; set; }

    [Column("completed")]
    public int Completed { get; set; }

    [Column("post_message")]
    public string PostMessage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

}



public class ValidationGetDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int SendCount { get; set; }
    public bool Completed { get; set; }
    public string PostMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string TrackingNumber { get; set; }
}

