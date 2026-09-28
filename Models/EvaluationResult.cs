using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace comply_flow_api.Models;

public class EvaluationResult
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
    public string Metric { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(10,4)")]
    public decimal Score { get; set; }

    [Required]
    [StringLength(30)]
    [Column(TypeName = "varchar(30)")]
    public string Unit { get; set; } = string.Empty;

    [Column(TypeName = "varchar(max)")]
    public string? Comment { get; set; }
}