using System.ComponentModel.DataAnnotations;

namespace DtekMonitor.Models;

/// <summary>
/// Represents a Telegram user subscribed to power outage notifications
/// </summary>
public class Subscriber
{
    /// <summary>
    /// Telegram Chat ID (Primary Key)
    /// </summary>
    [Key]
    public long ChatId { get; set; }

    /// <summary>
    /// The DTEK group the user is subscribed to (e.g., "GPV4.1")
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string GroupName { get; set; } = string.Empty;

    /// <summary>
    /// Optional username for logging purposes
    /// </summary>
    [MaxLength(100)]
    public string? Username { get; set; }

    /// <summary>
    /// When the subscription was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the subscription was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}


