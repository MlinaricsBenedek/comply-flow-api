using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace comply_flow_api.Models;

public enum ProcessingStatus
{
    Created,
    Processing,
    Completed,
    Failed
}

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

    public ProcessingStatus Status { get; private set; } = ProcessingStatus.Created;

    [Column(TypeName = "varchar(200)")]
    public string? ErrorType { get; private set; }

    [Column(TypeName = "varchar(max)")]
    public string? ErrorMessage { get; private set; }

    [Required]
    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? StartedAt { get; private set; }

    [Column(TypeName = "datetime")]
    public DateTime? FinishedAt { get; private set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();

    public PreProcessing? PreProcessing { get; set; }

    public Rules? Rules { get; set; }

    public ResponsePlan? ResponsePlan { get; set; }

    public Response? Response { get; set; }

    public ICollection<EvaluationResult> EvaluationResults { get; set; } = new List<EvaluationResult>();

    public void TransitionTo(ProcessingStatus nextStatus)
    {
        var isAllowed = (Status, nextStatus) is
            (ProcessingStatus.Created, ProcessingStatus.Processing) or
            (ProcessingStatus.Processing, ProcessingStatus.Completed) or
            (ProcessingStatus.Processing, ProcessingStatus.Failed);

        if (!isAllowed)
        {
            throw new InvalidOperationException(
                $"Cannot transition processing from '{Status}' to '{nextStatus}'.");
        }

        Status = nextStatus;
        if (nextStatus == ProcessingStatus.Processing)
        {
            StartedAt = DateTime.UtcNow;
        }
        else if (nextStatus is ProcessingStatus.Completed or ProcessingStatus.Failed)
        {
            FinishedAt = DateTime.UtcNow;
        }
    }

    public void Fail(
        string errorType,
        string errorMessage,
        bool completionWasNotPersisted = false)
    {
        if (string.IsNullOrWhiteSpace(errorType))
        {
            throw new ArgumentException("Error type cannot be empty.", nameof(errorType));
        }

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Error message cannot be empty.", nameof(errorMessage));
        }

        if (Status == ProcessingStatus.Completed && completionWasNotPersisted)
        {
            Status = ProcessingStatus.Processing;
            FinishedAt = null;
        }

        if (Status != ProcessingStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Cannot mark processing '{Status}' as failed.");
        }

        ErrorType = errorType;
        ErrorMessage = errorMessage;
        TransitionTo(ProcessingStatus.Failed);
    }
}