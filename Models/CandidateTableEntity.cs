using Azure;
using Azure.Data.Tables;
using System.Text.Json;

namespace HRMCPServer.Models;

/// <summary>
/// Azure Table Storage entity for candidate data
/// </summary>
public class CandidateTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "Candidate";
    public string RowKey { get; set; } = string.Empty; // Will be the email
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // Candidate properties
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CurrentRole { get; set; } = string.Empty;
    
    // JSON serialized arrays for complex properties
    public string SkillsJson { get; set; } = "[]";
    public string SpokenLanguagesJson { get; set; } = "[]";

    public CandidateTableEntity()
    {
    }

    public CandidateTableEntity(Candidate candidate)
    {
        RowKey = candidate.Email;
        FirstName = candidate.FirstName;
        LastName = candidate.LastName;
        Email = candidate.Email;
        CurrentRole = candidate.CurrentRole;
        SkillsJson = JsonSerializer.Serialize(candidate.Skills);
        SpokenLanguagesJson = JsonSerializer.Serialize(candidate.SpokenLanguages);
    }

    /// <summary>
    /// Convert Table Entity back to Candidate model
    /// </summary>
    public Candidate ToCandidate()
    {
        var skills = new List<string>();
        var spokenLanguages = new List<string>();

        try
        {
            if (!string.IsNullOrEmpty(SkillsJson))
                skills = JsonSerializer.Deserialize<List<string>>(SkillsJson) ?? new List<string>();
        }
        catch (JsonException)
        {
            // Fallback to empty list if JSON is invalid
            skills = new List<string>();
        }

        try
        {
            if (!string.IsNullOrEmpty(SpokenLanguagesJson))
                spokenLanguages = JsonSerializer.Deserialize<List<string>>(SpokenLanguagesJson) ?? new List<string>();
        }
        catch (JsonException)
        {
            // Fallback to empty list if JSON is invalid
            spokenLanguages = new List<string>();
        }

        return new Candidate
        {
            FirstName = FirstName,
            LastName = LastName,
            Email = Email,
            CurrentRole = CurrentRole,
            Skills = skills,
            SpokenLanguages = spokenLanguages
        };
    }
}