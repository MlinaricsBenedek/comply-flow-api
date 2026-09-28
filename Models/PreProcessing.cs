using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace comply_flow_api.Models;

public class PreProcessing
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProcessingId { get; set; }

    [ForeignKey(nameof(ProcessingId))]
    public Processing Processing { get; set; } = null!;

    [Required]
    [Column(TypeName = "varchar(max)")]
    public string CleanedText { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Column(TypeName = "varchar(100)")]
    public string CaseType { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "varchar(max)")]
    public string StructuredCaseStateJson { get; set; } = string.Empty;
}