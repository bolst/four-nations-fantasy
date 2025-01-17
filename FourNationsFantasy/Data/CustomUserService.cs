using Microsoft.AspNetCore.DataProtection;
using Supabase.Gotrue;
using Blazored.LocalStorage;

namespace FourNationsFantasy.Data;

public class CustomUserService
{
    private ILocalStorageService _localStorageService;
    private IDataProtector _protector;
    private readonly IFNFData _FNFData;
    private readonly Supabase.Client _supabase;

    public CustomUserService(ILocalStorageService localStorageService, IFNFData FNFData, string url, string key)
    {
        _localStorageService = localStorageService;
        _protector = DataProtectionProvider.Create("FNF_NBC").CreateProtector("creds");
        _FNFData = FNFData;
        _supabase = new Supabase.Client(url, key);
    }

    public async Task<(bool, string)> SendOTP(string email)
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

    public async Task<(bool, string)> VerifyOTP(string email, string token)
    {
        try
        {
            var session = await _supabase.Auth.VerifyOTP(email, token, Constants.EmailOtpType.Email);

            if (session?.AccessToken is not null)
            {
                await PersistUserToBrowserAsync(email, session.AccessToken);
            }
            
            return (true, string.Empty);
        }
        catch
        {
            return (false, "Invalid OTP");
        }
    }

    public async Task<User?> LookupUserInDatabase(string email)
    {
        return await _FNFData.GetUserByEmailAsync(email);
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

    public async Task LogoutAsync()
    {
        await _localStorageService.RemoveItemAsync("user");
        await _localStorageService.RemoveItemAsync("login");
        await _supabase.Auth.SignOut();
    }

}