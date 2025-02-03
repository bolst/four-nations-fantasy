using System.Security.Claims;

namespace FourNationsFantasy.Data;

public class FNFPlayer : IEquatable<FNFPlayer>
{
    public string nhl_id { get; set; }
    public string firstname { get; set; }
    public string lastname { get; set; }
    public string position { get; set; }
    public string nationality { get; set; }
    public int? draft_number { get; set;}
    public int? user_id { get; set; }
    public string? headshot { get; set; }
    public string? hero_image { get; set; }
    public int sweater_number { get; set; }

    #region Season totals

    public int games_played { get; set; } = 0;
    public int goals { get; set; }
    public int assists { get; set; }
    public int pp_points { get; set; }
    public int sh_points { get; set; }
    public int shots_on_goal { get; set; }
    public int hits { get; set; }
    public int blocks { get; set; }
    public int goalie_wins { get; set; }
    public double goalie_gaa { get; set; }
    public double goalie_sv_pctg { get; set; }
    public int goalie_shutouts { get; set; }
    public int goalie_goals_against { get; set; }
    public int goalie_saves { get; set; }

    public int? Points => goals + assists;
    #endregion

    public int NhlIdInt => int.Parse(nhl_id);

    public string Flag => this.nationality switch
    {
        "CAN" => "\ud83c\udde8\ud83c\udde6",
        "USA" => "\ud83c\uddfa\ud83c\uddf8",
        "SWE" => "\ud83c\uddf8\ud83c\uddea",
        "FIN" => "\ud83c\uddeb\ud83c\uddee",
        _ => "\ud83c\udf0d",
    };

    public string FullPosition => this.position switch
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
        return NhlIdInt;
    }

    public bool Equals(FNFPlayer other)
    {
        if (other is null) return false;
        return (this.nhl_id == other.nhl_id);
    }
}

public class User
{
    public int id { get; set; }
    public string email { get; set; }
    public string? firstname { get; set; }
    public string? lastname { get; set; }
    public string? teamname { get; set; }
    public string role { get; set; }
    public bool IsAdmin => role.ToLower().Equals("admin");

    public ClaimsPrincipal ToClaimsPrincipal()
    {
        return new(new ClaimsIdentity(new Claim[]
        {
            new (ClaimTypes.Sid, id.ToString()),
            new (ClaimTypes.Name, email),
            new (nameof(firstname), firstname),
            new (nameof(lastname), lastname),
            new (nameof(teamname), teamname),
            new(ClaimTypes.Role, role)
        }, "FNF"));
    }

    public static User FromClaimsPrincipal(ClaimsPrincipal principal) => new()
    {
        id = int.Parse(principal.FindFirstValue(ClaimTypes.Sid)),
        email = principal.FindFirstValue(ClaimTypes.Email),
        firstname = principal.FindFirstValue(nameof(firstname)),
        lastname = principal.FindFirstValue(nameof(lastname)),
        teamname = principal.FindFirstValue(nameof(teamname)),
        role = principal.FindFirstValue(ClaimTypes.Role),
    };
}
