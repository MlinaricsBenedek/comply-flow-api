using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace comply_flow_api.Models;

public class Conversation
{
    [Key]
    public int Id { get; set; }

    [StringLength(200)]
    [Column(TypeName = "varchar(200)")]
    public string? Title { get; set; }

    [Required]
    [StringLength(30)]
    [Column(TypeName = "varchar(30)")]
    public string Status { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();

    public ICollection<Processing> Processings { get; set; } = new List<Processing>();
}