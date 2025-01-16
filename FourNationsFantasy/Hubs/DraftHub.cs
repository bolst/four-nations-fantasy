using Microsoft.AspNetCore.SignalR;

namespace FourNationsFantasy.Hubs;

public class DraftHub : Hub
{
    public const string HubUrl = "/drafthub";

    public async Task DraftPlayer(Data.FNFPlayer player, Data.User user)
    {
        await Clients.All.SendAsync("DraftPlayer", player, user);
    }

    public override Task OnConnectedAsync()
    {
        Console.WriteLine($"{Context.ConnectionId} connected");
        return base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception e)
    {
        Console.WriteLine($"Disconnected {e?.Message} {Context.ConnectionId}");
        await base.OnDisconnectedAsync(e);
    }
}