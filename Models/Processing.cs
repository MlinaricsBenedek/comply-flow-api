using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace comply_flow_api.Models;

public class Processing
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ConversationId { get; set; }

    [ForeignKey(nameof(ConversationId))]
    public Conversation Conversation { get; set; } = null!;

    [Required]
    public int InputMessageId { get; set; }

    [ForeignKey(nameof(InputMessageId))]
    public Message InputMessage { get; set; } = null!;

    [Required]
    public int ConfigurationId { get; set; }

    [ForeignKey(nameof(ConfigurationId))]
    public Configuration Configuration { get; set; } = null!;

    [Required]
    [StringLength(30)]
    [Column(TypeName = "varchar(30)")]
    public string Status { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();

    public PreProcessing? PreProcessing { get; set; }

    public Rules? Rules { get; set; }

    public ResponsePlan? ResponsePlan { get; set; }

    public Response? Response { get; set; }

    public ICollection<EvaluationResult> EvaluationResults { get; set; } = new List<EvaluationResult>();
}