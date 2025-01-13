using System.Security.Claims;
using Dapper;
using Npgsql;

namespace FourNationsFantasy.Data;

public abstract class QueryDapperBase
{
    protected readonly string _connectionString;
    private readonly IServiceProvider _serviceProvider;

    public QueryDapperBase(string connectionString, IServiceProvider serviceProvider)
    {
        _connectionString = connectionString;
        _serviceProvider = serviceProvider;
    }

    protected async Task<IEnumerable<T>> QueryDbAsync<T>(string query, object? param = null)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QueryAsync<T>(query, param);
    }

    protected async Task<T?> QueryDbSingleAsync<T>(string query, object? param = null)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<T>(query, param);
    }
}

public interface IFNFData
{
    Task<IEnumerable<FNFPlayer>> GetAllPlayersAsync();
}

public class FNFData : QueryDapperBase, IFNFData
{
    public FNFData(string connectionString, IServiceProvider serviceProvider) : base(connectionString, serviceProvider) {}
    
    public async Task<IEnumerable<FNFPlayer>> GetAllPlayersAsync()
    {
        string sql = @"SELECT
                          nhl_id,
                          firstname AS FirstName,
                          lastname AS LastName,
                          position AS Position,
                          nationality AS Nationality
                        FROM
                          players";
        return await QueryDbAsync<FNFPlayer>(sql);
    }
}


public class FNFPlayer
{
    public string nhl_id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Position { get; set; }
    public string Nationality { get; set; }
}

public class User
{
    public int Id { get; set; }
    public string Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? TeamName { get; set; }

    public ClaimsPrincipal ToClaimsPrincipal()
    {
        return new(new ClaimsIdentity(new Claim[]
        {
            new (ClaimTypes.Sid, Id.ToString()),
            new (ClaimTypes.Name, Email),
            new (nameof(FirstName), FirstName),
            new (nameof(LastName), LastName),
            new (nameof(TeamName), TeamName)
        }, "FNF"));
    }

    public static User FromClaimsPrincipal(ClaimsPrincipal principal) => new()
    {
        Id = int.Parse(principal.FindFirstValue(ClaimTypes.Sid)),
        Email = principal.FindFirstValue(ClaimTypes.Email),
        FirstName = principal.FindFirstValue(nameof(FirstName)),
        LastName = principal.FindFirstValue(nameof(LastName)),
        TeamName = principal.FindFirstValue(nameof(TeamName))
    };
}
    
