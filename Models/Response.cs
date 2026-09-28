using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace comply_flow_api.Models;

public class Response
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProcessingId { get; set; }

    [ForeignKey(nameof(ProcessingId))]
    public Processing Processing { get; set; } = null!;

    [Required]
    [StringLength(30)]
    [Column(TypeName = "varchar(30)")]
    public string GenerationMode { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "varchar(max)")]
    public string ResponseText { get; set; } = string.Empty;

    [StringLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string? TemplateVersion { get; set; }

    [StringLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string? PromptVersion { get; set; }

    [StringLength(100)]
    [Column(TypeName = "varchar(100)")]
    public string? ModelName { get; set; }

    [Column(TypeName = "varchar(max)")]
    public string? ModelParametersJson { get; set; }

    [Column(TypeName = "varchar(max)")]
    public string? InputContextJson { get; set; }

    [Required]
    public int ProcessingTimeMs { get; set; }
}