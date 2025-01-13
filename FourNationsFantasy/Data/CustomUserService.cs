using Microsoft.AspNetCore.DataProtection;
using Supabase.Gotrue;
using Blazored.LocalStorage;

namespace FourNationsFantasy.Data;

public class CustomUserService
{
    private ILocalStorageService _localStorageService;
    private IDataProtector _protector;
    private readonly Supabase.Client _supabase;

    public CustomUserService(ILocalStorageService localStorageService, string url, string key)
    {
        _localStorageService = localStorageService;
        _protector = DataProtectionProvider.Create("FNF_NBC").CreateProtector("creds");
        _supabase = new Supabase.Client(url, key);
    }

    public async Task<(bool, string)> SendMagicLink(string email)
    {
        var options = new SignInOptions { RedirectTo = "http://localhost:5257/auth", FlowType = Constants.OAuthFlowType.PKCE};
        bool status = await _supabase.Auth.SendMagicLink(email, options);
        var errorMsg = string.Empty;
        
        if (!status)
        {
            // TODO: log error message
        }
        
        return (status, errorMsg);
    }

    public async Task VerifyToken(string email, string token)
    {
        var session = await _supabase.Auth.VerifyOTP(email, token, Constants.EmailOtpType.Email);
        
        if (session?.AccessToken is not null)
        {
            await PersistUserToBrowserAsync(email, session.AccessToken);
        }
    }

    public async Task<User?> LookupUserInDatabase(string email)
    {
        return null;
    }

    public async Task PersistUserToBrowserAsync(string email, string token)
    {
        await _localStorageService.SetItemAsync("user", _protector.Protect(email));
        await _localStorageService.SetItemAsync("login", _protector.Protect(token));
    }

    public async Task<User?> FetchUserFromBrowserAsync()
    {
        User user = new();

        try
        {
            user.Email = _protector.Unprotect(await _localStorageService.GetItemAsync<string>("user"));
            return user;
        }
        catch
        {
            return null;
        }
    }

}