using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace comply_flow_api.Models;

public class Message
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ConversationId { get; set; }

    [ForeignKey(nameof(ConversationId))]
    public Conversation Conversation { get; set; } = null!;

    [Required]
    [StringLength(20)]
    [Column(TypeName = "varchar(20)")]
    public string Role { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "varchar(max)")]
    public string Content { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    public int? ProcessingId { get; set; }

    [ForeignKey(nameof(ProcessingId))]
    public Processing? Processing { get; set; }

    public ICollection<Processing> InputForProcessings { get; set; } = new List<Processing>();
}