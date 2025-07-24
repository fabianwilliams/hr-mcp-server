using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace HRMCPServer.Services;

/// <summary>
/// Service to seed initial candidate data into storage
/// </summary>
public class DataSeedingService
{
    private readonly ICandidateService _candidateService;
    private readonly ILogger<DataSeedingService> _logger;

    public DataSeedingService(
        ICandidateService candidateService,
        ILogger<DataSeedingService> logger)
    {
        _candidateService = candidateService ?? throw new ArgumentNullException(nameof(candidateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Seed data from JSON file if storage is empty
    /// </summary>
    public async Task SeedDataIfEmptyAsync(string candidatesFilePath)
    {
        try
        {
            // Check if we already have candidates
            var existingCandidates = await _candidateService.GetAllCandidatesAsync();
            if (existingCandidates.Any())
            {
                _logger.LogInformation("Storage already contains {Count} candidates, skipping seeding", 
                    existingCandidates.Count);
                return;
            }

            _logger.LogInformation("Storage is empty, seeding initial data from {FilePath}", candidatesFilePath);

            if (!File.Exists(candidatesFilePath))
            {
                _logger.LogWarning("Candidates file not found at: {FilePath}", candidatesFilePath);
                return;
            }

            var jsonContent = await File.ReadAllTextAsync(candidatesFilePath);
            var candidates = JsonSerializer.Deserialize<List<Candidate>>(jsonContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true
            });

            if (candidates == null || !candidates.Any())
            {
                _logger.LogWarning("No candidates found in JSON file: {FilePath}", candidatesFilePath);
                return;
            }

            int seedCount = 0;
            foreach (var candidate in candidates)
            {
                try
                {
                    var added = await _candidateService.AddCandidateAsync(candidate);
                    if (added)
                    {
                        seedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to seed candidate: {Email}", candidate.Email);
                }
            }

            _logger.LogInformation("Successfully seeded {SeedCount} out of {TotalCount} candidates", 
                seedCount, candidates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data seeding from file: {FilePath}", candidatesFilePath);
        }
    }
}