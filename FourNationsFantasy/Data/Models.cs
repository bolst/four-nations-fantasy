using System.Security.Claims;

namespace FourNationsFantasy.Data;

public class FNFPlayer : IEquatable<FNFPlayer>
{
    public string NhlId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Position { get; set; }
    public string Nationality { get; set; }
    public int? DraftNumber { get; set;}
    public int? UserId { get; set; }

    public int NhlIdInt => int.Parse(NhlId);

    public string Flag => this.Nationality switch
    {
        "CAN" => "\ud83c\udde8\ud83c\udde6",
        "USA" => "\ud83c\uddfa\ud83c\uddf8",
        "SWE" => "\ud83c\uddf8\ud83c\uddea",
        "FIN" => "\ud83c\uddeb\ud83c\uddee",
        _ => "\ud83c\udf0d",
    };

    public string FullPosition => this.Position switch
    {
        "F" => "Forward",
        "D" => "Defense",
        "G" => "Goalie",
        _ => string.Empty,
    };
    
    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        FNFPlayer player = obj as FNFPlayer;
        if (player is null) return false;
        else return Equals(player);
    }

    public override int GetHashCode()
    {
        return int.Parse(NhlId);
    }

    public bool Equals(FNFPlayer other)
    {
        if (other is null) return false;
        return (this.NhlId == other.NhlId);
    }
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
