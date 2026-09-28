using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace comply_flow_api.Models;

public class Rules
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProcessingId { get; set; }

    [ForeignKey(nameof(ProcessingId))]
    public Processing Processing { get; set; } = null!;

    [Required]
    [StringLength(100)]
    [Column(TypeName = "varchar(100)")]
    public string Decision { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "varchar(max)")]
    public string Reason { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string RuleSetVersion { get; set; } = string.Empty;

    public ICollection<MatchedRules> MatchedRules { get; set; } = new List<MatchedRules>();
}