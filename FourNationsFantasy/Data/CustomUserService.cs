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

    public async Task<(User?, bool)> FetchUserFromBrowserAsync()
    {
        User user = new();

        try
        {
            var masq = await _localStorageService.GetItemAsync<string>("masq");

            if (masq is null)
            {
                user.Email = _protector.Unprotect(await _localStorageService.GetItemAsync<string>("user"));
            }
            else
            {
                user.Email = _protector.Unprotect(masq);
            }

            return (user, masq is not null);
        }
        catch
        {
            return (null, false);
        }
    }

    public async Task BeginMasquerade(User user)
    {
        await _localStorageService.SetItemAsync("masq", _protector.Protect(user.Email));
    }

    public async Task EndMasquerade()
    {
        await _localStorageService.RemoveItemAsync("masq");
    }

    public async Task ChangeUserTheme(bool isDark)
    {
        var themeStr = isDark ? "dark" : "light";
        await _localStorageService.SetItemAsync("theme", themeStr);
    }

    public async Task<bool> IsUserThemeDark()
    {
        var themeStr = await _localStorageService.GetItemAsync<string>("theme");
        return string.IsNullOrEmpty(themeStr) || themeStr == "dark";
    }

    public async Task LogoutAsync()
    {
        await _localStorageService.RemoveItemAsync("user");
        await _localStorageService.RemoveItemAsync("login");
        await _localStorageService.RemoveItemAsync("masq");
        await _supabase.Auth.SignOut();
    }

}