using Microsoft.AspNetCore.SignalR;

namespace FourNationsFantasy.Hubs;

public class DraftHub : Hub
{
    public const string HubUrl = "/drafthub";

    public async Task DraftPlayer(Data.FNFPlayer player, Data.User user)
    {
        await Clients.All.SendAsync("DraftPlayer", player, user);
    }
    
}