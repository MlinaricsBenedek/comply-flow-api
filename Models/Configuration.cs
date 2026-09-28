using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace comply_flow_api.Models;

public class Configuration
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    [Column(TypeName = "varchar(100)")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string RuleSetVersion { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    [Column(TypeName = "varchar(30)")]
    public string GenerationMode { get; set; } = string.Empty;

    [StringLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string? TemplateVersion { get; set; }

    [StringLength(100)]
    [Column(TypeName = "varchar(100)")]
    public string? ModelName { get; set; }

    [Column(TypeName = "varchar(max)")]
    public string? ModelParametersJson { get; set; }

    [Required]
    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    public ICollection<Processing> Processings { get; set; } = new List<Processing>();
}