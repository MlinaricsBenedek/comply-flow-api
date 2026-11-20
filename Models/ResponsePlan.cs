using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace comply_flow_api.Models;

public class ResponsePlan
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
    [Column(TypeName = "varchar(max)")]
    public string RequiredElementsJson { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "varchar(max)")]
    public string ResponseStructureJson { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "varchar(max)")]
    public string SourcesJson { get; set; } = string.Empty;
}