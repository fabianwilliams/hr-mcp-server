using Azure.Data.Tables;
using HRMCPServer.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HRMCPServer.Services;

/// <summary>
/// Service for managing candidate data in Azure Table Storage
/// </summary>
public class TableStorageCandidateService : ICandidateService
{
    private readonly TableClient _tableClient;
    private readonly ILogger<TableStorageCandidateService> _logger;
    private const string TABLE_NAME = "Candidates";

    public TableStorageCandidateService(
        IConfiguration configuration,
        ILogger<TableStorageCandidateService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        var connectionString = configuration.GetConnectionString("TableStorage") 
            ?? Environment.GetEnvironmentVariable("TABLE_STORAGE_CONN_STRING");
            
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("TABLE_STORAGE_CONN_STRING environment variable or TableStorage connection string is required");
        }

        _tableClient = new TableClient(connectionString, TABLE_NAME);
        _logger.LogInformation("Initialized Azure Table Storage client for table: {TableName}", TABLE_NAME);
    }

    private async Task EnsureTableExistsAsync()
    {
        try
        {
            await _tableClient.CreateIfNotExistsAsync();
            _logger.LogDebug("Ensured table exists: {TableName}", TABLE_NAME);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create table: {TableName}", TABLE_NAME);
            throw;
        }
    }

    public async Task<List<Candidate>> GetAllCandidatesAsync()
    {
        try
        {
            await EnsureTableExistsAsync();
            _logger.LogDebug("Retrieving all candidates from Table Storage");
            var candidates = new List<Candidate>();
            
            await foreach (var entity in _tableClient.QueryAsync<CandidateTableEntity>(
                filter: TableClient.CreateQueryFilter($"PartitionKey eq 'Candidate'")))
            {
                candidates.Add(entity.ToCandidate());
            }
            
            _logger.LogInformation("Retrieved {Count} candidates from Table Storage", candidates.Count);
            return candidates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving candidates from Table Storage");
            throw;
        }
    }

    public async Task<bool> AddCandidateAsync(Candidate candidate)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));

        try
        {
            await EnsureTableExistsAsync();
            var entity = new CandidateTableEntity(candidate);
            
            // Check if candidate already exists
            var existingEntity = await GetCandidateEntityAsync(candidate.Email);
            if (existingEntity != null)
            {
                _logger.LogWarning("Candidate with email {Email} already exists", candidate.Email);
                return false;
            }

            await _tableClient.AddEntityAsync(entity);
            _logger.LogInformation("Added new candidate: {FullName} ({Email})", candidate.FullName, candidate.Email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding candidate {Email} to Table Storage", candidate.Email);
            throw;
        }
    }

    public async Task<bool> UpdateCandidateAsync(string email, Action<Candidate> updateAction)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or empty", nameof(email));
        
        if (updateAction == null)
            throw new ArgumentNullException(nameof(updateAction));

        try
        {
            var entity = await GetCandidateEntityAsync(email);
            if (entity == null)
            {
                _logger.LogWarning("Candidate with email {Email} not found for update", email);
                return false;
            }

            // Convert to candidate, apply updates, convert back
            var candidate = entity.ToCandidate();
            updateAction(candidate);
            
            // Update the entity with new data
            var updatedEntity = new CandidateTableEntity(candidate)
            {
                ETag = entity.ETag // Preserve ETag for optimistic concurrency
            };

            await _tableClient.UpdateEntityAsync(updatedEntity, entity.ETag);
            _logger.LogInformation("Updated candidate with email: {Email}", email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating candidate {Email} in Table Storage", email);
            throw;
        }
    }

    public async Task<bool> RemoveCandidateAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or empty", nameof(email));

        try
        {
            var entity = await GetCandidateEntityAsync(email);
            if (entity == null)
            {
                _logger.LogWarning("Candidate with email {Email} not found for removal", email);
                return false;
            }

            await _tableClient.DeleteEntityAsync("Candidate", email);
            _logger.LogInformation("Removed candidate with email: {Email}", email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing candidate {Email} from Table Storage", email);
            throw;
        }
    }

    public async Task<List<Candidate>> SearchCandidatesAsync(string searchTerm)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return await GetAllCandidatesAsync();
            }

            _logger.LogDebug("Searching candidates for term: {SearchTerm}", searchTerm);
            var searchTermLower = searchTerm.Trim().ToLowerInvariant();
            var candidates = new List<Candidate>();

            // Note: Table Storage doesn't support complex text search, so we need to retrieve all and filter
            // For production, consider using Azure Cognitive Search for better search capabilities
            await foreach (var entity in _tableClient.QueryAsync<CandidateTableEntity>(
                filter: TableClient.CreateQueryFilter($"PartitionKey eq 'Candidate'")))
            {
                var candidate = entity.ToCandidate();
                
                if (candidate.FirstName.ToLowerInvariant().Contains(searchTermLower) ||
                    candidate.LastName.ToLowerInvariant().Contains(searchTermLower) ||
                    candidate.Email.ToLowerInvariant().Contains(searchTermLower) ||
                    candidate.CurrentRole.ToLowerInvariant().Contains(searchTermLower) ||
                    candidate.Skills.Any(skill => skill.ToLowerInvariant().Contains(searchTermLower)) ||
                    candidate.SpokenLanguages.Any(lang => lang.ToLowerInvariant().Contains(searchTermLower)))
                {
                    candidates.Add(candidate);
                }
            }

            _logger.LogInformation("Found {Count} candidates matching search term: {SearchTerm}", 
                candidates.Count, searchTerm);
            return candidates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching candidates in Table Storage for term: {SearchTerm}", searchTerm);
            throw;
        }
    }

    private async Task<CandidateTableEntity?> GetCandidateEntityAsync(string email)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<CandidateTableEntity>("Candidate", email);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null; // Entity not found
        }
    }
}