using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace comply_flow_api.Models;

public class MatchedRules
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int RuleId { get; set; }

    [ForeignKey(nameof(RuleId))]
    public Rules Rule { get; set; } = null!;

    [Required]
    public bool Matched { get; set; }

    [Required]
    [Column(TypeName = "varchar(max)")]
    public string Reason { get; set; } = string.Empty;
}